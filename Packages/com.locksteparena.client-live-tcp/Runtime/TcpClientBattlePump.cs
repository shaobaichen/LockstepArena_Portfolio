using System;
using System.IO;
using System.Net.Sockets;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.Client.LiveTcp
{
    public sealed class TcpClientBattlePump : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly LengthPrefixedFrameDecoder _decoder;
        private readonly ActiveRoster _expectedRoster;
        private readonly LegacySimulationState? _legacySimulation;
        private readonly byte[] _receiveBuffer;
        private readonly int _receiveOffset;
        private readonly int _receiveReadCapacity;
        private readonly int _maxPayloadLength;
        private bool _disposed;
        private bool _faulted;

        public TcpClientBattlePump(
            TcpClient connectedClient,
            BattleState initialState,
            int maxPayloadLength,
            int receiveBufferLength,
            int receiveOffset,
            int receiveReadCapacity)
        {
            if (connectedClient is null)
            {
                throw new ArgumentNullException(nameof(connectedClient));
            }

            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
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

            var decoder = new LengthPrefixedFrameDecoder(maxPayloadLength);
            if (connectedClient.Client.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("The connected client must use IPv4.", nameof(connectedClient));
            }

            if (!connectedClient.Connected)
            {
                throw new InvalidOperationException("The TCP client must already be connected.");
            }

            NetworkStream stream = connectedClient.GetStream();
            var receiveBuffer = new byte[receiveBufferLength];
            var legacySimulation = new LegacySimulationState(initialState);

            _client = connectedClient;
            _stream = stream;
            _decoder = decoder;
            _expectedRoster = initialState.Roster;
            _legacySimulation = legacySimulation;
            _receiveBuffer = receiveBuffer;
            _receiveOffset = receiveOffset;
            _receiveReadCapacity = receiveReadCapacity;
            _maxPayloadLength = maxPayloadLength;
        }

        private TcpClientBattlePump(
            TcpClient connectedClient,
            ActiveRoster expectedRoster,
            LengthPrefixedFrameDecoder decoder,
            NetworkStream stream,
            byte[] receiveBuffer,
            int receiveOffset,
            int receiveReadCapacity,
            int maxPayloadLength)
        {
            _client = connectedClient;
            _stream = stream;
            _decoder = decoder;
            _expectedRoster = expectedRoster;
            _legacySimulation = null;
            _receiveBuffer = receiveBuffer;
            _receiveOffset = receiveOffset;
            _receiveReadCapacity = receiveReadCapacity;
            _maxPayloadLength = maxPayloadLength;
        }

        public BattleState ClientState =>
            _legacySimulation?.Simulation.State ??
            throw new InvalidOperationException(
                "Transport-only client pumps do not own a BattleSimulation.");

        internal static TcpClientBattlePump CreateTransportOnly(
            TcpClient connectedClient,
            ActiveRoster expectedRoster,
            int maxPayloadLength,
            int receiveBufferLength,
            int receiveOffset,
            int receiveReadCapacity)
        {
            if (connectedClient is null)
            {
                throw new ArgumentNullException(nameof(connectedClient));
            }

            if (expectedRoster is null)
            {
                throw new ArgumentNullException(nameof(expectedRoster));
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

            var decoder = new LengthPrefixedFrameDecoder(maxPayloadLength);
            if (connectedClient.Client.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException(
                    "The connected client must use IPv4.",
                    nameof(connectedClient));
            }

            if (!connectedClient.Connected)
            {
                throw new InvalidOperationException("The TCP client must already be connected.");
            }

            return new TcpClientBattlePump(
                connectedClient,
                expectedRoster,
                decoder,
                connectedClient.GetStream(),
                new byte[receiveBufferLength],
                receiveOffset,
                receiveReadCapacity,
                maxPayloadLength);
        }

        public void SendInput(PlayerId submittedPlayerId, InputFrame input)
        {
            ThrowIfDisposed();
            if (_faulted)
            {
                throw new InvalidOperationException("The TCP client battle pump is faulted.");
            }

            PlayerInputSubmissionMessage wire = ProtocolMapper.ToWire(submittedPlayerId, input);
            byte[] payload = wire.ToByteArray();
            byte[] framed = LengthPrefixedFrameEncoder.Encode(payload, _maxPayloadLength);
            try
            {
                _stream.Write(framed, 0, framed.Length);
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        public FrameData[] PumpReceiveOnce()
        {
            ThrowIfDisposed();
            if (_faulted)
            {
                throw new InvalidOperationException("The TCP client battle pump is faulted.");
            }

            try
            {
                FrameData[] frames = ReceiveMappedFrames();
                for (int index = 0; index < frames.Length; index++)
                {
                    _legacySimulation!.Simulation.Step(frames[index]);
                }

                return frames;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        internal FrameData[] PumpReceiveTransportOnlyOnce()
        {
            ThrowIfDisposed();
            if (_faulted)
            {
                throw new InvalidOperationException("The TCP client battle pump is faulted.");
            }

            try
            {
                return ReceiveMappedFrames();
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
            try
            {
                _stream.Dispose();
            }
            finally
            {
                _client.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TcpClientBattlePump));
            }
        }

        private FrameData[] ReceiveMappedFrames()
        {
            if (!_client.Client.Poll(0, SelectMode.SelectRead))
            {
                return Array.Empty<FrameData>();
            }

            if (_client.Client.Available == 0)
            {
                throw new EndOfStreamException("The TCP stream ended before battle completion.");
            }

            int bytesRead = _stream.Read(
                _receiveBuffer,
                _receiveOffset,
                _receiveReadCapacity);
            if (bytesRead == 0)
            {
                throw new EndOfStreamException("The TCP stream ended before battle completion.");
            }

            byte[][] payloads = _decoder.Feed(
                _receiveBuffer,
                _receiveOffset,
                bytesRead);
            if (payloads.Length == 0)
            {
                return Array.Empty<FrameData>();
            }

            var frames = new FrameData[payloads.Length];
            for (int index = 0; index < payloads.Length; index++)
            {
                AuthoritativeFrameMessage wire =
                    AuthoritativeFrameMessage.Parser.ParseFrom(payloads[index]);
                frames[index] = ProtocolMapper.ToDomain(wire, _expectedRoster);
            }

            return frames;
        }

        private sealed class LegacySimulationState
        {
            public LegacySimulationState(BattleState initialState)
            {
                Simulation = new BattleSimulation(initialState);
            }

            public BattleSimulation Simulation { get; }
        }
    }
}
