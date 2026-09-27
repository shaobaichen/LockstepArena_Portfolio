using System;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal static class CoordinatorRejectTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("DuplicateSlotIsRejectedWithoutPublication", DuplicateSlotIsRejectedWithoutPublication),
            new TestCase("UnknownPlayerIdIsRejectedWithoutPublication", UnknownPlayerIdIsRejectedWithoutPublication),
            new TestCase("PlayerIdSlotMismatchIsRejectedWithoutPublication", PlayerIdSlotMismatchIsRejectedWithoutPublication),
            new TestCase("RosterOutOfRangeSlotIsRejectedWithoutPublication", RosterOutOfRangeSlotIsRejectedWithoutPublication),
            new TestCase("FirstInvalidSubmissionDoesNotPolluteNewTick", FirstInvalidSubmissionDoesNotPolluteNewTick),
            new TestCase("RejectPreservesAcceptedInputsAndOtherPendingTicks", RejectPreservesAcceptedInputsAndOtherPendingTicks),
        };

        private static void DuplicateSlotIsRejectedWithoutPublication()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(100U, 0));
            AssertRejectPreservesPublishedState<InvalidOperationException>(
                coordinator,
                () => coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(100U, 0)));
        }

        private static void UnknownPlayerIdIsRejectedWithoutPublication()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            AssertRejectPreservesPublishedState<ArgumentException>(
                coordinator,
                () => coordinator.Submit(new PlayerId(999UL), CoordinatorTestData.CreateInput(100U, 0)));
        }

        private static void PlayerIdSlotMismatchIsRejectedWithoutPublication()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            AssertRejectPreservesPublishedState<ArgumentException>(
                coordinator,
                () => coordinator.Submit(roster.GetPlayerId(new PlayerSlot(1)), CoordinatorTestData.CreateInput(100U, 0)));
        }

        private static void RosterOutOfRangeSlotIsRejectedWithoutPublication()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            AssertRejectPreservesPublishedState<ArgumentOutOfRangeException>(
                coordinator,
                () => coordinator.Submit(
                    roster.GetPlayerId(new PlayerSlot(0)),
                    CoordinatorTestData.CreateInput(100U, 2)));
        }

        private static void FirstInvalidSubmissionDoesNotPolluteNewTick()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            AssertRejectPreservesPublishedState<ArgumentException>(
                coordinator,
                () => coordinator.Submit(new PlayerId(999UL), CoordinatorTestData.CreateInput(101U, 0)));

            TestAssert.Equal(
                0,
                coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(101U, 0)).Length);
            TestAssert.Equal(
                0,
                coordinator.Submit(roster.GetPlayerId(new PlayerSlot(1)), CoordinatorTestData.CreateInput(101U, 1)).Length);
        }

        private static void RejectPreservesAcceptedInputsAndOtherPendingTicks()
        {
            ActiveRoster roster = CoordinatorTestData.CreateRoster(2);
            AuthoritativeFrameCoordinator coordinator = CreateCoordinator(roster);
            coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(100U, 0));
            coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(101U, 0));
            coordinator.Submit(roster.GetPlayerId(new PlayerSlot(1)), CoordinatorTestData.CreateInput(101U, 1));

            AssertRejectPreservesPublishedState<InvalidOperationException>(
                coordinator,
                () => coordinator.Submit(roster.GetPlayerId(new PlayerSlot(0)), CoordinatorTestData.CreateInput(100U, 0)));

            FrameData[] publication = coordinator.Submit(
                roster.GetPlayerId(new PlayerSlot(1)),
                CoordinatorTestData.CreateInput(100U, 1));
            TestAssert.Equal(2, publication.Length);
            TestAssert.Equal(100U, publication[0].Tick);
            TestAssert.Equal(101U, publication[1].Tick);
            TestAssert.Equal(102U, coordinator.NextPublishTick);
            FrameData[] history = coordinator.GetAuthoritativeHistorySnapshot();
            TestAssert.Equal(2, history.Length);
            TestAssert.Equal(100U, history[0].Tick);
            TestAssert.Equal(101U, history[1].Tick);
        }

        private static AuthoritativeFrameCoordinator CreateCoordinator(ActiveRoster roster)
        {
            return new AuthoritativeFrameCoordinator(roster, 100U, 2U, 4);
        }

        private static void AssertRejectPreservesPublishedState<TException>(
            AuthoritativeFrameCoordinator coordinator,
            Action submission)
            where TException : Exception
        {
            uint tickBefore = coordinator.NextPublishTick;
            FrameData[] historyBefore = coordinator.GetAuthoritativeHistorySnapshot();
            TestAssert.Throws<TException>(submission);
            TestAssert.Equal(tickBefore, coordinator.NextPublishTick);
            FrameData[] historyAfter = coordinator.GetAuthoritativeHistorySnapshot();
            TestAssert.Equal(historyBefore.Length, historyAfter.Length);
            for (int index = 0; index < historyBefore.Length; index++)
            {
                TestAssert.Same(historyBefore[index], historyAfter[index]);
            }
        }
    }
}
