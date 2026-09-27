using System;

namespace LockstepArena.Simulation.Tests
{
    internal static class ContractTests
    {
        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(TwoPlayerFrameCanonicalizesArrivalOrder), TwoPlayerFrameCanonicalizesArrivalOrder),
            new TestCase(nameof(ThreePlayerFrameCanonicalizesArrivalOrder), ThreePlayerFrameCanonicalizesArrivalOrder),
            new TestCase(nameof(FourPlayerFrameCanonicalizesArrivalOrder), FourPlayerFrameCanonicalizesArrivalOrder),
            new TestCase(nameof(FrameDataRejectsMissingSlot), FrameDataRejectsMissingSlot),
            new TestCase(nameof(FrameDataRejectsDuplicateSlot), FrameDataRejectsDuplicateSlot),
            new TestCase(nameof(FrameDataRejectsUnknownSlot), FrameDataRejectsUnknownSlot),
            new TestCase(nameof(FrameDataRejectsWrongInputTick), FrameDataRejectsWrongInputTick),
            new TestCase(nameof(FrameDataCopiesReceivedInputs), FrameDataCopiesReceivedInputs),
            new TestCase(nameof(InputFrameRejectsInvalidMovementAndNegativeSlot), InputFrameRejectsInvalidMovementAndNegativeSlot),
            new TestCase(nameof(InitialStateCopiesStatesAndChecksSlotRange), InitialStateCopiesStatesAndChecksSlotRange),
        };

        private static void TwoPlayerFrameCanonicalizesArrivalOrder()
        {
            ActiveRoster roster = Roster(9_003, 42);
            FrameData frame = FrameData.Create(roster, 7, new[]
            {
                Input(7, 1, -1, 0, 40_000),
                Input(7, 0, 1, 0, 2_000),
            });

            TestAssert.Equal(2, frame.InputCount);
            TestAssert.Equal(new PlayerSlot(0), frame.GetInput(new PlayerSlot(0)).PlayerSlot);
            TestAssert.Equal(new PlayerSlot(1), frame.GetInput(new PlayerSlot(1)).PlayerSlot);
            TestAssert.Equal((ushort)2_000, frame.GetInput(new PlayerSlot(0)).Aim);
            TestAssert.Equal((ushort)40_000, frame.GetInput(new PlayerSlot(1)).Aim);
        }

        private static void ThreePlayerFrameCanonicalizesArrivalOrder()
        {
            ActiveRoster roster = Roster(9_003, 42, 7_000_001);
            FrameData frame = FrameData.Create(roster, 11, new[]
            {
                Input(11, 2, 0, -1, 300),
                Input(11, 0, 1, 0, 100),
                Input(11, 1, -1, 1, 200),
            });

            TestAssert.Equal((ushort)100, frame.GetInput(new PlayerSlot(0)).Aim);
            TestAssert.Equal((ushort)200, frame.GetInput(new PlayerSlot(1)).Aim);
            TestAssert.Equal((ushort)300, frame.GetInput(new PlayerSlot(2)).Aim);
        }

        private static void FourPlayerFrameCanonicalizesArrivalOrder()
        {
            ActiveRoster roster = Roster(9_003, 42, 7_000_001, 88);
            FrameData frame = FrameData.Create(roster, 19, new[]
            {
                Input(19, 3, 0, 1, 4),
                Input(19, 1, 0, -1, 2),
                Input(19, 0, 1, 0, 1),
                Input(19, 2, -1, 0, 3),
            });

            for (int index = 0; index < 4; index++)
            {
                InputFrame input = frame.GetInput(new PlayerSlot(index));
                TestAssert.Equal(new PlayerSlot(index), input.PlayerSlot);
                TestAssert.Equal((ushort)(index + 1), input.Aim);
            }
        }

        private static void FrameDataRejectsMissingSlot()
        {
            ActiveRoster roster = Roster(9_003, 42, 7_000_001);

            TestAssert.Throws<ArgumentException>(() => FrameData.Create(roster, 3, new[]
            {
                Input(3, 0),
                Input(3, 2),
            }));
        }

        private static void FrameDataRejectsDuplicateSlot()
        {
            ActiveRoster roster = Roster(9_003, 42, 7_000_001);

            TestAssert.Throws<ArgumentException>(() => FrameData.Create(roster, 3, new[]
            {
                Input(3, 0),
                Input(3, 0),
                Input(3, 2),
            }));
        }

        private static void FrameDataRejectsUnknownSlot()
        {
            ActiveRoster roster = Roster(9_003, 42, 7_000_001);

            TestAssert.Throws<ArgumentOutOfRangeException>(() => FrameData.Create(roster, 3, new[]
            {
                Input(3, 0),
                Input(3, 1),
                Input(3, 3),
            }));
        }

        private static void FrameDataRejectsWrongInputTick()
        {
            ActiveRoster roster = Roster(9_003, 42);

            TestAssert.Throws<ArgumentException>(() => FrameData.Create(roster, 7, new[]
            {
                Input(7, 0),
                Input(8, 1),
            }));
        }

        private static void FrameDataCopiesReceivedInputs()
        {
            ActiveRoster roster = Roster(9_003, 42);
            InputFrame[] received =
            {
                Input(5, 0, 1, 0, 123),
                Input(5, 1, -1, 0, 456),
            };
            FrameData frame = FrameData.Create(roster, 5, received);

            received[0] = Input(5, 0, -1, 0, 999);

            TestAssert.Equal((sbyte)1, frame.GetInput(new PlayerSlot(0)).MoveX);
            TestAssert.Equal((ushort)123, frame.GetInput(new PlayerSlot(0)).Aim);
            TestAssert.Throws<ArgumentOutOfRangeException>(() => frame.GetInput(new PlayerSlot(2)));
        }

        private static void InputFrameRejectsInvalidMovementAndNegativeSlot()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new PlayerSlot(-1));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, new PlayerSlot(0), -2, 0, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, new PlayerSlot(0), 2, 0, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, new PlayerSlot(0), 0, -2, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, new PlayerSlot(0), 0, 2, 0));
        }

        private static void InitialStateCopiesStatesAndChecksSlotRange()
        {
            ActiveRoster roster = Roster(9_003, 42, 7_000_001);
            PlayerState[] players =
            {
                new PlayerState(-1_000, 0, 10),
                new PlayerState(1_000, 0, 20),
                new PlayerState(0, 1_000, 30),
            };
            BattleState state = BattleState.CreateInitial(roster, players);

            players[0] = new PlayerState(4_000, 2_000, 999);

            TestAssert.Equal(0U, state.Tick);
            TestAssert.Equal(3, state.PlayerCount);
            TestAssert.Equal(-1_000, state.GetPlayerState(new PlayerSlot(0)).PositionX);
            TestAssert.Equal((ushort)10, state.GetPlayerState(new PlayerSlot(0)).Aim);
            TestAssert.Throws<ArgumentOutOfRangeException>(() => state.GetPlayerState(new PlayerSlot(3)));
            TestAssert.Throws<ArgumentException>(() => BattleState.CreateInitial(
                roster,
                new[] { new PlayerState(0, 0, 0) }));
        }

        private static ActiveRoster Roster(params ulong[] playerIds)
        {
            PlayerId[] ids = new PlayerId[playerIds.Length];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = new PlayerId(playerIds[index]);
            }

            return new ActiveRoster(ids);
        }

        private static InputFrame Input(
            uint tick,
            int slot,
            sbyte moveX = 0,
            sbyte moveZ = 0,
            ushort aim = 0)
        {
            return new InputFrame(tick, new PlayerSlot(slot), moveX, moveZ, aim);
        }
    }
}
