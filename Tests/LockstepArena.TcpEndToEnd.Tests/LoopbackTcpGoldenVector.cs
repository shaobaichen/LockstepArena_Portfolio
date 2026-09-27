using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.ProtocolAuthority;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.TcpEndToEnd.Tests
{
    internal sealed class LoopbackTcpGoldenResult
    {
        public LoopbackTcpGoldenResult(
            IPAddress listenerAddress,
            int listenerPort,
            IPAddress clientRemoteAddress,
            int clientRemotePort,
            int serverReadCallCount,
            int clientReadCallCount,
            byte[][] submissionPayloads,
            byte[][] recoveredSubmissionPayloads,
            int[] processorOutputCounts,
            byte[][] authoritativePayloads,
            byte[][] recoveredAuthoritativePayloads,
            FrameData[] authoritativeFrames,
            BattleState[] clientStates,
            ulong[] clientDigests,
            BattleState serverState,
            BattleState clientState,
            uint nextPublishTick)
        {
            ListenerAddress = listenerAddress;
            ListenerPort = listenerPort;
            ClientRemoteAddress = clientRemoteAddress;
            ClientRemotePort = clientRemotePort;
            ServerReadCallCount = serverReadCallCount;
            ClientReadCallCount = clientReadCallCount;
            SubmissionPayloads = submissionPayloads;
            RecoveredSubmissionPayloads = recoveredSubmissionPayloads;
            ProcessorOutputCounts = processorOutputCounts;
            AuthoritativePayloads = authoritativePayloads;
            RecoveredAuthoritativePayloads = recoveredAuthoritativePayloads;
            AuthoritativeFrames = authoritativeFrames;
            ClientStates = clientStates;
            ClientDigests = clientDigests;
            ServerState = serverState;
            ClientState = clientState;
            NextPublishTick = nextPublishTick;
        }

        public IPAddress ListenerAddress { get; }

        public int ListenerPort { get; }

        public IPAddress ClientRemoteAddress { get; }

        public int ClientRemotePort { get; }

        public int ServerReadCallCount { get; }

        public int ClientReadCallCount { get; }

        public byte[][] SubmissionPayloads { get; }

        public byte[][] RecoveredSubmissionPayloads { get; }

        public int[] ProcessorOutputCounts { get; }

        public byte[][] AuthoritativePayloads { get; }

        public byte[][] RecoveredAuthoritativePayloads { get; }

        public FrameData[] AuthoritativeFrames { get; }

        public BattleState[] ClientStates { get; }

        public ulong[] ClientDigests { get; }

        public BattleState ServerState { get; }

        public BattleState ClientState { get; }

        public uint NextPublishTick { get; }
    }

    internal static class LoopbackTcpGoldenVector
    {
        private const int MaxPayloadLength = 4096;

        internal static LoopbackTcpGoldenResult Run()
        {
            ActiveRoster serverRoster = CreateRoster();
            ActiveRoster clientRoster = CreateRoster();
            BattleState serverInitialState = CreateInitialState(serverRoster);
            BattleState clientInitialState = CreateInitialState(clientRoster);
            byte[][] submissionPayloads = CreateSubmissionPayloads(serverRoster);
            byte[] submissionStream = FramePayloads(submissionPayloads);
            var processor = new ProtocolAuthorityProcessor(serverInitialState, 2U, 5);
            var clientSimulation = new BattleSimulation(clientInitialState);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            try
            {
                var listenerEndpoint = (IPEndPoint)listener.LocalEndpoint;
                using var client = new TcpClient(AddressFamily.InterNetwork);
                client.Connect(IPAddress.Loopback, listenerEndpoint.Port);
                using TcpClient acceptedClient = listener.AcceptTcpClient();
                var clientRemoteEndpoint = (IPEndPoint)(client.Client.RemoteEndPoint ??
                    throw new InvalidOperationException("Client remote endpoint was not assigned."));
                using NetworkStream clientStream = client.GetStream();
                using NetworkStream serverStream = acceptedClient.GetStream();

                clientStream.Write(submissionStream, 0, submissionStream.Length);

                var recoveredSubmissions = new List<byte[]>();
                int serverReadCallCount = ReadPayloads(
                    serverStream,
                    new LengthPrefixedFrameDecoder(MaxPayloadLength),
                    new byte[16],
                    3,
                    3,
                    12,
                    recoveredSubmissions);

                var outputCounts = new int[recoveredSubmissions.Count];
                var authoritativePayloads = new List<byte[]>();
                for (int index = 0; index < recoveredSubmissions.Count; index++)
                {
                    byte[][] output = processor.SubmitPlayerInputPayload(recoveredSubmissions[index]);
                    outputCounts[index] = output.Length;
                    authoritativePayloads.AddRange(output);
                }

                byte[][] authoritativePayloadArray = authoritativePayloads.ToArray();
                byte[] authoritativeStream = FramePayloads(authoritativePayloadArray);
                serverStream.Write(authoritativeStream, 0, authoritativeStream.Length);

                var recoveredAuthoritative = new List<byte[]>();
                int clientReadCallCount = ReadPayloads(
                    clientStream,
                    new LengthPrefixedFrameDecoder(MaxPayloadLength),
                    new byte[16],
                    5,
                    5,
                    3,
                    recoveredAuthoritative);

                var frames = new FrameData[recoveredAuthoritative.Count];
                var clientStates = new BattleState[recoveredAuthoritative.Count];
                var clientDigests = new ulong[recoveredAuthoritative.Count];
                for (int index = 0; index < recoveredAuthoritative.Count; index++)
                {
                    AuthoritativeFrameMessage wire =
                        AuthoritativeFrameMessage.Parser.ParseFrom(recoveredAuthoritative[index]);
                    FrameData frame = ProtocolMapper.ToDomain(wire, clientRoster);
                    clientSimulation.Step(frame);
                    frames[index] = frame;
                    clientStates[index] = clientSimulation.State;
                    clientDigests[index] = StateDigest.Compute(clientSimulation.State);
                }

                return new LoopbackTcpGoldenResult(
                    listenerEndpoint.Address,
                    listenerEndpoint.Port,
                    clientRemoteEndpoint.Address,
                    clientRemoteEndpoint.Port,
                    serverReadCallCount,
                    clientReadCallCount,
                    submissionPayloads,
                    recoveredSubmissions.ToArray(),
                    outputCounts,
                    authoritativePayloadArray,
                    recoveredAuthoritative.ToArray(),
                    frames,
                    clientStates,
                    clientDigests,
                    processor.ServerState,
                    clientSimulation.State,
                    processor.NextPublishTick);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static int ReadPayloads(
            NetworkStream stream,
            LengthPrefixedFrameDecoder decoder,
            byte[] receiveBuffer,
            int offset,
            int readCapacity,
            int requiredPayloadCount,
            List<byte[]> recoveredPayloads)
        {
            int readCallCount = 0;
            while (recoveredPayloads.Count < requiredPayloadCount)
            {
                int bytesRead = stream.Read(receiveBuffer, offset, readCapacity);
                readCallCount++;
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("TCP stream ended before all required payloads were recovered.");
                }

                byte[][] completed = decoder.Feed(receiveBuffer, offset, bytesRead);
                recoveredPayloads.AddRange(completed);
                Array.Fill(receiveBuffer, (byte)0xA5);
            }

            return readCallCount;
        }

        private static byte[][] CreateSubmissionPayloads(ActiveRoster roster)
        {
            var arrivalOrder = new (uint Tick, int Slot)[]
            {
                (100U, 0), (100U, 2), (100U, 1),
                (101U, 3), (101U, 1), (101U, 0), (101U, 2),
                (102U, 2), (102U, 0), (102U, 3), (102U, 1),
                (100U, 3),
            };
            var payloads = new byte[arrivalOrder.Length][];
            for (int index = 0; index < arrivalOrder.Length; index++)
            {
                (uint tick, int slotValue) = arrivalOrder[index];
                var slot = new PlayerSlot(slotValue);
                PlayerInputSubmissionMessage wire = ProtocolMapper.ToWire(
                    roster.GetPlayerId(slot),
                    CreateInput(tick, slotValue));
                payloads[index] = wire.ToByteArray();
            }

            return payloads;
        }

        private static byte[] FramePayloads(byte[][] payloads)
        {
            var frames = new byte[payloads.Length][];
            int length = 0;
            for (int index = 0; index < payloads.Length; index++)
            {
                frames[index] = LengthPrefixedFrameEncoder.Encode(payloads[index], MaxPayloadLength);
                length = checked(length + frames[index].Length);
            }

            var stream = new byte[length];
            int offset = 0;
            for (int index = 0; index < frames.Length; index++)
            {
                Array.Copy(frames[index], 0, stream, offset, frames[index].Length);
                offset += frames[index].Length;
            }

            return stream;
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
