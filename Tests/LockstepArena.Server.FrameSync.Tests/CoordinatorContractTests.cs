using System;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal static class CoordinatorContractTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("ConstructorRejectsNullRoster", ConstructorRejectsNullRoster),
            new TestCase("ConstructorRejectsNonPositiveHistoryCapacity", ConstructorRejectsNonPositiveHistoryCapacity),
            new TestCase("ConstructorExposesRosterAndInitialTick", ConstructorExposesRosterAndInitialTick),
            new TestCase("InitialMaxTickHasEmptyHistory", InitialMaxTickHasEmptyHistory),
        };

        private static void ConstructorRejectsNullRoster()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => new AuthoritativeFrameCoordinator(null!, 0U, 3U, 2));
        }

        private static void ConstructorRejectsNonPositiveHistoryCapacity()
        {
            ActiveRoster roster = CreateRoster();
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new AuthoritativeFrameCoordinator(roster, 0U, 3U, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new AuthoritativeFrameCoordinator(roster, 0U, 3U, -1));
        }

        private static void ConstructorExposesRosterAndInitialTick()
        {
            ActiveRoster roster = CreateRoster();
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(roster, 73U, 3U, 2);

            TestAssert.Same(roster, coordinator.Roster);
            TestAssert.Equal(73U, coordinator.NextPublishTick);
        }

        private static void InitialMaxTickHasEmptyHistory()
        {
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(CreateRoster(), uint.MaxValue, 3U, 2);

            FrameData[] first = coordinator.GetAuthoritativeHistorySnapshot();
            TestAssert.Equal(0, first.Length);
            first = new FrameData[1];
            TestAssert.Equal(0, coordinator.GetAuthoritativeHistorySnapshot().Length);
        }

        private static ActiveRoster CreateRoster()
        {
            return new ActiveRoster(new[] { new PlayerId(91UL), new PlayerId(17UL) });
        }
    }
}
