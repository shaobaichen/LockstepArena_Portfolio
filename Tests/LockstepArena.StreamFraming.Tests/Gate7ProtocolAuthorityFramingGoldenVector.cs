using System;
using System.Collections.Generic;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.ProtocolAuthority;
using LockstepArena.Simulation;

namespace LockstepArena.StreamFraming.Tests
{
    internal sealed class Gate7ProtocolAuthorityFramingGoldenResult
    {
        public Gate7ProtocolAuthorityFramingGoldenResult(
            byte[][] submissionPayloads,
            byte[][] recoveredSubmissionPayloads,
            int[] preGapOutputLengths,
            byte[][] authoritativePayloads,
            byte[][] recoveredAuthoritativePayloads,
            FrameData[] authoritativeFrames,
            BattleState[] clientStates,
            ulong[] clientDigests,
            BattleState serverState,
            BattleState clientState,
            uint nextPublishTick)
        {
            SubmissionPayloads = submissionPayloads;
            RecoveredSubmissionPayloads = recoveredSubmissionPayloads;
            PreGapOutputLengths = preGapOutputLengths;
            AuthoritativePayloads = authoritativePayloads;
            RecoveredAuthoritativePayloads = recoveredAuthoritativePayloads;
            AuthoritativeFrames = authoritativeFrames;
            ClientStates = clientStates;
            ClientDigests = clientDigests;
            ServerState = serverState;
            ClientState = clientState;
            NextPublishTick = nextPublishTick;
        }

        public byte[][] SubmissionPayloads { get; }

        public byte[][] RecoveredSubmissionPayloads { get; }

        public int[] PreGapOutputLengths { get; }

        public byte[][] AuthoritativePayloads { get; }

        public byte[][] RecoveredAuthoritativePayloads { get; }

        public FrameData[] AuthoritativeFrames { get; }

        public BattleState[] ClientStates { get; }

        public ulong[] ClientDigests { get; }

        public BattleState ServerState { get; }

        public BattleState ClientState { get; }

        public uint NextPublishTick { get; }
    }

    internal static class Gate7ProtocolAuthorityFramingGoldenVector
    {
        private const int MaxPayloadLength = 4096;

        private static readonly (uint Tick, int Slot)[] SubmissionOrder =
        {
            (100U, 0), (100U, 2), (100U, 1),
            (101U, 3), (101U, 1), (101U, 0), (101U, 2),
            (102U, 2), (102U, 0), (102U, 3), (102U, 1),
            (100U, 3),
        };

        public static Gate7ProtocolAuthorityFramingGoldenResult RunPrimarySegmentation()
        {
            return Run(
                new int[] { 1, 2, 7, 3, 11, 5 },
                new int[] { 4, 1, 9, 2, 13, 3 });
        }

        public static Gate7ProtocolAuthorityFramingGoldenResult RunAlternateSegmentation()
        {
            return Run(
                new int[] { 17, 1, 1, 6 },
                new int[] { 2, 2, 2, 2, 19 });
        }

        private static Gate7ProtocolAuthorityFramingGoldenResult Run(
            int[] clientToServerSegments,
            int[] serverToClientSegments)
        {
            ActiveRoster serverRoster = CreateRoster();
            ActiveRoster clientRoster = CreateRoster();
            var processor = new ProtocolAuthorityProcessor(
                CreateInitialState(serverRoster),
                2U,
                5);
            var clientSimulation = new BattleSimulation(CreateInitialState(clientRoster));

            byte[][] submissionPayloads = CreateSubmissionPayloads(serverRoster);
            byte[] clientToServerStream = FrameAndConcatenate(submissionPayloads);
            byte[][] recoveredSubmissions = DecodeContinuousStream(
                clientToServerStream,
                clientToServerSegments,
                3);

            var preGapOutputLengths = new int[recoveredSubmissions.Length - 1];
            byte[][] authoritativePayloads = Array.Empty<byte[]>();
            for (int index = 0; index < recoveredSubmissions.Length; index++)
            {
                byte[][] output = processor.SubmitPlayerInputPayload(recoveredSubmissions[index]);
                if (index < preGapOutputLengths.Length)
                {
                    preGapOutputLengths[index] = output.Length;
                }
                else
                {
                    authoritativePayloads = output;
                }
            }

            byte[] serverToClientStream = FrameAndConcatenate(authoritativePayloads);
            byte[][] recoveredAuthoritativePayloads = DecodeContinuousStream(
                serverToClientStream,
                serverToClientSegments,
                5);
            var frames = new FrameData[recoveredAuthoritativePayloads.Length];
            var clientStates = new BattleState[recoveredAuthoritativePayloads.Length];
            var clientDigests = new ulong[recoveredAuthoritativePayloads.Length];
            for (int index = 0; index < recoveredAuthoritativePayloads.Length; index++)
            {
                AuthoritativeFrameMessage wire =
                    AuthoritativeFrameMessage.Parser.ParseFrom(recoveredAuthoritativePayloads[index]);
                FrameData frame = ProtocolMapper.ToDomain(wire, clientRoster);
                clientSimulation.Step(frame);
                frames[index] = frame;
                clientStates[index] = clientSimulation.State;
                clientDigests[index] = StateDigest.Compute(clientSimulation.State);
            }

            return new Gate7ProtocolAuthorityFramingGoldenResult(
                submissionPayloads,
                recoveredSubmissions,
                preGapOutputLengths,
                authoritativePayloads,
                recoveredAuthoritativePayloads,
                frames,
                clientStates,
                clientDigests,
                processor.ServerState,
                clientSimulation.State,
                processor.NextPublishTick);
        }

        private static byte[][] CreateSubmissionPayloads(ActiveRoster roster)
        {
            var payloads = new byte[SubmissionOrder.Length][];
            for (int index = 0; index < SubmissionOrder.Length; index++)
            {
                (uint tick, int slotValue) = SubmissionOrder[index];
                PlayerSlot slot = new PlayerSlot(slotValue);
                PlayerInputSubmissionMessage wire = ProtocolMapper.ToWire(
                    roster.GetPlayerId(slot),
                    CreateInput(tick, slotValue));
                payloads[index] = wire.ToByteArray();
            }

            return payloads;
        }

        private static byte[] FrameAndConcatenate(byte[][] payloads)
        {
            var frames = new byte[payloads.Length][];
            int streamLength = 0;
            for (int index = 0; index < payloads.Length; index++)
            {
                frames[index] = LengthPrefixedFrameEncoder.Encode(payloads[index], MaxPayloadLength);
                streamLength = checked(streamLength + frames[index].Length);
            }

            byte[] stream = new byte[streamLength];
            int offset = 0;
            foreach (byte[] frame in frames)
            {
                Array.Copy(frame, 0, stream, offset, frame.Length);
                offset += frame.Length;
            }

            return stream;
        }

        private static byte[][] DecodeContinuousStream(
            byte[] stream,
            int[] segmentPattern,
            int receiveBufferOffset)
        {
            var decoder = new LengthPrefixedFrameDecoder(MaxPayloadLength);
            var recovered = new List<byte[]>();
            int largestSegment = 0;
            foreach (int value in segmentPattern)
            {
                largestSegment = Math.Max(largestSegment, value);
            }

            byte[] receiveBuffer = new byte[receiveBufferOffset + largestSegment + 1];
            int streamOffset = 0;
            int patternIndex = 0;
            while (streamOffset < stream.Length)
            {
                int requested = segmentPattern[patternIndex % segmentPattern.Length];
                int count = Math.Min(requested, stream.Length - streamOffset);
                Array.Fill(receiveBuffer, (byte)0xCC);
                Array.Copy(stream, streamOffset, receiveBuffer, receiveBufferOffset, count);
                recovered.AddRange(decoder.Feed(receiveBuffer, receiveBufferOffset, count));
                Array.Fill(receiveBuffer, (byte)0xDD);
                streamOffset += count;
                patternIndex++;
            }

            return recovered.ToArray();
        }

        private static ActiveRoster CreateRoster()
        {
            return new ActiveRoster(new[]
            {
                new PlayerId(0x0102030405060708UL),
                new PlayerId(0x000000000000002AUL),
                new PlayerId(0xFFEEDDCCBBAA0099UL),
                new PlayerId(0x00000000000F4243UL),
            });
        }

        private static BattleState CreateInitialState(ActiveRoster roster)
        {
            return new BattleState(100U, roster, new[]
            {
                new PlayerState(-300, 0, 1_000),
                new PlayerState(300, 0, 2_000),
                new PlayerState(0, -300, 3_000),
                new PlayerState(0, 300, 4_000),
            });
        }

        private static InputFrame CreateInput(uint tick, int slotValue)
        {
            (int moveX, int moveZ, int aim) = (tick, slotValue) switch
            {
                (100U, 0) => (1, 0, 10_100),
                (100U, 1) => (-1, 0, 20_100),
                (100U, 2) => (0, 1, 30_100),
                (100U, 3) => (0, -1, 40_100),
                (101U, 0) => (0, 1, 10_101),
                (101U, 1) => (0, -1, 20_101),
                (101U, 2) => (1, 0, 30_101),
                (101U, 3) => (-1, 0, 40_101),
                (102U, 0) => (-1, 0, 10_102),
                (102U, 1) => (1, 0, 20_102),
                (102U, 2) => (0, -1, 30_102),
                (102U, 3) => (0, 1, 40_102),
                _ => throw new ArgumentOutOfRangeException(nameof(tick)),
            };

            return new InputFrame(
                tick,
                new PlayerSlot(slotValue),
                checked((sbyte)moveX),
                checked((sbyte)moveZ),
                checked((ushort)aim));
        }
    }
}
