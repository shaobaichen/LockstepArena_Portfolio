using LockstepArena.Server.FrameSync;
using LockstepArena.Simulation;

namespace LockstepArena.Server.TickPacing.Tests
{
    internal static class StopwatchTickDriverTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("StopwatchDriverConstructionStartsBaselineWithoutAdvancing", StopwatchDriverConstructionStartsBaselineWithoutAdvancing),
            new TestCase("StopwatchDriverTerminalPollReturnsEmptyRepeatedly", StopwatchDriverTerminalPollReturnsEmptyRepeatedly),
        };

        private static void StopwatchDriverConstructionStartsBaselineWithoutAdvancing()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(100U, 2U);
            ulong collectionTick = publisher.CollectionTick;
            uint? eligibilityCeiling = publisher.EligibilityCeiling;
            uint nextPublishTick = publisher.NextPublishTick;

            _ = new StopwatchTickDriver(publisher);

            TestAssert.Equal(collectionTick, publisher.CollectionTick);
            TestAssert.Equal(eligibilityCeiling, publisher.EligibilityCeiling);
            TestAssert.Equal(nextPublishTick, publisher.NextPublishTick);
            TestAssert.Equal(0, publisher.GetAuthoritativeHistorySnapshot().Length);
        }

        private static void StopwatchDriverTerminalPollReturnsEmptyRepeatedly()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(uint.MaxValue - 1U, 0U);
            StopwatchTickDriver driver = new StopwatchTickDriver(publisher);

            TestAssert.Equal(0, driver.Poll().Length);
            TestAssert.Equal(0, driver.Poll().Length);
            TestAssert.Equal((ulong)uint.MaxValue - 1UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(uint.MaxValue - 1U, publisher.EligibilityCeiling);
            TestAssert.Equal(uint.MaxValue - 1U, publisher.NextPublishTick);
        }

        private static TickDrivenFramePublisher CreatePublisher(
            uint initialTick,
            uint inputDelayTicks)
        {
            ActiveRoster roster = new ActiveRoster(new[] { new PlayerId(1000UL) });
            return new TickDrivenFramePublisher(
                roster,
                initialTick,
                inputDelayTicks,
                1U,
                2);
        }
    }
}
