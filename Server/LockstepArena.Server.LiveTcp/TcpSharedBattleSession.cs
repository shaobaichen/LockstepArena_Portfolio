using System;
using System.IO;
using System.Net.Sockets;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.ProtocolAuthority;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.Server.LiveTcp
{
    public sealed class TcpSharedBattleSession : IDisposable
    {
        private readonly ParticipantState[] _participants;
        private readonly ProtocolAuthorityProcessor _processor;
        private bool _disposed;
        private bool _faulted;

        public TcpSharedBattleSession(
            BattleState initialState,
            TcpBattleParticipantBinding[] participants,
            uint inputDelayTicks,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity,
            int maxPayloadLength,
            int receiveBufferLength,
            int receiveOffset,
            int receiveReadCapacity)
        {
            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (participants is null)
            {
                throw new ArgumentNullException(nameof(participants));
            }

            if (participants.Length != initialState.Roster.Count)
            {
                throw new ArgumentException(
                    "Participant count must match the active roster.",
                    nameof(participants));
            }

            for (int index = 0; index < participants.Length; index++)
            {
                if (participants[index] is null)
                {
                    throw new ArgumentNullException(
                        nameof(participants),
                        "Participant entries cannot be null.");
                }
            }

            if (receiveBufferLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveBufferLength));
            }

            if (receiveOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveOffset));
            }

            if (receiveReadCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveReadCapacity));
            }

            if (receiveReadCapacity > receiveBufferLength ||
                receiveOffset > receiveBufferLength - receiveReadCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveOffset));
            }

            _ = new LengthPrefixedFrameDecoder(maxPayloadLength);

            var orderedBindings = new TcpBattleParticipantBinding[participants.Length];
            var seenSlots = new bool[participants.Length];
            for (int index = 0; index < participants.Length; index++)
            {
                TcpBattleParticipantBinding candidate = participants[index];
                int slotValue = candidate.PlayerSlot.Value;
                if (slotValue >= participants.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(participants),
                        "Participant slot is outside the active roster.");
                }

                if (seenSlots[slotValue])
                {
                    throw new ArgumentException(
                        "Participant slots must be unique.",
                        nameof(participants));
                }

                for (int previous = 0; previous < index; previous++)
                {
                    TcpBattleParticipantBinding prior = participants[previous];
                    if (prior.PlayerId == candidate.PlayerId)
                    {
                        throw new ArgumentException(
                            "Participant PlayerIds must be unique.",
                            nameof(participants));
                    }

                    if (ReferenceEquals(prior.ConnectedClient, candidate.ConnectedClient))
                    {
                        throw new ArgumentException(
                            "Participant TcpClient references must be unique.",
                            nameof(participants));
                    }
                }

                if (initialState.Roster.GetPlayerId(candidate.PlayerSlot) != candidate.PlayerId)
                {
                    throw new ArgumentException(
                        "Participant identity must match the active roster.",
                        nameof(participants));
                }

                if (candidate.ConnectedClient.Client.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new ArgumentException(
                        "Participant TCP clients must use IPv4.",
                        nameof(participants));
                }

                if (!candidate.ConnectedClient.Connected)
                {
                    throw new InvalidOperationException(
                        "Participant TCP clients must already be connected.");
                }

                seenSlots[slotValue] = true;
                orderedBindings[slotValue] = candidate;
            }

            var participantStates = new ParticipantState[orderedBindings.Length];
            for (int index = 0; index < orderedBindings.Length; index++)
            {
                TcpBattleParticipantBinding binding = orderedBindings[index];
                participantStates[index] = new ParticipantState(
                    binding,
                    binding.ConnectedClient.GetStream(),
                    new LengthPrefixedFrameDecoder(maxPayloadLength),
                    new byte[receiveBufferLength],
                    receiveOffset,
                    receiveReadCapacity,
                    maxPayloadLength);
            }

            var processor = new ProtocolAuthorityProcessor(
                initialState,
                inputDelayTicks,
                maxFutureTickOffset,
                authoritativeHistoryCapacity);

            _participants = participantStates;
            _processor = processor;
        }

        public BattleState ServerState => _processor.ServerState;

        public uint NextPublishTick => _processor.NextPublishTick;

        public int ParticipantCount => _participants.Length;

        public int PumpOnce()
        {
            ThrowIfDisposed();
            if (_faulted)
            {
                throw new InvalidOperationException("The shared TCP battle session is faulted.");
            }

            try
            {
                int published = 0;
                for (int participantIndex = 0;
                    participantIndex < _participants.Length;
                    participantIndex++)
                {
                    ParticipantState participant = _participants[participantIndex];
                    Socket socket = participant.Binding.ConnectedClient.Client;
                    if (!socket.Poll(0, SelectMode.SelectRead))
                    {
                        continue;
                    }

                    if (socket.Available == 0)
                    {
                        throw new EndOfStreamException(
                            "A participant stream ended before battle completion.");
                    }

                    int bytesRead = participant.Stream.Read(
                        participant.ReceiveBuffer,
                        participant.ReceiveOffset,
                        participant.ReceiveReadCapacity);
                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException(
                            "A participant stream ended before battle completion.");
                    }

                    byte[][] payloads = participant.Decoder.Feed(
                        participant.ReceiveBuffer,
                        participant.ReceiveOffset,
                        bytesRead);
                    for (int payloadIndex = 0; payloadIndex < payloads.Length; payloadIndex++)
                    {
                        byte[] payload = payloads[payloadIndex];
                        PlayerInputSubmissionMessage wire =
                            PlayerInputSubmissionMessage.Parser.ParseFrom(payload);
                        (PlayerId submittedPlayerId, InputFrame input) =
                            ProtocolMapper.ToDomain(wire);
                        if (submittedPlayerId != participant.Binding.PlayerId ||
                            input.PlayerSlot != participant.Binding.PlayerSlot ||
                            ServerState.Roster.GetPlayerId(participant.Binding.PlayerSlot) !=
                                participant.Binding.PlayerId)
                        {
                            throw new ArgumentException(
                                "Submission identity does not match the bound participant.");
                        }

                        published = checked(
                            published + Broadcast(
                                _processor.SubmitPlayerInputPayload(payload)));
                    }
                }

                published = checked(published + Broadcast(_processor.PollAuthority()));
                return published;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int index = 0; index < _participants.Length; index++)
            {
                _participants[index].Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TcpSharedBattleSession));
            }
        }

        private int Broadcast(byte[][] payloads)
        {
            if (payloads.Length == 0)
            {
                return 0;
            }

            var framed = new byte[payloads.Length][];
            for (int index = 0; index < payloads.Length; index++)
            {
                framed[index] = LengthPrefixedFrameEncoder.Encode(
                    payloads[index],
                    _participants[0].MaxPayloadLength);
            }

            for (int frameIndex = 0; frameIndex < framed.Length; frameIndex++)
            {
                byte[] frame = framed[frameIndex];
                for (int participantIndex = 0;
                    participantIndex < _participants.Length;
                    participantIndex++)
                {
                    _participants[participantIndex].Stream.Write(frame, 0, frame.Length);
                }
            }

            return payloads.Length;
        }

        private sealed class ParticipantState : IDisposable
        {
            public ParticipantState(
                TcpBattleParticipantBinding binding,
                NetworkStream stream,
                LengthPrefixedFrameDecoder decoder,
                byte[] receiveBuffer,
                int receiveOffset,
                int receiveReadCapacity,
                int maxPayloadLength)
            {
                Binding = binding;
                Stream = stream;
                Decoder = decoder;
                ReceiveBuffer = receiveBuffer;
                ReceiveOffset = receiveOffset;
                ReceiveReadCapacity = receiveReadCapacity;
                MaxPayloadLength = maxPayloadLength;
            }

            public TcpBattleParticipantBinding Binding { get; }

            public NetworkStream Stream { get; }

            public LengthPrefixedFrameDecoder Decoder { get; }

            public byte[] ReceiveBuffer { get; }

            public int ReceiveOffset { get; }

            public int ReceiveReadCapacity { get; }

            public int MaxPayloadLength { get; }

            public void Dispose()
            {
                try
                {
                    Stream.Dispose();
                }
                finally
                {
                    Binding.ConnectedClient.Dispose();
                }
            }
        }
    }
}
