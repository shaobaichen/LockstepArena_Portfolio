using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using Google.Protobuf;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.LiveTcp;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.LiveTcp.Tests
{
    internal static class TcpClientBattlePumpTests
    {
        private const int MaxPayloadLength = 4096;

        public static readonly TestCase[] All =
        {
            new TestCase(nameof(ClientSendWritesOneFramedSubmission), ClientSendWritesOneFramedSubmission),
            new TestCase(nameof(ClientReceiveNoReadableBytesReturnsEmpty), ClientReceiveNoReadableBytesReturnsEmpty),
            new TestCase(nameof(OneClientReadProcessesMultipleAuthorityPayloadsInOrder), OneClientReadProcessesMultipleAuthorityPayloadsInOrder),
            new TestCase(nameof(ClientUsesExpectedRosterAndStepsOnlyAuthoritativeFrames), ClientUsesExpectedRosterAndStepsOnlyAuthoritativeFrames),
            new TestCase(nameof(ClientEofOrMalformedAuthorityIsFailStop), ClientEofOrMalformedAuthorityIsFailStop),
            new TestCase(nameof(DisposedPumpsRejectFurtherOperations), DisposedPumpsRejectFurtherOperations),
        };

        private static void ClientSendWritesOneFramedSubmission()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using var connection = new LoopbackConnection();
            using TcpClientBattlePump pump = CreatePump(connection, roster, 4096);
            InputFrame input = ScheduledProtocolAuthorityTests.CreateInput(100U, 0);
            pump.SendInput(roster.GetPlayerId(new PlayerSlot(0)), input);

            byte[] payload = ReadOnePayload(connection.Accepted);
            (PlayerId playerId, InputFrame decoded) = ProtocolMapper.ToDomain(
                PlayerInputSubmissionMessage.Parser.ParseFrom(payload));
            TestAssert.Equal(roster.GetPlayerId(new PlayerSlot(0)), playerId);
            AssertInput(input, decoded);
        }

        private static void ClientReceiveNoReadableBytesReturnsEmpty()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            BattleState initial = ScheduledProtocolAuthorityTests.CreateInitialState(roster);
            using var connection = new LoopbackConnection();
            using var pump = new TcpClientBattlePump(connection.Client, initial, MaxPayloadLength, 16, 5, 5);

            TestAssert.Equal(0, pump.PumpReceiveOnce().Length);
            TestAssert.Same(initial, pump.ClientState);
        }

        private static void OneClientReadProcessesMultipleAuthorityPayloadsInOrder()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using var connection = new LoopbackConnection();
            using TcpClientBattlePump pump = CreatePump(connection, roster, 4096);
            byte[] stream = TcpServerBattlePumpTests.JoinFrames(new[]
            {
                CreateAuthorityPayload(roster, 100U, 101),
                CreateAuthorityPayload(roster, 101U, 102),
            });
            connection.Accepted.GetStream().Write(stream, 0, stream.Length);
            TcpServerBattlePumpTests.WaitForAvailable(connection.Client.Client, stream.Length);

            FrameData[] frames = pump.PumpReceiveOnce();
            TestAssert.Equal(2, frames.Length);
            TestAssert.Equal(100U, frames[0].Tick);
            TestAssert.Equal(101U, frames[1].Tick);
            TestAssert.Equal(102U, pump.ClientState.Tick);
        }

        private static void ClientUsesExpectedRosterAndStepsOnlyAuthoritativeFrames()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using (var connection = new LoopbackConnection())
            using (TcpClientBattlePump pump = CreatePump(connection, roster, 4096))
            {
                InputFrame localInput = ScheduledProtocolAuthorityTests.CreateInput(100U, 0);
                pump.SendInput(roster.GetPlayerId(new PlayerSlot(0)), localInput);
                TestAssert.Equal(100U, pump.ClientState.Tick);
                _ = ReadOnePayload(connection.Accepted);

                byte[] authority = TcpServerBattlePumpTests.Frame(CreateAuthorityPayload(roster, 100U, 101));
                connection.Accepted.GetStream().Write(authority, 0, authority.Length);
                TcpServerBattlePumpTests.WaitForAvailable(connection.Client.Client, authority.Length);
                TestAssert.Equal(1, pump.PumpReceiveOnce().Length);
                TestAssert.Equal(101U, pump.ClientState.Tick);
            }

            ActiveRoster different = new ActiveRoster(new[] { new PlayerId(999UL) });
            using var mismatchConnection = new LoopbackConnection();
            using TcpClientBattlePump mismatchPump = CreatePump(mismatchConnection, roster, 4096);
            byte[] mismatch = TcpServerBattlePumpTests.Frame(CreateAuthorityPayload(different, 100U, 101));
            mismatchConnection.Accepted.GetStream().Write(mismatch, 0, mismatch.Length);
            TcpServerBattlePumpTests.WaitForAvailable(mismatchConnection.Client.Client, mismatch.Length);
            TestAssert.Throws<ProtocolMappingException>(() => mismatchPump.PumpReceiveOnce());
            TestAssert.Throws<InvalidOperationException>(() => mismatchPump.PumpReceiveOnce());
        }

        private static void ClientEofOrMalformedAuthorityIsFailStop()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using (var eofConnection = new LoopbackConnection())
            using (TcpClientBattlePump eofPump = CreatePump(eofConnection, roster, 5))
            {
                eofConnection.Accepted.Client.Shutdown(SocketShutdown.Send);
                TcpServerBattlePumpTests.WaitForReadable(eofConnection.Client.Client);
                TestAssert.Throws<EndOfStreamException>(() => eofPump.PumpReceiveOnce());
                TestAssert.Throws<InvalidOperationException>(() => eofPump.PumpReceiveOnce());
            }

            using var malformedConnection = new LoopbackConnection();
            using TcpClientBattlePump malformedPump = CreatePump(malformedConnection, roster, 4096);
            byte[] malformed = TcpServerBattlePumpTests.Frame(new byte[] { 0x12, 0x05, 0x01 });
            malformedConnection.Accepted.GetStream().Write(malformed, 0, malformed.Length);
            TcpServerBattlePumpTests.WaitForAvailable(malformedConnection.Client.Client, malformed.Length);
            TestAssert.Throws<InvalidProtocolBufferException>(() => malformedPump.PumpReceiveOnce());
            TestAssert.Throws<InvalidOperationException>(() => malformedPump.PumpReceiveOnce());
        }

        private static void DisposedPumpsRejectFurtherOperations()
        {
            ActiveRoster roster = ScheduledProtocolAuthorityTests.CreateRoster(1);
            using var connection = new LoopbackConnection();
            var server = TcpServerBattlePumpTests.CreatePump(connection, roster, 2U, 3);
            var client = CreatePump(connection, roster, 5);
            server.Dispose();
            client.Dispose();
            server.Dispose();
            client.Dispose();

            TestAssert.Throws<ObjectDisposedException>(() => server.PumpOnce());
            TestAssert.Throws<ObjectDisposedException>(() => client.PumpReceiveOnce());
            TestAssert.Throws<ObjectDisposedException>(() => client.SendInput(
                roster.GetPlayerId(new PlayerSlot(0)),
                ScheduledProtocolAuthorityTests.CreateInput(100U, 0)));
        }

        internal static TcpClientBattlePump CreatePump(
            LoopbackConnection connection,
            ActiveRoster roster,
            int readCapacity)
        {
            return new TcpClientBattlePump(
                connection.Client,
                ScheduledProtocolAuthorityTests.CreateInitialState(roster),
                MaxPayloadLength,
                Math.Max(16, readCapacity + 5),
                5,
                readCapacity);
        }

        internal static byte[] CreateAuthorityPayload(ActiveRoster roster, uint tick, int aim)
        {
            var input = new InputFrame(tick, new PlayerSlot(0), 1, 0, checked((ushort)aim));
            FrameData frame = FrameData.Create(roster, tick, new[] { input });
            return ProtocolMapper.ToWire(frame).ToByteArray();
        }

        private static byte[] ReadOnePayload(TcpClient peer)
        {
            var decoder = new LengthPrefixedFrameDecoder(MaxPayloadLength);
            var buffer = new byte[4096];
            Stopwatch deadline = Stopwatch.StartNew();
            while (deadline.ElapsedMilliseconds < 5_000)
            {
                if (!peer.Client.Poll(0, SelectMode.SelectRead))
                {
                    continue;
                }

                int read = peer.GetStream().Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                byte[][] payloads = decoder.Feed(buffer, 0, read);
                if (payloads.Length > 0)
                {
                    TestAssert.Equal(1, payloads.Length);
                    return payloads[0];
                }
            }

            throw new InvalidOperationException("Timed out waiting for the test payload.");
        }

        private static void AssertInput(InputFrame expected, InputFrame actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.Equal(expected.PlayerSlot, actual.PlayerSlot);
            TestAssert.Equal(expected.MoveX, actual.MoveX);
            TestAssert.Equal(expected.MoveZ, actual.MoveZ);
            TestAssert.Equal(expected.Aim, actual.Aim);
        }
    }
}
