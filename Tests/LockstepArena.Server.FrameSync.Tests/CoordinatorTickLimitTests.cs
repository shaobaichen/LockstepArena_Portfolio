using System;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal static class CoordinatorTickLimitTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("UintMaxValueInputIsAlwaysRejected", UintMaxValueInputIsAlwaysRejected),
            new TestCase("LastConsumableTickPublishesThenCoordinatorIsExhausted", LastConsumableTickPublishesThenCoordinatorIsExhausted),
            new TestCase("LargeFutureOffsetNeverWrapsToLowTicks", LargeFutureOffsetNeverWrapsToLowTicks),
        };

        private static void UintMaxValueInputIsAlwaysRejected()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(roster, 100U, uint.MaxValue, 2);

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => coordinator.Submit(
                    roster.GetPlayerId(new PlayerSlot(0)),
                    CoordinatorTestData.CreateInput(uint.MaxValue, 0)));
            TestAssert.Equal(100U, coordinator.NextPublishTick);
            TestAssert.Equal(0, coordinator.GetAuthoritativeHistorySnapshot().Length);
        }

        private static void LastConsumableTickPublishesThenCoordinatorIsExhausted()
        {
            uint lastTick = uint.MaxValue - 1U;
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(roster, lastTick, 0U, 2);

            FrameData[] publication = CoordinatorPublicationTests.CompleteTick(coordinator, roster, lastTick);
            CoordinatorPublicationTests.AssertTicks(new[] { lastTick }, publication);
            TestAssert.Equal(uint.MaxValue, coordinator.NextPublishTick);
            TestAssert.Throws<InvalidOperationException>(
                () => coordinator.Submit(
                    roster.GetPlayerId(new PlayerSlot(0)),
                    CoordinatorTestData.CreateInput(lastTick, 0)));
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => coordinator.Submit(
                    roster.GetPlayerId(new PlayerSlot(0)),
                    CoordinatorTestData.CreateInput(uint.MaxValue, 0)));
        }

        private static void LargeFutureOffsetNeverWrapsToLowTicks()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = new AuthoritativeFrameCoordinator(
                roster,
                uint.MaxValue - 2U,
                uint.MaxValue,
                2);

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => coordinator.Submit(
                    roster.GetPlayerId(new PlayerSlot(0)),
                    CoordinatorTestData.CreateInput(0U, 0)));
            TestAssert.Equal(
                0,
                coordinator.Submit(
                    roster.GetPlayerId(new PlayerSlot(0)),
                    CoordinatorTestData.CreateInput(uint.MaxValue - 1U, 0)).Length);
        }
    }
}
