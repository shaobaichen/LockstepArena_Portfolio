using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
using Google.Protobuf;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.LivePrediction.Tests
{
    internal static class PredictedTcpClientBattleRuntimeTests
    {
        public static readonly TestCase[] TransportTests =
        {
            new TestCase(nameof(LegacyTcpClientBattlePumpStillStepsItsOwnSimulation), LegacyTcpClientBattlePumpStillStepsItsOwnSimulation),
            new TestCase(nameof(TransportOnlyReceiveMapsWithoutPersistentSimulation), TransportOnlyReceiveMapsWithoutPersistentSimulation),
        };

        public static readonly TestCase[] CoreTests =
        {
            new TestCase(nameof(PredictedRuntimeRejectsLocalIdentityMismatch), PredictedRuntimeRejectsLocalIdentityMismatch),
            new TestCase(nameof(PredictedRuntimeRejectsNonPositiveCapacityLimits), PredictedRuntimeRejectsNonPositiveCapacityLimits),
            new TestCase(nameof(UpdateReconcilesAuthorityBeforeCreatingPrediction), UpdateReconcilesAuthorityBeforeCreatingPrediction),
            new TestCase(nameof(UpdateCreatesAtMostOnePredictionAndSendsOnlyLocalInput), UpdateCreatesAtMostOnePredictionAndSendsOnlyLocalInput),
            new TestCase(nameof(PredictionCapacityReturnsNotSentWithoutNetworkWrite), PredictionCapacityReturnsNotSentWithoutNetworkWrite),
            new TestCase(nameof(SendFailureAfterPredictionFaultsWithoutUndoingPrediction), SendFailureAfterPredictionFaultsWithoutUndoingPrediction),
            new TestCase(nameof(NeutralRemoteSeedUsesInitialPlayerAim), NeutralRemoteSeedUsesInitialPlayerAim),
            new TestCase(nameof(NewPredictionRepeatsLatestSuccessfullyReconciledRemoteInput), NewPredictionRepeatsLatestSuccessfullyReconciledRemoteInput),
            new TestCase(nameof(RetainedPredictionsRemainFieldForFieldUnchangedAfterNewAuthority), RetainedPredictionsRemainFieldForFieldUnchangedAfterNewAuthority),
            new TestCase(nameof(ConsecutiveDirtyAuthoritiesReplayDeterministically), ConsecutiveDirtyAuthoritiesReplayDeterministically),
            new TestCase(nameof(WrongRemoteFirePredictionRollsBackAndConvergesGameplayState), WrongRemoteFirePredictionRollsBackAndConvergesGameplayState),
        };

        public static readonly TestCase[] BoundedAuthorityTests =
        {
            new TestCase(nameof(ExistingAuthorityBacklogSkipsNetworkRead), ExistingAuthorityBacklogSkipsNetworkRead),
            new TestCase(nameof(CatchUpNeverExceedsMaxAuthoritativeFramesPerUpdate), CatchUpNeverExceedsMaxAuthoritativeFramesPerUpdate),
            new TestCase(nameof(CoalescedExactCapacityBatchIsAdmittedAtomicallyInTickOrder), CoalescedExactCapacityBatchIsAdmittedAtomicallyInTickOrder),
            new TestCase(nameof(OverCapacityBatchFaultsWithZeroAdmissionAndReconciliation), OverCapacityBatchFaultsWithZeroAdmissionAndReconciliation),
            new TestCase(nameof(InvalidRosterOrTickBatchFaultsBeforeQueueOrTimelineMutation), InvalidRosterOrTickBatchFaultsBeforeQueueOrTimelineMutation),
            new TestCase(nameof(ReplayReservationCountsHistoryPendingAndCandidateFrames), ReplayReservationCountsHistoryPendingAndCandidateFrames),
            new TestCase(nameof(ReplayCapacityRejectionAdmitsNoCandidateFrame), ReplayCapacityRejectionAdmitsNoCandidateFrame),
            new TestCase(nameof(ReplayHistoryNeverEvictsOrGrowsBeyondCapacity), ReplayHistoryNeverEvictsOrGrowsBeyondCapacity),
            new TestCase(nameof(ReplayReconstructsAuthoritativeStateAndDigest), ReplayReconstructsAuthoritativeStateAndDigest),
            new TestCase(nameof(CapturedReplaySurvivesDisposeWithoutSharingMutableHistory), CapturedReplaySurvivesDisposeWithoutSharingMutableHistory),
            new TestCase(nameof(GameplayAuthorityReplayReconstructsFullStateAndDigest), GameplayAuthorityReplayReconstructsFullStateAndDigest),
            new TestCase(nameof(ReplayCorruptionEntersStickyFailStop), ReplayCorruptionEntersStickyFailStop),
        };

        private static void LegacyTcpClientBattlePumpStillStepsItsOwnSimulation()
        {
            BattleState initialState = TcpSharedBattleSessionTests.CreateState(1);
            using var pair = new LoopbackPair();
            using var pump = new TcpClientBattlePump(
                pair.Client,
                initialState,
                4096,
                64,
                3,
                61);
            FrameData authority = CreateFrame(initialState.Roster, 100U, 1, 0, 1234);

            SendAuthority(pair.Accepted, pair.Client, authority);
            FrameData[] received = pump.PumpReceiveOnce();

            TestAssert.Equal(1, received.Length);
            TcpSharedBattleSessionTests.AssertFramesEqual(authority, received[0]);
            TestAssert.Equal(101U, pump.ClientState.Tick);
        }

        private static void PredictedRuntimeRejectsLocalIdentityMismatch()
        {
            BattleState state = TcpSharedBattleSessionTests.CreateState(2);
            using var first = new LoopbackPair();
            TestAssert.Throws<ArgumentException>(() => CreateRuntime(
                first.Client,
                state,
                new PlayerId(999UL),
                new PlayerSlot(0)));

            using var second = new LoopbackPair();
            TestAssert.Throws<ArgumentOutOfRangeException>(() => CreateRuntime(
                second.Client,
                state,
                state.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(2)));
        }

        private static void PredictedRuntimeRejectsNonPositiveCapacityLimits()
        {
            BattleState state = TcpSharedBattleSessionTests.CreateState(1);
            for (int capacityIndex = 0; capacityIndex < 4; capacityIndex++)
            {
                using var pair = new LoopbackPair();
                int prediction = capacityIndex == 0 ? 0 : 4;
                int perUpdate = capacityIndex == 1 ? 0 : 4;
                int pending = capacityIndex == 2 ? 0 : 8;
                int replay = capacityIndex == 3 ? 0 : 8;
                TestAssert.Throws<ArgumentOutOfRangeException>(() => new PredictedTcpClientBattleRuntime(
                    pair.Client,
                    state,
                    state.Roster.GetPlayerId(new PlayerSlot(0)),
                    new PlayerSlot(0),
                    prediction,
                    perUpdate,
                    pending,
                    replay,
                    4096,
                    64,
                    3,
                    61));
            }
        }

        private static void UpdateReconcilesAuthorityBeforeCreatingPrediction()
        {
            using var fixture = new PredictedClientFixture(2);
            fixture.SendAuthority(CreateFrame(
                fixture.State.Roster,
                100U,
                new InputFrame(100U, new PlayerSlot(0), 1, 0, 101),
                new InputFrame(100U, new PlayerSlot(1), -1, 0, 202)));

            PredictedClientUpdateResult result = fixture.Runtime.Update(
                new LocalInputSample(0, 1, 303));

            TestAssert.Equal(1, result.ReconciledAuthoritativeFrameCount);
            TestAssert.True(result.LocalPredictionSent);
            TestAssert.Equal(101U, fixture.Runtime.AuthoritativeState.Tick);
            TestAssert.Equal(102U, fixture.Runtime.PredictedState.Tick);
            InputFrame sent = fixture.ReadSubmission();
            TestAssert.Equal(101U, sent.Tick);
        }

        private static void UpdateCreatesAtMostOnePredictionAndSendsOnlyLocalInput()
        {
            using var fixture = new PredictedClientFixture(2);
            PredictedClientUpdateResult result = fixture.Runtime.Update(
                new LocalInputSample(1, -1, 444, true));

            TestAssert.True(result.LocalPredictionSent);
            TestAssert.Equal(1, fixture.Runtime.PendingPredictionCount);
            InputFrame sent = fixture.ReadSubmission();
            TestAssert.Equal(new PlayerSlot(0), sent.PlayerSlot);
            TestAssert.Equal((sbyte)1, sent.MoveX);
            TestAssert.Equal((sbyte)-1, sent.MoveZ);
            TestAssert.Equal((ushort)444, sent.Aim);
            TestAssert.True(sent.Fire);
        }

        private static void PredictionCapacityReturnsNotSentWithoutNetworkWrite()
        {
            using var fixture = new PredictedClientFixture(2, maxPredictionTicks: 1);
            fixture.Runtime.Update(new LocalInputSample(1, 0, 100));
            fixture.ReadSubmission();

            PredictedClientUpdateResult result = fixture.Runtime.Update(
                new LocalInputSample(0, 1, 101));

            TestAssert.True(!result.LocalPredictionSent);
            TestAssert.Equal(1, fixture.Runtime.PendingPredictionCount);
            TestAssert.True(!fixture.Accepted.Client.Poll(1_000, SelectMode.SelectRead));
        }

        private static void SendFailureAfterPredictionFaultsWithoutUndoingPrediction()
        {
            using var fixture = new PredictedClientFixture(1);
            fixture.Client.Client.Shutdown(SocketShutdown.Send);

            TestAssert.Throws<Exception>(() => fixture.Runtime.Update(
                new LocalInputSample(1, 0, 100)));
            TestAssert.Equal(1, fixture.Runtime.PendingPredictionCount);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Runtime.Update(null));
        }

        private static void NeutralRemoteSeedUsesInitialPlayerAim()
        {
            using var fixture = new PredictedClientFixture(2);
            fixture.Runtime.Update(new LocalInputSample(1, 0, 101));

            PlayerState remote = fixture.Runtime.PredictedState.GetPlayerState(new PlayerSlot(1));
            PlayerState initialRemote = fixture.State.GetPlayerState(new PlayerSlot(1));
            TestAssert.Equal(initialRemote.PositionX, remote.PositionX);
            TestAssert.Equal(initialRemote.PositionZ, remote.PositionZ);
            TestAssert.Equal(initialRemote.Aim, remote.Aim);
        }

        private static void NewPredictionRepeatsLatestSuccessfullyReconciledRemoteInput()
        {
            using var fixture = new PredictedClientFixture(2);
            fixture.SendAuthority(CreateFrame(
                fixture.State.Roster,
                100U,
                new InputFrame(100U, new PlayerSlot(0), 0, 0, 101),
                new InputFrame(100U, new PlayerSlot(1), -1, 1, 202, true)));
            fixture.Runtime.Update(null);
            fixture.Runtime.Update(new LocalInputSample(0, 0, 102));

            PlayerState remote = fixture.Runtime.PredictedState.GetPlayerState(new PlayerSlot(1));
            FrameData prediction = GetPredictedFrames(fixture.Runtime)[0];
            TestAssert.Equal(-100, remote.PositionX);
            TestAssert.Equal(200, remote.PositionZ);
            TestAssert.Equal((ushort)202, remote.Aim);
            TestAssert.True(prediction.GetInput(new PlayerSlot(1)).Fire);
        }

        private static void RetainedPredictionsRemainFieldForFieldUnchangedAfterNewAuthority()
        {
            using var fixture = new PredictedClientFixture(2);
            fixture.Runtime.Update(new LocalInputSample(1, 0, 101));
            fixture.ReadSubmission();
            fixture.Runtime.Update(new LocalInputSample(0, 1, 102));
            fixture.ReadSubmission();
            FrameData retainedBefore = GetPredictedFrames(fixture.Runtime)[1];

            fixture.SendAuthority(CreateFrame(
                fixture.State.Roster,
                100U,
                new InputFrame(100U, new PlayerSlot(0), 1, 0, 101),
                new InputFrame(100U, new PlayerSlot(1), -1, 0, 202)));
            fixture.Runtime.Update(null);

            FrameData retainedAfter = GetPredictedFrames(fixture.Runtime)[0];
            TestAssert.Same(retainedBefore, retainedAfter);
            TcpSharedBattleSessionTests.AssertFramesEqual(retainedBefore, retainedAfter);
        }

        private static void ConsecutiveDirtyAuthoritiesReplayDeterministically()
        {
            using var fixture = new PredictedClientFixture(2, maxAuthoritativeFramesPerUpdate: 1);
            fixture.Runtime.Update(new LocalInputSample(1, 0, 101));
            fixture.ReadSubmission();
            fixture.Runtime.Update(new LocalInputSample(0, 1, 102));
            fixture.ReadSubmission();
            fixture.SendAuthorities(
                CreateFrame(
                    fixture.State.Roster,
                    100U,
                    new InputFrame(100U, new PlayerSlot(0), 1, 0, 101),
                    new InputFrame(100U, new PlayerSlot(1), -1, 0, 201)),
                CreateFrame(
                    fixture.State.Roster,
                    101U,
                    new InputFrame(101U, new PlayerSlot(0), 0, 1, 102),
                    new InputFrame(101U, new PlayerSlot(1), 0, -1, 202)));

            PredictedClientUpdateResult first = fixture.Runtime.Update(null);
            PredictedClientUpdateResult second = fixture.Runtime.Update(null);
            TestAssert.Equal(1, first.DirtyFrameCount);
            TestAssert.Equal(1, second.DirtyFrameCount);
            TestAssert.Equal(102U, fixture.Runtime.AuthoritativeState.Tick);
            TestAssert.Equal(102U, fixture.Runtime.PredictedState.Tick);
        }

        private static void WrongRemoteFirePredictionRollsBackAndConvergesGameplayState()
        {
            BattleState initialState = CreateGameplayState();
            using var pair = new LoopbackPair();
            using PredictedTcpClientBattleRuntime runtime = CreateRuntime(
                pair.Client,
                initialState,
                initialState.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(0));
            runtime.Update(new LocalInputSample(0, 0, 0, false));
            FrameData authority = CreateFrame(
                initialState.Roster,
                0U,
                new InputFrame(0U, new PlayerSlot(0), 0, 0, 0, false),
                new InputFrame(0U, new PlayerSlot(1), 0, 0, 32_768, true));
            var expectedSimulation = new BattleSimulation(initialState);
            expectedSimulation.Step(authority);
            SendAuthority(pair.Accepted, pair.Client, authority);

            PredictedClientUpdateResult result = runtime.Update(null);

            TestAssert.Equal(1, result.DirtyFrameCount);
            TestAssert.Equal(75, runtime.AuthoritativeState.GetPlayerState(new PlayerSlot(0)).HitPoints);
            TestAssert.True(BattleStateValueComparer.HaveSameValue(
                expectedSimulation.State,
                runtime.PredictedState));
        }

        private static void ExistingAuthorityBacklogSkipsNetworkRead()
        {
            using var fixture = new PredictedClientFixture(
                1,
                maxAuthoritativeFramesPerUpdate: 1);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 100U, 1),
                SinglePlayerFrame(fixture.State.Roster, 101U, 2));
            PredictedClientUpdateResult first = fixture.Runtime.Update(null);
            TestAssert.Equal(1, first.ReconciledAuthoritativeFrameCount);
            TestAssert.Equal(1, fixture.Runtime.PendingAuthoritativeFrameCount);

            fixture.SendRawAuthorityPayload(new byte[] { 0xFF, 0xFF });
            PredictedClientUpdateResult second = fixture.Runtime.Update(null);
            TestAssert.Equal(1, second.ReconciledAuthoritativeFrameCount);
            TestAssert.Equal(0, fixture.Runtime.PendingAuthoritativeFrameCount);
            TestAssert.Throws<Exception>(() => fixture.Runtime.Update(null));
        }

        private static void CatchUpNeverExceedsMaxAuthoritativeFramesPerUpdate()
        {
            using var fixture = new PredictedClientFixture(
                1,
                maxAuthoritativeFramesPerUpdate: 2);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 100U, 1),
                SinglePlayerFrame(fixture.State.Roster, 101U, 2),
                SinglePlayerFrame(fixture.State.Roster, 102U, 3));

            PredictedClientUpdateResult first = fixture.Runtime.Update(null);
            TestAssert.Equal(2, first.ReconciledAuthoritativeFrameCount);
            TestAssert.Equal(1, fixture.Runtime.PendingAuthoritativeFrameCount);
            PredictedClientUpdateResult second = fixture.Runtime.Update(null);
            TestAssert.Equal(1, second.ReconciledAuthoritativeFrameCount);
            TestAssert.Equal(103U, fixture.Runtime.AuthoritativeState.Tick);
        }

        private static void CoalescedExactCapacityBatchIsAdmittedAtomicallyInTickOrder()
        {
            using var fixture = new PredictedClientFixture(
                1,
                maxAuthoritativeFramesPerUpdate: 1,
                maxPendingAuthoritativeFrames: 3,
                maxReplayFrames: 3);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 100U, 1),
                SinglePlayerFrame(fixture.State.Roster, 101U, 2),
                SinglePlayerFrame(fixture.State.Roster, 102U, 3));

            fixture.Runtime.Update(null);
            TestAssert.Equal(101U, fixture.Runtime.AuthoritativeState.Tick);
            TestAssert.Equal(2, fixture.Runtime.PendingAuthoritativeFrameCount);
            TestAssert.Equal(1, fixture.Runtime.ReplayFrameCount);
        }

        private static void OverCapacityBatchFaultsWithZeroAdmissionAndReconciliation()
        {
            using var fixture = new PredictedClientFixture(
                1,
                maxPendingAuthoritativeFrames: 2,
                maxReplayFrames: 8);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 100U, 1),
                SinglePlayerFrame(fixture.State.Roster, 101U, 2),
                SinglePlayerFrame(fixture.State.Roster, 102U, 3));

            TestAssert.Throws<InvalidOperationException>(() => fixture.Runtime.Update(null));
            TestAssert.Equal(100U, fixture.Runtime.AuthoritativeState.Tick);
            TestAssert.Equal(0, fixture.Runtime.PendingAuthoritativeFrameCount);
            TestAssert.Equal(0, fixture.Runtime.ReplayFrameCount);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Runtime.Update(null));
        }

        private static void InvalidRosterOrTickBatchFaultsBeforeQueueOrTimelineMutation()
        {
            using (var fixture = new PredictedClientFixture(1))
            {
                BattleState other = TcpSharedBattleSessionTests.CreateState(2);
                fixture.SendAuthority(SinglePlayerFrame(other.Roster, 100U, 1, playerCount: 2));
                TestAssert.Throws<Exception>(() => fixture.Runtime.Update(null));
                AssertUnchangedFrontier(fixture.Runtime, 100U);
            }

            using (var fixture = new PredictedClientFixture(1))
            {
                fixture.SendAuthority(SinglePlayerFrame(fixture.State.Roster, 101U, 1));
                TestAssert.Throws<InvalidOperationException>(() => fixture.Runtime.Update(null));
                AssertUnchangedFrontier(fixture.Runtime, 100U);
            }
        }

        private static void ReplayReservationCountsHistoryPendingAndCandidateFrames()
        {
            using var fixture = new PredictedClientFixture(
                1,
                maxAuthoritativeFramesPerUpdate: 1,
                maxPendingAuthoritativeFrames: 3,
                maxReplayFrames: 3);
            fixture.SendAuthority(SinglePlayerFrame(fixture.State.Roster, 100U, 1));
            fixture.Runtime.Update(null);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 101U, 2),
                SinglePlayerFrame(fixture.State.Roster, 102U, 3));
            fixture.Runtime.Update(null);

            TestAssert.Equal(2, fixture.Runtime.ReplayFrameCount);
            TestAssert.Equal(1, fixture.Runtime.PendingAuthoritativeFrameCount);
            TestAssert.Equal(102U, fixture.Runtime.AuthoritativeState.Tick);
        }

        private static void ReplayCapacityRejectionAdmitsNoCandidateFrame()
        {
            using var fixture = new PredictedClientFixture(
                1,
                maxReplayFrames: 2);
            fixture.SendAuthority(SinglePlayerFrame(fixture.State.Roster, 100U, 1));
            fixture.Runtime.Update(null);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 101U, 2),
                SinglePlayerFrame(fixture.State.Roster, 102U, 3));

            TestAssert.Throws<InvalidOperationException>(() => fixture.Runtime.Update(null));
            TestAssert.Equal(1, fixture.Runtime.ReplayFrameCount);
            TestAssert.Equal(0, fixture.Runtime.PendingAuthoritativeFrameCount);
            TestAssert.Equal(101U, fixture.Runtime.AuthoritativeState.Tick);
        }

        private static void ReplayHistoryNeverEvictsOrGrowsBeyondCapacity()
        {
            using var fixture = new PredictedClientFixture(1, maxReplayFrames: 2);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 100U, 1),
                SinglePlayerFrame(fixture.State.Roster, 101U, 2));
            fixture.Runtime.Update(null);
            TestAssert.Equal(2, fixture.Runtime.ReplayFrameCount);

            fixture.SendAuthority(SinglePlayerFrame(fixture.State.Roster, 102U, 3));
            TestAssert.Throws<InvalidOperationException>(() => fixture.Runtime.Update(null));
            TestAssert.Equal(2, fixture.Runtime.ReplayFrameCount);
        }

        private static void ReplayReconstructsAuthoritativeStateAndDigest()
        {
            using var fixture = new PredictedClientFixture(1, maxReplayFrames: 3);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 100U, 1),
                SinglePlayerFrame(fixture.State.Roster, 101U, 2));
            fixture.Runtime.Update(null);

            BattleState reconstructed = fixture.Runtime.ReconstructAuthoritativeState();
            AssertStatesEqual(fixture.Runtime.AuthoritativeState, reconstructed);
            TestAssert.Equal(
                StateDigest.Compute(fixture.Runtime.AuthoritativeState),
                StateDigest.Compute(reconstructed));
        }

        private static void CapturedReplaySurvivesDisposeWithoutSharingMutableHistory()
        {
            using var fixture = new PredictedClientFixture(1);
            fixture.SendAuthorities(
                SinglePlayerFrame(fixture.State.Roster, 100U, 1),
                SinglePlayerFrame(fixture.State.Roster, 101U, 2));
            fixture.Runtime.Update(null);
            BattleReplaySnapshot snapshot = fixture.Runtime.CaptureReplaySnapshot();
            BattleState initial = snapshot.InitialState;
            IReadOnlyList<FrameData> frames = snapshot.AuthoritativeFrames;
            TestAssert.Same(fixture.State, initial);
            TestAssert.Equal(2, frames.Count);
            TestAssert.Equal(100U, frames[0].Tick);
            TestAssert.Equal(101U, frames[1].Tick);
            TestAssert.True(frames is not FrameData[]);
            TestAssert.Throws<NotSupportedException>(() => ((IList<FrameData>)frames)[0] = frames[1]);
            fixture.SendAuthority(SinglePlayerFrame(fixture.State.Roster, 102U, 3));
            fixture.Runtime.Update(null);
            TestAssert.Equal(2, frames.Count);
            fixture.Runtime.Dispose();
            var simulation = new BattleSimulation(initial);
            foreach (FrameData frame in frames) simulation.Step(frame);
            TestAssert.Equal(102U, simulation.State.Tick);
            var expected = new BattleSimulation(fixture.State);
            expected.Step(SinglePlayerFrame(fixture.State.Roster, 100U, 1));
            expected.Step(SinglePlayerFrame(fixture.State.Roster, 101U, 2));
            AssertStatesEqual(expected.State, simulation.State);
            TestAssert.Equal(StateDigest.Compute(expected.State), StateDigest.Compute(simulation.State));
        }

        private static void GameplayAuthorityReplayReconstructsFullStateAndDigest()
        {
            BattleState initialState = CreateGameplayState();
            using var pair = new LoopbackPair();
            using PredictedTcpClientBattleRuntime runtime = CreateRuntime(
                pair.Client,
                initialState,
                initialState.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(0));
            FrameData authority = CreateFrame(
                initialState.Roster,
                0U,
                new InputFrame(0U, new PlayerSlot(0), 0, 0, 16_384, true),
                new InputFrame(0U, new PlayerSlot(1), 0, 0, 32_768, false));
            SendAuthority(pair.Accepted, pair.Client, authority);
            runtime.Update(null);

            BattleState reconstructed = runtime.ReconstructAuthoritativeState();

            TestAssert.Equal(1, runtime.AuthoritativeState.ProjectileCount);
            TestAssert.True(BattleStateValueComparer.HaveSameValue(
                runtime.AuthoritativeState,
                reconstructed));
            TestAssert.Equal(
                StateDigest.Compute(runtime.AuthoritativeState),
                StateDigest.Compute(reconstructed));
        }

        private static void ReplayCorruptionEntersStickyFailStop()
        {
            using var fixture = new PredictedClientFixture(1, maxReplayFrames: 3);
            fixture.SendAuthority(SinglePlayerFrame(fixture.State.Roster, 100U, 1));
            fixture.Runtime.Update(null);
            BattleState authoritativeBefore = fixture.Runtime.AuthoritativeState;
            BattleState predictedBefore = fixture.Runtime.PredictedState;
            CorruptReplay(fixture.Runtime, fixture.State.Roster);

            TestAssert.Throws<InvalidOperationException>(
                () => fixture.Runtime.ReconstructAuthoritativeState());
            TestAssert.Same(authoritativeBefore, fixture.Runtime.AuthoritativeState);
            TestAssert.Same(predictedBefore, fixture.Runtime.PredictedState);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Runtime.Update(null));
        }

        private static void TransportOnlyReceiveMapsWithoutPersistentSimulation()
        {
            BattleState initialState = TcpSharedBattleSessionTests.CreateState(1);
            using var pair = new LoopbackPair();
            MethodInfo factory = typeof(TcpClientBattlePump).GetMethod(
                "CreateTransportOnly",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(TcpClient),
                    typeof(ActiveRoster),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                },
                modifiers: null) ?? throw new InvalidOperationException(
                    "Missing internal CreateTransportOnly API.");
            MethodInfo receive = typeof(TcpClientBattlePump).GetMethod(
                "PumpReceiveTransportOnlyOnce",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null) ?? throw new InvalidOperationException(
                    "Missing internal PumpReceiveTransportOnlyOnce API.");

            using var pump = (TcpClientBattlePump)(factory.Invoke(
                null,
                new object[] { pair.Client, initialState.Roster, 4096, 64, 3, 61 }) ??
                throw new InvalidOperationException("Transport-only factory returned null."));

            FieldInfo[] fields = typeof(TcpClientBattlePump).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                TestAssert.True(fields[index].FieldType != typeof(BattleSimulation));
            }

            FrameData authority = CreateFrame(initialState.Roster, 100U, -1, 1, 4321);
            SendAuthority(pair.Accepted, pair.Client, authority);
            FrameData[] received = (FrameData[])(receive.Invoke(pump, null) ??
                throw new InvalidOperationException("Transport-only receive returned null."));

            TestAssert.Equal(1, received.Length);
            TcpSharedBattleSessionTests.AssertFramesEqual(authority, received[0]);
            TestAssert.Equal(100U, initialState.Tick);
        }

        private static FrameData CreateFrame(
            ActiveRoster roster,
            uint tick,
            sbyte moveX,
            sbyte moveZ,
            ushort aim)
        {
            return FrameData.Create(
                roster,
                tick,
                new[] { new InputFrame(tick, new PlayerSlot(0), moveX, moveZ, aim) });
        }

        internal static FrameData CreateFrame(
            ActiveRoster roster,
            uint tick,
            params InputFrame[] inputs)
        {
            return FrameData.Create(roster, tick, inputs);
        }

        private static PredictedTcpClientBattleRuntime CreateRuntime(
            TcpClient client,
            BattleState state,
            PlayerId playerId,
            PlayerSlot playerSlot,
            int maxPredictionTicks = 4,
            int maxAuthoritativeFramesPerUpdate = 4,
            int maxPendingAuthoritativeFrames = 8,
            int maxReplayFrames = 8)
        {
            return new PredictedTcpClientBattleRuntime(
                client,
                state,
                playerId,
                playerSlot,
                maxPredictionTicks,
                maxAuthoritativeFramesPerUpdate,
                maxPendingAuthoritativeFrames,
                maxReplayFrames,
                4096,
                4096,
                3,
                4093);
        }

        private static BattleState CreateGameplayState()
        {
            var roster = new ActiveRoster(new[] { new PlayerId(101UL), new PlayerId(202UL) });
            var gameplay = new GameplayConfig(
                100,
                25,
                100,
                2,
                200,
                10,
                50,
                10,
                30,
                5,
                100,
                1,
                1,
                2);
            var arena = new ArenaConfig(
                "prediction-test",
                new ArenaRectangle(-1_000, 1_000, -1_000, 1_000),
                new[] { new ArenaPoint(-100, 0), new ArenaPoint(100, 0) },
                Array.Empty<ArenaRectangle>());
            var definition = new BattleDefinition(gameplay, arena);
            return new BattleState(
                0U,
                roster,
                new[]
                {
                    new PlayerState(-100, 0, 0, 100, 0, 0),
                    new PlayerState(100, 0, 32_768, 100, 0, 0),
                },
                definition,
                BattlePhase.Playing,
                0U,
                100U,
                RoundResult.None,
                null,
                Array.Empty<ProjectileState>(),
                1UL);
        }

        private static FrameData SinglePlayerFrame(
            ActiveRoster roster,
            uint tick,
            ushort aim,
            int playerCount = 1)
        {
            var inputs = new InputFrame[playerCount];
            for (int index = 0; index < playerCount; index++)
            {
                inputs[index] = new InputFrame(
                    tick,
                    new PlayerSlot(index),
                    index == 0 ? (sbyte)1 : (sbyte)0,
                    0,
                    checked((ushort)(aim + index)));
            }

            return FrameData.Create(roster, tick, inputs);
        }

        private static void AssertUnchangedFrontier(
            PredictedTcpClientBattleRuntime runtime,
            uint tick)
        {
            TestAssert.Equal(tick, runtime.AuthoritativeState.Tick);
            TestAssert.Equal(tick, runtime.PredictedState.Tick);
            TestAssert.Equal(0, runtime.PendingPredictionCount);
            TestAssert.Equal(0, runtime.PendingAuthoritativeFrameCount);
            TestAssert.Equal(0, runtime.ReplayFrameCount);
        }

        internal static void AssertStatesEqual(BattleState expected, BattleState actual)
        {
            TestAssert.True(BattleStateValueComparer.HaveSameValue(expected, actual));
        }

        private static void CorruptReplay(
            PredictedTcpClientBattleRuntime runtime,
            ActiveRoster roster)
        {
            FieldInfo? replayField = typeof(PredictedTcpClientBattleRuntime).GetField(
                "_replay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (replayField is null || replayField.FieldType != typeof(FrameData[]))
            {
                throw new InvalidOperationException("Replay container was not found.");
            }

            replayField.SetValue(runtime, new[] { SinglePlayerFrame(roster, 101U, 999) });
        }

        private static FrameData[] GetPredictedFrames(
            PredictedTcpClientBattleRuntime runtime)
        {
            FieldInfo? timelineField = null;
            FieldInfo[] runtimeFields = typeof(PredictedTcpClientBattleRuntime).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < runtimeFields.Length; index++)
            {
                if (runtimeFields[index].FieldType.FullName ==
                    "LockstepArena.Client.Prediction.ClientPredictionTimeline")
                {
                    timelineField = runtimeFields[index];
                    break;
                }
            }

            object timeline = timelineField?.GetValue(runtime) ??
                throw new InvalidOperationException("Prediction timeline was not found.");
            FieldInfo? historyField = null;
            FieldInfo[] timelineFields = timeline.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < timelineFields.Length; index++)
            {
                if (timelineFields[index].FieldType.IsArray &&
                    timelineFields[index].FieldType.GetElementType()?.Name == "PredictionRecord")
                {
                    historyField = timelineFields[index];
                    break;
                }
            }

            var records = (Array)(historyField?.GetValue(timeline) ??
                throw new InvalidOperationException("Prediction history was not found."));
            var frames = new FrameData[records.Length];
            for (int index = 0; index < records.Length; index++)
            {
                object record = records.GetValue(index) ??
                    throw new InvalidOperationException("Prediction record was null.");
                frames[index] = (FrameData)(record.GetType().GetProperty(
                    "PredictedFrame",
                    BindingFlags.Instance | BindingFlags.Public)?.GetValue(record) ??
                    throw new InvalidOperationException("Predicted frame was not found."));
            }

            return frames;
        }

        private static void SendAuthority(
            TcpClient serverClient,
            TcpClient receivingClient,
            FrameData frame)
        {
            byte[] payload = ProtocolMapper.ToWire(frame).ToByteArray();
            byte[] framed = TcpSharedBattleSessionTests.Frame(payload);
            NetworkStream stream = serverClient.GetStream();
            stream.Write(framed, 0, framed.Length);
            TcpSharedBattleSessionTests.WaitForReadable(receivingClient.Client);
        }
    }

    internal sealed class PredictedClientFixture : IDisposable
    {
        private readonly LoopbackPair _pair;
        private readonly LengthPrefixedFrameDecoder _submissionDecoder =
            new LengthPrefixedFrameDecoder(4096);

        public PredictedClientFixture(
            int playerCount,
            int maxPredictionTicks = 4,
            int maxAuthoritativeFramesPerUpdate = 4,
            int maxPendingAuthoritativeFrames = 8,
            int maxReplayFrames = 8)
        {
            State = TcpSharedBattleSessionTests.CreateState(playerCount);
            _pair = new LoopbackPair();
            Client = _pair.Client;
            Accepted = _pair.Accepted;
            Runtime = new PredictedTcpClientBattleRuntime(
                Client,
                State,
                State.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(0),
                maxPredictionTicks,
                maxAuthoritativeFramesPerUpdate,
                maxPendingAuthoritativeFrames,
                maxReplayFrames,
                4096,
                4096,
                3,
                4093);
        }

        public BattleState State { get; }

        public TcpClient Client { get; }

        public TcpClient Accepted { get; }

        public PredictedTcpClientBattleRuntime Runtime { get; }

        public void SendAuthority(FrameData frame)
        {
            SendAuthorities(frame);
        }

        public void SendAuthorities(params FrameData[] frames)
        {
            var framedMessages = new byte[frames.Length][];
            int totalLength = 0;
            for (int index = 0; index < frames.Length; index++)
            {
                byte[] payload = ProtocolMapper.ToWire(frames[index]).ToByteArray();
                framedMessages[index] = TcpSharedBattleSessionTests.Frame(payload);
                totalLength = checked(totalLength + framedMessages[index].Length);
            }

            var streamBytes = new byte[totalLength];
            int offset = 0;
            for (int index = 0; index < framedMessages.Length; index++)
            {
                byte[] framed = framedMessages[index];
                Array.Copy(framed, 0, streamBytes, offset, framed.Length);
                offset += framed.Length;
            }

            NetworkStream stream = Accepted.GetStream();
            stream.Write(streamBytes, 0, streamBytes.Length);
            TcpSharedBattleSessionTests.WaitForReadable(Client.Client);
        }

        public void SendRawAuthorityPayload(byte[] payload)
        {
            byte[] framed = TcpSharedBattleSessionTests.Frame(payload);
            NetworkStream stream = Accepted.GetStream();
            stream.Write(framed, 0, framed.Length);
            TcpSharedBattleSessionTests.WaitForReadable(Client.Client);
        }

        public InputFrame ReadSubmission()
        {
            TcpSharedBattleSessionTests.WaitForReadable(Accepted.Client);
            var buffer = new byte[128];
            int bytesRead = Accepted.GetStream().Read(buffer, 3, buffer.Length - 3);
            byte[][] payloads = _submissionDecoder.Feed(buffer, 3, bytesRead);
            if (payloads.Length != 1)
            {
                throw new InvalidOperationException("Expected exactly one submission payload.");
            }

            PlayerInputSubmissionMessage message =
                PlayerInputSubmissionMessage.Parser.ParseFrom(payloads[0]);
            return ProtocolMapper.ToDomain(message).Input;
        }

        public void Dispose()
        {
            Runtime.Dispose();
            _pair.Dispose();
        }
    }
}
