using System;

namespace LockstepArena.Simulation.Tests
{
    internal static class BattleSimulationTests
    {
        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(NeutralInputsAdvanceOneTickWithoutMovement), NeutralInputsAdvanceOneTickWithoutMovement),
            new TestCase(nameof(FourPlayerMovementUpdatesInSlotOrder), FourPlayerMovementUpdatesInSlotOrder),
            new TestCase(nameof(InputAimReplacesEveryPlayerAim), InputAimReplacesEveryPlayerAim),
            new TestCase(nameof(MovementClampsAtEveryArenaBoundary), MovementClampsAtEveryArenaBoundary),
            new TestCase(nameof(UnexpectedFrameTickIsRejectedWithoutMutation), UnexpectedFrameTickIsRejectedWithoutMutation),
            new TestCase(nameof(StructurallyEqualRosterInstanceIsAccepted), StructurallyEqualRosterInstanceIsAccepted),
            new TestCase(nameof(DifferentRosterIsRejectedWithoutMutation), DifferentRosterIsRejectedWithoutMutation),
            new TestCase(nameof(StepUsesCopiedInitialPlayerStates), StepUsesCopiedInitialPlayerStates),
        };

        private static void NeutralInputsAdvanceOneTickWithoutMovement()
        {
            ActiveRoster roster = Roster(10, 20);
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial(roster, new[]
            {
                new PlayerState(-1_000, 0, 0),
                new PlayerState(1_000, 0, 0),
            }));

            simulation.Step(Frame(roster, 0, Input(0, 1), Input(0, 0)));

            TestAssert.Equal(1U, simulation.State.Tick);
            TestAssert.Equal(-1_000, simulation.State.GetPlayerState(new PlayerSlot(0)).PositionX);
            TestAssert.Equal(1_000, simulation.State.GetPlayerState(new PlayerSlot(1)).PositionX);
        }

        private static void FourPlayerMovementUpdatesInSlotOrder()
        {
            ActiveRoster roster = Roster(90, 10, 70, 20);
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial(roster, new[]
            {
                new PlayerState(-1_000, 0, 0),
                new PlayerState(1_000, 0, 0),
                new PlayerState(0, -1_000, 0),
                new PlayerState(0, 1_000, 0),
            }));
            FrameData frame = Frame(roster, 0,
                Input(0, 3, 0, -1),
                Input(0, 1, -1, 0),
                Input(0, 0, 1, 0),
                Input(0, 2, 0, 1));

            simulation.Step(frame);

            AssertPlayer(simulation.State, 0, -900, 0, 0);
            AssertPlayer(simulation.State, 1, 900, 0, 0);
            AssertPlayer(simulation.State, 2, 0, -900, 0);
            AssertPlayer(simulation.State, 3, 0, 900, 0);
        }

        private static void InputAimReplacesEveryPlayerAim()
        {
            ActiveRoster roster = Roster(90, 10, 70, 20);
            BattleSimulation simulation = new BattleSimulation(new BattleState(12, roster, new[]
            {
                new PlayerState(-200, 300, 111),
                new PlayerState(400, -500, 222),
                new PlayerState(600, 700, 333),
                new PlayerState(-800, -900, 444),
            }));

            simulation.Step(Frame(roster, 12,
                Input(12, 2, 0, 0, 40_000),
                Input(12, 0, 0, 0, 10_000),
                Input(12, 3, 0, 0, 65_535),
                Input(12, 1, 0, 0, 20_000)));

            TestAssert.Equal((ushort)10_000, simulation.State.GetPlayerState(new PlayerSlot(0)).Aim);
            TestAssert.Equal((ushort)20_000, simulation.State.GetPlayerState(new PlayerSlot(1)).Aim);
            TestAssert.Equal((ushort)40_000, simulation.State.GetPlayerState(new PlayerSlot(2)).Aim);
            TestAssert.Equal((ushort)65_535, simulation.State.GetPlayerState(new PlayerSlot(3)).Aim);
        }

        private static void MovementClampsAtEveryArenaBoundary()
        {
            ActiveRoster roster = Roster(90, 10, 70, 20);
            BattleSimulation simulation = new BattleSimulation(new BattleState(4, roster, new[]
            {
                new PlayerState(SimulationConfig.ArenaMaxX, 0, 0),
                new PlayerState(SimulationConfig.ArenaMinX, 0, 0),
                new PlayerState(0, SimulationConfig.ArenaMaxZ, 0),
                new PlayerState(0, SimulationConfig.ArenaMinZ, 0),
            }));

            simulation.Step(Frame(roster, 4,
                Input(4, 0, 1, 0),
                Input(4, 1, -1, 0),
                Input(4, 2, 0, 1),
                Input(4, 3, 0, -1)));

            AssertPlayer(simulation.State, 0, 5_000, 0, 0);
            AssertPlayer(simulation.State, 1, -5_000, 0, 0);
            AssertPlayer(simulation.State, 2, 0, 3_000, 0);
            AssertPlayer(simulation.State, 3, 0, -3_000, 0);
        }

        private static void UnexpectedFrameTickIsRejectedWithoutMutation()
        {
            ActiveRoster roster = Roster(10, 20);
            BattleState initial = BattleState.CreateInitial(roster, new[]
            {
                new PlayerState(-1_000, 0, 0),
                new PlayerState(1_000, 0, 0),
            });
            BattleSimulation simulation = new BattleSimulation(initial);

            TestAssert.Throws<ArgumentException>(() => simulation.Step(Frame(
                roster,
                1,
                Input(1, 0, 1, 0),
                Input(1, 1, -1, 0))));

            TestAssert.Equal(true, ReferenceEquals(initial, simulation.State));
            TestAssert.Equal(0U, simulation.State.Tick);
        }

        private static void StructurallyEqualRosterInstanceIsAccepted()
        {
            ActiveRoster stateRoster = Roster(90, 10, 70);
            ActiveRoster frameRoster = Roster(90, 10, 70);
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial(stateRoster, new[]
            {
                new PlayerState(0, 0, 0),
                new PlayerState(0, 0, 0),
                new PlayerState(0, 0, 0),
            }));

            simulation.Step(Frame(frameRoster, 0, Input(0, 2), Input(0, 0), Input(0, 1)));

            TestAssert.Equal(1U, simulation.State.Tick);
        }

        private static void DifferentRosterIsRejectedWithoutMutation()
        {
            ActiveRoster stateRoster = Roster(90, 10, 70);
            ActiveRoster frameRoster = Roster(10, 90, 70);
            BattleState initial = BattleState.CreateInitial(stateRoster, new[]
            {
                new PlayerState(0, 0, 0),
                new PlayerState(0, 0, 0),
                new PlayerState(0, 0, 0),
            });
            BattleSimulation simulation = new BattleSimulation(initial);

            TestAssert.Throws<ArgumentException>(() => simulation.Step(Frame(
                frameRoster,
                0,
                Input(0, 0),
                Input(0, 1),
                Input(0, 2))));

            TestAssert.Equal(true, ReferenceEquals(initial, simulation.State));
        }

        private static void StepUsesCopiedInitialPlayerStates()
        {
            ActiveRoster roster = Roster(10, 20);
            PlayerState[] source =
            {
                new PlayerState(-1_000, 200, 11),
                new PlayerState(1_000, -200, 22),
            };
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial(roster, source));
            source[0] = new PlayerState(4_000, 2_000, 999);

            simulation.Step(Frame(roster, 0, Input(0, 0), Input(0, 1)));

            AssertPlayer(simulation.State, 0, -1_000, 200, 0);
            AssertPlayer(simulation.State, 1, 1_000, -200, 0);
        }

        private static void AssertPlayer(BattleState state, int slot, int x, int z, ushort aim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slot));
            TestAssert.Equal(x, player.PositionX);
            TestAssert.Equal(z, player.PositionZ);
            TestAssert.Equal(aim, player.Aim);
        }

        private static ActiveRoster Roster(params ulong[] values)
        {
            PlayerId[] ids = new PlayerId[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                ids[index] = new PlayerId(values[index]);
            }

            return new ActiveRoster(ids);
        }

        private static FrameData Frame(ActiveRoster roster, uint tick, params InputFrame[] inputs)
        {
            return FrameData.Create(roster, tick, inputs);
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
