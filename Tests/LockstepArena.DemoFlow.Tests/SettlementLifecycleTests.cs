using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Google.Protobuf;
using LockstepArena.Client.Demo;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.DemoHost;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class SettlementLifecycleTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(ClientNeverSubmitsInputPastFinalInputTick), ClientNeverSubmitsInputPastFinalInputTick),
            new TestCase(nameof(TickLimitCreatesOneNormalSettlementAtExactFinalTick), TickLimitCreatesOneNormalSettlementAtExactFinalTick),
            new TestCase(nameof(EarlySettlementRemainsPendingUntilFinalAuthority), EarlySettlementRemainsPendingUntilFinalAuthority),
            new TestCase(nameof(MatchingSettlementVerifiesCompleteStateAndDigest), MatchingSettlementVerifiesCompleteStateAndDigest),
            new TestCase(nameof(SettlementMismatchOrImpossibleTickFailsStop), SettlementMismatchOrImpossibleTickFailsStop),
            new TestCase(nameof(NormalSettlementDisposesBattleButPreservesControlSession), NormalSettlementDisposesBattleButPreservesControlSession),
            new TestCase(nameof(OnlyVerifiedSettlementRetainsReplayUntilLobbyReturn), OnlyVerifiedSettlementRetainsReplayUntilLobbyReturn),
            new TestCase(nameof(BattleFailureAbortsRoomAndNotifiesSurvivingControls), BattleFailureAbortsRoomAndNotifiesSurvivingControls),
            new TestCase(nameof(GameplayMatchEndedCreatesWinnerAndScoreSettlement), GameplayMatchEndedCreatesWinnerAndScoreSettlement),
            new TestCase(nameof(GameplayBattleDisconnectSettlesAsForfeit), GameplayBattleDisconnectSettlesAsForfeit),
        };

        private static void ClientNeverSubmitsInputPastFinalInputTick()
        {
            BattleState state = CreateState(1U);
            using var fixture = new ClientFixture(state, 1U);
            DemoClientPumpResult result = fixture.Client.PumpOnce(new LocalInputSample(1, 0, 1234));
            TestAssert.True(!result.PredictionSent);
            TestAssert.Equal(1U, fixture.Client.Snapshot.PredictedTick);
            TestAssert.Equal(0, fixture.Peer.Client.Available);
        }

        private static void TickLimitCreatesOneNormalSettlementAtExactFinalTick()
        {
            using var fixture = new BattlePreparationTests.PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            StopBattleClock(fixture.Preparation);
            fixture.Host.ClearEvents();
            fixture.Guest.ClearEvents();

            DemoServerPumpResult result = default;
            for (uint tick = 0; tick < 4; tick++)
            {
                SendInput(first, fixture.Preparation.InitialState.Roster.GetPlayerId(new PlayerSlot(0)), new InputFrame(tick, new PlayerSlot(0), 0, 0, 1000));
                SendInput(second, fixture.Preparation.InitialState.Roster.GetPlayerId(new PlayerSlot(1)), new InputFrame(tick, new PlayerSlot(1), 0, 0, 1001));
                fixture.Pump(40);
                ForceNextPollAdvances(fixture.Preparation, 1U);
                result = fixture.Server.PumpOnce();
            }

            ForceNextPollAdvances(fixture.Preparation, 1U);
            result = fixture.Server.PumpOnce();
            TestAssert.Equal(1, result.CompletedBattles);
            TestAssert.Equal(DemoRoomLifecycle.Settled, fixture.Room.Lifecycle);
            TestAssert.Equal(DemoSessionPhase.Settlement, fixture.Host.Phase);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.BattleSettlement, fixture.Host.LastEvent.EventCase);
            TestAssert.Equal(4U, fixture.Host.LastEvent.BattleSettlement.FinalState.Tick);
            TestAssert.Equal(BattleSettlementReasonMessage.BattleSettlementReasonTickLimitReached, fixture.Host.LastEvent.BattleSettlement.Reason);
            TestAssert.Equal(0, fixture.Server.PumpOnce().CompletedBattles);
        }

        private static void EarlySettlementRemainsPendingUntilFinalAuthority()
        {
            BattleState authority = CreateState(0U);
            BattleState finalState = CreateState(2U);
            using var fixture = new ClientFixture(authority, 2U);
            fixture.Enqueue(CreateSettlement(1UL, finalState, StateDigest.Compute(finalState)));
            fixture.Client.PumpOnce(null);
            TestAssert.Equal(DemoClientPhase.SettlementPendingAuthority, fixture.Client.Phase);
            TestAssert.True(!fixture.Client.Snapshot.SettlementVerified);
            TestAssert.Equal(0U, fixture.Client.Snapshot.AuthoritativeTick);
        }

        private static void MatchingSettlementVerifiesCompleteStateAndDigest()
        {
            BattleState state = CreateState(1U);
            ulong digest = StateDigest.Compute(state);
            using var fixture = new ClientFixture(state, 1U);
            fixture.Enqueue(CreateSettlement(1UL, state, digest));
            fixture.Client.PumpOnce(null);
            TestAssert.Equal(DemoClientPhase.Settlement, fixture.Client.Phase);
            TestAssert.True(fixture.Client.Snapshot.SettlementVerified);
            TestAssert.Equal(digest, fixture.Client.Snapshot.AuthoritativeDigest);
            TestAssert.Equal(digest, fixture.Client.Snapshot.PredictedDigest);
        }

        private static void SettlementMismatchOrImpossibleTickFailsStop()
        {
            BattleState state = CreateState(1U);
            using (var mismatch = new ClientFixture(state, 1U))
            {
                mismatch.Enqueue(CreateSettlement(1UL, state, StateDigest.Compute(state) ^ 1UL));
                TestAssert.Throws<InvalidDataException>(() => mismatch.Client.PumpOnce(null));
                TestAssert.Equal(DemoClientPhase.Faulted, mismatch.Client.Phase);
            }

            BattleState beyondFinal = CreateState(2U);
            using var impossible = new ClientFixture(beyondFinal, 1U);
            BattleState expectedFinal = CreateState(1U);
            impossible.Enqueue(CreateSettlement(1UL, expectedFinal, StateDigest.Compute(expectedFinal)));
            TestAssert.Throws<InvalidDataException>(() => impossible.Client.PumpOnce(null));
            TestAssert.Equal(DemoClientPhase.Faulted, impossible.Client.Phase);
        }

        private static void OnlyVerifiedSettlementRetainsReplayUntilLobbyReturn()
        {
            BattleState state = CreateState(1U);
            using var fixture = new ClientFixture(state, 1U);
            TestAssert.True(fixture.Client.RetainedReplay is null);
            fixture.Enqueue(CreateSettlement(1UL, state, StateDigest.Compute(state)));
            fixture.Client.PumpOnce(null);
            BattleReplaySnapshot snapshot = fixture.Client.RetainedReplay!;
            TestAssert.True(snapshot is not null);
            TestAssert.True(GetPrivateField(fixture.Client, "_battleRuntime") is null);
            BattleState initial = snapshot!.InitialState;
            TestAssert.True(BattleStateValueComparer.HaveSameValue(state, initial));
            fixture.Client.ReturnToLobby();
            TestAssert.True(fixture.Client.RetainedReplay is null);
            fixture.Enqueue(new ServerControlEventMessage { LobbyEntered = new LobbyEnteredEventMessage() });
            fixture.Client.PumpOnce(null);
            TestAssert.Equal(DemoClientPhase.Lobby, fixture.Client.Phase);
            TestAssert.True(fixture.Client.RetainedReplay is null);

            using var mismatch = new ClientFixture(state, 1U);
            mismatch.Enqueue(CreateSettlement(1UL, state, StateDigest.Compute(state) ^ 1UL));
            TestAssert.Throws<InvalidDataException>(() => mismatch.Client.PumpOnce(null));
            TestAssert.True(mismatch.Client.RetainedReplay is null);
            using var aborted = new ClientFixture(state, 1U);
            aborted.Enqueue(new ServerControlEventMessage { BattleSettlement = new BattleSettlementEventMessage
            {
                BattleId = 1UL, Reason = BattleSettlementReasonMessage.BattleSettlementReasonAborted,
            } });
            aborted.Client.PumpOnce(null);
            TestAssert.True(aborted.Client.RetainedReplay is null);
        }

        private static void NormalSettlementDisposesBattleButPreservesControlSession()
        {
            BattleState state = CreateState(1U);
            using var fixture = new ClientFixture(state, 1U, 77UL);
            fixture.Enqueue(CreateSettlement(1UL, state, StateDigest.Compute(state)));
            fixture.Client.PumpOnce(null);
            TestAssert.Equal((ulong)77, fixture.Client.Snapshot.SessionId);
            TestAssert.True(GetPrivateField(fixture.Client, "_battleRuntime") is null);
            fixture.Client.ReturnToLobby();
            TestAssert.Equal(DemoClientPhase.Settlement, fixture.Client.Phase);
        }

        private static void BattleFailureAbortsRoomAndNotifiesSurvivingControls()
        {
            using var server = SessionRoomTests.CreateServer();
            using TcpClient hostControl = SessionRoomTests.ConnectNamed(server, "Host", 1);
            using TcpClient guestControl = SessionRoomTests.ConnectNamed(server, "Guest", 2);
            DemoSession host = server.GetSession(1);
            DemoSession guest = server.GetSession(2);
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            BattlePreparation preparation = server.StartBattle(host.SessionId);
            using TcpClient first = AttachBattle(server, preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = AttachBattle(server, preparation.GetTicket(new PlayerSlot(1)));
            var decoder = new LengthPrefixedFrameDecoder(1024);
            var received = new List<ServerControlEventMessage>();
            ReadAvailableControlEvents(guestControl, decoder, received);
            first.Client.Shutdown(SocketShutdown.Both);
            first.Dispose();
            for (int index = 0; index < 100 && server.RoomCount > 0; index++) server.PumpOnce();
            TestAssert.Equal(0, server.RoomCount);
            TestAssert.True(preparation.IsInvalidated);
            TestAssert.Equal(DemoSessionPhase.Settlement, guest.Phase);
            for (int index = 0; index < 100; index++) server.PumpOnce();
            ReadAvailableControlEvents(guestControl, decoder, received);
            TestAssert.True(received.Exists(message =>
                message.EventCase == ServerControlEventMessage.EventOneofCase.BattleSettlement &&
                message.BattleSettlement.Reason == BattleSettlementReasonMessage.BattleSettlementReasonAborted));
            received.Clear();

            SessionRoomTests.WriteCommand(guestControl, new ClientControlCommandMessage
            {
                ReturnToLobby = new ReturnToLobbyCommandMessage(),
            });
            for (int index = 0; index < 100 && guest.Phase != DemoSessionPhase.Lobby; index++) server.PumpOnce();
            TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
            TestAssert.Equal(0UL, guest.RoomId);
            for (int index = 0; index < 100; index++) server.PumpOnce();
            ReadAvailableControlEvents(guestControl, decoder, received);
            TestAssert.True(received.Exists(message => message.EventCase == ServerControlEventMessage.EventOneofCase.LobbyEntered));
            received.Clear();

            SessionRoomTests.WriteCommand(guestControl, new ClientControlCommandMessage
            {
                RequestRoomList = new RequestRoomListCommandMessage(),
            });
            for (int index = 0; index < 100; index++) server.PumpOnce();
            ReadAvailableControlEvents(guestControl, decoder, received);
            TestAssert.True(received.Exists(message =>
                message.EventCase == ServerControlEventMessage.EventOneofCase.RoomList &&
                message.RoomList.Rooms.Count == 0));
            TestAssert.Equal(0, server.RoomCount);
        }

        private static void GameplayMatchEndedCreatesWinnerAndScoreSettlement()
        {
            using var fixture = new GameplayBattleFixture();
            StopBattleClock(fixture.Preparation);
            fixture.Host.ClearEvents();
            fixture.Guest.ClearEvents();
            uint submittedTick = uint.MaxValue;
            for (int iteration = 0; iteration < 40 && fixture.Room.Lifecycle == DemoRoomLifecycle.InBattle; iteration++)
            {
                uint tick = fixture.Preparation.SharedSession!.NextPublishTick;
                if (tick != submittedTick)
                {
                    BattleState state = fixture.Preparation.SharedSession.ServerState;
                    bool hostFires = state.Phase == BattlePhase.Playing;
                    SendInput(fixture.HostBattle, state.Roster.GetPlayerId(new PlayerSlot(0)),
                        new InputFrame(tick, new PlayerSlot(0), 0, 0, 0, hostFires));
                    SendInput(fixture.GuestBattle, state.Roster.GetPlayerId(new PlayerSlot(1)),
                        new InputFrame(tick, new PlayerSlot(1), 0, 0, 32768, false));
                    submittedTick = tick;
                }

                ForceNextPollAdvances(fixture.Preparation, 1U);
                fixture.Server.PumpOnce();
            }

            TestAssert.Equal(DemoRoomLifecycle.Settled, fixture.Room.Lifecycle);
            BattleSettlementEventMessage settlement = fixture.Host.LastEvent.BattleSettlement;
            TestAssert.Equal(BattleSettlementReasonMessage.BattleSettlementReasonMatchCompleted, settlement.Reason);
            TestAssert.Equal(fixture.Host.SessionId, settlement.WinnerPlayerId);
            TestAssert.Equal(2U, settlement.Slot0RoundWins);
            TestAssert.Equal(0U, settlement.Slot1RoundWins);
            TestAssert.True(settlement.FinalState.Tick < fixture.Preparation.FinalStateTick);
        }

        private static void GameplayBattleDisconnectSettlesAsForfeit()
        {
            using var fixture = new GameplayBattleFixture();
            fixture.Host.ClearEvents();
            fixture.Guest.ClearEvents();
            fixture.HostBattle.Client.Shutdown(SocketShutdown.Both);
            fixture.HostBattle.Dispose();
            DemoServerPumpResult result = default;
            for (int index = 0; index < 100 && fixture.Room.Lifecycle == DemoRoomLifecycle.InBattle; index++)
                result = fixture.Server.PumpOnce();

            TestAssert.Equal(DemoRoomLifecycle.Settled, fixture.Room.Lifecycle);
            TestAssert.Equal(1, result.CompletedBattles);
            BattleSettlementEventMessage settlement = fixture.Guest.LastEvent.BattleSettlement;
            TestAssert.Equal(BattleSettlementReasonMessage.BattleSettlementReasonDisconnectForfeit, settlement.Reason);
            TestAssert.Equal(fixture.Guest.SessionId, settlement.WinnerPlayerId);
            TestAssert.True(settlement.FinalState is not null);
        }

        private static TcpClient AttachBattle(TcpDemoServer server, byte[] ticket)
        {
            var client = new TcpClient(AddressFamily.InterNetwork);
            client.Connect(IPAddress.Loopback, server.BattlePort);
            client.GetStream().Write(ticket, 0, ticket.Length);
            for (int index = 0; index < 30; index++) server.PumpOnce();
            return client;
        }

        private static void ReadAvailableControlEvents(
            TcpClient client,
            LengthPrefixedFrameDecoder decoder,
            List<ServerControlEventMessage> received)
        {
            var buffer = new byte[128];
            while (client.Client.Available > 0)
            {
                int count = client.GetStream().Read(buffer, 0, buffer.Length);
                foreach (byte[] payload in decoder.Feed(buffer, 0, count))
                    received.Add(ServerControlEventMessage.Parser.ParseFrom(payload));
            }
        }

        private static BattleState CreateState(uint tick)
        {
            var roster = new ActiveRoster(new[] { new PlayerId(11UL), new PlayerId(22UL) });
            return new BattleState(tick, roster, new[]
            {
                new PlayerState(-100, 0, 1000),
                new PlayerState(100, 0, 1001),
            });
        }

        private static ServerControlEventMessage CreateSettlement(ulong battleId, BattleState state, ulong digest)
        {
            var finalState = new FinalBattleStateMessage { Tick = state.Tick, StateDigest = digest };
            for (int index = 0; index < state.PlayerCount; index++)
            {
                var slot = new PlayerSlot(index);
                PlayerState player = state.GetPlayerState(slot);
                finalState.PlayerStates.Add(new SettlementPlayerStateMessage
                {
                    PlayerSlot = checked((uint)index),
                    PlayerId = state.Roster.GetPlayerId(slot).Value,
                    PositionX = player.PositionX,
                    PositionZ = player.PositionZ,
                    Aim = player.Aim,
                });
            }

            return new ServerControlEventMessage
            {
                BattleSettlement = new BattleSettlementEventMessage
                {
                    BattleId = battleId,
                    Reason = BattleSettlementReasonMessage.BattleSettlementReasonTickLimitReached,
                    FinalState = finalState,
                },
            };
        }

        private static void SendInput(TcpClient client, PlayerId playerId, InputFrame input)
        {
            byte[] payload = ProtocolMapper.ToWire(playerId, input).ToByteArray();
            var frame = new byte[payload.Length + 4];
            uint length = checked((uint)payload.Length);
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            Array.Copy(payload, 0, frame, 4, payload.Length);
            client.GetStream().Write(frame, 0, frame.Length);
        }

        internal static BattleDefinition CreateFastGameplayDefinition()
        {
            var gameplay = new GameplayConfig(
                1, 1, 100, 1, 500, 10, 50, 10, 30, 5,
                30, 1, 1, 2);
            var arena = new ArenaConfig(
                "fast-settlement",
                new ArenaRectangle(-1_000, 1_000, -1_000, 1_000),
                new[] { new ArenaPoint(-200, 0), new ArenaPoint(200, 0) },
                Array.Empty<ArenaRectangle>());
            return new BattleDefinition(gameplay, arena);
        }

        internal static void StopBattleClock(BattlePreparation preparation)
        {
            object shared = preparation.SharedSession ?? throw new InvalidOperationException("Missing shared session.");
            object processor = GetFieldByTypeName(shared, "ProtocolAuthorityProcessor");
            object driver = GetFieldByTypeName(processor, "StopwatchTickDriver");
            var stopwatch = (Stopwatch)GetFieldByTypeName(driver, nameof(Stopwatch));
            stopwatch.Stop();
        }

        internal static void ForceNextPollAdvances(BattlePreparation preparation, uint dueAdvances)
        {
            object shared = preparation.SharedSession ?? throw new InvalidOperationException("Missing shared session.");
            object processor = GetFieldByTypeName(shared, "ProtocolAuthorityProcessor");
            object driver = GetFieldByTypeName(processor, "StopwatchTickDriver");
            var stopwatch = (Stopwatch)GetFieldByTypeName(driver, nameof(Stopwatch));
            FieldInfo baseline = GetUniquePrivateField(driver.GetType(), typeof(long));
            long stableElapsed = stopwatch.ElapsedTicks;
            UInt128 numerator = ((UInt128)(ulong)Stopwatch.Frequency * dueAdvances) + (uint)SimulationConfig.TickRate - 1U;
            long requiredElapsed = checked((long)(numerator / (uint)SimulationConfig.TickRate));
            baseline.SetValue(driver, checked(stableElapsed - requiredElapsed));
        }

        private static object GetFieldByTypeName(object owner, string typeName)
        {
            FieldInfo[] fields = owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            object? result = null;
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType.Name != typeName) continue;
                if (result is not null) throw new InvalidOperationException($"Multiple private {typeName} fields found.");
                result = fields[index].GetValue(owner);
            }
            return result ?? throw new InvalidOperationException($"Missing private {typeName} field.");
        }

        private static FieldInfo GetUniquePrivateField(Type owner, Type fieldType)
        {
            FieldInfo? result = null;
            FieldInfo[] fields = owner.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType != fieldType) continue;
                if (result is not null) throw new InvalidOperationException($"Multiple private {fieldType.Name} fields found.");
                result = fields[index];
            }
            return result ?? throw new InvalidOperationException($"Missing private {fieldType.Name} field.");
        }

        private static object? GetPrivateField(object owner, string name)
        {
            return owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner);
        }

        private static void SetPrivateField(object owner, string name, object? value)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"Missing private field {name}.");
            field.SetValue(owner, value);
        }

        private sealed class ClientFixture : IDisposable
        {
            private readonly TcpListener _battleListener;
            private readonly TcpListener _controlListener;
            private readonly TcpClient _controlPeer;

            internal ClientFixture(BattleState initialState, uint finalStateTick, ulong sessionId = 1UL)
            {
                _battleListener = new TcpListener(IPAddress.Loopback, 0);
                _battleListener.Start();
                var transportClient = new TcpClient(AddressFamily.InterNetwork);
                transportClient.Connect(IPAddress.Loopback, ((IPEndPoint)_battleListener.LocalEndpoint).Port);
                Peer = _battleListener.AcceptTcpClient();
                var runtime = new PredictedTcpClientBattleRuntime(transportClient, initialState, initialState.Roster.GetPlayerId(new PlayerSlot(0)), new PlayerSlot(0), 8, 8, 8, 16, 1024, 32, 3, 8);
                Client = new TcpDemoClient(new DemoClientOptions(1, 1024, 2048, 32, 3, 8, 4, 64, 8, 8, 8, 16, 1024, 32, 3, 8));
                _controlListener = new TcpListener(IPAddress.Loopback, 0);
                _controlListener.Start();
                var controlClient = new TcpClient(AddressFamily.InterNetwork);
                controlClient.Connect(IPAddress.Loopback, ((IPEndPoint)_controlListener.LocalEndpoint).Port);
                _controlPeer = _controlListener.AcceptTcpClient();
                var bootstrap = new BattleBootstrapMessage
                {
                    BattleId = 1UL,
                    InitialTick = initialState.Tick,
                    BattleDurationTicks = 1U,
                    InputDelayTicks = 2U,
                    FinalStateTick = finalStateTick,
                };
                SetPrivateField(Client, "_sessionId", sessionId);
                SetPrivateField(Client, "_client", controlClient);
                SetPrivateField(Client, "_stream", controlClient.GetStream());
                SetPrivateField(Client, "_battleId", 1UL);
                SetPrivateField(Client, "_battleInitialState", initialState);
                SetPrivateField(Client, "_preparing", new BattlePreparingEventMessage
                {
                    BattleId = 1UL,
                    LocalPlayerId = initialState.Roster.GetPlayerId(new PlayerSlot(0)).Value,
                    LocalPlayerSlot = 0U,
                    Bootstrap = bootstrap,
                });
                SetPrivateField(Client, "_finalStateTick", finalStateTick);
                SetPrivateField(Client, "_finalInputTick", finalStateTick == 0U ? 0U : finalStateTick - 1U);
                SetPrivateField(Client, "_battleRuntime", runtime);
                SetPrivateField(Client, "_phase", DemoClientPhase.InBattle);
            }

            internal TcpDemoClient Client { get; }
            internal TcpClient Peer { get; }

            internal void Enqueue(ServerControlEventMessage message)
            {
                var queue = (Queue<byte[]>)GetPrivateField(Client, "_inbound")!;
                queue.Enqueue(message.ToByteArray());
            }

            public void Dispose()
            {
                Client.Dispose();
                Peer.Dispose();
                _controlPeer.Dispose();
                _battleListener.Stop();
                _controlListener.Stop();
            }
        }

        private sealed class GameplayBattleFixture : IDisposable
        {
            internal GameplayBattleFixture()
            {
                BattleDefinition definition = CreateFastGameplayDefinition();
                Server = new TcpDemoServer(SessionRoomTests.CreateOptions(
                    maxSessions: 2,
                    maxRooms: 1,
                    maxRoomCapacity: 2,
                    battleDefinition: definition,
                    battleReadyTimeout: TimeSpan.FromSeconds(5),
                    battleDurationTicks: 100));
                Host = Server.EnterSession("GameplayHost");
                Guest = Server.EnterSession("GameplayGuest");
                Room = Server.CreateRoom(Host.SessionId, "Gameplay", 2);
                Server.JoinRoom(Guest.SessionId, Room.RoomId);
                Server.SetReady(Host.SessionId, true);
                Server.SetReady(Guest.SessionId, true);
                Preparation = Server.StartBattle(Host.SessionId);
                HostBattle = AttachBattle(Server, Preparation.GetTicket(new PlayerSlot(0)));
                GuestBattle = AttachBattle(Server, Preparation.GetTicket(new PlayerSlot(1)));
                ulong hash = BattleConfigHash.Compute(definition);
                Server.MarkBattleReady(Host.SessionId, Preparation.BattleId, hash);
                Server.MarkBattleReady(Guest.SessionId, Preparation.BattleId, hash);
                TestAssert.Equal(DemoRoomLifecycle.InBattle, Room.Lifecycle);
            }

            internal TcpDemoServer Server { get; }
            internal DemoSession Host { get; }
            internal DemoSession Guest { get; }
            internal DemoRoom Room { get; }
            internal BattlePreparation Preparation { get; }
            internal TcpClient HostBattle { get; }
            internal TcpClient GuestBattle { get; }

            public void Dispose()
            {
                HostBattle.Dispose();
                GuestBattle.Dispose();
                Server.Dispose();
            }
        }
    }
}
