using System;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class ControlProtocolTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(ControlCommandOneOfRoundTripsEveryApprovedVariant), ControlCommandOneOfRoundTripsEveryApprovedVariant),
            new TestCase(nameof(ControlEventOneOfRoundTripsEveryApprovedVariant), ControlEventOneOfRoundTripsEveryApprovedVariant),
            new TestCase(nameof(ExistingBattleMessagesRemainWireCompatible), ExistingBattleMessagesRemainWireCompatible),
            new TestCase(nameof(BattleBootstrapRoundTripPreservesCanonicalState), BattleBootstrapRoundTripPreservesCanonicalState),
            new TestCase(nameof(GameplayBootstrapCarriesConfigHashAndRejectsMismatch), GameplayBootstrapCarriesConfigHashAndRejectsMismatch),
            new TestCase(nameof(BattleBootstrapRejectsMissingRosterOrState), BattleBootstrapRejectsMissingRosterOrState),
            new TestCase(nameof(BattleBootstrapRejectsDuplicateMissingUnknownOrNoncontiguousSlot), BattleBootstrapRejectsDuplicateMissingUnknownOrNoncontiguousSlot),
            new TestCase(nameof(BattleBootstrapRejectsOutOfRangePositionOrAim), BattleBootstrapRejectsOutOfRangePositionOrAim),
            new TestCase(nameof(WireIdentifierCapacityAndPortNarrowingRejectBeforeMutation), WireIdentifierCapacityAndPortNarrowingRejectBeforeMutation),
        };

        private static void ControlCommandOneOfRoundTripsEveryApprovedVariant()
        {
            ClientControlCommandMessage[] values =
            {
                new ClientControlCommandMessage { EnterSession = new EnterSessionCommandMessage { Nickname = "Alpha" } },
                new ClientControlCommandMessage { RequestRoomList = new RequestRoomListCommandMessage() },
                new ClientControlCommandMessage { CreateRoom = new CreateRoomCommandMessage { RoomName = "Room", Capacity = 4 } },
                new ClientControlCommandMessage { JoinRoom = new JoinRoomCommandMessage { RoomId = 7 } },
                new ClientControlCommandMessage { LeaveRoom = new LeaveRoomCommandMessage() },
                new ClientControlCommandMessage { SetReady = new SetReadyCommandMessage { IsReady = true } },
                new ClientControlCommandMessage { StartBattle = new StartBattleCommandMessage() },
                new ClientControlCommandMessage { ReturnToLobby = new ReturnToLobbyCommandMessage() },
                new ClientControlCommandMessage { ExitSession = new ExitSessionCommandMessage() },
                new ClientControlCommandMessage { BattleReady = new BattleReadyCommandMessage { BattleId = 4, BattleConfigHash = 5 } },
            };

            for (int index = 0; index < values.Length; index++)
            {
                ClientControlCommandMessage parsed = ClientControlCommandMessage.Parser.ParseFrom(values[index].ToByteArray());
                TestAssert.True(parsed.CommandCase != ClientControlCommandMessage.CommandOneofCase.None);
                TestAssert.Equal(values[index].CommandCase, parsed.CommandCase);
            }
        }

        private static void ControlEventOneOfRoundTripsEveryApprovedVariant()
        {
            ServerControlEventMessage[] values =
            {
                new ServerControlEventMessage { SessionEntered = new SessionEnteredEventMessage { SessionId = 1, Nickname = "Alpha" } },
                new ServerControlEventMessage { RoomList = new RoomListEventMessage() },
                new ServerControlEventMessage { RoomSnapshot = new RoomSnapshotEventMessage { RoomId = 1 } },
                new ServerControlEventMessage { BattlePreparing = CreatePreparing() },
                new ServerControlEventMessage { BattleStarted = new BattleStartedEventMessage { BattleId = 1 } },
                new ServerControlEventMessage { BattleStatus = new BattleStatusEventMessage { BattleId = 1, ServerStateTick = 1, NextPublishTick = 1 } },
                new ServerControlEventMessage { BattleSettlement = new BattleSettlementEventMessage { BattleId = 1, Reason = BattleSettlementReasonMessage.BattleSettlementReasonTickLimitReached } },
                new ServerControlEventMessage { BattleSettlement = new BattleSettlementEventMessage { BattleId = 2, Reason = BattleSettlementReasonMessage.BattleSettlementReasonMatchCompleted, WinnerPlayerId = 11, Slot0RoundWins = 2, Slot1RoundWins = 1 } },
                new ServerControlEventMessage { BattleSettlement = new BattleSettlementEventMessage { BattleId = 3, Reason = BattleSettlementReasonMessage.BattleSettlementReasonDisconnectForfeit, WinnerPlayerId = 22 } },
                new ServerControlEventMessage { CommandRejected = new CommandRejectedEventMessage { Reason = ControlRejectReasonMessage.ControlRejectReasonNotHost } },
                new ServerControlEventMessage { LobbyEntered = new LobbyEnteredEventMessage() },
            };

            for (int index = 0; index < values.Length; index++)
            {
                ServerControlEventMessage parsed = ServerControlEventMessage.Parser.ParseFrom(values[index].ToByteArray());
                TestAssert.True(parsed.EventCase != ServerControlEventMessage.EventOneofCase.None);
                TestAssert.Equal(values[index].EventCase, parsed.EventCase);
            }
        }

        private static void ExistingBattleMessagesRemainWireCompatible()
        {
            var message = new PlayerInputSubmissionMessage
            {
                SubmittedPlayerId = 17,
                Input = new InputFrameMessage { Tick = 9, PlayerSlot = 2, MoveX = -1, MoveZ = 1, Aim = 123 },
            };
            PlayerInputSubmissionMessage parsed = PlayerInputSubmissionMessage.Parser.ParseFrom(message.ToByteArray());
            TestAssert.Equal<ulong>(17, parsed.SubmittedPlayerId);
            TestAssert.Equal<uint>(9, parsed.Input.Tick);
            TestAssert.Equal(-1, parsed.Input.MoveX);
        }

        private static void BattleBootstrapRoundTripPreservesCanonicalState()
        {
            BattleState state = CreateState();
            BattleBootstrapMessage wire = ProtocolMapper.ToWireBattleBootstrap(4, state, 4, 2, 4);
            BattleState mapped = ProtocolMapper.ToDomainBattleBootstrap(
                BattleBootstrapMessage.Parser.ParseFrom(wire.ToByteArray()),
                new PlayerId(20),
                new PlayerSlot(0));

            AssertStateEqual(state, mapped);
        }

        private static void GameplayBootstrapCarriesConfigHashAndRejectsMismatch()
        {
            var roster = new ActiveRoster(new[] { new PlayerId(20), new PlayerId(10) });
            BattleDefinition definition = BattleDefinition.CreateDefault();
            BattleState state = BattleState.CreateGameplayInitial(roster, definition);
            BattleBootstrapMessage wire = ProtocolMapper.ToWireBattleBootstrap(4, state, 20_000, 2, 20_000);

            TestAssert.Equal(BattleConfigHash.Compute(definition), wire.BattleConfigHash);
            BattleState mapped = ProtocolMapper.ToDomainGameplayBattleBootstrap(
                BattleBootstrapMessage.Parser.ParseFrom(wire.ToByteArray()),
                new PlayerId(20),
                new PlayerSlot(0),
                definition);
            TestAssert.True(BattleStateValueComparer.HaveSameValue(state, mapped));

            BattleDefinition mismatched = new BattleDefinition(
                new GameplayConfig(101, 25, 100, 6, 300, 45, 100, 20, 70, 5, 1_800, 90, 30, 2),
                definition.Arena);
            TestAssert.Throws<ProtocolMappingException>(() =>
                ProtocolMapper.ToDomainGameplayBattleBootstrap(wire, new PlayerId(20), new PlayerSlot(0), mismatched));
        }

        private static void BattleBootstrapRejectsMissingRosterOrState()
        {
            BattleBootstrapMessage missingRoster = CreatePreparing().Bootstrap.Clone();
            missingRoster.Roster = null;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(missingRoster, new PlayerId(20), new PlayerSlot(0)));

            BattleBootstrapMessage missingState = CreatePreparing().Bootstrap.Clone();
            missingState.PlayerStates.Clear();
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(missingState, new PlayerId(20), new PlayerSlot(0)));
        }

        private static void BattleBootstrapRejectsDuplicateMissingUnknownOrNoncontiguousSlot()
        {
            BattleBootstrapMessage duplicate = CreatePreparing().Bootstrap.Clone();
            duplicate.PlayerStates[1].PlayerSlot = 0;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(duplicate, new PlayerId(20), new PlayerSlot(0)));

            BattleBootstrapMessage unknown = CreatePreparing().Bootstrap.Clone();
            unknown.PlayerStates[1].PlayerSlot = 2;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(unknown, new PlayerId(20), new PlayerSlot(0)));

            BattleBootstrapMessage noncontiguousRoster = CreatePreparing().Bootstrap.Clone();
            noncontiguousRoster.Roster.Players[1].PlayerSlot = 2;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(noncontiguousRoster, new PlayerId(20), new PlayerSlot(0)));
        }

        private static void BattleBootstrapRejectsOutOfRangePositionOrAim()
        {
            BattleBootstrapMessage position = CreatePreparing().Bootstrap.Clone();
            position.PlayerStates[0].PositionX = SimulationConfig.ArenaMaxX + 1;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(position, new PlayerId(20), new PlayerSlot(0)));

            BattleBootstrapMessage aim = CreatePreparing().Bootstrap.Clone();
            aim.PlayerStates[0].Aim = ushort.MaxValue + 1U;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(aim, new PlayerId(20), new PlayerSlot(0)));
        }

        private static void WireIdentifierCapacityAndPortNarrowingRejectBeforeMutation()
        {
            BattleBootstrapMessage zeroBattle = CreatePreparing().Bootstrap.Clone();
            zeroBattle.BattleId = 0;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(zeroBattle, new PlayerId(20), new PlayerSlot(0)));

            BattleBootstrapMessage slotOverflow = CreatePreparing().Bootstrap.Clone();
            slotOverflow.PlayerStates[0].PlayerSlot = uint.MaxValue;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomainBattleBootstrap(slotOverflow, new PlayerId(20), new PlayerSlot(0)));
        }

        private static BattlePreparingEventMessage CreatePreparing()
        {
            BattleState state = CreateState();
            return new BattlePreparingEventMessage
            {
                RoomId = 1,
                BattleId = 4,
                BattlePort = 46001,
                LocalPlayerId = 20,
                LocalPlayerSlot = 0,
                Bootstrap = ProtocolMapper.ToWireBattleBootstrap(4, state, 4, 2, 4),
            };
        }

        private static BattleState CreateState()
        {
            var roster = new ActiveRoster(new[] { new PlayerId(20), new PlayerId(10) });
            return new BattleState(0, roster, new[] { new PlayerState(-300, 0, 1000), new PlayerState(300, 0, 2000) });
        }

        private static void AssertStateEqual(BattleState expected, BattleState actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.True(expected.Roster.HasSameStructure(actual.Roster));
            for (int index = 0; index < expected.PlayerCount; index++)
            {
                PlayerState left = expected.GetPlayerState(new PlayerSlot(index));
                PlayerState right = actual.GetPlayerState(new PlayerSlot(index));
                TestAssert.Equal(left.PositionX, right.PositionX);
                TestAssert.Equal(left.PositionZ, right.PositionZ);
                TestAssert.Equal(left.Aim, right.Aim);
            }
        }
    }
}
