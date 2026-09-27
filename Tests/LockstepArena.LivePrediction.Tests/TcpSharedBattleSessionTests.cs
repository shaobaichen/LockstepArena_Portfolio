using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.FrameSync;
using LockstepArena.Server.LiveTcp;
using LockstepArena.Server.ProtocolAuthority;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.LivePrediction.Tests
{
    internal static class TcpSharedBattleSessionTests
    {
        private const int MaxPayloadLength = 4096;

        public static readonly TestCase[] ConstructionTests =
        {
            new TestCase(nameof(ParticipantBindingRejectsNullTcpClient), ParticipantBindingRejectsNullTcpClient),
            new TestCase(nameof(SessionRejectsNullInitialState), SessionRejectsNullInitialState),
            new TestCase(nameof(SessionRejectsNullParticipantArray), SessionRejectsNullParticipantArray),
            new TestCase(nameof(SessionRejectsWrongParticipantCount), SessionRejectsWrongParticipantCount),
            new TestCase(nameof(SessionRejectsNullParticipantEntry), SessionRejectsNullParticipantEntry),
            new TestCase(nameof(SessionRejectsDuplicateTcpClient), SessionRejectsDuplicateTcpClient),
            new TestCase(nameof(SessionRejectsDuplicatePlayerId), SessionRejectsDuplicatePlayerId),
            new TestCase(nameof(SessionRejectsDuplicatePlayerSlot), SessionRejectsDuplicatePlayerSlot),
            new TestCase(nameof(SessionRejectsOutOfRosterOrMismatchedBinding), SessionRejectsOutOfRosterOrMismatchedBinding),
            new TestCase(nameof(SessionRejectsDisconnectedParticipant), SessionRejectsDisconnectedParticipant),
            new TestCase(nameof(SessionRejectsNonIpv4Participant), SessionRejectsNonIpv4Participant),
            new TestCase(nameof(ConstructionFailureLeavesCallerConnectionsUsable), ConstructionFailureLeavesCallerConnectionsUsable),
            new TestCase(nameof(SharedSessionUsesExactlyOneProtocolAuthorityProcessor), SharedSessionUsesExactlyOneProtocolAuthorityProcessor),
        };

        public static readonly TestCase[] PumpTests =
        {
            new TestCase(nameof(TwoRealClientsFeedOneSharedAuthorityTimeline), TwoRealClientsFeedOneSharedAuthorityTimeline),
            new TestCase(nameof(PlayerIdSpoofIsRejectedBeforeAuthorityMutation), PlayerIdSpoofIsRejectedBeforeAuthorityMutation),
            new TestCase(nameof(PlayerSlotSpoofIsRejectedBeforeAuthorityMutation), PlayerSlotSpoofIsRejectedBeforeAuthorityMutation),
            new TestCase(nameof(ParticipantsAreReadInIncreasingPlayerSlotOrder), ParticipantsAreReadInIncreasingPlayerSlotOrder),
            new TestCase(nameof(SuccessfulNonterminalPumpPollsAuthorityExactlyOnce), SuccessfulNonterminalPumpPollsAuthorityExactlyOnce),
            new TestCase(nameof(SubmissionAuthorityPrecedesSamePumpPollAuthority), SubmissionAuthorityPrecedesSamePumpPollAuthority),
            new TestCase(nameof(AuthorityBroadcastsToEveryParticipantInIdenticalOrder), AuthorityBroadcastsToEveryParticipantInIdenticalOrder),
            new TestCase(nameof(ParticipantEofFaultsEntireSession), ParticipantEofFaultsEntireSession),
            new TestCase(nameof(BroadcastFailureFaultsSessionWithoutAuthorityRollback), BroadcastFailureFaultsSessionWithoutAuthorityRollback),
            new TestCase(nameof(StickySessionFaultRejectsBeforeFurtherIo), StickySessionFaultRejectsBeforeFurtherIo),
        };

        private static void ParticipantBindingRejectsNullTcpClient()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => new TcpBattleParticipantBinding(null!, new PlayerId(1UL), new PlayerSlot(0)));
        }

        private static void SessionRejectsNullInitialState()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => CreateSession(null!, Array.Empty<TcpBattleParticipantBinding>()));
        }

        private static void SessionRejectsNullParticipantArray()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => CreateSession(CreateState(1), null!));
        }

        private static void SessionRejectsWrongParticipantCount()
        {
            TestAssert.Throws<ArgumentException>(
                () => CreateSession(CreateState(2), Array.Empty<TcpBattleParticipantBinding>()));
        }

        private static void SessionRejectsNullParticipantEntry()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => CreateSession(
                    CreateState(1),
                    new TcpBattleParticipantBinding[] { null! }));
        }

        private static void SessionRejectsDuplicateTcpClient()
        {
            BattleState state = CreateState(2);
            using var pair = new LoopbackPair();
            var bindings = new[]
            {
                new TcpBattleParticipantBinding(pair.Accepted, state.Roster.GetPlayerId(new PlayerSlot(0)), new PlayerSlot(0)),
                new TcpBattleParticipantBinding(pair.Accepted, state.Roster.GetPlayerId(new PlayerSlot(1)), new PlayerSlot(1)),
            };

            TestAssert.Throws<ArgumentException>(() => CreateSession(state, bindings));
        }

        private static void SessionRejectsDuplicatePlayerId()
        {
            BattleState state = CreateState(2);
            using var first = new LoopbackPair();
            using var second = new LoopbackPair();
            PlayerId duplicate = state.Roster.GetPlayerId(new PlayerSlot(0));
            var bindings = new[]
            {
                new TcpBattleParticipantBinding(first.Accepted, duplicate, new PlayerSlot(0)),
                new TcpBattleParticipantBinding(second.Accepted, duplicate, new PlayerSlot(1)),
            };

            TestAssert.Throws<ArgumentException>(() => CreateSession(state, bindings));
        }

        private static void SessionRejectsDuplicatePlayerSlot()
        {
            BattleState state = CreateState(2);
            using var first = new LoopbackPair();
            using var second = new LoopbackPair();
            var bindings = new[]
            {
                new TcpBattleParticipantBinding(first.Accepted, state.Roster.GetPlayerId(new PlayerSlot(0)), new PlayerSlot(0)),
                new TcpBattleParticipantBinding(second.Accepted, state.Roster.GetPlayerId(new PlayerSlot(1)), new PlayerSlot(0)),
            };

            TestAssert.Throws<ArgumentException>(() => CreateSession(state, bindings));
        }

        private static void SessionRejectsOutOfRosterOrMismatchedBinding()
        {
            BattleState state = CreateState(1);
            using var first = new LoopbackPair();
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => CreateSession(
                    state,
                    new[]
                    {
                        new TcpBattleParticipantBinding(
                            first.Accepted,
                            state.Roster.GetPlayerId(new PlayerSlot(0)),
                            new PlayerSlot(1)),
                    }));

            using var second = new LoopbackPair();
            TestAssert.Throws<ArgumentException>(
                () => CreateSession(
                    state,
                    new[]
                    {
                        new TcpBattleParticipantBinding(
                            second.Accepted,
                            new PlayerId(999UL),
                            new PlayerSlot(0)),
                    }));
        }

        private static void SessionRejectsDisconnectedParticipant()
        {
            BattleState state = CreateState(1);
            using var client = new TcpClient(AddressFamily.InterNetwork);
            var binding = new TcpBattleParticipantBinding(
                client,
                state.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(0));

            TestAssert.Throws<InvalidOperationException>(
                () => CreateSession(state, new[] { binding }));
        }

        private static void SessionRejectsNonIpv4Participant()
        {
            BattleState state = CreateState(1);
            using var client = new TcpClient(AddressFamily.InterNetworkV6);
            var binding = new TcpBattleParticipantBinding(
                client,
                state.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(0));

            TestAssert.Throws<ArgumentException>(
                () => CreateSession(state, new[] { binding }));
        }

        private static void ConstructionFailureLeavesCallerConnectionsUsable()
        {
            BattleState state = CreateState(1);
            using var pair = new LoopbackPair();
            var binding = new TcpBattleParticipantBinding(
                pair.Accepted,
                new PlayerId(999UL),
                new PlayerSlot(0));

            TestAssert.Throws<ArgumentException>(
                () => CreateSession(state, new[] { binding }));

            TestAssert.True(pair.Accepted.Connected);
            TestAssert.True(pair.Accepted.GetStream().CanRead);
            TestAssert.True(pair.Accepted.GetStream().CanWrite);
        }

        private static void SharedSessionUsesExactlyOneProtocolAuthorityProcessor()
        {
            BattleState state = CreateState(1);
            using var pair = new LoopbackPair();
            var bindings = new[]
            {
                new TcpBattleParticipantBinding(
                    pair.Accepted,
                    state.Roster.GetPlayerId(new PlayerSlot(0)),
                    new PlayerSlot(0)),
            };
            using TcpSharedBattleSession session = CreateSession(state, bindings);

            int processorFields = 0;
            FieldInfo[] fields = typeof(TcpSharedBattleSession).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType.FullName ==
                    "LockstepArena.Server.ProtocolAuthority.ProtocolAuthorityProcessor")
                {
                    processorFields++;
                }
            }

            TestAssert.Equal(1, processorFields);
            TestAssert.Same(state, session.ServerState);
            TestAssert.Equal(state.Tick, session.NextPublishTick);
            TestAssert.Equal(1, session.ParticipantCount);
        }

        private static void TwoRealClientsFeedOneSharedAuthorityTimeline()
        {
            using var fixture = new SharedSessionFixture(2, 0U);
            fixture.SendSubmission(0, 100U, 1, 0, 101);
            fixture.SendSubmission(1, 100U, -1, 0, 202);
            fixture.WaitForServerInputs();
            TestAssert.Equal(1, fixture.Session.PumpOnce());
            FrameData first = fixture.ReadAuthority(0, 1)[0];
            FrameData second = fixture.ReadAuthority(1, 1)[0];
            AssertFramesEqual(first, second);
            TestAssert.Equal(101U, fixture.Session.ServerState.Tick);
        }

        private static void PlayerIdSpoofIsRejectedBeforeAuthorityMutation()
        {
            using var fixture = new SharedSessionFixture(2, 0U);
            fixture.SendRawSubmission(
                0,
                new PlayerId(999UL),
                new InputFrame(100U, new PlayerSlot(0), 0, 0, 1));
            WaitForReadable(fixture.Accepted[0].Client);
            BattleState before = fixture.Session.ServerState;
            TestAssert.Throws<ArgumentException>(() => fixture.Session.PumpOnce());
            TestAssert.Same(before, fixture.Session.ServerState);
            TestAssert.Equal(100U, fixture.Session.NextPublishTick);
        }

        private static void PlayerSlotSpoofIsRejectedBeforeAuthorityMutation()
        {
            using var fixture = new SharedSessionFixture(2, 0U);
            fixture.SendRawSubmission(
                0,
                fixture.State.Roster.GetPlayerId(new PlayerSlot(0)),
                new InputFrame(100U, new PlayerSlot(1), 0, 0, 1));
            WaitForReadable(fixture.Accepted[0].Client);
            BattleState before = fixture.Session.ServerState;
            TestAssert.Throws<ArgumentException>(() => fixture.Session.PumpOnce());
            TestAssert.Same(before, fixture.Session.ServerState);
            TestAssert.Equal(100U, fixture.Session.NextPublishTick);
        }

        private static void ParticipantsAreReadInIncreasingPlayerSlotOrder()
        {
            using var fixture = new SharedSessionFixture(2, 0U, reverseBindings: true);
            fixture.SendRawFrame(0, Frame(new byte[] { 0x12, 0x05, 0x01 }));
            fixture.Clients[1].Client.Shutdown(SocketShutdown.Send);
            WaitForReadable(fixture.Accepted[0].Client);
            WaitForReadable(fixture.Accepted[1].Client);
            TestAssert.Throws<InvalidProtocolBufferException>(() => fixture.Session.PumpOnce());
        }

        private static void SuccessfulNonterminalPumpPollsAuthorityExactlyOnce()
        {
            using var fixture = new SharedSessionFixture(1, 2U);
            TickDrivenFramePublisher publisher = GetScheduledPublisher(GetProcessor(fixture.Session));
            ulong before = publisher.CollectionTick;
            ForceNextPollAdvances(fixture.Session, 1U);
            TestAssert.Equal(0, fixture.Session.PumpOnce());
            TestAssert.Equal(before + 1UL, publisher.CollectionTick);
            TestAssert.Equal(0, fixture.Session.PumpOnce());
            TestAssert.Equal(before + 1UL, publisher.CollectionTick);
        }

        private static void SubmissionAuthorityPrecedesSamePumpPollAuthority()
        {
            using var fixture = new SharedSessionFixture(2, 0U);
            fixture.SendSubmission(0, 100U, 1, 0, 101);
            fixture.SendSubmission(1, 100U, -1, 0, 202);
            fixture.WaitForServerInputs();
            TestAssert.Equal(1, fixture.Session.PumpOnce());
            FrameData frame = fixture.ReadAuthority(0, 1)[0];
            TestAssert.Equal(100U, frame.Tick);
            TestAssert.Equal(101U, fixture.Session.NextPublishTick);
        }

        private static void AuthorityBroadcastsToEveryParticipantInIdenticalOrder()
        {
            using var fixture = new SharedSessionFixture(2, 0U);
            fixture.SendSubmission(0, 100U, 1, 0, 101);
            fixture.SendSubmission(1, 100U, -1, 0, 202);
            fixture.WaitForServerInputs();
            TestAssert.Equal(1, fixture.Session.PumpOnce());
            FrameData first = fixture.ReadAuthority(0, 1)[0];
            FrameData second = fixture.ReadAuthority(1, 1)[0];
            AssertFramesEqual(first, second);
        }

        private static void ParticipantEofFaultsEntireSession()
        {
            using var fixture = new SharedSessionFixture(2, 0U);
            fixture.Clients[0].Client.Shutdown(SocketShutdown.Send);
            WaitForReadable(fixture.Accepted[0].Client);
            TestAssert.Throws<EndOfStreamException>(() => fixture.Session.PumpOnce());
            TestAssert.Throws<InvalidOperationException>(() => fixture.Session.PumpOnce());
        }

        private static void BroadcastFailureFaultsSessionWithoutAuthorityRollback()
        {
            using var fixture = new SharedSessionFixture(2, 0U);
            fixture.SendSubmission(0, 100U, 1, 0, 101);
            fixture.SendSubmission(1, 100U, -1, 0, 202);
            fixture.WaitForServerInputs();
            fixture.Accepted[1].Client.Shutdown(SocketShutdown.Send);
            TestAssert.Throws<Exception>(() => fixture.Session.PumpOnce());
            TestAssert.Equal(101U, fixture.Session.ServerState.Tick);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Session.PumpOnce());
        }

        private static void StickySessionFaultRejectsBeforeFurtherIo()
        {
            using var fixture = new SharedSessionFixture(1, 0U);
            fixture.SendRawSubmission(
                0,
                new PlayerId(999UL),
                new InputFrame(100U, new PlayerSlot(0), 0, 0, 1));
            WaitForReadable(fixture.Accepted[0].Client);
            TestAssert.Throws<ArgumentException>(() => fixture.Session.PumpOnce());
            fixture.Clients[0].Dispose();
            TestAssert.Throws<InvalidOperationException>(() => fixture.Session.PumpOnce());
        }

        private static TcpSharedBattleSession CreateSession(
            BattleState initialState,
            TcpBattleParticipantBinding[] participants)
        {
            return new TcpSharedBattleSession(
                initialState,
                participants,
                2U,
                8U,
                8,
                MaxPayloadLength,
                16,
                3,
                3);
        }

        internal static void ForceNextPollAdvances(
            TcpSharedBattleSession session,
            uint dueAdvances)
        {
            ProtocolAuthorityProcessor processor = GetProcessor(session);
            StopwatchTickDriver driver =
                (StopwatchTickDriver)(GetUniquePrivateField(
                    typeof(ProtocolAuthorityProcessor),
                    typeof(StopwatchTickDriver)).GetValue(processor) ??
                    throw new InvalidOperationException("Missing StopwatchTickDriver."));
            Stopwatch stopwatch =
                (Stopwatch)(GetUniquePrivateField(
                    typeof(StopwatchTickDriver),
                    typeof(Stopwatch)).GetValue(driver) ??
                    throw new InvalidOperationException("Missing Stopwatch."));
            FieldInfo baselineField = GetUniquePrivateField(
                typeof(StopwatchTickDriver),
                typeof(long));

            stopwatch.Stop();
            long stableElapsed = stopwatch.ElapsedTicks;
            UInt128 numerator =
                ((UInt128)(ulong)Stopwatch.Frequency * dueAdvances) +
                (uint)SimulationConfig.TickRate - 1U;
            long requiredElapsed = checked(
                (long)(numerator / (uint)SimulationConfig.TickRate));
            baselineField.SetValue(driver, checked(stableElapsed - requiredElapsed));
        }

        internal static ProtocolAuthorityProcessor GetProcessor(
            TcpSharedBattleSession session)
        {
            return (ProtocolAuthorityProcessor)(GetUniquePrivateField(
                typeof(TcpSharedBattleSession),
                typeof(ProtocolAuthorityProcessor)).GetValue(session) ??
                throw new InvalidOperationException("Missing ProtocolAuthorityProcessor."));
        }

        private static TickDrivenFramePublisher GetScheduledPublisher(
            ProtocolAuthorityProcessor processor)
        {
            return (TickDrivenFramePublisher)(GetUniquePrivateField(
                typeof(ProtocolAuthorityProcessor),
                typeof(TickDrivenFramePublisher)).GetValue(processor) ??
                throw new InvalidOperationException("Missing TickDrivenFramePublisher."));
        }

        private static FieldInfo GetUniquePrivateField(Type ownerType, Type fieldType)
        {
            FieldInfo? match = null;
            FieldInfo[] fields = ownerType.GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType != fieldType)
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        $"Multiple private {fieldType.Name} fields found on {ownerType.Name}.");
                }

                match = fields[index];
            }

            return match ?? throw new InvalidOperationException(
                $"No private {fieldType.Name} field found on {ownerType.Name}.");
        }

        internal static byte[] Frame(byte[] payload)
        {
            return LengthPrefixedFrameEncoder.Encode(payload, MaxPayloadLength);
        }

        internal static void WaitForReadable(Socket socket)
        {
            if (!socket.Poll(5_000_000, SelectMode.SelectRead))
            {
                throw new InvalidOperationException("Timed out waiting for loopback data.");
            }
        }

        internal static void AssertFramesEqual(FrameData expected, FrameData actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.Equal(expected.Roster.Count, actual.Roster.Count);
            for (int index = 0; index < expected.Roster.Count; index++)
            {
                var slot = new PlayerSlot(index);
                TestAssert.Equal(
                    expected.Roster.GetPlayerId(slot),
                    actual.Roster.GetPlayerId(slot));
                InputFrame expectedInput = expected.GetInput(slot);
                InputFrame actualInput = actual.GetInput(slot);
                TestAssert.Equal(expectedInput.Tick, actualInput.Tick);
                TestAssert.Equal(expectedInput.PlayerSlot, actualInput.PlayerSlot);
                TestAssert.Equal(expectedInput.MoveX, actualInput.MoveX);
                TestAssert.Equal(expectedInput.MoveZ, actualInput.MoveZ);
                TestAssert.Equal(expectedInput.Aim, actualInput.Aim);
                TestAssert.Equal(expectedInput.Fire, actualInput.Fire);
            }
        }

        internal static BattleState CreateState(int playerCount, uint tick = 100U)
        {
            var ids = new PlayerId[playerCount];
            var states = new PlayerState[playerCount];
            for (int index = 0; index < playerCount; index++)
            {
                ids[index] = new PlayerId((ulong)(1001 + index));
                states[index] = new PlayerState(index * 100, 0, checked((ushort)(1000 + index)));
            }

            return new BattleState(tick, new ActiveRoster(ids), states);
        }
    }

    internal sealed class SharedSessionFixture : IDisposable
    {
        private const int MaxPayloadLength = 4096;
        private readonly TcpListener[] _listeners;
        private readonly LengthPrefixedFrameDecoder[] _clientDecoders;

        public SharedSessionFixture(
            int playerCount,
            uint inputDelayTicks,
            bool reverseBindings = false)
        {
            State = TcpSharedBattleSessionTests.CreateState(playerCount);
            Clients = new TcpClient[playerCount];
            Accepted = new TcpClient[playerCount];
            _listeners = new TcpListener[playerCount];
            _clientDecoders = new LengthPrefixedFrameDecoder[playerCount];
            var bindings = new TcpBattleParticipantBinding[playerCount];

            for (int index = 0; index < playerCount; index++)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start(1);
                _listeners[index] = listener;
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var client = new TcpClient(AddressFamily.InterNetwork);
                client.Connect(IPAddress.Loopback, endpoint.Port);
                Clients[index] = client;
                Accepted[index] = listener.AcceptTcpClient();
                _clientDecoders[index] = new LengthPrefixedFrameDecoder(MaxPayloadLength);

                var slot = new PlayerSlot(index);
                bindings[index] = new TcpBattleParticipantBinding(
                    Accepted[index],
                    State.Roster.GetPlayerId(slot),
                    slot);
            }

            if (reverseBindings)
            {
                Array.Reverse(bindings);
            }

            Session = new TcpSharedBattleSession(
                State,
                bindings,
                inputDelayTicks,
                8U,
                16,
                MaxPayloadLength,
                64,
                3,
                61);
        }

        public BattleState State { get; }

        public TcpClient[] Clients { get; }

        public TcpClient[] Accepted { get; }

        public TcpSharedBattleSession Session { get; }

        public void SendSubmission(
            int participantIndex,
            uint tick,
            sbyte moveX,
            sbyte moveZ,
            ushort aim)
        {
            var slot = new PlayerSlot(participantIndex);
            SendRawSubmission(
                participantIndex,
                State.Roster.GetPlayerId(slot),
                new InputFrame(tick, slot, moveX, moveZ, aim));
        }

        public void SendRawSubmission(
            int participantIndex,
            PlayerId playerId,
            InputFrame input)
        {
            PlayerInputSubmissionMessage message = ProtocolMapper.ToWire(playerId, input);
            SendRawFrame(participantIndex, TcpSharedBattleSessionTests.Frame(message.ToByteArray()));
        }

        public void SendRawFrame(int participantIndex, byte[] framedPayload)
        {
            NetworkStream stream = Clients[participantIndex].GetStream();
            stream.Write(framedPayload, 0, framedPayload.Length);
        }

        public void WaitForServerInputs()
        {
            for (int index = 0; index < Accepted.Length; index++)
            {
                TcpSharedBattleSessionTests.WaitForReadable(Accepted[index].Client);
            }
        }

        public FrameData[] ReadAuthority(int participantIndex, int expectedCount)
        {
            var frames = new List<FrameData>();
            var receiveBuffer = new byte[128];
            NetworkStream stream = Clients[participantIndex].GetStream();
            while (frames.Count < expectedCount)
            {
                TcpSharedBattleSessionTests.WaitForReadable(Clients[participantIndex].Client);
                int bytesRead = stream.Read(receiveBuffer, 5, receiveBuffer.Length - 5);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }

                byte[][] payloads = _clientDecoders[participantIndex].Feed(
                    receiveBuffer,
                    5,
                    bytesRead);
                for (int index = 0; index < payloads.Length; index++)
                {
                    AuthoritativeFrameMessage message =
                        AuthoritativeFrameMessage.Parser.ParseFrom(payloads[index]);
                    frames.Add(ProtocolMapper.ToDomain(message, State.Roster));
                }
            }

            return frames.ToArray();
        }

        public void Dispose()
        {
            Session.Dispose();
            for (int index = 0; index < Clients.Length; index++)
            {
                Clients[index].Dispose();
                _listeners[index].Stop();
            }
        }
    }

    internal sealed class LoopbackPair : IDisposable
    {
        private readonly TcpListener _listener;

        public LoopbackPair()
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
