using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using LockstepArena.Client.Demo;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.DemoHost;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class BattlePreparationTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(StartRejectsNonHostWithoutMutation), StartRejectsNonHostWithoutMutation),
            new TestCase(nameof(StartRejectsNonFullRoomWithoutMutation), StartRejectsNonFullRoomWithoutMutation),
            new TestCase(nameof(StartRejectsAnyUnreadyParticipantWithoutMutation), StartRejectsAnyUnreadyParticipantWithoutMutation),
            new TestCase(nameof(StartAtomicallyFreezesRosterBootstrapTicketsAndEvents), StartAtomicallyFreezesRosterBootstrapTicketsAndEvents),
            new TestCase(nameof(RosterSlotsFollowJoinOrderRatherThanPlayerId), RosterSlotsFollowJoinOrderRatherThanPlayerId),
            new TestCase(nameof(BattleIdExhaustionRejectsWithoutPreparationMutation), BattleIdExhaustionRejectsWithoutPreparationMutation),
            new TestCase(nameof(TicketsAreSixteenByteRandomScopedUniqueAndSingleUse), TicketsAreSixteenByteRandomScopedUniqueAndSingleUse),
            new TestCase(nameof(BattleAttachmentAccumulatesPartialTicketWithoutOverread), BattleAttachmentAccumulatesPartialTicketWithoutOverread),
            new TestCase(nameof(InvalidExpiredReusedOrWrongScopeTicketClosesOnlyCandidate), InvalidExpiredReusedOrWrongScopeTicketClosesOnlyCandidate),
            new TestCase(nameof(ClientWaitsForAcceptanceAndBattleStartedBeforeBattleInput), ClientWaitsForAcceptanceAndBattleStartedBeforeBattleInput),
            new TestCase(nameof(PreparingBattleExplicitLeaveIsRejected), PreparingBattleExplicitLeaveIsRejected),
            new TestCase(nameof(InBattleExplicitLeaveIsRejected), InBattleExplicitLeaveIsRejected),
            new TestCase(nameof(PreparingControlLossInvalidatesTicketsAndClosesPreparedSockets), PreparingControlLossInvalidatesTicketsAndClosesPreparedSockets),
            new TestCase(nameof(AllAttachmentsCreateExactlyOneGate13SharedBattleSession), AllAttachmentsCreateExactlyOneGate13SharedBattleSession),
            new TestCase(nameof(ServerAndClientPumpsPerformOnlyFrozenBoundedWork), ServerAndClientPumpsPerformOnlyFrozenBoundedWork),
            new TestCase(nameof(GameplayBattleUsesDefinitionAndWaitsForEveryBattleReady), GameplayBattleUsesDefinitionAndWaitsForEveryBattleReady),
            new TestCase(nameof(ConfigMismatchOrReadyTimeoutAbortsPreparationToLobby), ConfigMismatchOrReadyTimeoutAbortsPreparationToLobby),
            new TestCase(nameof(GameplayClientsSendBattleReadyOnlyAfterSceneSignal), GameplayClientsSendBattleReadyOnlyAfterSceneSignal),
            new TestCase(nameof(GameplayClientsCompleteFastBo3AndReturnToLobby), GameplayClientsCompleteFastBo3AndReturnToLobby),
        };

        private static void StartRejectsNonHostWithoutMutation()
        {
            using var server = FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(guest.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
        }

        private static void StartRejectsNonFullRoomWithoutMutation()
        {
            using var server = SessionRoomTests.CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.SetReady(host.SessionId, true);
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
        }

        private static void StartRejectsAnyUnreadyParticipantWithoutMutation()
        {
            using var server = FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room);
            server.SetReady(host.SessionId, true);
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
            TestAssert.True(!guest.IsReady);
        }

        private static void StartAtomicallyFreezesRosterBootstrapTicketsAndEvents()
        {
            using var server = FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room, 1028);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            host.ClearEvents();
            guest.ClearEvents();
            SessionRoomTests.FillControlCapacity(host);
            int retainedHostEvents = host.EventCount;
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
            TestAssert.True(host.IsReady);
            TestAssert.True(guest.IsReady);
            TestAssert.Equal(retainedHostEvents, host.EventCount);
            TestAssert.Equal(0, guest.EventCount);
            host.ClearEvents();
            BattlePreparation preparation = server.StartBattle(host.SessionId);
            TestAssert.Equal(DemoRoomLifecycle.PreparingBattle, room.Lifecycle);
            TestAssert.Equal((ulong)1, preparation.BattleId);
            TestAssert.Equal(2, preparation.ParticipantCount);
            TestAssert.Equal(16, preparation.GetTicket(new PlayerSlot(0)).Length);
            TestAssert.Equal(16, preparation.GetTicket(new PlayerSlot(1)).Length);
            TestAssert.True(!BytesEqual(preparation.GetTicket(new PlayerSlot(0)), preparation.GetTicket(new PlayerSlot(1))));
            TestAssert.Equal(host.SessionId, preparation.InitialState.Roster.GetPlayerId(new PlayerSlot(0)).Value);
            TestAssert.Equal(4U, preparation.FinalStateTick);
            TestAssert.Equal(Protocol.Wire.ServerControlEventMessage.EventOneofCase.BattlePreparing, host.LastEvent.EventCase);
            TestAssert.Equal(Protocol.Wire.ServerControlEventMessage.EventOneofCase.BattlePreparing, guest.LastEvent.EventCase);
        }

        private static void RosterSlotsFollowJoinOrderRatherThanPlayerId()
        {
            using var server = SessionRoomTests.CreateServer();
            DemoSession earlier = server.EnterSession("Bravo");
            DemoSession laterHost = server.EnterSession("Alpha");
            DemoRoom room = server.CreateRoom(laterHost.SessionId, "Room", 2);
            server.JoinRoom(earlier.SessionId, room.RoomId);
            server.SetReady(laterHost.SessionId, true);
            server.SetReady(earlier.SessionId, true);
            BattlePreparation preparation = server.StartBattle(laterHost.SessionId);
            TestAssert.Equal(laterHost.SessionId, preparation.InitialState.Roster.GetPlayerId(new PlayerSlot(0)).Value);
            TestAssert.Equal(earlier.SessionId, preparation.InitialState.Roster.GetPlayerId(new PlayerSlot(1)).Value);
        }

        private static void BattleIdExhaustionRejectsWithoutPreparationMutation()
        {
            using var server = FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            typeof(TcpDemoServer).GetField("_nextBattleId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(server, 0UL);
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
        }

        private static void TicketsAreSixteenByteRandomScopedUniqueAndSingleUse()
        {
            using var fixture = new PreparedFixture();
            byte[] first = fixture.Preparation.GetTicket(new PlayerSlot(0));
            byte[] second = fixture.Preparation.GetTicket(new PlayerSlot(1));
            TestAssert.Equal(16, first.Length);
            TestAssert.Equal(16, second.Length);
            TestAssert.True(!BytesEqual(first, second));
            using TcpClient accepted = fixture.Attach(first);
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
            using TcpClient reused = fixture.ConnectAndWrite(first);
            fixture.Pump(20);
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
        }

        private static void BattleAttachmentAccumulatesPartialTicketWithoutOverread()
        {
            using var fixture = new PreparedFixture();
            byte[] ticket = fixture.Preparation.GetTicket(new PlayerSlot(0));
            using var client = new TcpClient(AddressFamily.InterNetwork);
            client.Connect(IPAddress.Loopback, fixture.Server.BattlePort);
            fixture.Server.PumpOnce();
            NetworkStream stream = client.GetStream();
            stream.Write(ticket, 0, 7);
            fixture.Server.PumpOnce();
            TestAssert.Equal(0, fixture.Preparation.AttachedCount);
            var tailWithSentinel = new byte[13];
            Array.Copy(ticket, 7, tailWithSentinel, 0, 9);
            tailWithSentinel[9] = 0xAA;
            tailWithSentinel[10] = 0xBB;
            tailWithSentinel[11] = 0xCC;
            tailWithSentinel[12] = 0xDD;
            stream.Write(tailWithSentinel, 0, tailWithSentinel.Length);
            fixture.Pump(20);
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
            TestAssert.Equal(4, fixture.Preparation.GetAttachedClient(new PlayerSlot(0)).Client.Available);
        }

        private static void InvalidExpiredReusedOrWrongScopeTicketClosesOnlyCandidate()
        {
            using var fixture = new PreparedFixture();
            using TcpClient invalid = fixture.ConnectAndWrite(new byte[16]);
            fixture.Pump(20);
            TestAssert.Equal(0, fixture.Preparation.AttachedCount);
            using TcpClient valid = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
            using var otherFixture = new PreparedFixture();
            using TcpClient wrongScope = fixture.ConnectAndWrite(otherFixture.Preparation.GetTicket(new PlayerSlot(0)));
            fixture.Pump(20);
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
        }

        private static void ClientWaitsForAcceptanceAndBattleStartedBeforeBattleInput()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            TestAssert.Equal(DemoSessionPhase.PreparingBattle, fixture.Host.Phase);
            using TcpClient second = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            fixture.Pump(20);
            TestAssert.Equal(DemoSessionPhase.InBattle, fixture.Host.Phase);
            TestAssert.Equal(DemoSessionPhase.InBattle, fixture.Guest.Phase);
        }

        private static void GameplayBattleUsesDefinitionAndWaitsForEveryBattleReady()
        {
            BattleDefinition definition = BattleDefinition.CreateDefault();
            using var server = new TcpDemoServer(SessionRoomTests.CreateOptions(
                maxSessions: 2,
                maxRooms: 1,
                maxRoomCapacity: 2,
                battleDefinition: definition));
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            BattlePreparation preparation = server.StartBattle(host.SessionId);

            TestAssert.True(preparation.InitialState.IsGameplayEnabled);
            TestAssert.Equal(BattlePhase.RoundCountdown, preparation.InitialState.Phase);
            TestAssert.Equal(BattleConfigHash.Compute(definition), preparation.GetPreparingEvent(new PlayerSlot(0)).Bootstrap.BattleConfigHash);

            using TcpClient first = Attach(server, preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = Attach(server, preparation.GetTicket(new PlayerSlot(1)));
            TestAssert.Equal(DemoSessionPhase.PreparingBattle, host.Phase);
            server.MarkBattleReady(host.SessionId, preparation.BattleId, BattleConfigHash.Compute(definition));
            TestAssert.Equal(DemoSessionPhase.PreparingBattle, host.Phase);
            server.MarkBattleReady(guest.SessionId, preparation.BattleId, BattleConfigHash.Compute(definition));
            TestAssert.Equal(DemoSessionPhase.InBattle, host.Phase);
            TestAssert.Equal(DemoSessionPhase.InBattle, guest.Phase);
            TestAssert.Equal(DemoRoomLifecycle.InBattle, room.Lifecycle);
        }

        private static void ConfigMismatchOrReadyTimeoutAbortsPreparationToLobby()
        {
            BattleDefinition definition = BattleDefinition.CreateDefault();
            using (var server = new TcpDemoServer(SessionRoomTests.CreateOptions(
                maxSessions: 2, maxRooms: 1, maxRoomCapacity: 2,
                battleDefinition: definition)))
            {
                DemoSession host = server.EnterSession("MismatchHost");
                DemoSession guest = server.EnterSession("MismatchGuest");
                DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
                server.JoinRoom(guest.SessionId, room.RoomId);
                server.SetReady(host.SessionId, true);
                server.SetReady(guest.SessionId, true);
                BattlePreparation preparation = server.StartBattle(host.SessionId);
                server.MarkBattleReady(host.SessionId, preparation.BattleId, BattleConfigHash.Compute(definition) + 1UL);
                TestAssert.Equal(DemoSessionPhase.Lobby, host.Phase);
                TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
                TestAssert.Equal(0, server.RoomCount);
            }

            using (var server = new TcpDemoServer(SessionRoomTests.CreateOptions(
                maxSessions: 2, maxRooms: 1, maxRoomCapacity: 2,
                battleDefinition: definition,
                battleReadyTimeout: TimeSpan.Zero)))
            {
                DemoSession host = server.EnterSession("TimeoutHost");
                DemoSession guest = server.EnterSession("TimeoutGuest");
                DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
                server.JoinRoom(guest.SessionId, room.RoomId);
                server.SetReady(host.SessionId, true);
                server.SetReady(guest.SessionId, true);
                server.StartBattle(host.SessionId);
                server.PumpOnce();
                TestAssert.Equal(DemoSessionPhase.Lobby, host.Phase);
                TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
                TestAssert.Equal(0, server.RoomCount);
            }
        }

        private static void GameplayClientsSendBattleReadyOnlyAfterSceneSignal()
        {
            BattleDefinition definition = BattleDefinition.CreateDefault();
            using var server = new TcpDemoServer(SessionRoomTests.CreateOptions(
                maxSessions: 2, maxRooms: 1, maxRoomCapacity: 2,
                battleDefinition: definition));
            using var host = CreateGameplayClient(server.ControlPort, definition);
            using var guest = CreateGameplayClient(server.ControlPort, definition);
            ConnectAndEnter(server, host, "Host");
            ConnectAndEnter(server, guest, "Guest");
            host.CreateRoom("Room", 2);
            PumpUntil(server, host, guest, () => host.Phase == DemoClientPhase.Room);
            guest.JoinRoom(host.Snapshot.RoomId);
            PumpUntil(server, host, guest, () => guest.Phase == DemoClientPhase.Room);
            host.SetReady(true);
            guest.SetReady(true);
            Pump(server, host, guest, 20);
            host.StartBattle();
            PumpUntil(server, host, guest, () =>
                host.Phase == DemoClientPhase.PreparingBattle &&
                guest.Phase == DemoClientPhase.PreparingBattle);
            Pump(server, host, guest, 30);
            TestAssert.Equal(DemoClientPhase.PreparingBattle, host.Phase);
            TestAssert.Equal(DemoClientPhase.PreparingBattle, guest.Phase);

            host.MarkBattleSceneReady();
            Pump(server, host, guest, 20);
            TestAssert.Equal(DemoClientPhase.PreparingBattle, host.Phase);
            guest.MarkBattleSceneReady();
            PumpUntil(server, host, guest, () =>
                host.Phase == DemoClientPhase.InBattle &&
                guest.Phase == DemoClientPhase.InBattle);
            TestAssert.True(host.PredictedBattleState is not null && host.PredictedBattleState.IsGameplayEnabled);
            TestAssert.True(guest.PredictedBattleState is not null && guest.PredictedBattleState.IsGameplayEnabled);
        }

        private static void GameplayClientsCompleteFastBo3AndReturnToLobby()
        {
            BattleDefinition definition = SettlementLifecycleTests.CreateFastGameplayDefinition();
            using var server = new TcpDemoServer(SessionRoomTests.CreateOptions(
                maxSessions: 2, maxRooms: 1, maxRoomCapacity: 2,
                battleDefinition: definition,
                battleReadyTimeout: TimeSpan.FromSeconds(5),
                battleDurationTicks: 100));
            using var host = CreateGameplayClient(server.ControlPort, definition);
            using var guest = CreateGameplayClient(server.ControlPort, definition);
            ConnectAndEnter(server, host, "FastHost");
            ConnectAndEnter(server, guest, "FastGuest");
            host.CreateRoom("FastRoom", 2);
            PumpUntil(server, host, guest, () => host.Phase == DemoClientPhase.Room);
            guest.JoinRoom(host.Snapshot.RoomId);
            PumpUntil(server, host, guest, () => guest.Phase == DemoClientPhase.Room);
            host.SetReady(true);
            guest.SetReady(true);
            Pump(server, host, guest, 20);
            host.StartBattle();
            PumpUntil(server, host, guest, () =>
                host.Phase == DemoClientPhase.PreparingBattle &&
                guest.Phase == DemoClientPhase.PreparingBattle);
            host.MarkBattleSceneReady();
            guest.MarkBattleSceneReady();
            PumpUntil(server, host, guest, () =>
                host.Phase == DemoClientPhase.InBattle &&
                guest.Phase == DemoClientPhase.InBattle);

            BattlePreparation preparation = server.GetRoom(host.Snapshot.RoomId).Preparation!;
            SettlementLifecycleTests.StopBattleClock(preparation);
            for (int iteration = 0; iteration < 5000 &&
                (host.Phase != DemoClientPhase.Settlement || guest.Phase != DemoClientPhase.Settlement);
                iteration++)
            {
                BattleState hostState = host.PredictedBattleState!;
                BattleState guestState = guest.PredictedBattleState!;
                LocalInputSample hostInput = new LocalInputSample(0, 0, 0,
                    hostState.Phase == BattlePhase.Playing);
                LocalInputSample guestInput = new LocalInputSample(0, 0, 32768,
                    false);
                host.PumpOnce(hostInput);
                guest.PumpOnce(guestInput);
                SettlementLifecycleTests.ForceNextPollAdvances(preparation, 1U);
                server.PumpOnce();
            }

            TestAssert.Equal(DemoClientPhase.Settlement, host.Phase);
            TestAssert.Equal(DemoClientPhase.Settlement, guest.Phase);
            TestAssert.True(host.Snapshot.SettlementVerified);
            TestAssert.True(guest.Snapshot.SettlementVerified);
            TestAssert.Equal(BattleSettlementReasonMessage.BattleSettlementReasonMatchCompleted,
                host.Snapshot.SettlementReason);
            TestAssert.Equal(host.Snapshot.SessionId, host.Snapshot.WinnerPlayerId);
            TestAssert.Equal(2U, host.Snapshot.Slot0RoundWins);
            TestAssert.Equal(0U, host.Snapshot.Slot1RoundWins);
            TestAssert.Equal(BattlePhase.MatchEnded, host.PredictedBattleState!.Phase);

            BattleReplaySnapshot replay = host.RetainedReplay!;
            TestAssert.True(replay is not null);
            BattleState replayInitial = replay!.InitialState;
            System.Collections.Generic.IReadOnlyList<FrameData> replayFrames = replay.AuthoritativeFrames;
            var offline = new BattleSimulation(replayInitial);
            foreach (FrameData frame in replayFrames) offline.Step(frame);
            TestAssert.Equal(host.Snapshot.ReplayFrameCount, replayFrames.Count);
            TestAssert.True(replayFrames.Count > 0);
            // Settlement display may retain a terminal prediction ahead of authority; replay must not.
            TestAssert.Equal(host.Snapshot.AuthoritativeTick, offline.State.Tick);
            TestAssert.Equal(BattlePhase.MatchEnded, offline.State.Phase);
            TestAssert.Equal(host.Snapshot.AuthoritativeDigest, StateDigest.Compute(offline.State));

            host.ReturnToLobby();
            guest.ReturnToLobby();
            PumpUntil(server, host, guest, () =>
                host.Phase == DemoClientPhase.Lobby && guest.Phase == DemoClientPhase.Lobby);
            TestAssert.Equal(0UL, host.Snapshot.RoomId);
            TestAssert.Equal(0UL, guest.Snapshot.RoomId);
            TestAssert.True(host.RetainedReplay is null);
            TestAssert.True(guest.RetainedReplay is null);
            host.CreateRoom("SecondRoom", 2);
            PumpUntil(server, host, guest, () => host.Phase == DemoClientPhase.Room);
            guest.JoinRoom(host.Snapshot.RoomId);
            PumpUntil(server, host, guest, () => guest.Phase == DemoClientPhase.Room);
            host.SetReady(true);
            guest.SetReady(true);
            Pump(server, host, guest, 20);
            host.StartBattle();
            PumpUntil(server, host, guest, () => host.Phase == DemoClientPhase.PreparingBattle && guest.Phase == DemoClientPhase.PreparingBattle);
            TestAssert.True(host.RetainedReplay is null);
            TestAssert.True(guest.RetainedReplay is null);
            TestAssert.Equal(0U, host.PredictedBattleState!.Tick);
        }

        private static TcpClient Attach(TcpDemoServer server, byte[] ticket)
        {
            var client = new TcpClient(AddressFamily.InterNetwork);
            client.Connect(server.BindAddress, server.BattlePort);
            client.GetStream().Write(ticket, 0, ticket.Length);
            for (int index = 0; index < 100; index++) server.PumpOnce();
            TestAssert.Equal(1, client.GetStream().ReadByte());
            return client;
        }

        private static void PreparingBattleExplicitLeaveIsRejected()
        {
            using var fixture = new PreparedFixture();
            TestAssert.Throws<InvalidOperationException>(() => fixture.Server.LeaveRoom(fixture.Host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.PreparingBattle, fixture.Room.Lifecycle);
        }

        private static void InBattleExplicitLeaveIsRejected()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            fixture.Pump(20);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Server.LeaveRoom(fixture.Host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.InBattle, fixture.Room.Lifecycle);
        }

        private static void PreparingControlLossInvalidatesTicketsAndClosesPreparedSockets()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            fixture.Server.CloseSession(fixture.Host.SessionId);
            TestAssert.True(fixture.Preparation.IsInvalidated);
            TestAssert.Equal(0, fixture.Server.RoomCount);

            ProveUnpressuredPhysicalControlLossReturnsSurvivorToLobby();
            ProvePressuredPhysicalControlLoss(false);
            ProvePressuredPhysicalControlLoss(true);
        }

        private static void ProveUnpressuredPhysicalControlLossReturnsSurvivorToLobby()
        {
            using var server = SessionRoomTests.CreateServer();
            using TcpClient hostControl = SessionRoomTests.ConnectNamed(server, "PhysicalHost", 1);
            using TcpClient guestControl = SessionRoomTests.ConnectNamed(server, "PhysicalGuest", 2);
            DemoSession host = server.GetSession(1);
            DemoSession guest = server.GetSession(2);
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            BattlePreparation preparation = server.StartBattle(host.SessionId);
            using TcpClient battle = ConnectBattle(server, preparation.GetTicket(new PlayerSlot(0)));

            hostControl.Client.Shutdown(SocketShutdown.Both);
            for (int index = 0; index < 100 && host.Phase != DemoSessionPhase.Closed; index++) server.PumpOnce();
            TestAssert.True(preparation.IsInvalidated);
            TestAssert.Equal(0, server.RoomCount);
            TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
            TestAssert.Equal(0UL, guest.RoomId);
            TestAssert.Equal(1, server.SessionCount);
        }

        private static void ProvePressuredPhysicalControlLoss(bool enterBattle)
        {
            using var server = SessionRoomTests.CreateServer(maxPendingControlBytesPerSession: 1028, controlReceiveReadCapacity: 64);
            using TcpClient hostControl = SessionRoomTests.ConnectNamed(server, "PhysicalHost", 1);
            using TcpClient guestControl = SessionRoomTests.ConnectNamed(server, "PhysicalGuest", 2);
            DemoSession host = server.GetSession(1);
            DemoSession guest = server.GetSession(2);
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            BattlePreparation preparation = server.StartBattle(host.SessionId);
            using TcpClient first = ConnectBattle(server, preparation.GetTicket(new PlayerSlot(0)));
            TcpClient? second = null;
            try
            {
                if (enterBattle)
                {
                    second = ConnectBattle(server, preparation.GetTicket(new PlayerSlot(1)));
                    for (int index = 0; index < 40 && room.Lifecycle != DemoRoomLifecycle.InBattle; index++) server.PumpOnce();
                    TestAssert.Equal(DemoRoomLifecycle.InBattle, room.Lifecycle);
                }
                else
                {
                    TestAssert.Equal(DemoRoomLifecycle.PreparingBattle, room.Lifecycle);
                }

                guest.ClearEvents();
                SessionRoomTests.FillControlCapacity(guest);
                hostControl.Client.Shutdown(SocketShutdown.Both);
                for (int index = 0; index < 20 && host.Phase != DemoSessionPhase.Closed; index++) server.PumpOnce();
                TestAssert.True(preparation.IsInvalidated);
                TestAssert.Equal(0, server.RoomCount);
                TestAssert.Equal(DemoSessionPhase.Closed, host.Phase);
                TestAssert.Equal(enterBattle ? DemoSessionPhase.Closed : DemoSessionPhase.Lobby, guest.Phase);
                TestAssert.Equal(enterBattle ? 0 : 1, server.SessionCount);
            }
            finally
            {
                second?.Dispose();
            }
        }

        private static TcpClient ConnectBattle(TcpDemoServer server, byte[] ticket)
        {
            var client = new TcpClient(AddressFamily.InterNetwork);
            client.Connect(IPAddress.Loopback, server.BattlePort);
            client.GetStream().Write(ticket, 0, ticket.Length);
            for (int index = 0; index < 40; index++) server.PumpOnce();
            return client;
        }

        private static void AllAttachmentsCreateExactlyOneGate13SharedBattleSession()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            fixture.Pump(20);
            TestAssert.True(fixture.Preparation.SharedSession is not null);
            object instance = fixture.Preparation.SharedSession!;
            fixture.Pump(5);
            TestAssert.True(ReferenceEquals(instance, fixture.Preparation.SharedSession));
        }

        private static void ServerAndClientPumpsPerformOnlyFrozenBoundedWork()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.ConnectAndWrite(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = fixture.ConnectAndWrite(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            DemoServerPumpResult result = fixture.Server.PumpOnce();
            TestAssert.True(result.AcceptedBattleConnections <= 1);
            TestAssert.True(result.AcceptedControlConnections <= 1);

            ProveControlAndBattleConnectProgressThroughPump();
            ProveNamedSessionsUseSessionIdOrder();
            ProveBufferedControlRetryWithoutNewBytes();
        }

        private static void ProveBufferedControlRetryWithoutNewBytes()
        {
            using var server = SessionRoomTests.CreateServer(maxSessions: 2, maxRooms: 1, maxRoomCapacity: 2,
                maxPendingControlBytesPerSession: 1028, controlReceiveReadCapacity: 64, maxControlSendBytesPerPump: 1);
            using TcpClient client = SessionRoomTests.ConnectNamed(server, "BufferedHost", 1);
            DemoSession session = server.GetSession(1);
            session.ClearEvents();
            session.Queue(new ServerControlEventMessage
            {
                CommandRejected = new CommandRejectedEventMessage
                {
                    Reason = ControlRejectReasonMessage.ControlRejectReasonResourceLimit,
                    Detail = new string('x', 1000),
                },
            });
            SessionRoomTests.WriteCommand(client, new ClientControlCommandMessage
            {
                CreateRoom = new CreateRoomCommandMessage { RoomName = "BufferedRoom", Capacity = 2 },
            });
            for (int index = 0; index < 5; index++) server.PumpOnce();
            TestAssert.Equal(0, server.RoomCount);
            int processed = 0;
            for (int index = 0; index < 2000 && server.RoomCount == 0; index++)
            {
                processed += server.PumpOnce().ProcessedControlCommands;
            }
            TestAssert.Equal(1, processed);
            TestAssert.Equal(1, server.RoomCount);
            TestAssert.Equal(DemoSessionPhase.InRoom, session.Phase);
            TestAssert.Equal((ulong)1, session.RoomId);
            var decoder = new LengthPrefixedFrameDecoder(1024);
            var receive = new byte[128];
            int roomSnapshots = 0;
            for (int index = 0; index < 2000 && roomSnapshots == 0; index++)
            {
                server.PumpOnce();
                if (client.Client.Available == 0) continue;
                int read = client.GetStream().Read(receive, 0, receive.Length);
                foreach (byte[] payload in decoder.Feed(receive, 0, read))
                {
                    ServerControlEventMessage message = ServerControlEventMessage.Parser.ParseFrom(payload);
                    if (message.EventCase != ServerControlEventMessage.EventOneofCase.RoomSnapshot) continue;
                    roomSnapshots++;
                    TestAssert.Equal((ulong)1, message.RoomSnapshot.RoomId);
                    TestAssert.Equal(1, message.RoomSnapshot.Participants.Count);
                }
            }
            TestAssert.Equal(1, roomSnapshots);
        }

        private static void ProveControlAndBattleConnectProgressThroughPump()
        {
            using var server = SessionRoomTests.CreateServer(maxSessions: 2, maxRooms: 1, maxRoomCapacity: 2, controlReceiveReadCapacity: 64);
            using var host = CreateClient(server.ControlPort);
            using var guest = CreateClient(server.ControlPort);
            ConnectAndEnter(server, host, "Host");
            ConnectAndEnter(server, guest, "Guest");
            host.CreateRoom("Room", 2);
            PumpUntil(server, host, guest, () => host.Phase == DemoClientPhase.Room);
            guest.JoinRoom(host.Snapshot.RoomId);
            PumpUntil(server, host, guest, () => guest.Phase == DemoClientPhase.Room);
            host.SetReady(true);
            guest.SetReady(true);
            Pump(server, host, guest, 40);
            host.StartBattle();
            PumpUntil(server, host, guest, () => host.Phase == DemoClientPhase.PreparingBattle);
            TestAssert.True(GetPrivateField<NetworkStream>(host, "_battleStream") is null);
            TestAssert.True(GetPrivateField<TcpClient>(host, "_battleClient") is not null);
            for (int index = 0; index < 40 && GetPrivateField<NetworkStream>(host, "_battleStream") is null; index++)
            {
                host.PumpOnce(null);
            }
            TestAssert.True(GetPrivateField<NetworkStream>(host, "_battleStream") is not null);
        }

        private static void ProveNamedSessionsUseSessionIdOrder()
        {
            using var server = SessionRoomTests.CreateServer(maxSessions: 2, maxRooms: 1, maxRoomCapacity: 2, controlReceiveReadCapacity: 64);
            using var acceptedFirst = CreateClient(server.ControlPort);
            using var acceptedSecond = CreateClient(server.ControlPort);

            acceptedFirst.BeginConnect();
            TestAssert.Equal(DemoClientPhase.ConnectingControl, acceptedFirst.Phase);
            PumpUntil(server, acceptedFirst, null, () => acceptedFirst.Phase == DemoClientPhase.AwaitingSessionEntry);
            acceptedSecond.BeginConnect();
            TestAssert.Equal(DemoClientPhase.ConnectingControl, acceptedSecond.Phase);
            PumpUntil(server, acceptedSecond, null, () => acceptedSecond.Phase == DemoClientPhase.AwaitingSessionEntry);

            acceptedSecond.EnterSession("SessionOne");
            PumpUntil(server, acceptedFirst, acceptedSecond, () => acceptedSecond.Phase == DemoClientPhase.Lobby);
            acceptedFirst.EnterSession("SessionTwo");
            PumpUntil(server, acceptedFirst, acceptedSecond, () => acceptedFirst.Phase == DemoClientPhase.Lobby);
            TestAssert.Equal((ulong)1, acceptedSecond.Snapshot.SessionId);
            TestAssert.Equal((ulong)2, acceptedFirst.Snapshot.SessionId);

            acceptedFirst.CreateRoom("WrongFirst", 2);
            acceptedSecond.CreateRoom("CorrectFirst", 2);
            acceptedFirst.PumpOnce(null);
            acceptedSecond.PumpOnce(null);
            for (int index = 0; index < 20 && server.RoomCount == 0; index++) server.PumpOnce();
            TestAssert.Equal((ulong)1, server.GetRoom(1).HostSessionId);

            PumpUntil(server, acceptedFirst, acceptedSecond, () => acceptedSecond.Phase == DemoClientPhase.Room);
            acceptedFirst.JoinRoom(1);
            PumpUntil(server, acceptedFirst, acceptedSecond, () => acceptedFirst.Phase == DemoClientPhase.Room);
            acceptedFirst.StartBattle();
            PumpUntil(server, acceptedFirst, acceptedSecond, () => acceptedFirst.Snapshot.LastRejection == "NOT_HOST");
            acceptedSecond.StartBattle();
            PumpUntil(server, acceptedFirst, acceptedSecond, () => acceptedSecond.Snapshot.LastRejection == "NOT_READY");
        }

        private static TcpDemoClient CreateClient(int controlPort)
        {
            return new TcpDemoClient(new DemoClientOptions(controlPort, 1024, 2048, 128, 3, 64, 4, 64, 4, 4, 8, 16, 1024, 32, 3, 8));
        }

        private static TcpDemoClient CreateGameplayClient(int controlPort, BattleDefinition definition)
        {
            return new TcpDemoClient(new DemoClientOptions(
                controlPort, 1024, 2048, 128, 3, 64, 4, 64,
                8, 8, 16, 128, 1024, 32, 3, 8,
                "127.0.0.1", definition));
        }

        private static void ConnectAndEnter(TcpDemoServer server, TcpDemoClient client, string nickname)
        {
            client.BeginConnect();
            TestAssert.Equal(DemoClientPhase.ConnectingControl, client.Phase);
            PumpUntil(server, client, null, () => client.Phase == DemoClientPhase.AwaitingSessionEntry);
            client.EnterSession(nickname);
            PumpUntil(server, client, null, () => client.Phase == DemoClientPhase.Lobby);
        }

        private static void PumpUntil(TcpDemoServer server, TcpDemoClient first, TcpDemoClient? second, Func<bool> condition)
        {
            for (int index = 0; index < 2000 && !condition(); index++)
            {
                server.PumpOnce();
                first.PumpOnce(null);
                second?.PumpOnce(null);
            }
            TestAssert.True(condition());
        }

        private static void Pump(TcpDemoServer server, TcpDemoClient first, TcpDemoClient second, int count)
        {
            for (int index = 0; index < count; index++)
            {
                server.PumpOnce();
                first.PumpOnce(null);
                second.PumpOnce(null);
            }
        }

        private static T? GetPrivateField<T>(TcpDemoClient client, string name)
            where T : class
        {
            FieldInfo field = typeof(TcpDemoClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing field {name}.");
            return field.GetValue(client) as T;
        }

        private static TcpDemoServer FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room, int maxPendingControlBytesPerSession = 2048)
        {
            TcpDemoServer server = SessionRoomTests.CreateServer(maxPendingControlBytesPerSession: maxPendingControlBytesPerSession);
            host = server.EnterSession("Host");
            guest = server.EnterSession("Guest");
            room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            return server;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }

        internal sealed class PreparedFixture : IDisposable
        {
            internal PreparedFixture()
            {
                Server = SessionRoomTests.CreateServer();
                Host = Server.EnterSession("Host");
                Guest = Server.EnterSession("Guest");
                Room = Server.CreateRoom(Host.SessionId, "Room", 2);
                Server.JoinRoom(Guest.SessionId, Room.RoomId);
                Server.SetReady(Host.SessionId, true);
                Server.SetReady(Guest.SessionId, true);
                Preparation = Server.StartBattle(Host.SessionId);
            }

            internal TcpDemoServer Server { get; }
            internal DemoSession Host { get; }
            internal DemoSession Guest { get; }
            internal DemoRoom Room { get; }
            internal BattlePreparation Preparation { get; }

            internal TcpClient ConnectAndWrite(byte[] ticket)
            {
                var client = new TcpClient(AddressFamily.InterNetwork);
                client.Connect(IPAddress.Loopback, Server.BattlePort);
                client.GetStream().Write(ticket, 0, ticket.Length);
                return client;
            }

            internal TcpClient Attach(byte[] ticket)
            {
                TcpClient client = ConnectAndWrite(ticket);
                Pump(30);
                return client;
            }

            internal void Pump(int count)
            {
                for (int index = 0; index < count; index++) Server.PumpOnce();
            }

            public void Dispose()
            {
                Server.Dispose();
            }
        }
    }
}
