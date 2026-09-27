using System;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal static class CoordinatorWindowTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("ZeroFutureOffsetAcceptsOnlyNextPublishTick", ZeroFutureOffsetAcceptsOnlyNextPublishTick),
            new TestCase("FutureWindowIncludesUpperBoundAndRejectsBeyondIt", FutureWindowIncludesUpperBoundAndRejectsBeyondIt),
            new TestCase("FutureWindowMovesAndPublishedTicksBecomeOld", FutureWindowMovesAndPublishedTicksBecomeOld),
        };

        private static void ZeroFutureOffsetAcceptsOnlyNextPublishTick()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(roster, 100U, 0U, 4);

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(101U, 0)));
            TestAssert.Equal(
                0,
                coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(100U, 0)).Length);
        }

        private static void FutureWindowIncludesUpperBoundAndRejectsBeyondIt()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(roster, 100U, 2U, 4);

            TestAssert.Equal(
                0,
                coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(102U, 0)).Length);
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(103U, 0)));
        }

        private static void FutureWindowMovesAndPublishedTicksBecomeOld()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(roster, 100U, 2U, 4);

            coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(100U, 0));
            FrameData[] publication = coordinator.Submit(
                roster.GetPlayerId(new PlayerSlot(1)),
                CoordinatorTestData.CreateInput(100U, 1));
            TestAssert.Equal(1, publication.Length);
            TestAssert.Equal(101U, coordinator.NextPublishTick);
            TestAssert.Equal(
                0,
                coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(103U, 0)).Length);
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(100U, 0)));
        }
    }
}
