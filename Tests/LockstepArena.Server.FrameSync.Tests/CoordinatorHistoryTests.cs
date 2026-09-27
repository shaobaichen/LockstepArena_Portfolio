using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal static class CoordinatorHistoryTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("HistoryStartsEmptyAndContainsOnlyPublishedFrames", HistoryStartsEmptyAndContainsOnlyPublishedFrames),
            new TestCase("BlockedCompleteFutureFrameIsAbsentFromHistory", BlockedCompleteFutureFrameIsAbsentFromHistory),
            new TestCase("HistoryEvictsOldestFramesAtCapacity", HistoryEvictsOldestFramesAtCapacity),
            new TestCase("HistorySnapshotOwnsItsContainer", HistorySnapshotOwnsItsContainer),
        };

        private static void HistoryStartsEmptyAndContainsOnlyPublishedFrames()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster, 4);
            TestAssert.Equal(0, coordinator.GetAuthoritativeHistorySnapshot().Length);

            CoordinatorPublicationTests.CompleteTick(coordinator, roster, 101U);
            TestAssert.Equal(0, coordinator.GetAuthoritativeHistorySnapshot().Length);
            CoordinatorPublicationTests.CompleteTick(coordinator, roster, 100U);
            CoordinatorPublicationTests.AssertTicks(
                new[] { 100U, 101U },
                coordinator.GetAuthoritativeHistorySnapshot());
        }

        private static void BlockedCompleteFutureFrameIsAbsentFromHistory()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster, 4);

            CoordinatorPublicationTests.CompleteTick(coordinator, roster, 102U);
            TestAssert.Equal(0, coordinator.GetAuthoritativeHistorySnapshot().Length);
            TestAssert.Equal(100U, coordinator.NextPublishTick);
        }

        private static void HistoryEvictsOldestFramesAtCapacity()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster, 2);

            CoordinatorPublicationTests.CompleteTick(coordinator, roster, 100U);
            CoordinatorPublicationTests.CompleteTick(coordinator, roster, 101U);
            CoordinatorPublicationTests.CompleteTick(coordinator, roster, 102U);
            CoordinatorPublicationTests.AssertTicks(
                new[] { 101U, 102U },
                coordinator.GetAuthoritativeHistorySnapshot());
        }

        private static void HistorySnapshotOwnsItsContainer()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster, 2);
            CoordinatorPublicationTests.CompleteTick(coordinator, roster, 100U);
            CoordinatorPublicationTests.CompleteTick(coordinator, roster, 101U);
            FrameData[] firstSnapshot = coordinator.GetAuthoritativeHistorySnapshot();
            FrameData originalFirst = firstSnapshot[0];
            FrameData originalSecond = firstSnapshot[1];

            firstSnapshot[0] = originalSecond;
            FrameData[] secondSnapshot = coordinator.GetAuthoritativeHistorySnapshot();
            TestAssert.Same(originalFirst, secondSnapshot[0]);
            TestAssert.Same(originalSecond, secondSnapshot[1]);
        }

        private static AuthoritativeFrameCoordinator CreateCoordinator(ActiveRoster roster, int capacity)
        {
            return new AuthoritativeFrameCoordinator(roster, 100U, 3U, capacity);
        }
    }
}
