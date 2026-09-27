using System;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal static class CoordinatorDeterminismTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("DifferentArrivalOrdersPublishIdenticalAuthoritativeFrames", DifferentArrivalOrdersPublishIdenticalAuthoritativeFrames),
            new TestCase("PublishedBatchCanLeadSimulationThenCatchUp", PublishedBatchCanLeadSimulationThenCatchUp),
            new TestCase("DualSimulationsMatchEveryDigestAndApprovedGolden", DualSimulationsMatchEveryDigestAndApprovedGolden),
            new TestCase("SimulationFailureDoesNotRollbackCoordinatorPublication", SimulationFailureDoesNotRollbackCoordinatorPublication),
        };

        private static void DifferentArrivalOrdersPublishIdenticalAuthoritativeFrames()
        {
            CoordinatorRunResult runA = Gate4MultiTickGoldenVector.RunCoordinatorA();
            CoordinatorRunResult runB = Gate4MultiTickGoldenVector.RunCoordinatorB();
            AssertInts(new[] { 4, 4, 4 }, runA.PublicationBatchSizes);
            AssertInts(new[] { 1, 3, 1, 3, 1, 3 }, runB.PublicationBatchSizes);
            TestAssert.Equal(12, runA.PublishedFrames.Length);
            TestAssert.Equal(12, runB.PublishedFrames.Length);

            for (int tick = 0; tick < 12; tick++)
            {
                FrameData frameA = runA.PublishedFrames[tick];
                FrameData frameB = runB.PublishedFrames[tick];
                TestAssert.Equal((uint)tick, frameA.Tick);
                TestAssert.Equal((uint)tick, frameB.Tick);
                TestAssert.Equal(true, frameA.Roster.HasSameStructure(frameB.Roster));
                for (int slotValue = 0; slotValue < 4; slotValue++)
                {
                    PlayerSlot slot = new PlayerSlot(slotValue);
                    AssertInputEqual(frameA.GetInput(slot), frameB.GetInput(slot));
                }
            }
        }

        private static void PublishedBatchCanLeadSimulationThenCatchUp()
        {
            (
                AuthoritativeFrameCoordinator coordinator,
                BattleSimulation simulation,
                FrameData[] publication) = Gate4MultiTickGoldenVector.CreateCoordinatorAFirstBlock();

            TestAssert.Equal(4U, coordinator.NextPublishTick);
            TestAssert.Equal(0U, simulation.State.Tick);
            TestAssert.Equal(4, publication.Length);
            for (int index = 0; index < publication.Length; index++)
            {
                simulation.Step(publication[index]);
            }

            TestAssert.Equal(4U, simulation.State.Tick);
            TestAssert.Equal(coordinator.NextPublishTick, simulation.State.Tick);
        }

        private static void DualSimulationsMatchEveryDigestAndApprovedGolden()
        {
            CoordinatorRunResult runA = Gate4MultiTickGoldenVector.RunCoordinatorA();
            CoordinatorRunResult runB = Gate4MultiTickGoldenVector.RunCoordinatorB();
            TestAssert.Equal(12, runA.DigestsAfterEachFrame.Length);
            TestAssert.Equal(12, runB.DigestsAfterEachFrame.Length);
            for (int index = 0; index < 12; index++)
            {
                TestAssert.Equal(runA.DigestsAfterEachFrame[index], runB.DigestsAfterEachFrame[index]);
            }

            AssertTicks(new[] { 7U, 8U, 9U, 10U, 11U }, runA.History);
            AssertTicks(new[] { 7U, 8U, 9U, 10U, 11U }, runB.History);
            AssertFinalState(runA.FinalState);
            AssertFinalState(runB.FinalState);
            TestAssert.Equal(0x5CFABE84CC00E1C3UL, StateDigest.Compute(runA.FinalState));
            TestAssert.Equal(0x5CFABE84CC00E1C3UL, StateDigest.Compute(runB.FinalState));
        }

        private static void SimulationFailureDoesNotRollbackCoordinatorPublication()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(roster, 0U, 0U, 2);
            FrameData[] publication = CoordinatorPublicationTests.CompleteTick(coordinator, roster, 0U);
            FrameData authoritativeFrame = publication[0];
            BattleSimulation mismatchedSimulation = new BattleSimulation(new BattleState(
                1U,
                CoordinatorTestData.CreateRoster(2),
                new[] { new PlayerState(0, 0, 0), new PlayerState(0, 0, 0) }));

            TestAssert.Throws<ArgumentException>(() => mismatchedSimulation.Step(authoritativeFrame));
            TestAssert.Equal(1U, coordinator.NextPublishTick);
            FrameData[] history = coordinator.GetAuthoritativeHistorySnapshot();
            TestAssert.Equal(1, history.Length);
            TestAssert.Same(authoritativeFrame, history[0]);
            TestAssert.Same(authoritativeFrame, publication[0]);
        }

        private static void AssertFinalState(BattleState state)
        {
            TestAssert.Equal(12U, state.Tick);
            AssertPlayer(state, 0, 200, 0, 11_001);
            AssertPlayer(state, 1, -200, 0, 22_002);
            AssertPlayer(state, 2, 0, 200, 33_003);
            AssertPlayer(state, 3, 0, -200, 44_004);
        }

        private static void AssertPlayer(
            BattleState state,
            int slotValue,
            int expectedX,
            int expectedZ,
            ushort expectedAim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slotValue));
            TestAssert.Equal(expectedX, player.PositionX);
            TestAssert.Equal(expectedZ, player.PositionZ);
            TestAssert.Equal(expectedAim, player.Aim);
        }

        private static void AssertInputEqual(InputFrame expected, InputFrame actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.Equal(expected.PlayerSlot, actual.PlayerSlot);
            TestAssert.Equal(expected.MoveX, actual.MoveX);
            TestAssert.Equal(expected.MoveZ, actual.MoveZ);
            TestAssert.Equal(expected.Aim, actual.Aim);
        }

        private static void AssertTicks(uint[] expectedTicks, FrameData[] frames)
        {
            TestAssert.Equal(expectedTicks.Length, frames.Length);
            for (int index = 0; index < expectedTicks.Length; index++)
            {
                TestAssert.Equal(expectedTicks[index], frames[index].Tick);
            }
        }

        private static void AssertInts(int[] expected, int[] actual)
        {
            TestAssert.Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                TestAssert.Equal(expected[index], actual[index]);
            }
        }
    }
}
