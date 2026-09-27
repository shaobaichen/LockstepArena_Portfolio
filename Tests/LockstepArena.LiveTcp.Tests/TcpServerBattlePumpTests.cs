using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.FrameSync;
using LockstepArena.Server.LiveTcp;
using LockstepArena.Server.ProtocolAuthority;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.LiveTcp.Tests
{
    internal static class TcpServerBattlePumpTests
    {
        private const int MaxPayloadLength = 4096;

        public static readonly TestCase[] All =
        {
            new TestCase(nameof(ServerPumpNoReadableBytesStillPollsExactlyOnce), ServerPumpNoReadableBytesStillPollsExactlyOnce),
            new TestCase(nameof(ServerPumpPerformsAtMostOneBoundedRead), ServerPumpPerformsAtMostOneBoundedRead),
            new TestCase(nameof(OneServerReadProcessesMultipleSubmissionsInWireOrder), OneServerReadProcessesMultipleSubmissionsInWireOrder),
            new TestCase(nameof(SubmitOutputsAreWrittenBeforeSamePumpPollOutputs), SubmitOutputsAreWrittenBeforeSamePumpPollOutputs),
            new TestCase(nameof(ZeroAuthorityOutputWritesNothing), ZeroAuthorityOutputWritesNothing),
            new TestCase(nameof(ServerEofFaultsSessionBeforePolling), ServerEofFaultsSessionBeforePolling),
            new TestCase(nameof(ServerOperationalFailureIsFailStop), ServerOperationalFailureIsFailStop),
        };

        private static void ServerPumpNoReadableBytesStillPollsExactlyOnce()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using var connection = new LoopbackConnection();
            using TcpServerBattlePump pump = CreatePump(connection, roster, 2U, 3);
            ProtocolAuthorityProcessor processor = GetProcessor(pump);
            TickDrivenFramePublisher publisher = ScheduledProtocolAuthorityTests.GetScheduledPublisher(processor);
            ScheduledProtocolAuthorityTests.ForceNextPollAdvances(processor, 1U);

            TestAssert.Equal(0, pump.PumpOnce());
            TestAssert.Equal(101UL, publisher.CollectionTick);
            TestAssert.Equal(0, pump.PumpOnce());
            TestAssert.Equal(101UL, publisher.CollectionTick);
        }

        private static void ServerPumpPerformsAtMostOneBoundedRead()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using var connection = new LoopbackConnection();
            using TcpServerBattlePump pump = CreatePump(connection, roster, 2U, 3);
            NetworkStream clientStream = connection.Client.GetStream();
            clientStream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
            WaitForAvailable(connection.Accepted.Client, 4);

            TestAssert.Equal(0, pump.PumpOnce());
            TestAssert.Throws<ProtocolMappingException>(() => pump.PumpOnce());
        }

        private static void OneServerReadProcessesMultipleSubmissionsInWireOrder()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(2);
            using var connection = new LoopbackConnection();
            using TcpServerBattlePump pump = CreatePump(connection, roster, 0U, 4096);
            byte[] stream = JoinFrames(new[]
            {
                CreateSubmission(roster, 100U, 0, 111),
                CreateSubmission(roster, 100U, 1, 222),
            });
            connection.Client.GetStream().Write(stream, 0, stream.Length);
            WaitForAvailable(connection.Accepted.Client, stream.Length);

            TestAssert.Equal(1, pump.PumpOnce());
            FrameData[] frames = ReadAuthorityFrames(connection.Client, roster, 1);
            TestAssert.Equal(100U, frames[0].Tick);
            TestAssert.Equal((ushort)111, frames[0].GetInput(new PlayerSlot(0)).Aim);
            TestAssert.Equal((ushort)222, frames[0].GetInput(new PlayerSlot(1)).Aim);
        }

        private static void SubmitOutputsAreWrittenBeforeSamePumpPollOutputs()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(4);
            using var connection = new LoopbackConnection();
            using TcpServerBattlePump pump = CreatePump(connection, roster, 2U, 4096);
            ProtocolAuthorityProcessor processor = GetProcessor(pump);
            CompleteDirect(processor, roster, 101U);
            CompleteDirect(processor, roster, 102U);
            SubmitDirect(processor, roster, 100U, 0);
            SubmitDirect(processor, roster, 100U, 2);
            SubmitDirect(processor, roster, 100U, 1);
            ScheduledProtocolAuthorityTests.ForceNextPollAdvances(processor, 3U);
            TestAssert.Equal(0, processor.PollAuthority().Length);
            ScheduledProtocolAuthorityTests.ForceNextPollAdvances(processor, 1U);
            byte[] last = Frame(CreateSubmission(roster, 100U, 3, 333));
            connection.Client.GetStream().Write(last, 0, last.Length);
            WaitForAvailable(connection.Accepted.Client, last.Length);

            TestAssert.Equal(3, pump.PumpOnce());
            FrameData[] frames = ReadAuthorityFrames(connection.Client, roster, 3);
            TestAssert.SequenceEqual(new[] { 100U, 101U, 102U },
                new[] { frames[0].Tick, frames[1].Tick, frames[2].Tick });
        }

        private static void ZeroAuthorityOutputWritesNothing()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using var connection = new LoopbackConnection();
            using TcpServerBattlePump pump = CreatePump(connection, roster, 2U, 3);
            TestAssert.Equal(0, pump.PumpOnce());
            TestAssert.Equal(0, connection.Client.Client.Available);
        }

        private static void ServerEofFaultsSessionBeforePolling()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using var connection = new LoopbackConnection();
            using TcpServerBattlePump pump = CreatePump(connection, roster, 2U, 3);
            ProtocolAuthorityProcessor processor = GetProcessor(pump);
            TickDrivenFramePublisher publisher = ScheduledProtocolAuthorityTests.GetScheduledPublisher(processor);
            ulong before = publisher.CollectionTick;
            connection.Client.Client.Shutdown(SocketShutdown.Send);
            WaitForReadable(connection.Accepted.Client);

            TestAssert.Throws<EndOfStreamException>(() => pump.PumpOnce());
            TestAssert.Equal(before, publisher.CollectionTick);
            TestAssert.Throws<InvalidOperationException>(() => pump.PumpOnce());
        }

        private static void ServerOperationalFailureIsFailStop()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using var connection = new LoopbackConnection();
            using TcpServerBattlePump pump = CreatePump(connection, roster, 2U, 4096);
            byte[] malformed = Frame(new byte[] { 0x12, 0x05, 0x01 });
            connection.Client.GetStream().Write(malformed, 0, malformed.Length);
            WaitForAvailable(connection.Accepted.Client, malformed.Length);

            TestAssert.Throws<InvalidProtocolBufferException>(() => pump.PumpOnce());
            TestAssert.Throws<InvalidOperationException>(() => pump.PumpOnce());
        }

        internal static TcpServerBattlePump CreatePump(
            LoopbackConnection connection,
            ActiveRoster roster,
            uint inputDelayTicks,
            int readCapacity)
        {
            return new TcpServerBattlePump(
                connection.Accepted,
                ScheduledProtocolAuthorityTests.CreateInitialState(roster),
                inputDelayTicks,
                8U,
                8,
                MaxPayloadLength,
                Math.Max(16, readCapacity + 3),
                3,
                readCapacity);
        }

        internal static ProtocolAuthorityProcessor GetProcessor(TcpServerBattlePump pump)
        {
            return (ProtocolAuthorityProcessor)(ScheduledProtocolAuthorityTests.GetUniquePrivateField(
                typeof(TcpServerBattlePump), typeof(ProtocolAuthorityProcessor)).GetValue(pump)
                ?? throw new InvalidOperationException("Server pump processor was null."));
        }

        internal static byte[] CreateSubmission(
            ActiveRoster roster,
            uint tick,
            int slotValue,
            int aim)
        {
            PlayerSlot slot = new PlayerSlot(slotValue);
            var input = new InputFrame(tick, slot, 0, 0, checked((ushort)aim));
            return ProtocolMapper.ToWire(roster.GetPlayerId(slot), input).ToByteArray();
        }

        internal static byte[] Frame(byte[] payload)
        {
            return LengthPrefixedFrameEncoder.Encode(payload, MaxPayloadLength);
        }

        internal static byte[] JoinFrames(byte[][] payloads)
        {
            var frames = new byte[payloads.Length][];
            int length = 0;
            for (int index = 0; index < payloads.Length; index++)
            {
                frames[index] = Frame(payloads[index]);
                length = checked(length + frames[index].Length);
            }

            var result = new byte[length];
            int offset = 0;
            for (int index = 0; index < frames.Length; index++)
            {
                Array.Copy(frames[index], 0, result, offset, frames[index].Length);
                offset += frames[index].Length;
            }

            return result;
        }

        internal static FrameData[] ReadAuthorityFrames(
            TcpClient client,
            ActiveRoster roster,
            int expectedCount)
        {
            var decoder = new LengthPrefixedFrameDecoder(MaxPayloadLength);
            var frames = new List<FrameData>();
            var buffer = new byte[4096];
            Stopwatch deadline = Stopwatch.StartNew();
            while (frames.Count < expectedCount && deadline.ElapsedMilliseconds < 5_000)
            {
                if (!client.Client.Poll(0, SelectMode.SelectRead))
                {
                    continue;
                }

                int read = client.GetStream().Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                byte[][] payloads = decoder.Feed(buffer, 0, read);
                for (int index = 0; index < payloads.Length; index++)
                {
                    frames.Add(ProtocolMapper.ToDomain(
                        AuthoritativeFrameMessage.Parser.ParseFrom(payloads[index]), roster));
                }
            }

            TestAssert.Equal(expectedCount, frames.Count);
            return frames.ToArray();
        }

        internal static void WaitForAvailable(Socket socket, int count)
        {
            Stopwatch deadline = Stopwatch.StartNew();
            while (socket.Available < count && deadline.ElapsedMilliseconds < 5_000)
            {
            }

            TestAssert.True(socket.Available >= count);
        }

        internal static void WaitForReadable(Socket socket)
        {
            Stopwatch deadline = Stopwatch.StartNew();
            while (!socket.Poll(0, SelectMode.SelectRead) && deadline.ElapsedMilliseconds < 5_000)
            {
            }

            TestAssert.True(socket.Poll(0, SelectMode.SelectRead));
        }

        private static void CompleteDirect(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster,
            uint tick)
        {
            for (int slot = 0; slot < roster.Count; slot++)
            {
                SubmitDirect(processor, roster, tick, slot);
            }
        }

        private static void SubmitDirect(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster,
            uint tick,
            int slot)
        {
            _ = processor.SubmitPlayerInputPayload(CreateSubmission(roster, tick, slot, 100 + slot));
        }
    }

    internal sealed class LoopbackConnection : IDisposable
    {
        private readonly TcpListener _listener;

        public LoopbackConnection()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start(1);
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            Client = new TcpClient(AddressFamily.InterNetwork);
            Client.Connect(IPAddress.Loopback, endpoint.Port);
            Accepted = _listener.AcceptTcpClient();
        }

        public TcpClient Client { get; }

        public TcpClient Accepted { get; }

        public void Dispose()
        {
            Accepted.Dispose();
            Client.Dispose();
            _listener.Stop();
        }
    }
}
