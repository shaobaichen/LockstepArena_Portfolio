using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal static class CoordinatorPublicationTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("CompleteFutureFrameWaitsForGap", CompleteFutureFrameWaitsForGap),
            new TestCase("GapFillPublishesCurrentAndNextTick", GapFillPublishesCurrentAndNextTick),
            new TestCase("GapFillPublishesSeveralCompletedFutureTicks", GapFillPublishesSeveralCompletedFutureTicks),
            new TestCase("IncompleteMiddleTickStopsContinuousBatch", IncompleteMiddleTickStopsContinuousBatch),
            new TestCase("PublicationBatchStartsAtPriorNextTickAndOwnsItsContainer", PublicationBatchStartsAtPriorNextTickAndOwnsItsContainer),
        };

        private static void CompleteFutureFrameWaitsForGap()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);

            TestAssert.Equal(0, CompleteTick(coordinator, roster, 101U).Length);
            TestAssert.Equal(100U, coordinator.NextPublishTick);
            TestAssert.Equal(0, coordinator.GetAuthoritativeHistorySnapshot().Length);
        }

        private static void GapFillPublishesCurrentAndNextTick()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            CompleteTick(coordinator, roster, 101U);

            AssertTicks(new[] { 100U, 101U }, CompleteTick(coordinator, roster, 100U));
            TestAssert.Equal(102U, coordinator.NextPublishTick);
        }

        private static void GapFillPublishesSeveralCompletedFutureTicks()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            CompleteTick(coordinator, roster, 103U);
            CompleteTick(coordinator, roster, 102U);
            CompleteTick(coordinator, roster, 101U);

            AssertTicks(new[] { 100U, 101U, 102U, 103U }, CompleteTick(coordinator, roster, 100U));
            TestAssert.Equal(104U, coordinator.NextPublishTick);
        }

        private static void IncompleteMiddleTickStopsContinuousBatch()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            CompleteTick(coordinator, roster, 101U);
            coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(102U, 0));
            CompleteTick(coordinator, roster, 103U);

            AssertTicks(new[] { 100U, 101U }, CompleteTick(coordinator, roster, 100U));
            TestAssert.Equal(102U, coordinator.NextPublishTick);
            AssertTicks(new[] { 100U, 101U }, coordinator.GetAuthoritativeHistorySnapshot());
        }

        private static void PublicationBatchStartsAtPriorNextTickAndOwnsItsContainer()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            CompleteTick(coordinator, roster, 101U);
            FrameData[] publication = CompleteTick(coordinator, roster, 100U);
            FrameData firstPublished = publication[0];
            FrameData secondPublished = publication[1];

            publication[0] = secondPublished;
            FrameData[] history = coordinator.GetAuthoritativeHistorySnapshot();
            TestAssert.Equal(100U, history[0].Tick);
            TestAssert.Same(firstPublished, history[0]);
            TestAssert.Same(secondPublished, history[1]);
        }

        internal static FrameData[] CompleteTick(
            AuthoritativeFrameCoordinator coordinator,
            ActiveRoster roster,
            uint tick)
        {
            FrameData[] publication = new FrameData[0];
            for (int slot = roster.Count - 1; slot >= 0; slot--)
            {
                publication = coordinator.Submit(
                    roster.GetPlayerId(new PlayerSlot(slot)),
                    CoordinatorTestData.CreateInput(tick, slot));
            }

            return publication;
        }

        internal static void AssertTicks(uint[] expectedTicks, FrameData[] frames)
        {
            TestAssert.Equal(expectedTicks.Length, frames.Length);
            for (int index = 0; index < expectedTicks.Length; index++)
            {
                TestAssert.Equal(expectedTicks[index], frames[index].Tick);
            }
        }

        private static AuthoritativeFrameCoordinator CreateCoordinator(ActiveRoster roster)
        {
            return new AuthoritativeFrameCoordinator(roster, 100U, 3U, 8);
        }
    }
}
