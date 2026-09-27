using System;
using System.Reflection;
using LockstepArena.Client.Prediction.Verification;
using LockstepArena.Simulation;

namespace LockstepArena.Client.Prediction.Tests
{
    internal static class ClientPredictionTimelineTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("ConstructorRejectsNullInitialState", ConstructorRejectsNullInitialState),
            new TestCase("ConstructorRejectsNonPositiveMaxPredictionTicks", ConstructorRejectsNonPositiveMaxPredictionTicks),
            new TestCase("ConstructorStartsAlignedWithEmptyHistory", ConstructorStartsAlignedWithEmptyHistory),
            new TestCase("PredictRejectsNullFrameWithoutMutation", PredictRejectsNullFrameWithoutMutation),
            new TestCase("PredictRejectsWrongTickWithoutMutation", PredictRejectsWrongTickWithoutMutation),
            new TestCase("PredictRejectsRosterMismatchWithoutMutation", PredictRejectsRosterMismatchWithoutMutation),
            new TestCase("PredictRejectsWhenLeadEqualsCapacityWithoutMutation", PredictRejectsWhenLeadEqualsCapacityWithoutMutation),
            new TestCase("PredictAdvancesOnlyPredictedTimeline", PredictAdvancesOnlyPredictedTimeline),
            new TestCase("PredictionSupportsTwoThreeAndFourPlayerRosters", PredictionSupportsTwoThreeAndFourPlayerRosters),
            new TestCase("PredictionUsesCanonicalSlotOrder", PredictionUsesCanonicalSlotOrder),
            new TestCase("FinalConsumablePredictionReachesUintMaxValue", FinalConsumablePredictionReachesUintMaxValue),
            new TestCase("PredictionBeyondTerminalIsRejectedWithoutMutation", PredictionBeyondTerminalIsRejectedWithoutMutation),
            new TestCase("ReconcileRejectsNullFrameWithoutMutation", ReconcileRejectsNullFrameWithoutMutation),
            new TestCase("ReconcileRejectsWrongTickWithoutMutation", ReconcileRejectsWrongTickWithoutMutation),
            new TestCase("ReconcileRejectsRosterMismatchWithoutMutation", ReconcileRejectsRosterMismatchWithoutMutation),
            new TestCase("ReconcileBoundaryFailureDoesNotFaultSubsequentValidWork", ReconcileBoundaryFailureDoesNotFaultSubsequentValidWork),
            new TestCase("ReconcileAtAlignedFrontierAdvancesBothTimelinesCleanly", ReconcileAtAlignedFrontierAdvancesBothTimelinesCleanly),
            new TestCase("StructurallyEqualRosterInstancesAreAccepted", StructurallyEqualRosterInstancesAreAccepted),
            new TestCase("EqualFrameDataInDifferentInstancesIsClean", EqualFrameDataInDifferentInstancesIsClean),
            new TestCase("CanonicallyEqualFramesFromDifferentConstructionOrderAreClean", CanonicallyEqualFramesFromDifferentConstructionOrderAreClean),
            new TestCase("CleanReconcileRemovesOldestWithoutRecomputingPredictedState", CleanReconcileRemovesOldestWithoutRecomputingPredictedState),
            new TestCase("CleanReconcileRetainsLaterPredictionSnapshots", CleanReconcileRetainsLaterPredictionSnapshots),
            new TestCase("MoveXDifferenceIsDirty", MoveXDifferenceIsDirty),
            new TestCase("MoveZDifferenceIsDirty", MoveZDifferenceIsDirty),
            new TestCase("AimDifferenceIsDirty", AimDifferenceIsDirty),
            new TestCase("FireDifferenceIsDirty", FireDifferenceIsDirty),
            new TestCase("DifferentInputWithSameClampedStateIsDirty", DifferentInputWithSameClampedStateIsDirty),
            new TestCase("EarliestPendingMismatchRollsBackAndReplaysAllLaterFrames", EarliestPendingMismatchRollsBackAndReplaysAllLaterFrames),
            new TestCase("LatestPendingMismatchRollsBackWithoutLaterReplay", LatestPendingMismatchRollsBackWithoutLaterReplay),
            new TestCase("DirtyReplayRebuildsRetainedSnapshots", DirtyReplayRebuildsRetainedSnapshots),
            new TestCase("CorrectPredictionGoldenConvergesCleanly", CorrectPredictionGoldenConvergesCleanly),
            new TestCase("WrongPredictionGoldenFindsTick101AndConvergesAfterReplay", WrongPredictionGoldenFindsTick101AndConvergesAfterReplay),
            new TestCase("TwinPredictorsMatchDigestsAfterEveryOperation", TwinPredictorsMatchDigestsAfterEveryOperation),
            new TestCase("ImpossibleInternalInvariantEntersStickyFailStopWithoutPartialCommit", ImpossibleInternalInvariantEntersStickyFailStopWithoutPartialCommit),
            new TestCase("StickyFaultRejectsBeforeArgumentValidation", StickyFaultRejectsBeforeArgumentValidation),
            new TestCase("FinalAuthoritativeFrameDrainsTerminalPredictionWithoutWrap", FinalAuthoritativeFrameDrainsTerminalPredictionWithoutWrap),
            new TestCase("AuthorityBeyondTerminalIsRejectedWithoutMutation", AuthorityBeyondTerminalIsRejectedWithoutMutation),
        };

        private static void ConstructorRejectsNullInitialState()
        {
            TestAssert.Throws<ArgumentNullException>(() => new ClientPredictionTimeline(null!, 1));
        }

        private static void ConstructorRejectsNonPositiveMaxPredictionTicks()
        {
            BattleState state = CreateState(10U, CreateRoster(2));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new ClientPredictionTimeline(state, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new ClientPredictionTimeline(state, -1));
        }

        private static void ConstructorStartsAlignedWithEmptyHistory()
        {
            BattleState state = CreateState(10U, CreateRoster(2));
            var timeline = new ClientPredictionTimeline(state, 4);

            TestAssert.Same(state, timeline.AuthoritativeState);
            TestAssert.Same(state, timeline.PredictedState);
            TestAssert.Equal(4, timeline.MaxPredictionTicks);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
        }

        private static void PredictRejectsNullFrameWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentNullException>(() => timeline.Predict(null!));

            AssertUnchanged(timeline, authoritative, predicted, 0);
        }

        private static void PredictRejectsWrongTickWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentException>(() => timeline.Predict(CreateNeutralFrame(predicted.Roster, 11U)));

            AssertUnchanged(timeline, authoritative, predicted, 0);
        }

        private static void PredictRejectsRosterMismatchWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster differentRoster = new ActiveRoster(new[] { new PlayerId(900UL), new PlayerId(700UL) });
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentException>(() => timeline.Predict(CreateNeutralFrame(differentRoster, 10U)));

            AssertUnchanged(timeline, authoritative, predicted, 0);
        }

        private static void PredictRejectsWhenLeadEqualsCapacityWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 1);
            timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, 10U));
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<InvalidOperationException>(() => timeline.Predict(CreateNeutralFrame(predicted.Roster, 11U)));

            AssertUnchanged(timeline, authoritative, predicted, 1);
        }

        private static void PredictAdvancesOnlyPredictedTimeline()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            BattleState authoritative = timeline.AuthoritativeState;

            timeline.Predict(CreateNeutralFrame(authoritative.Roster, 10U));

            TestAssert.Same(authoritative, timeline.AuthoritativeState);
            TestAssert.Equal(10U, timeline.AuthoritativeState.Tick);
            TestAssert.Equal(11U, timeline.PredictedState.Tick);
            TestAssert.Equal(1, timeline.PendingPredictionCount);
            TestAssert.NotSame(authoritative, timeline.PredictedState);
        }

        private static void PredictionSupportsTwoThreeAndFourPlayerRosters()
        {
            for (int count = 2; count <= 4; count++)
            {
                ClientPredictionTimeline timeline = CreateTimeline(20U, count, 2);
                timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, 20U));

                TestAssert.Equal(count, timeline.PredictedState.PlayerCount);
                TestAssert.Equal(21U, timeline.PredictedState.Tick);
                TestAssert.Equal(1, timeline.PendingPredictionCount);
            }
        }

        private static void PredictionUsesCanonicalSlotOrder()
        {
            ActiveRoster roster = CreateRoster(3);
            BattleState initial = CreateState(30U, roster);
            var timeline = new ClientPredictionTimeline(initial, 2);
            FrameData shuffled = FrameData.Create(
                roster,
                30U,
                new[]
                {
                    new InputFrame(30U, new PlayerSlot(2), -1, 0, 302),
                    new InputFrame(30U, new PlayerSlot(0), 1, 0, 100),
                    new InputFrame(30U, new PlayerSlot(1), 0, 1, 201),
                });

            timeline.Predict(shuffled);

            AssertPlayer(timeline.PredictedState, 0, 100, 0, 100);
            AssertPlayer(timeline.PredictedState, 1, 0, 100, 201);
            AssertPlayer(timeline.PredictedState, 2, -100, 0, 302);
        }

        private static void FinalConsumablePredictionReachesUintMaxValue()
        {
            ClientPredictionTimeline timeline = CreateTimeline(uint.MaxValue - 1U, 2, 2);

            timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, uint.MaxValue - 1U));

            TestAssert.Equal(uint.MaxValue - 1U, timeline.AuthoritativeState.Tick);
            TestAssert.Equal(uint.MaxValue, timeline.PredictedState.Tick);
            TestAssert.Equal(1, timeline.PendingPredictionCount);
        }

        private static void PredictionBeyondTerminalIsRejectedWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(uint.MaxValue - 1U, 2, 2);
            timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, uint.MaxValue - 1U));
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<InvalidOperationException>(() => timeline.Predict(CreateNeutralFrame(predicted.Roster, uint.MaxValue)));

            AssertUnchanged(timeline, authoritative, predicted, 1);
        }

        private static void ReconcileRejectsNullFrameWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentNullException>(() => timeline.ReconcileAuthoritative(null!));

            AssertUnchanged(timeline, authoritative, predicted, 0);
        }

        private static void ReconcileRejectsWrongTickWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, 10U));
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentException>(() => timeline.ReconcileAuthoritative(CreateNeutralFrame(predicted.Roster, 9U)));
            AssertUnchanged(timeline, authoritative, predicted, 1);
            TestAssert.Throws<ArgumentException>(() => timeline.ReconcileAuthoritative(CreateNeutralFrame(predicted.Roster, 11U)));
            AssertUnchanged(timeline, authoritative, predicted, 1);
        }

        private static void ReconcileRejectsRosterMismatchWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, 10U));
            ActiveRoster differentRoster = new ActiveRoster(new[] { new PlayerId(900UL), new PlayerId(700UL) });
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentException>(() => timeline.ReconcileAuthoritative(CreateNeutralFrame(differentRoster, 10U)));

            AssertUnchanged(timeline, authoritative, predicted, 1);
        }

        private static void ReconcileBoundaryFailureDoesNotFaultSubsequentValidWork()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.AuthoritativeState.Roster;
            TestAssert.Throws<ArgumentException>(() => timeline.ReconcileAuthoritative(CreateNeutralFrame(roster, 11U)));

            bool dirty = timeline.ReconcileAuthoritative(CreateNeutralFrame(roster, 10U));

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(11U, timeline.AuthoritativeState.Tick);
            TestAssert.Same(timeline.AuthoritativeState, timeline.PredictedState);
        }

        private static void ReconcileAtAlignedFrontierAdvancesBothTimelinesCleanly()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);

            bool dirty = timeline.ReconcileAuthoritative(CreateNeutralFrame(timeline.AuthoritativeState.Roster, 10U));

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(11U, timeline.AuthoritativeState.Tick);
            TestAssert.Same(timeline.AuthoritativeState, timeline.PredictedState);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
        }

        private static void StructurallyEqualRosterInstancesAreAccepted()
        {
            ActiveRoster originalRoster = CreateRoster(3);
            var timeline = new ClientPredictionTimeline(CreateState(10U, originalRoster), 4);
            ActiveRoster equivalentRoster = CreateRoster(3);

            bool dirty = timeline.ReconcileAuthoritative(CreateNeutralFrame(equivalentRoster, 10U));

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(11U, timeline.AuthoritativeState.Tick);
        }

        private static void EqualFrameDataInDifferentInstancesIsClean()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateNeutralFrame(roster, 10U));
            FrameData separatelyAllocatedAuthority = CreateNeutralFrame(roster, 10U);

            bool dirty = timeline.ReconcileAuthoritative(separatelyAllocatedAuthority);

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
        }

        private static void CanonicallyEqualFramesFromDifferentConstructionOrderAreClean()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 3, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            FrameData predicted = FrameData.Create(
                roster,
                10U,
                new[]
                {
                    new InputFrame(10U, new PlayerSlot(2), -1, 0, 3002),
                    new InputFrame(10U, new PlayerSlot(0), 1, 0, 3000),
                    new InputFrame(10U, new PlayerSlot(1), 0, 1, 3001),
                });
            FrameData authoritative = FrameData.Create(
                roster,
                10U,
                new[]
                {
                    new InputFrame(10U, new PlayerSlot(1), 0, 1, 3001),
                    new InputFrame(10U, new PlayerSlot(2), -1, 0, 3002),
                    new InputFrame(10U, new PlayerSlot(0), 1, 0, 3000),
                });
            timeline.Predict(predicted);

            bool dirty = timeline.ReconcileAuthoritative(authoritative);

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
        }

        private static void CleanReconcileRemovesOldestWithoutRecomputingPredictedState()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            FrameData frame10 = CreateNeutralFrame(roster, 10U);
            timeline.Predict(frame10);
            timeline.Predict(CreateNeutralFrame(roster, 11U));
            BattleState predicted = timeline.PredictedState;

            bool dirty = timeline.ReconcileAuthoritative(CreateNeutralFrame(roster, 10U));

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(11U, timeline.AuthoritativeState.Tick);
            TestAssert.Same(predicted, timeline.PredictedState);
            TestAssert.Equal(1, timeline.PendingPredictionCount);
        }

        private static void CleanReconcileRetainsLaterPredictionSnapshots()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateNeutralFrame(roster, 10U));
            timeline.Predict(CreateNeutralFrame(roster, 11U));
            timeline.ReconcileAuthoritative(CreateNeutralFrame(roster, 10U));
            BattleState predicted = timeline.PredictedState;

            bool dirty = timeline.ReconcileAuthoritative(CreateNeutralFrame(roster, 11U));

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(12U, timeline.AuthoritativeState.Tick);
            TestAssert.Same(predicted, timeline.PredictedState);
            TestAssert.Equal(12U, timeline.PredictedState.Tick);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
        }

        private static void MoveXDifferenceIsDirty()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateFrame(roster, 10U, new sbyte[] { 1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 100, 200 }));

            bool dirty = timeline.ReconcileAuthoritative(
                CreateFrame(roster, 10U, new sbyte[] { -1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 100, 200 }));

            TestAssert.Equal(true, dirty);
            AssertPlayer(timeline.AuthoritativeState, 0, -100, 0, 100);
            AssertPlayer(timeline.PredictedState, 0, -100, 0, 100);
        }

        private static void MoveZDifferenceIsDirty()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateFrame(roster, 10U, new sbyte[] { 0, 0 }, new sbyte[] { 1, 0 }, new ushort[] { 100, 200 }));

            bool dirty = timeline.ReconcileAuthoritative(
                CreateFrame(roster, 10U, new sbyte[] { 0, 0 }, new sbyte[] { -1, 0 }, new ushort[] { 100, 200 }));

            TestAssert.Equal(true, dirty);
            AssertPlayer(timeline.PredictedState, 0, 0, -100, 100);
        }

        private static void AimDifferenceIsDirty()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateFrame(roster, 10U, new sbyte[] { 0, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 111, 200 }));

            bool dirty = timeline.ReconcileAuthoritative(
                CreateFrame(roster, 10U, new sbyte[] { 0, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 112, 200 }));

            TestAssert.Equal(true, dirty);
            AssertPlayer(timeline.PredictedState, 0, 0, 0, 112);
        }

        private static void FireDifferenceIsDirty()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            FrameData predicted = FrameData.Create(
                roster,
                10U,
                new[]
                {
                    new InputFrame(10U, new PlayerSlot(0), 0, 0, 111, true),
                    new InputFrame(10U, new PlayerSlot(1), 0, 0, 222, false),
                });
            FrameData authoritative = FrameData.Create(
                roster,
                10U,
                new[]
                {
                    new InputFrame(10U, new PlayerSlot(0), 0, 0, 111, false),
                    new InputFrame(10U, new PlayerSlot(1), 0, 0, 222, false),
                });
            timeline.Predict(predicted);

            bool dirty = timeline.ReconcileAuthoritative(authoritative);

            TestAssert.Equal(true, dirty);
        }

        private static void DifferentInputWithSameClampedStateIsDirty()
        {
            ActiveRoster roster = CreateRoster(2);
            var initial = new BattleState(
                10U,
                roster,
                new[]
                {
                    new PlayerState(SimulationConfig.ArenaMaxX, 0, 10),
                    new PlayerState(0, 0, 20),
                });
            var timeline = new ClientPredictionTimeline(initial, 4);
            timeline.Predict(CreateFrame(roster, 10U, new sbyte[] { 1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 10, 20 }));

            bool dirty = timeline.ReconcileAuthoritative(
                CreateFrame(roster, 10U, new sbyte[] { 0, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 10, 20 }));

            TestAssert.Equal(true, dirty);
            AssertPlayer(timeline.PredictedState, 0, SimulationConfig.ArenaMaxX, 0, 10);
        }

        private static void EarliestPendingMismatchRollsBackAndReplaysAllLaterFrames()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateFrame(roster, 10U, new sbyte[] { 1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 100, 200 }));
            timeline.Predict(CreateFrame(roster, 11U, new sbyte[] { 0, 0 }, new sbyte[] { 1, 0 }, new ushort[] { 101, 201 }));
            timeline.Predict(CreateFrame(roster, 12U, new sbyte[] { 1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 102, 202 }));

            bool dirty = timeline.ReconcileAuthoritative(
                CreateFrame(roster, 10U, new sbyte[] { -1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 100, 200 }));

            TestAssert.Equal(true, dirty);
            TestAssert.Equal(11U, timeline.AuthoritativeState.Tick);
            TestAssert.Equal(13U, timeline.PredictedState.Tick);
            TestAssert.Equal(2, timeline.PendingPredictionCount);
            AssertPlayer(timeline.PredictedState, 0, 0, 100, 102);
        }

        private static void LatestPendingMismatchRollsBackWithoutLaterReplay()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateNeutralFrame(roster, 10U));
            timeline.Predict(CreateFrame(roster, 11U, new sbyte[] { 1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 101, 201 }));
            timeline.ReconcileAuthoritative(CreateNeutralFrame(roster, 10U));

            bool dirty = timeline.ReconcileAuthoritative(
                CreateFrame(roster, 11U, new sbyte[] { -1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 101, 201 }));

            TestAssert.Equal(true, dirty);
            TestAssert.Equal(12U, timeline.AuthoritativeState.Tick);
            TestAssert.Equal(12U, timeline.PredictedState.Tick);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
            AssertPlayer(timeline.PredictedState, 0, -100, 0, 101);
        }

        private static void DirtyReplayRebuildsRetainedSnapshots()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateFrame(roster, 10U, new sbyte[] { 1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 100, 200 }));
            FrameData frame11 = CreateFrame(roster, 11U, new sbyte[] { 0, 0 }, new sbyte[] { 1, 0 }, new ushort[] { 101, 201 });
            timeline.Predict(frame11);
            timeline.ReconcileAuthoritative(
                CreateFrame(roster, 10U, new sbyte[] { -1, 0 }, new sbyte[] { 0, 0 }, new ushort[] { 100, 200 }));

            bool dirty = timeline.ReconcileAuthoritative(
                CreateFrame(roster, 11U, new sbyte[] { 0, 0 }, new sbyte[] { 1, 0 }, new ushort[] { 101, 201 }));

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(12U, timeline.AuthoritativeState.Tick);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
            AssertPlayer(timeline.AuthoritativeState, 0, -100, 100, 101);
        }

        private static void CorrectPredictionGoldenConvergesCleanly()
        {
            Gate12PredictionGoldenResult result = Gate12PredictionGoldenVector.RunCorrect();

            AssertGoldenState(result.PredictedStatesAfterPredict[0], 101U, 0xD95809E1EB5CDDAAUL, 0, -200, 0, 10100, 1, 200, 0, 20100, 2, 0, -200, 30100, 3, 0, 200, 40100);
            AssertGoldenState(result.PredictedStatesAfterPredict[1], 102U, 0xA96B83267DD72A7DUL, 0, -200, 100, 10101, 1, 200, -100, 20101, 2, 100, -200, 30101, 3, -100, 200, 40101);
            AssertGoldenState(result.PredictedStatesAfterPredict[2], 103U, 0x386C4BB11A7EB7E0UL, 0, -300, 100, 10102, 1, 300, -100, 20102, 2, 100, -300, 30102, 3, -100, 300, 40102);
            AssertGoldenReconciliation(result, new[] { false, false, false }, new[] { 2, 1, 0 });
            AssertGoldenState(result.AuthoritativeStatesAfterReconcile[0], 101U, 0xD95809E1EB5CDDAAUL, 0, -200, 0, 10100, 1, 200, 0, 20100, 2, 0, -200, 30100, 3, 0, 200, 40100);
            AssertGoldenState(result.AuthoritativeStatesAfterReconcile[1], 102U, 0xA96B83267DD72A7DUL, 0, -200, 100, 10101, 1, 200, -100, 20101, 2, 100, -200, 30101, 3, -100, 200, 40101);
            AssertGoldenState(result.AuthoritativeStatesAfterReconcile[2], 103U, 0x386C4BB11A7EB7E0UL, 0, -300, 100, 10102, 1, 300, -100, 20102, 2, 100, -300, 30102, 3, -100, 300, 40102);
            AssertStatesEqual(result.AuthoritativeStatesAfterReconcile[2], result.PredictedStatesAfterReconcile[2]);
        }

        private static void WrongPredictionGoldenFindsTick101AndConvergesAfterReplay()
        {
            Gate12PredictionGoldenResult result = Gate12PredictionGoldenVector.RunWrong();

            AssertGoldenState(result.PredictedStatesAfterPredict[1], 102U, 0x8506E4507001B972UL, 0, -200, 100, 10101, 1, 200, -100, 20101, 2, -100, -200, 30101, 3, -100, 200, 40101);
            AssertGoldenState(result.PredictedStatesAfterPredict[2], 103U, 0x7D0D3A230618500FUL, 0, -300, 100, 10102, 1, 300, -100, 20102, 2, -100, -300, 30102, 3, -100, 300, 40102);
            AssertGoldenReconciliation(result, new[] { false, true, false }, new[] { 2, 1, 0 });
            AssertGoldenState(result.AuthoritativeStatesAfterReconcile[1], 102U, 0xA96B83267DD72A7DUL, 0, -200, 100, 10101, 1, 200, -100, 20101, 2, 100, -200, 30101, 3, -100, 200, 40101);
            AssertGoldenState(result.PredictedStatesAfterReconcile[1], 103U, 0x386C4BB11A7EB7E0UL, 0, -300, 100, 10102, 1, 300, -100, 20102, 2, 100, -300, 30102, 3, -100, 300, 40102);
            AssertGoldenState(result.AuthoritativeStatesAfterReconcile[2], 103U, 0x386C4BB11A7EB7E0UL, 0, -300, 100, 10102, 1, 300, -100, 20102, 2, 100, -300, 30102, 3, -100, 300, 40102);
            AssertStatesEqual(result.AuthoritativeStatesAfterReconcile[2], result.PredictedStatesAfterReconcile[2]);
        }

        private static void TwinPredictorsMatchDigestsAfterEveryOperation()
        {
            AssertGoldenResultsEqual(
                Gate12PredictionGoldenVector.RunCorrect(),
                Gate12PredictionGoldenVector.RunCorrect());
            AssertGoldenResultsEqual(
                Gate12PredictionGoldenVector.RunWrong(),
                Gate12PredictionGoldenVector.RunWrong());
        }

        private static void ImpossibleInternalInvariantEntersStickyFailStopWithoutPartialCommit()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateNeutralFrame(roster, 10U));
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;
            CorruptPredictionHistory(timeline);

            TestAssert.Throws<InvalidOperationException>(() => timeline.Predict(CreateNeutralFrame(roster, 11U)));

            AssertUnchanged(timeline, authoritative, predicted, 1);
        }

        private static void StickyFaultRejectsBeforeArgumentValidation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateNeutralFrame(roster, 10U));
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;
            CorruptPredictionHistory(timeline);
            TestAssert.Throws<InvalidOperationException>(() => timeline.Predict(CreateNeutralFrame(roster, 11U)));

            TestAssert.Throws<InvalidOperationException>(() => timeline.Predict(null!));
            TestAssert.Throws<InvalidOperationException>(() => timeline.ReconcileAuthoritative(null!));
            AssertUnchanged(timeline, authoritative, predicted, 1);
            TestAssert.Equal(4, timeline.MaxPredictionTicks);
        }

        private static void FinalAuthoritativeFrameDrainsTerminalPredictionWithoutWrap()
        {
            ClientPredictionTimeline timeline = CreateTimeline(uint.MaxValue - 1U, 2, 2);
            ActiveRoster roster = timeline.PredictedState.Roster;
            timeline.Predict(CreateNeutralFrame(roster, uint.MaxValue - 1U));

            bool dirty = timeline.ReconcileAuthoritative(CreateNeutralFrame(roster, uint.MaxValue - 1U));

            TestAssert.Equal(false, dirty);
            TestAssert.Equal(uint.MaxValue, timeline.AuthoritativeState.Tick);
            TestAssert.Equal(uint.MaxValue, timeline.PredictedState.Tick);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
        }

        private static void AuthorityBeyondTerminalIsRejectedWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(uint.MaxValue, 2, 2);
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<InvalidOperationException>(
                () => timeline.ReconcileAuthoritative(CreateNeutralFrame(predicted.Roster, uint.MaxValue)));

            AssertUnchanged(timeline, authoritative, predicted, 0);
        }

        private static ClientPredictionTimeline CreateTimeline(uint tick, int playerCount, int capacity)
        {
            return new ClientPredictionTimeline(CreateState(tick, CreateRoster(playerCount)), capacity);
        }

        private static ActiveRoster CreateRoster(int count)
        {
            var ids = new PlayerId[count];
            for (int index = 0; index < count; index++)
            {
                ids[index] = new PlayerId(10_000UL - checked((ulong)(index * 17)));
            }

            return new ActiveRoster(ids);
        }

        private static BattleState CreateState(uint tick, ActiveRoster roster)
        {
            var players = new PlayerState[roster.Count];
            for (int index = 0; index < players.Length; index++)
            {
                players[index] = new PlayerState(0, 0, checked((ushort)(1000 + index)));
            }

            return new BattleState(tick, roster, players);
        }

        private static FrameData CreateNeutralFrame(ActiveRoster roster, uint tick)
        {
            var inputs = new InputFrame[roster.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                inputs[index] = new InputFrame(tick, new PlayerSlot(index), 0, 0, checked((ushort)(2000 + index)));
            }

            return FrameData.Create(roster, tick, inputs);
        }

        private static FrameData CreateFrame(
            ActiveRoster roster,
            uint tick,
            sbyte[] moveX,
            sbyte[] moveZ,
            ushort[] aim)
        {
            var inputs = new InputFrame[roster.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                inputs[index] = new InputFrame(
                    tick,
                    new PlayerSlot(index),
                    moveX[index],
                    moveZ[index],
                    aim[index]);
            }

            return FrameData.Create(roster, tick, inputs);
        }

        private static void CorruptPredictionHistory(ClientPredictionTimeline timeline)
        {
            FieldInfo[] fields = typeof(ClientPredictionTimeline).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo? historyField = null;
            for (int index = 0; index < fields.Length; index++)
            {
                Type fieldType = fields[index].FieldType;
                Type? elementType = fieldType.GetElementType();
                if (fieldType.IsArray &&
                    elementType is not null &&
                    elementType.GetProperty("StateBefore") is not null &&
                    elementType.GetProperty("PredictedFrame") is not null)
                {
                    if (historyField is not null)
                    {
                        throw new InvalidOperationException("Expected one prediction-history field.");
                    }

                    historyField = fields[index];
                }
            }

            if (historyField is null)
            {
                throw new InvalidOperationException("Prediction-history field was not found.");
            }

            Type recordType = historyField.FieldType.GetElementType()!;
            historyField.SetValue(timeline, Array.CreateInstance(recordType, 1));
        }

        private static void AssertUnchanged(
            ClientPredictionTimeline timeline,
            BattleState authoritative,
            BattleState predicted,
            int pendingCount)
        {
            TestAssert.Same(authoritative, timeline.AuthoritativeState);
            TestAssert.Same(predicted, timeline.PredictedState);
            TestAssert.Equal(pendingCount, timeline.PendingPredictionCount);
        }

        private static void AssertPlayer(BattleState state, int slotValue, int x, int z, ushort aim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slotValue));
            TestAssert.Equal(x, player.PositionX);
            TestAssert.Equal(z, player.PositionZ);
            TestAssert.Equal(aim, player.Aim);
        }

        private static void AssertGoldenReconciliation(
            Gate12PredictionGoldenResult result,
            bool[] expectedDirty,
            int[] expectedPending)
        {
            TestAssert.Equal(3, result.DirtyResults.Length);
            TestAssert.Equal(3, result.PendingCountsAfterReconcile.Length);
            for (int index = 0; index < 3; index++)
            {
                TestAssert.Equal(expectedDirty[index], result.DirtyResults[index]);
                TestAssert.Equal(expectedPending[index], result.PendingCountsAfterReconcile[index]);
            }
        }

        private static void AssertGoldenResultsEqual(
            Gate12PredictionGoldenResult left,
            Gate12PredictionGoldenResult right)
        {
            for (int index = 0; index < 3; index++)
            {
                AssertStatesEqual(left.PredictedStatesAfterPredict[index], right.PredictedStatesAfterPredict[index]);
                AssertStatesEqual(left.AuthoritativeStatesAfterReconcile[index], right.AuthoritativeStatesAfterReconcile[index]);
                AssertStatesEqual(left.PredictedStatesAfterReconcile[index], right.PredictedStatesAfterReconcile[index]);
                TestAssert.Equal(left.DirtyResults[index], right.DirtyResults[index]);
                TestAssert.Equal(left.PendingCountsAfterReconcile[index], right.PendingCountsAfterReconcile[index]);
            }
        }

        private static void AssertStatesEqual(BattleState expected, BattleState actual)
        {
            TestAssert.Equal(true, BattleStateValueComparer.HaveSameValue(expected, actual));
            TestAssert.Equal(StateDigest.Compute(expected), StateDigest.Compute(actual));
        }

        private static void AssertGoldenState(
            BattleState state,
            uint tick,
            ulong digest,
            int slot0,
            int x0,
            int z0,
            ushort aim0,
            int slot1,
            int x1,
            int z1,
            ushort aim1,
            int slot2,
            int x2,
            int z2,
            ushort aim2,
            int slot3,
            int x3,
            int z3,
            ushort aim3)
        {
            TestAssert.Equal(tick, state.Tick);
            TestAssert.Equal(digest, StateDigest.Compute(state));
            AssertPlayer(state, slot0, x0, z0, aim0);
            AssertPlayer(state, slot1, x1, z1, aim1);
            AssertPlayer(state, slot2, x2, z2, aim2);
            AssertPlayer(state, slot3, x3, z3, aim3);
        }
    }
}
