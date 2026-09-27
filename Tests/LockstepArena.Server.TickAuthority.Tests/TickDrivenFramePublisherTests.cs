using System;
using LockstepArena.Server.FrameSync;
using LockstepArena.Simulation;

namespace LockstepArena.Server.TickAuthority.Tests
{
    internal static class TickDrivenFramePublisherTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("ConstructorRejectsNullRoster", ConstructorRejectsNullRoster),
            new TestCase("ConstructorAllowsFutureWindowSmallerThanInputDelay", ConstructorAllowsFutureWindowSmallerThanInputDelay),
            new TestCase("ZeroDelayStartsWithInitialTickEligible", ZeroDelayStartsWithInitialTickEligible),
            new TestCase("ZeroDelayCompleteInitialFramePublishesImmediatelyOnSubmit", ZeroDelayCompleteInitialFramePublishesImmediatelyOnSubmit),
            new TestCase("PositiveDelayStartsWithoutEligibilityCeiling", PositiveDelayStartsWithoutEligibilityCeiling),
            new TestCase("AdvanceOneTickIncrementsCollectionTickExactlyOnce", AdvanceOneTickIncrementsCollectionTickExactlyOnce),
            new TestCase("TickFirstBecomesEligibleAtDelayPlusTickOffset", TickFirstBecomesEligibleAtDelayPlusTickOffset),
            new TestCase("EligibilityCeilingAdvancesAtMostOneFrameTickPerAdvance", EligibilityCeilingAdvancesAtMostOneFrameTickPerAdvance),
            new TestCase("CompleteFrameBeforeMaturityReturnsEmpty", CompleteFrameBeforeMaturityReturnsEmpty),
            new TestCase("AdvancePublishesCompleteFrameAtExactMaturity", AdvancePublishesCompleteFrameAtExactMaturity),
            new TestCase("MatureIncompleteFrameRemainsUnpublished", MatureIncompleteFrameRemainsUnpublished),
            new TestCase("LateCompletionOfMatureFramePublishesImmediately", LateCompletionOfMatureFramePublishesImmediately),
            new TestCase("MatureFutureFramesRemainBlockedByNextPublishGap", MatureFutureFramesRemainBlockedByNextPublishGap),
            new TestCase("GapFillPublishesOnlyEligibleContinuousPrefix", GapFillPublishesOnlyEligibleContinuousPrefix),
            new TestCase("CompleteButIneligibleFramesRemainPendingAfterGapFill", CompleteButIneligibleFramesRemainPendingAfterGapFill),
            new TestCase("LaterAdvancesPublishNewlyEligibleFramesInOrder", LaterAdvancesPublishNewlyEligibleFramesInOrder),
            new TestCase("AuthoritativeHistoryContainsOnlyPublishedFrames", AuthoritativeHistoryContainsOnlyPublishedFrames),
            new TestCase("AuthoritativeHistoryCapacityRemainsBounded", AuthoritativeHistoryCapacityRemainsBounded),
            new TestCase("RejectedSubmitPreservesScheduleAndExistingPendingInputs", RejectedSubmitPreservesScheduleAndExistingPendingInputs),
            new TestCase("PublicationBatchContainerIsIndependentFromHistorySnapshot", PublicationBatchContainerIsIndependentFromHistorySnapshot),
            new TestCase("FinalConsumableTickBecomesEligibleWithoutWrap", FinalConsumableTickBecomesEligibleWithoutWrap),
            new TestCase("EligibilityCeilingSaturatesAtFinalConsumableTick", EligibilityCeilingSaturatesAtFinalConsumableTick),
            new TestCase("AdvanceAfterEligibilitySaturationThrowsWithoutMutation", AdvanceAfterEligibilitySaturationThrowsWithoutMutation),
            new TestCase("UintMaxInputIsRejectedWithoutScheduleMutation", UintMaxInputIsRejectedWithoutScheduleMutation),
            new TestCase("TwoPlayerWarmupGoldenMatchesStateAndDigest", TwoPlayerWarmupGoldenMatchesStateAndDigest),
            new TestCase("ThreePlayerLateCompletionGoldenMatchesStateAndDigest", ThreePlayerLateCompletionGoldenMatchesStateAndDigest),
            new TestCase("FourPlayerArrivalOrdersProduceSameAuthorityAndPerTickDigests", FourPlayerArrivalOrdersProduceSameAuthorityAndPerTickDigests),
        };

        private static void ConstructorRejectsNullRoster()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => new TickDrivenFramePublisher(null!, 100U, 2U, 4U, 2));
        }

        private static void ConstructorAllowsFutureWindowSmallerThanInputDelay()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 100U, 2U, 1U, 4);

            AssertEmpty(CompleteTick(publisher, roster, 100U));
            AssertEmpty(CompleteTick(publisher, roster, 101U));
            AssertEmpty(publisher.AdvanceOneTick());
            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
            AssertEmpty(Submit(publisher, roster, 102U, 0));
        }

        private static void ZeroDelayStartsWithInitialTickEligible()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 73U, 0U, 2U, 2);

            TestAssert.Same(roster, publisher.Roster);
            TestAssert.Equal(0U, publisher.InputDelayTicks);
            TestAssert.Equal(73UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(73U, publisher.EligibilityCeiling);
            TestAssert.Equal(73U, publisher.NextPublishTick);
        }

        private static void ZeroDelayCompleteInitialFramePublishesImmediatelyOnSubmit()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 100U, 0U, 2U, 2);

            AssertEmpty(Submit(publisher, roster, 100U, 0));
            AssertTicks(new[] { 100U }, Submit(publisher, roster, 100U, 1));
            TestAssert.Equal(101U, publisher.NextPublishTick);
            TestAssert.Equal(100UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(100U, publisher.EligibilityCeiling);
        }

        private static void PositiveDelayStartsWithoutEligibilityCeiling()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(2), 100U, 2U, 4U, 2);

            TestAssert.Equal(100UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(null, publisher.EligibilityCeiling);
        }

        private static void AdvanceOneTickIncrementsCollectionTickExactlyOnce()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(2), 100U, 3U, 4U, 2);

            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal(101UL, publisher.CollectionTick);
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal(102UL, publisher.CollectionTick);
        }

        private static void TickFirstBecomesEligibleAtDelayPlusTickOffset()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 4);
            CompleteTick(publisher, roster, 102U);
            CompleteTick(publisher, roster, 101U);
            CompleteTick(publisher, roster, 100U);

            AssertEmpty(publisher.AdvanceOneTick());
            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 101U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 102U }, publisher.AdvanceOneTick());
        }

        private static void EligibilityCeilingAdvancesAtMostOneFrameTickPerAdvance()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(2), 100U, 1U, 4U, 2);

            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal<uint?>(100U, publisher.EligibilityCeiling);
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal<uint?>(101U, publisher.EligibilityCeiling);
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal<uint?>(102U, publisher.EligibilityCeiling);
        }

        private static void CompleteFrameBeforeMaturityReturnsEmpty()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 2);

            AssertEmpty(CompleteTick(publisher, roster, 100U));
            TestAssert.Equal(100U, publisher.NextPublishTick);
            AssertEmpty(publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void AdvancePublishesCompleteFrameAtExactMaturity()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 2);
            CompleteTick(publisher, roster, 100U);

            AssertEmpty(publisher.AdvanceOneTick());
            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
        }

        private static void MatureIncompleteFrameRemainsUnpublished()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 2);
            Submit(publisher, roster, 100U, 0);

            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal(100U, publisher.NextPublishTick);
            AssertEmpty(publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void LateCompletionOfMatureFramePublishesImmediately()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 2);
            Submit(publisher, roster, 100U, 0);
            publisher.AdvanceOneTick();

            AssertTicks(new[] { 100U }, Submit(publisher, roster, 100U, 1));
        }

        private static void MatureFutureFramesRemainBlockedByNextPublishGap()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 4);
            CompleteTick(publisher, roster, 101U);
            CompleteTick(publisher, roster, 102U);

            publisher.AdvanceOneTick();
            publisher.AdvanceOneTick();
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal(100U, publisher.NextPublishTick);
            AssertEmpty(publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void GapFillPublishesOnlyEligibleContinuousPrefix()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 4);
            CompleteTick(publisher, roster, 101U);
            CompleteTick(publisher, roster, 102U);
            publisher.AdvanceOneTick();
            publisher.AdvanceOneTick();
            publisher.AdvanceOneTick();

            AssertTicks(new[] { 100U, 101U }, CompleteTick(publisher, roster, 100U));
            TestAssert.Equal(102U, publisher.NextPublishTick);
        }

        private static void CompleteButIneligibleFramesRemainPendingAfterGapFill()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 0U, 4U, 4);
            CompleteTick(publisher, roster, 100U);

            AssertEmpty(CompleteTick(publisher, roster, 101U));
            TestAssert.Equal(101U, publisher.NextPublishTick);
            AssertTicks(new[] { 100U }, publisher.GetAuthoritativeHistorySnapshot());
            AssertTicks(new[] { 101U }, publisher.AdvanceOneTick());
        }

        private static void LaterAdvancesPublishNewlyEligibleFramesInOrder()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 4);
            CompleteTick(publisher, roster, 102U);
            CompleteTick(publisher, roster, 103U);
            CompleteTick(publisher, roster, 100U);

            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
            CompleteTick(publisher, roster, 101U);
            AssertTicks(new[] { 101U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 102U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 103U }, publisher.AdvanceOneTick());
        }

        private static void AuthoritativeHistoryContainsOnlyPublishedFrames()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 4);
            CompleteTick(publisher, roster, 101U);
            CompleteTick(publisher, roster, 100U);

            AssertEmpty(publisher.GetAuthoritativeHistorySnapshot());
            publisher.AdvanceOneTick();
            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 100U }, publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void AuthoritativeHistoryCapacityRemainsBounded()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 0U, 4U, 2);
            CompleteTick(publisher, roster, 100U);
            CompleteTick(publisher, roster, 101U);
            publisher.AdvanceOneTick();
            CompleteTick(publisher, roster, 102U);
            publisher.AdvanceOneTick();

            AssertTicks(new[] { 101U, 102U }, publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void RejectedSubmitPreservesScheduleAndExistingPendingInputs()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 4);
            Submit(publisher, roster, 100U, 0);
            CompleteTick(publisher, roster, 101U);
            ulong collectionBefore = publisher.CollectionTick;
            uint? ceilingBefore = publisher.EligibilityCeiling;
            uint nextBefore = publisher.NextPublishTick;
            FrameData[] historyBefore = publisher.GetAuthoritativeHistorySnapshot();

            TestAssert.Throws<InvalidOperationException>(() => Submit(publisher, roster, 100U, 0));
            TestAssert.Throws<ArgumentException>(
                () => publisher.Submit(roster.GetPlayerId(new PlayerSlot(1)), CreateInput(100U, 0)));
            TestAssert.Equal(collectionBefore, publisher.CollectionTick);
            TestAssert.Equal(ceilingBefore, publisher.EligibilityCeiling);
            TestAssert.Equal(nextBefore, publisher.NextPublishTick);
            TestAssert.SequenceEqual(historyBefore, publisher.GetAuthoritativeHistorySnapshot());

            publisher.AdvanceOneTick();
            AssertTicks(new[] { 100U }, Submit(publisher, roster, 100U, 1));
            AssertTicks(new[] { 101U }, publisher.AdvanceOneTick());
        }

        private static void PublicationBatchContainerIsIndependentFromHistorySnapshot()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 0U, 4U, 2);
            FrameData[] publication = CompleteTick(publisher, roster, 100U);
            FrameData publishedFrame = publication[0];
            publication[0] = CreateStandaloneFrame(roster, 999U);

            FrameData[] history = publisher.GetAuthoritativeHistorySnapshot();
            TestAssert.Same(publishedFrame, history[0]);
            TestAssert.Equal(100U, history[0].Tick);
        }

        private static void FinalConsumableTickBecomesEligibleWithoutWrap()
        {
            uint finalTick = uint.MaxValue - 1U;
            ActiveRoster roster = CreateRoster(2);
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => CreatePublisher(roster, uint.MaxValue, 1U, 0U, 2));
            TickDrivenFramePublisher publisher = CreatePublisher(roster, finalTick, 2U, 0U, 2);
            CompleteTick(publisher, roster, finalTick);

            AssertEmpty(publisher.AdvanceOneTick());
            AssertTicks(new[] { finalTick }, publisher.AdvanceOneTick());
            TestAssert.Equal((ulong)uint.MaxValue + 1UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(finalTick, publisher.EligibilityCeiling);
            TestAssert.Equal(uint.MaxValue, publisher.NextPublishTick);
        }

        private static void EligibilityCeilingSaturatesAtFinalConsumableTick()
        {
            uint initialTick = uint.MaxValue - 2U;
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(2), initialTick, 1U, 1U, 2);

            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal<uint?>(initialTick, publisher.EligibilityCeiling);
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal<uint?>(uint.MaxValue - 1U, publisher.EligibilityCeiling);
            TestAssert.Equal((ulong)uint.MaxValue, publisher.CollectionTick);
        }

        private static void AdvanceAfterEligibilitySaturationThrowsWithoutMutation()
        {
            uint firstTick = uint.MaxValue - 2U;
            uint finalTick = uint.MaxValue - 1U;
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, firstTick, 1U, 1U, 2);
            Submit(publisher, roster, firstTick, 0);
            Submit(publisher, roster, finalTick, 0);
            publisher.AdvanceOneTick();
            publisher.AdvanceOneTick();
            ulong collectionBefore = publisher.CollectionTick;
            uint? ceilingBefore = publisher.EligibilityCeiling;
            uint nextBefore = publisher.NextPublishTick;
            FrameData[] historyBefore = publisher.GetAuthoritativeHistorySnapshot();

            TestAssert.Throws<InvalidOperationException>(() => publisher.AdvanceOneTick());
            TestAssert.Equal(collectionBefore, publisher.CollectionTick);
            TestAssert.Equal(ceilingBefore, publisher.EligibilityCeiling);
            TestAssert.Equal(nextBefore, publisher.NextPublishTick);
            TestAssert.SequenceEqual(historyBefore, publisher.GetAuthoritativeHistorySnapshot());
            AssertEmpty(Submit(publisher, roster, finalTick, 1));
            AssertTicks(new[] { firstTick, finalTick }, Submit(publisher, roster, firstTick, 1));
        }

        private static void UintMaxInputIsRejectedWithoutScheduleMutation()
        {
            uint finalTick = uint.MaxValue - 1U;
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, finalTick, 1U, 0U, 2);
            Submit(publisher, roster, finalTick, 0);
            ulong collectionBefore = publisher.CollectionTick;
            uint? ceilingBefore = publisher.EligibilityCeiling;
            uint nextBefore = publisher.NextPublishTick;
            FrameData[] historyBefore = publisher.GetAuthoritativeHistorySnapshot();

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => Submit(publisher, roster, uint.MaxValue, 0));
            TestAssert.Equal(collectionBefore, publisher.CollectionTick);
            TestAssert.Equal(ceilingBefore, publisher.EligibilityCeiling);
            TestAssert.Equal(nextBefore, publisher.NextPublishTick);
            TestAssert.SequenceEqual(historyBefore, publisher.GetAuthoritativeHistorySnapshot());

            AssertEmpty(publisher.AdvanceOneTick());
            AssertTicks(new[] { finalTick }, Submit(publisher, roster, finalTick, 1));
        }

        private static void TwoPlayerWarmupGoldenMatchesStateAndDigest()
        {
            Gate9GoldenResult result = Gate9TickAuthorityGoldenVector.RunTwoPlayer();

            AssertBatchTicks(new[] { new[] { 10U } }, result.PublicationBatches);
            AssertTicks(new[] { 10U }, result.AuthoritativeFrames);
            AssertTicks(new[] { 10U }, result.History);
            TestAssert.Equal(12UL, result.CollectionTick);
            TestAssert.Equal<uint?>(10U, result.EligibilityCeiling);
            TestAssert.Equal(11U, result.NextPublishTick);
            TestAssert.SequenceEqual(new[] { 0xAE353BEBCCF29139UL }, result.Digests);
            TestAssert.Equal(1, result.SimulationStates.Length);
            AssertState(
                result.SimulationStates[0],
                11U,
                new[]
                {
                    new PlayerState(100, 0, 101),
                    new PlayerState(-100, 0, 201),
                });
            AssertState(
                result.FinalState,
                11U,
                new[]
                {
                    new PlayerState(100, 0, 101),
                    new PlayerState(-100, 0, 201),
                });
        }

        private static void ThreePlayerLateCompletionGoldenMatchesStateAndDigest()
        {
            Gate9GoldenResult result = Gate9TickAuthorityGoldenVector.RunThreePlayer();

            AssertBatchTicks(new[] { new[] { 20U } }, result.PublicationBatches);
            AssertTicks(new[] { 20U }, result.AuthoritativeFrames);
            AssertTicks(new[] { 20U }, result.History);
            TestAssert.Equal(21UL, result.CollectionTick);
            TestAssert.Equal<uint?>(20U, result.EligibilityCeiling);
            TestAssert.Equal(21U, result.NextPublishTick);
            TestAssert.SequenceEqual(new[] { 0x38CCC825F57B7655UL }, result.Digests);
            TestAssert.Equal(1, result.SimulationStates.Length);
            AssertState(
                result.SimulationStates[0],
                21U,
                new[]
                {
                    new PlayerState(100, 0, 1001),
                    new PlayerState(0, 100, 2001),
                    new PlayerState(-100, 0, 3001),
                });
            AssertState(
                result.FinalState,
                21U,
                new[]
                {
                    new PlayerState(100, 0, 1001),
                    new PlayerState(0, 100, 2001),
                    new PlayerState(-100, 0, 3001),
                });
        }

        private static void FourPlayerArrivalOrdersProduceSameAuthorityAndPerTickDigests()
        {
            Gate9GoldenResult primary = Gate9TickAuthorityGoldenVector.RunFourPlayerPrimary();
            Gate9GoldenResult alternative = Gate9TickAuthorityGoldenVector.RunFourPlayerAlternative();
            uint[][] expectedBatches =
            {
                new[] { 100U, 101U },
                new[] { 102U },
                new[] { 103U },
            };
            ulong[] expectedDigests =
            {
                0xD95809E1EB5CDDAAUL,
                0xA96B83267DD72A7DUL,
                0x386C4BB11A7EB7E0UL,
                0x9F41F69F63A24BCBUL,
            };

            AssertFourPlayerResult(primary, expectedBatches, expectedDigests);
            AssertFourPlayerResult(alternative, expectedBatches, expectedDigests);
            AssertGoldenResultsEqual(primary, alternative);
        }

        private static void AssertFourPlayerResult(
            Gate9GoldenResult result,
            uint[][] expectedBatches,
            ulong[] expectedDigests)
        {
            AssertBatchTicks(expectedBatches, result.PublicationBatches);
            AssertTicks(new[] { 100U, 101U, 102U, 103U }, result.AuthoritativeFrames);
            AssertTicks(new[] { 101U, 102U, 103U }, result.History);
            TestAssert.Equal(105UL, result.CollectionTick);
            TestAssert.Equal<uint?>(103U, result.EligibilityCeiling);
            TestAssert.Equal(104U, result.NextPublishTick);
            TestAssert.SequenceEqual(expectedDigests, result.Digests);
            TestAssert.Equal(4, result.SimulationStates.Length);
            AssertState(result.SimulationStates[0], 101U, new[]
            {
                new PlayerState(-200, 0, 10100),
                new PlayerState(200, 0, 20100),
                new PlayerState(0, -200, 30100),
                new PlayerState(0, 200, 40100),
            });
            AssertState(result.SimulationStates[1], 102U, new[]
            {
                new PlayerState(-200, 100, 10101),
                new PlayerState(200, -100, 20101),
                new PlayerState(100, -200, 30101),
                new PlayerState(-100, 200, 40101),
            });
            AssertState(result.SimulationStates[2], 103U, new[]
            {
                new PlayerState(-300, 100, 10102),
                new PlayerState(300, -100, 20102),
                new PlayerState(100, -300, 30102),
                new PlayerState(-100, 300, 40102),
            });
            AssertState(result.SimulationStates[3], 104U, new[]
            {
                new PlayerState(-300, 0, 10103),
                new PlayerState(300, 0, 20103),
                new PlayerState(0, -300, 30103),
                new PlayerState(0, 300, 40103),
            });
            AssertState(result.FinalState, 104U, new[]
            {
                new PlayerState(-300, 0, 10103),
                new PlayerState(300, 0, 20103),
                new PlayerState(0, -300, 30103),
                new PlayerState(0, 300, 40103),
            });
        }

        private static void AssertGoldenResultsEqual(Gate9GoldenResult expected, Gate9GoldenResult actual)
        {
            TestAssert.Equal(expected.PublicationBatches.Length, actual.PublicationBatches.Length);
            for (int batchIndex = 0; batchIndex < expected.PublicationBatches.Length; batchIndex++)
            {
                AssertFramesEqual(
                    expected.PublicationBatches[batchIndex],
                    actual.PublicationBatches[batchIndex]);
            }

            AssertFramesEqual(expected.AuthoritativeFrames, actual.AuthoritativeFrames);
            TestAssert.Equal(expected.SimulationStates.Length, actual.SimulationStates.Length);
            for (int index = 0; index < expected.SimulationStates.Length; index++)
            {
                AssertStatesEqual(expected.SimulationStates[index], actual.SimulationStates[index]);
                TestAssert.Equal(expected.Digests[index], actual.Digests[index]);
            }

            AssertStatesEqual(expected.FinalState, actual.FinalState);
        }

        private static void AssertFramesEqual(FrameData[] expected, FrameData[] actual)
        {
            TestAssert.Equal(expected.Length, actual.Length);
            for (int frameIndex = 0; frameIndex < expected.Length; frameIndex++)
            {
                FrameData expectedFrame = expected[frameIndex];
                FrameData actualFrame = actual[frameIndex];
                TestAssert.Equal(expectedFrame.Tick, actualFrame.Tick);
                TestAssert.Equal(expectedFrame.Roster.Count, actualFrame.Roster.Count);
                TestAssert.Equal(expectedFrame.InputCount, actualFrame.InputCount);
                for (int slotValue = 0; slotValue < expectedFrame.Roster.Count; slotValue++)
                {
                    PlayerSlot slot = new PlayerSlot(slotValue);
                    TestAssert.Equal(
                        expectedFrame.Roster.GetPlayerId(slot),
                        actualFrame.Roster.GetPlayerId(slot));
                    InputFrame expectedInput = expectedFrame.GetInput(slot);
                    InputFrame actualInput = actualFrame.GetInput(slot);
                    TestAssert.Equal(expectedInput.Tick, actualInput.Tick);
                    TestAssert.Equal(expectedInput.PlayerSlot, actualInput.PlayerSlot);
                    TestAssert.Equal(expectedInput.MoveX, actualInput.MoveX);
                    TestAssert.Equal(expectedInput.MoveZ, actualInput.MoveZ);
                    TestAssert.Equal(expectedInput.Aim, actualInput.Aim);
                }
            }
        }

        private static void AssertStatesEqual(BattleState expected, BattleState actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.Equal(expected.PlayerCount, actual.PlayerCount);
            for (int slotValue = 0; slotValue < expected.PlayerCount; slotValue++)
            {
                PlayerSlot slot = new PlayerSlot(slotValue);
                TestAssert.Equal(expected.Roster.GetPlayerId(slot), actual.Roster.GetPlayerId(slot));
                AssertPlayerEqual(expected.GetPlayerState(slot), actual.GetPlayerState(slot));
            }
        }

        private static void AssertState(BattleState state, uint expectedTick, PlayerState[] expectedPlayers)
        {
            TestAssert.Equal(expectedTick, state.Tick);
            TestAssert.Equal(expectedPlayers.Length, state.PlayerCount);
            for (int slotValue = 0; slotValue < expectedPlayers.Length; slotValue++)
            {
                AssertPlayerEqual(expectedPlayers[slotValue], state.GetPlayerState(new PlayerSlot(slotValue)));
            }
        }

        private static void AssertPlayerEqual(PlayerState expected, PlayerState actual)
        {
            TestAssert.Equal(expected.PositionX, actual.PositionX);
            TestAssert.Equal(expected.PositionZ, actual.PositionZ);
            TestAssert.Equal(expected.Aim, actual.Aim);
        }

        private static void AssertBatchTicks(uint[][] expectedTicks, FrameData[][] batches)
        {
            TestAssert.Equal(expectedTicks.Length, batches.Length);
            for (int index = 0; index < expectedTicks.Length; index++)
            {
                AssertTicks(expectedTicks[index], batches[index]);
            }
        }

        private static TickDrivenFramePublisher CreatePublisher(
            ActiveRoster roster,
            uint initialTick,
            uint inputDelayTicks,
            uint maxFutureTickOffset,
            int historyCapacity)
        {
            return new TickDrivenFramePublisher(
                roster,
                initialTick,
                inputDelayTicks,
                maxFutureTickOffset,
                historyCapacity);
        }

        private static ActiveRoster CreateRoster(int playerCount)
        {
            ulong[] values = { 91UL, 17UL, 73UL, 44UL };
            PlayerId[] playerIds = new PlayerId[playerCount];
            for (int index = 0; index < playerCount; index++)
            {
                playerIds[index] = new PlayerId(values[index]);
            }

            return new ActiveRoster(playerIds);
        }

        private static InputFrame CreateInput(uint tick, int slot)
        {
            return new InputFrame(
                tick,
                new PlayerSlot(slot),
                (sbyte)((slot % 3) - 1),
                (sbyte)(((slot + 1) % 3) - 1),
                checked((ushort)(1000 + slot)));
        }

        private static FrameData[] Submit(
            TickDrivenFramePublisher publisher,
            ActiveRoster roster,
            uint tick,
            int slot)
        {
            return publisher.Submit(roster.GetPlayerId(new PlayerSlot(slot)), CreateInput(tick, slot));
        }

        private static FrameData[] CompleteTick(
            TickDrivenFramePublisher publisher,
            ActiveRoster roster,
            uint tick)
        {
            FrameData[] publication = Array.Empty<FrameData>();
            for (int slot = 0; slot < roster.Count; slot++)
            {
                publication = Submit(publisher, roster, tick, slot);
            }

            return publication;
        }

        private static FrameData CreateStandaloneFrame(ActiveRoster roster, uint tick)
        {
            InputFrame[] inputs = new InputFrame[roster.Count];
            for (int slot = 0; slot < roster.Count; slot++)
            {
                inputs[slot] = CreateInput(tick, slot);
            }

            return FrameData.Create(roster, tick, inputs);
        }

        private static void AssertEmpty(FrameData[] frames)
        {
            TestAssert.Equal(0, frames.Length);
        }

        private static void AssertTicks(uint[] expectedTicks, FrameData[] frames)
        {
            TestAssert.Equal(expectedTicks.Length, frames.Length);
            for (int index = 0; index < expectedTicks.Length; index++)
            {
                TestAssert.Equal(expectedTicks[index], frames[index].Tick);
            }
        }
    }
}
