using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Google.Protobuf;
using LockstepArena.Client.Demo;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.DemoHost;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class SessionRoomTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(NicknameValidationUsesTrimControlUtf8AndOrdinalRules), NicknameValidationUsesTrimControlUtf8AndOrdinalRules),
            new TestCase(nameof(RoomNameValidationUsesTrimControlAndUtf8Rules), RoomNameValidationUsesTrimControlAndUtf8Rules),
            new TestCase(nameof(SessionIdsStartAtOneIncreaseAndAreNeverReused), SessionIdsStartAtOneIncreaseAndAreNeverReused),
            new TestCase(nameof(SessionIdExhaustionRejectsWithoutWrapOrMutation), SessionIdExhaustionRejectsWithoutWrapOrMutation),
            new TestCase(nameof(DuplicateActiveNicknameRejectsWithoutAllocatingSession), DuplicateActiveNicknameRejectsWithoutAllocatingSession),
            new TestCase(nameof(CreateRoomAutoJoinsHostAtJoinOrdinalZero), CreateRoomAutoJoinsHostAtJoinOrdinalZero),
            new TestCase(nameof(CreateRoomCapacityUsesConfiguredSpawnBoundNotOneVsOne), CreateRoomCapacityUsesConfiguredSpawnBoundNotOneVsOne),
            new TestCase(nameof(RoomIdExhaustionRejectsWithoutRoomMutation), RoomIdExhaustionRejectsWithoutRoomMutation),
            new TestCase(nameof(RoomListUsesStableCreationOrderAndCompleteSummaries), RoomListUsesStableCreationOrderAndCompleteSummaries),
            new TestCase(nameof(JoinAppendsStableJoinOrderAndBroadcastsSnapshot), JoinAppendsStableJoinOrderAndBroadcastsSnapshot),
            new TestCase(nameof(JoinRejectsMissingClosedFullOrAlreadyJoinedRoom), JoinRejectsMissingClosedFullOrAlreadyJoinedRoom),
            new TestCase(nameof(ReadyAndUnreadyBroadcastAtomicRoomSnapshots), ReadyAndUnreadyBroadcastAtomicRoomSnapshots),
            new TestCase(nameof(OpenNonHostLeaveRemovesOnlyThatParticipant), OpenNonHostLeaveRemovesOnlyThatParticipant),
            new TestCase(nameof(OpenHostLossClosesRoomAndReturnsSurvivorsToLobby), OpenHostLossClosesRoomAndReturnsSurvivorsToLobby),
            new TestCase(nameof(LanEndpointsUseConfiguredIpv4AddressAndValidateOptions), LanEndpointsUseConfiguredIpv4AddressAndValidateOptions),
            new TestCase(nameof(TcpClientsExposeStructuredLobbyAndRoomReadModels), TcpClientsExposeStructuredLobbyAndRoomReadModels),
            new TestCase(nameof(AsyncCommandRejectionsAreTypedConsumableAndDoNotPolluteSuccess), AsyncCommandRejectionsAreTypedConsumableAndDoNotPolluteSuccess),
            new TestCase(nameof(JoinRacesReturnRoomNotFoundAndRoomFullRejections), JoinRacesReturnRoomNotFoundAndRoomFullRejections),
        };

        private static void TcpClientsExposeStructuredLobbyAndRoomReadModels()
        {
            using var server = CreateServer();
            using var host = CreateClient(server);
            using var guest = CreateClient(server);
            host.BeginConnect();
            guest.BeginConnect();
            PumpUntil(server, host, guest, DemoClientPhase.AwaitingSessionEntry);
            host.EnterSession("Host");
            guest.EnterSession("Guest");
            PumpUntil(server, host, guest, DemoClientPhase.Lobby);

            host.CreateRoom("Room", 2);
            PumpUntil(server, host, DemoClientPhase.Room);
            guest.RequestRoomList();
            PumpUntil(server, host, guest, () => guest.Snapshot.Rooms.Count == 1);

            DemoClientSnapshot lobby = guest.Snapshot;
            TestAssert.Equal(1, lobby.Rooms.Count);
            TestAssert.Equal("Room", lobby.Rooms[0].RoomName);
            TestAssert.Equal((uint)2, lobby.Rooms[0].Capacity);
            TestAssert.Equal((uint)1, lobby.Rooms[0].ParticipantCount);
            TestAssert.Equal(RoomLifecycleMessage.RoomLifecycleOpen, lobby.Rooms[0].Lifecycle);

            guest.JoinRoom(lobby.Rooms[0].RoomId);
            PumpUntil(server, host, guest, DemoClientPhase.Room);
            host.SetReady(true);
            PumpUntil(server, host, guest, () => host.Snapshot.Participants.Count == 2 &&
                host.Snapshot.Participants[0].IsReady);

            DemoClientSnapshot room = host.Snapshot;
            TestAssert.Equal(room.SessionId, room.RoomHostSessionId);
            TestAssert.Equal((uint)2, room.RoomCapacity);
            TestAssert.Equal(RoomLifecycleMessage.RoomLifecycleOpen, room.RoomLifecycle);
            TestAssert.Equal(2, room.Participants.Count);
            TestAssert.True(room.Participants[0].IsHost);
            TestAssert.True(room.Participants[0].IsReady);
            TestAssert.Equal("Guest", room.Participants[1].Nickname);
        }

        private static void AsyncCommandRejectionsAreTypedConsumableAndDoNotPolluteSuccess()
        {
            using var server = CreateServer();
            using var host = CreateClient(server);
            using var guest = CreateClient(server);
            host.BeginConnect();
            guest.BeginConnect();
            PumpUntil(server, host, guest, DemoClientPhase.AwaitingSessionEntry);
            host.EnterSession("Host");
            guest.EnterSession("Guest");
            PumpUntil(server, host, guest, DemoClientPhase.Lobby);
            host.CreateRoom("Room", 2);
            PumpUntil(server, host, DemoClientPhase.Room);
            guest.JoinRoom(host.Snapshot.RoomId);
            PumpUntil(server, host, guest, DemoClientPhase.Room);

            guest.StartBattle();
            (ControlRejectReasonMessage notHost, string notHostDetail) =
                PumpUntilRejection(server, host, guest, guest);
            TestAssert.Equal(ControlRejectReasonMessage.ControlRejectReasonNotHost, notHost);
            TestAssert.Equal("Only the host may start.", notHostDetail);
            TestAssert.True(!guest.TryConsumeRejection(out _, out _));

            host.StartBattle();
            (ControlRejectReasonMessage notReady, string notReadyDetail) =
                PumpUntilRejection(server, host, guest, host);
            TestAssert.Equal(ControlRejectReasonMessage.ControlRejectReasonNotReady, notReady);
            TestAssert.Equal("Every participant must be ready.", notReadyDetail);
            TestAssert.True(!host.TryConsumeRejection(out _, out _));

            host.SetReady(true);
            guest.SetReady(true);
            PumpUntil(server, host, guest, () => host.Snapshot.Participants.Count == 2 &&
                host.Snapshot.Participants[0].IsReady && host.Snapshot.Participants[1].IsReady);
            TestAssert.True(!host.TryConsumeRejection(out _, out _));
            TestAssert.True(!guest.TryConsumeRejection(out _, out _));
        }

        private static void JoinRacesReturnRoomNotFoundAndRoomFullRejections()
        {
            using var server = CreateServer();
            using var host = CreateClient(server);
            using var guest = CreateClient(server);
            using var late = CreateClient(server);
            host.BeginConnect();
            guest.BeginConnect();
            late.BeginConnect();
            PumpUntil(server, host, guest, late, () =>
                host.Phase == DemoClientPhase.AwaitingSessionEntry &&
                guest.Phase == DemoClientPhase.AwaitingSessionEntry &&
                late.Phase == DemoClientPhase.AwaitingSessionEntry);
            host.EnterSession("Host");
            guest.EnterSession("Guest");
            late.EnterSession("Late");
            PumpUntil(server, host, guest, late, () =>
                host.Phase == DemoClientPhase.Lobby &&
                guest.Phase == DemoClientPhase.Lobby &&
                late.Phase == DemoClientPhase.Lobby);

            late.JoinRoom(999UL);
            (ControlRejectReasonMessage missing, string missingDetail) =
                PumpUntilRejection(server, host, guest, late, late);
            TestAssert.Equal(ControlRejectReasonMessage.ControlRejectReasonRoomNotFound, missing);
            TestAssert.Equal("Room was not found.", missingDetail);

            host.CreateRoom("Room", 2);
            PumpUntil(server, host, DemoClientPhase.Room);
            guest.JoinRoom(host.Snapshot.RoomId);
            PumpUntil(server, host, guest, DemoClientPhase.Room);
            late.JoinRoom(host.Snapshot.RoomId);
            (ControlRejectReasonMessage full, string fullDetail) =
                PumpUntilRejection(server, host, guest, late, late);
            TestAssert.Equal(ControlRejectReasonMessage.ControlRejectReasonRoomFull, full);
            TestAssert.Equal("Room is full.", fullDetail);
            TestAssert.Equal(DemoClientPhase.Lobby, late.Phase);
        }

        private static void NicknameValidationUsesTrimControlUtf8AndOrdinalRules()
        {
            using var server = CreateServer();
            DemoSession alpha = server.EnterSession("  Alpha  ");
            TestAssert.Equal("Alpha", alpha.Nickname);
            DemoSession lower = server.EnterSession("alpha");
            TestAssert.Equal((ulong)2, lower.SessionId);
            TestAssert.Throws<ArgumentException>(() => server.EnterSession("bad\nname"));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => server.EnterSession(new string('\u754c', 11)));
            ControlEntryUsesBoundedPumpAndConfirmedEvent();
        }

        private static void ControlEntryUsesBoundedPumpAndConfirmedEvent()
        {
            using var server = CreateServer();
            using var client = new TcpDemoClient(new DemoClientOptions(server.ControlPort, 1024, 2048, 32, 3, 8, 4, 64, 4, 4, 8, 16, 1024, 32, 3, 8));
            client.BeginConnect();
            TestAssert.Equal(DemoClientPhase.ConnectingControl, client.Phase);
            PumpUntil(server, client, DemoClientPhase.AwaitingSessionEntry);
            client.EnterSession("NetworkAlpha");
            TestAssert.Equal(DemoClientPhase.AwaitingSessionEntry, client.Phase);
            for (int index = 0; index < 200 && client.Phase != DemoClientPhase.Lobby; index++)
            {
                server.PumpOnce();
                client.PumpOnce(null);
            }

            TestAssert.Equal(DemoClientPhase.Lobby, client.Phase);
            TestAssert.Equal("NetworkAlpha", client.Snapshot.Nickname);
            TestAssert.True(client.Snapshot.SessionId > 0);

            client.CreateRoom("Network Room", 2);
            PumpUntil(server, client, DemoClientPhase.Room);
            client.StartBattle();
            client.PumpOnce(null);
            TestAssert.Equal(DemoClientPhase.Room, client.Phase);
            for (int index = 0; index < 200 && client.Snapshot.LastRejection.Length == 0; index++)
            {
                server.PumpOnce();
                client.PumpOnce(null);
            }
            TestAssert.Equal(DemoClientPhase.Room, client.Phase);
            TestAssert.Equal("NOT_READY", client.Snapshot.LastRejection);

            client.LeaveRoom();
            TestAssert.Equal(DemoClientPhase.Room, client.Phase);
            PumpUntil(server, client, DemoClientPhase.Lobby);
        }

        private static void PumpUntil(TcpDemoServer server, TcpDemoClient client, DemoClientPhase phase)
        {
            for (int index = 0; index < 400 && client.Phase != phase; index++)
            {
                server.PumpOnce();
                client.PumpOnce(null);
            }
            TestAssert.Equal(phase, client.Phase);
        }

        private static void PumpUntil(
            TcpDemoServer server,
            TcpDemoClient first,
            TcpDemoClient second,
            DemoClientPhase phase)
        {
            PumpUntil(server, first, second, () => first.Phase == phase && second.Phase == phase);
            TestAssert.Equal(phase, first.Phase);
            TestAssert.Equal(phase, second.Phase);
        }

        private static void PumpUntil(
            TcpDemoServer server,
            TcpDemoClient first,
            TcpDemoClient second,
            Func<bool> complete)
        {
            for (int index = 0; index < 800 && !complete(); index++)
            {
                server.PumpOnce();
                first.PumpOnce(null);
                second.PumpOnce(null);
            }
            TestAssert.True(complete());
        }

        private static void PumpUntil(
            TcpDemoServer server,
            TcpDemoClient first,
            TcpDemoClient second,
            TcpDemoClient third,
            Func<bool> complete)
        {
            for (int index = 0; index < 800 && !complete(); index++)
            {
                server.PumpOnce();
                first.PumpOnce(null);
                second.PumpOnce(null);
                third.PumpOnce(null);
            }
            TestAssert.True(complete());
        }

        private static (ControlRejectReasonMessage Reason, string Detail) PumpUntilRejection(
            TcpDemoServer server,
            TcpDemoClient first,
            TcpDemoClient second,
            TcpDemoClient target)
        {
            for (int index = 0; index < 800; index++)
            {
                server.PumpOnce();
                first.PumpOnce(null);
                second.PumpOnce(null);
                if (target.TryConsumeRejection(out ControlRejectReasonMessage reason, out string detail))
                    return (reason, detail);
            }
            throw new InvalidOperationException("Expected a command rejection.");
        }

        private static (ControlRejectReasonMessage Reason, string Detail) PumpUntilRejection(
            TcpDemoServer server,
            TcpDemoClient first,
            TcpDemoClient second,
            TcpDemoClient third,
            TcpDemoClient target)
        {
            for (int index = 0; index < 800; index++)
            {
                server.PumpOnce();
                first.PumpOnce(null);
                second.PumpOnce(null);
                third.PumpOnce(null);
                if (target.TryConsumeRejection(out ControlRejectReasonMessage reason, out string detail))
                    return (reason, detail);
            }
            throw new InvalidOperationException("Expected a command rejection.");
        }

        private static TcpDemoClient CreateClient(TcpDemoServer server)
        {
            return new TcpDemoClient(new DemoClientOptions(
                server.ControlPort, 1024, 2048, 32, 3, 8, 4, 64,
                4, 4, 8, 16, 1024, 32, 3, 8));
        }

        private static void RoomNameValidationUsesTrimControlAndUtf8Rules()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoRoom room = server.CreateRoom(host.SessionId, "  Golden Room  ", 2);
            TestAssert.Equal("Golden Room", room.RoomName);
            TestAssert.Throws<ArgumentException>(() => server.CreateRoom(server.EnterSession("B").SessionId, "bad\troom", 2));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => server.CreateRoom(server.EnterSession("C").SessionId, new string('\u754c', 17), 2));
        }

        private static void SessionIdsStartAtOneIncreaseAndAreNeverReused()
        {
            using var server = CreateServer();
            DemoSession first = server.EnterSession("A");
            DemoSession second = server.EnterSession("B");
            server.CloseSession(first.SessionId);
            DemoSession third = server.EnterSession("C");
            TestAssert.Equal((ulong)1, first.SessionId);
            TestAssert.Equal((ulong)2, second.SessionId);
            TestAssert.Equal((ulong)3, third.SessionId);
        }

        private static void SessionIdExhaustionRejectsWithoutWrapOrMutation()
        {
            using var server = CreateServer(maxSessions: 3, maxRoomCapacity: 3);
            SetCounter(server, "_nextSessionId", ulong.MaxValue);
            DemoSession last = server.EnterSession("Last");
            TestAssert.Equal(ulong.MaxValue, last.SessionId);
            int count = server.SessionCount;
            TestAssert.Throws<InvalidOperationException>(() => server.EnterSession("Never"));
            TestAssert.Equal(count, server.SessionCount);
        }

        private static void DuplicateActiveNicknameRejectsWithoutAllocatingSession()
        {
            using var server = CreateServer();
            server.EnterSession("Alpha");
            TestAssert.Throws<InvalidOperationException>(() => server.EnterSession("Alpha"));
            TestAssert.Equal((ulong)2, server.EnterSession("Bravo").SessionId);
        }

        private static void CreateRoomAutoJoinsHostAtJoinOrdinalZero()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            TestAssert.Equal(host.SessionId, room.HostSessionId);
            TestAssert.Equal(1, room.ParticipantCount);
            TestAssert.Equal(host.SessionId, room.GetParticipant(0).SessionId);
            TestAssert.Equal(0, room.GetParticipant(0).JoinOrdinal);
            TestAssert.Equal(DemoSessionPhase.InRoom, host.Phase);
        }

        private static void CreateRoomCapacityUsesConfiguredSpawnBoundNotOneVsOne()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(() => CreateOptions(maxSessions: 3, maxRoomCapacity: 4));
            using (var equal = new TcpDemoServer(CreateOptions(maxSessions: 4, maxRoomCapacity: 4)))
            {
                DemoSession host = equal.EnterSession("Host");
                DemoRoom room = equal.CreateRoom(host.SessionId, "Four", 4);
                TestAssert.Equal(4, room.Capacity);
            }

            TestAssert.Throws<ArgumentOutOfRangeException>(() => new DemoServerOptions(0, 0, 2, 2, 2, SpawnStates(2), 2, 8, 0, 4, 1024, 2048, 32, 3, 8, 4, 64, 1024, 32, 3, 8));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new DemoServerOptions(0, 0, 4, 2, 4, SpawnStates(3), 2, 8, 8, 4, 1024, 2048, 32, 3, 8, 4, 64, 1024, 32, 3, 8));
        }

        private static void LanEndpointsUseConfiguredIpv4AddressAndValidateOptions()
        {
            DemoServerOptions serverOptions = CreateOptions(
                bindAddress: "127.0.0.1",
                battleDefinition: BattleDefinition.CreateDefault(),
                battleReadyTimeout: TimeSpan.FromSeconds(15));
            using var server = new TcpDemoServer(serverOptions);
            TestAssert.Equal(IPAddress.Loopback, server.BindAddress);

            using var client = new TcpDemoClient(new DemoClientOptions(
                server.ControlPort, 1024, 2048, 32, 3, 8, 4, 64,
                4, 4, 8, 16, 1024, 32, 3, 8,
                "127.0.0.1", BattleDefinition.CreateDefault()));
            client.BeginConnect();
            PumpUntil(server, client, DemoClientPhase.AwaitingSessionEntry);

            TestAssert.Throws<ArgumentException>(() => CreateOptions(bindAddress: "localhost"));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => CreateOptions(battleReadyTimeout: TimeSpan.FromTicks(-1)));
            TestAssert.Throws<ArgumentException>(() => new DemoClientOptions(
                1, 1024, 2048, 32, 3, 8, 4, 64,
                4, 4, 8, 16, 1024, 32, 3, 8,
                "localhost", BattleDefinition.CreateDefault()));
        }

        private static void RoomIdExhaustionRejectsWithoutRoomMutation()
        {
            using var server = CreateServer(maxRooms: 3);
            DemoSession first = server.EnterSession("A");
            DemoSession second = server.EnterSession("B");
            SetCounter(server, "_nextRoomId", ulong.MaxValue);
            TestAssert.Equal(ulong.MaxValue, server.CreateRoom(first.SessionId, "Last", 2).RoomId);
            int count = server.RoomCount;
            TestAssert.Throws<InvalidOperationException>(() => server.CreateRoom(second.SessionId, "Never", 2));
            TestAssert.Equal(count, server.RoomCount);
        }

        private static void RoomListUsesStableCreationOrderAndCompleteSummaries()
        {
            using var server = CreateServer(maxSessions: 4, maxRooms: 3, maxRoomCapacity: 2);
            DemoRoom first = server.CreateRoom(server.EnterSession("A").SessionId, "First", 2);
            DemoRoom second = server.CreateRoom(server.EnterSession("B").SessionId, "Second", 2);
            RoomSummaryMessage[] summaries = server.GetRoomSummaries();
            TestAssert.Equal(2, summaries.Length);
            TestAssert.Equal(first.RoomId, summaries[0].RoomId);
            TestAssert.Equal("A", summaries[0].HostNickname);
            TestAssert.Equal((uint)1, summaries[0].ParticipantCount);
            TestAssert.Equal(second.RoomId, summaries[1].RoomId);
        }

        private static void JoinAppendsStableJoinOrderAndBroadcastsSnapshot()
        {
            using var server = CreateServer(maxPendingControlBytesPerSession: 1028);
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            host.ClearEvents();
            guest.ClearEvents();
            FillControlCapacity(host);
            int retainedHostEvents = host.EventCount;
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(guest.SessionId, room.RoomId));
            TestAssert.Equal(1, room.ParticipantCount);
            TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
            TestAssert.Equal(retainedHostEvents, host.EventCount);
            TestAssert.Equal(0, guest.EventCount);
            host.ClearEvents();
            server.JoinRoom(guest.SessionId, room.RoomId);
            TestAssert.Equal(guest.SessionId, room.GetParticipant(1).SessionId);
            TestAssert.Equal(1, room.GetParticipant(1).JoinOrdinal);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.RoomSnapshot, host.LastEvent.EventCase);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.RoomSnapshot, guest.LastEvent.EventCase);
        }

        private static void JoinRejectsMissingClosedFullOrAlreadyJoinedRoom()
        {
            using var server = CreateServer(maxSessions: 4);
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoSession extra = server.EnterSession("Extra");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(guest.SessionId, 999));
            server.JoinRoom(guest.SessionId, room.RoomId);
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(guest.SessionId, room.RoomId));
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(extra.SessionId, room.RoomId));
            server.CloseSession(host.SessionId);
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(extra.SessionId, room.RoomId));
        }

        private static void ReadyAndUnreadyBroadcastAtomicRoomSnapshots()
        {
            using var server = CreateServer(maxPendingControlBytesPerSession: 1028);
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            host.ClearEvents();
            guest.ClearEvents();
            FillControlCapacity(host);
            int retainedHostEvents = host.EventCount;
            TestAssert.Throws<InvalidOperationException>(() => server.SetReady(guest.SessionId, true));
            TestAssert.True(!room.GetParticipant(1).IsReady);
            TestAssert.Equal(retainedHostEvents, host.EventCount);
            TestAssert.Equal(0, guest.EventCount);
            host.ClearEvents();
            server.SetReady(guest.SessionId, true);
            TestAssert.True(room.GetParticipant(1).IsReady);
            TestAssert.True(host.LastEvent.RoomSnapshot.Participants[1].IsReady);
            server.SetReady(guest.SessionId, false);
            TestAssert.True(!room.GetParticipant(1).IsReady);
            TestAssert.True(!host.LastEvent.RoomSnapshot.Participants[1].IsReady);
        }

        private static void OpenNonHostLeaveRemovesOnlyThatParticipant()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            guest.ClearEvents();
            server.LeaveRoom(guest.SessionId);
            TestAssert.Equal(1, room.ParticipantCount);
            TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.LobbyEntered, guest.LastEvent.EventCase);
        }

        private static void OpenHostLossClosesRoomAndReturnsSurvivorsToLobby()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            guest.ClearEvents();
            server.CloseSession(host.SessionId);
            TestAssert.Equal(0, server.RoomCount);
            TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.LobbyEntered, guest.LastEvent.EventCase);

            using var pressuredServer = CreateServer(maxPendingControlBytesPerSession: 1028, controlReceiveReadCapacity: 64);
            using TcpClient physicalHost = ConnectNamed(pressuredServer, "PhysicalHost", 1);
            using TcpClient physicalGuest = ConnectNamed(pressuredServer, "PhysicalGuest", 2);
            DemoSession lostHost = pressuredServer.GetSession(1);
            DemoSession pressuredGuest = pressuredServer.GetSession(2);
            pressuredServer.CreateRoom(lostHost.SessionId, "PhysicalRoom", 2);
            pressuredServer.JoinRoom(pressuredGuest.SessionId, 1);
            pressuredGuest.ClearEvents();
            FillControlCapacity(pressuredGuest);
            physicalHost.Client.Shutdown(SocketShutdown.Both);
            for (int index = 0; index < 20 && lostHost.Phase != DemoSessionPhase.Closed; index++) pressuredServer.PumpOnce();
            TestAssert.Equal(0, pressuredServer.RoomCount);
            TestAssert.Equal(DemoSessionPhase.Closed, lostHost.Phase);
            TestAssert.Equal(DemoSessionPhase.Closed, pressuredGuest.Phase);
            TestAssert.Equal(0, pressuredServer.SessionCount);
        }

        internal static TcpClient ConnectNamed(TcpDemoServer server, string nickname, int expectedSessionCount)
        {
            var client = new TcpClient(AddressFamily.InterNetwork);
            client.Connect(IPAddress.Loopback, server.ControlPort);
            for (int index = 0; index < 20; index++) server.PumpOnce();
            WriteCommand(client, new ClientControlCommandMessage
            {
                EnterSession = new EnterSessionCommandMessage { Nickname = nickname },
            });
            for (int index = 0; index < 100 && server.SessionCount < expectedSessionCount; index++) server.PumpOnce();
            TestAssert.Equal(expectedSessionCount, server.SessionCount);
            return client;
        }

        internal static void WriteCommand(TcpClient client, ClientControlCommandMessage command)
        {
            byte[] framed = LengthPrefixedFrameEncoder.Encode(command.ToByteArray(), 1024);
            client.GetStream().Write(framed, 0, framed.Length);
        }

        internal static TcpDemoServer CreateServer(int maxSessions = 4, int maxRooms = 4, int maxRoomCapacity = 4, int maxPendingControlBytesPerSession = 2048, int controlReceiveReadCapacity = 8, int maxControlSendBytesPerPump = 64)
        {
            return new TcpDemoServer(CreateOptions(maxSessions, maxRooms, maxRoomCapacity, maxPendingControlBytesPerSession, controlReceiveReadCapacity, maxControlSendBytesPerPump));
        }

        internal static DemoServerOptions CreateOptions(int maxSessions = 4, int maxRooms = 4, int maxRoomCapacity = 4, int maxPendingControlBytesPerSession = 2048, int controlReceiveReadCapacity = 8, int maxControlSendBytesPerPump = 64, string bindAddress = "127.0.0.1", BattleDefinition? battleDefinition = null, TimeSpan? battleReadyTimeout = null, uint battleDurationTicks = 4)
        {
            return new DemoServerOptions(0, 0, maxSessions, maxRooms, maxRoomCapacity, SpawnStates(maxRoomCapacity), 2, 8, 8, battleDurationTicks, 1024, maxPendingControlBytesPerSession, 128, 3, controlReceiveReadCapacity, 4, maxControlSendBytesPerPump, 1024, 32, 3, 8, bindAddress, battleDefinition, battleReadyTimeout);
        }

        internal static void FillControlCapacity(DemoSession session)
        {
            session.Queue(new ServerControlEventMessage
            {
                CommandRejected = new CommandRejectedEventMessage
                {
                    Reason = ControlRejectReasonMessage.ControlRejectReasonResourceLimit,
                    Detail = new string('x', 990),
                },
            });
        }

        private static PlayerState[] SpawnStates(int count)
        {
            var states = new PlayerState[count];
            for (int index = 0; index < states.Length; index++)
            {
                states[index] = new PlayerState(-300 + (index * 200), 0, (ushort)(1000 + index));
            }

            return states;
        }

        private static void SetCounter(TcpDemoServer server, string name, ulong value)
        {
            FieldInfo field = typeof(TcpDemoServer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"Missing counter {name}.");
            field.SetValue(server, value);
        }
    }
}
