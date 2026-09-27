using LockstepArena.Client.Demo;
using LockstepArena.Simulation;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class Gate14DemoGoldenTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(GoldenCompletesFullNicknameRoomReadyStartBattleFlow), GoldenCompletesFullNicknameRoomReadyStartBattleFlow),
            new TestCase(nameof(GoldenBothClientsDirtyRollbackAndConvergeAtStateFour), GoldenBothClientsDirtyRollbackAndConvergeAtStateFour),
            new TestCase(nameof(GoldenSettlementReturnRemovesRoomAndPreservesSessions), GoldenSettlementReturnRemovesRoomAndPreservesSessions),
            new TestCase(nameof(AlternateControlAndBattleSegmentationProducesSameGoldenResult), AlternateControlAndBattleSegmentationProducesSameGoldenResult),
        };

        private static void GoldenCompletesFullNicknameRoomReadyStartBattleFlow()
        {
            Gate14DemoGoldenResult result = Gate14DemoGoldenVector.Run(false);
            TestAssert.Equal((ulong)1, result.BravoSessionId);
            TestAssert.Equal((ulong)2, result.AlphaSessionId);
            TestAssert.Equal((ulong)1, result.RoomId);
            TestAssert.Equal((ulong)1, result.BattleId);
            TestAssert.Equal(new PlayerId(2UL), result.InitialState.Roster.GetPlayerId(new PlayerSlot(0)));
            TestAssert.Equal(new PlayerId(1UL), result.InitialState.Roster.GetPlayerId(new PlayerSlot(1)));
        }

        private static void GoldenBothClientsDirtyRollbackAndConvergeAtStateFour()
        {
            Gate14DemoGoldenResult result = Gate14DemoGoldenVector.Run(false);
            AssertState(result.InitialState, 0U, -300, 0, 1000, 300, 0, 2000, 0xD08BA63E403C71AFUL);
            TestAssert.Equal(4, result.ServerStates.Length);
            AssertState(result.ServerStates[0], 1U, -400, 0, 10100, 400, 0, 20100, 0xF82D3BE4A98024B5UL);
            AssertState(result.ServerStates[1], 2U, -400, 100, 10101, 400, -100, 20101, 0xA4DAA5FBE7E940EDUL);
            AssertState(result.ServerStates[2], 3U, -300, 100, 10102, 300, -100, 20102, 0x7A653646579E6AECUL);
            AssertState(result.ServerStates[3], 4U, -300, 0, 10103, 300, 0, 20103, 0xD8E54FF828A4C670UL);
            AssertDirty(result.AlphaDirtyFrames);
            AssertDirty(result.BravoDirtyFrames);
            TestAssert.Equal(4U, result.AlphaAuthoritativeTick);
            TestAssert.Equal(4U, result.AlphaPredictedTick);
            TestAssert.Equal(4U, result.BravoAuthoritativeTick);
            TestAssert.Equal(4U, result.BravoPredictedTick);
            TestAssert.Equal(0xD8E54FF828A4C670UL, result.AlphaAuthoritativeDigest);
            TestAssert.Equal(0xD8E54FF828A4C670UL, result.AlphaPredictedDigest);
            TestAssert.Equal(0xD8E54FF828A4C670UL, result.BravoAuthoritativeDigest);
            TestAssert.Equal(0xD8E54FF828A4C670UL, result.BravoPredictedDigest);
            TestAssert.Equal(4, result.AlphaReplayFrameCount);
            TestAssert.Equal(4, result.BravoReplayFrameCount);
        }

        private static void GoldenSettlementReturnRemovesRoomAndPreservesSessions()
        {
            Gate14DemoGoldenResult result = Gate14DemoGoldenVector.Run(false);
            TestAssert.True(result.AlphaSettlementVerified);
            TestAssert.True(result.BravoSettlementVerified);
            TestAssert.Equal(DemoClientPhase.Lobby, result.AlphaFinalPhase);
            TestAssert.Equal(DemoClientPhase.Lobby, result.BravoFinalPhase);
            TestAssert.Equal(2, result.RetainedSessionCount);
            TestAssert.Equal(0, result.RetainedRoomCount);
        }

        private static void AlternateControlAndBattleSegmentationProducesSameGoldenResult()
        {
            Gate14DemoGoldenResult first = Gate14DemoGoldenVector.Run(false);
            Gate14DemoGoldenResult second = Gate14DemoGoldenVector.Run(true);
            TestAssert.Equal(first.BravoSessionId, second.BravoSessionId);
            TestAssert.Equal(first.AlphaSessionId, second.AlphaSessionId);
            TestAssert.Equal(first.RoomId, second.RoomId);
            TestAssert.Equal(first.BattleId, second.BattleId);
            TestAssert.Equal(first.ServerStates.Length, second.ServerStates.Length);
            for (int index = 0; index < first.ServerStates.Length; index++) AssertStatesEqual(first.ServerStates[index], second.ServerStates[index]);
            AssertBooleanArraysEqual(first.AlphaDirtyFrames, second.AlphaDirtyFrames);
            AssertBooleanArraysEqual(first.BravoDirtyFrames, second.BravoDirtyFrames);
            TestAssert.Equal(first.AlphaAuthoritativeDigest, second.AlphaAuthoritativeDigest);
            TestAssert.Equal(first.AlphaPredictedDigest, second.AlphaPredictedDigest);
            TestAssert.Equal(first.BravoAuthoritativeDigest, second.BravoAuthoritativeDigest);
            TestAssert.Equal(first.BravoPredictedDigest, second.BravoPredictedDigest);
            TestAssert.Equal(first.RetainedSessionCount, second.RetainedSessionCount);
            TestAssert.Equal(first.RetainedRoomCount, second.RetainedRoomCount);
        }

        private static void AssertState(BattleState state, uint tick, int x0, int z0, ushort aim0, int x1, int z1, ushort aim1, ulong digest)
        {
            TestAssert.Equal(tick, state.Tick);
            PlayerState first = state.GetPlayerState(new PlayerSlot(0));
            PlayerState second = state.GetPlayerState(new PlayerSlot(1));
            TestAssert.Equal(x0, first.PositionX);
            TestAssert.Equal(z0, first.PositionZ);
            TestAssert.Equal(aim0, first.Aim);
            TestAssert.Equal(x1, second.PositionX);
            TestAssert.Equal(z1, second.PositionZ);
            TestAssert.Equal(aim1, second.Aim);
            TestAssert.Equal(digest, StateDigest.Compute(state));
        }

        private static void AssertDirty(bool[] values)
        {
            TestAssert.Equal(4, values.Length);
            for (int index = 0; index < values.Length; index++) TestAssert.True(values[index]);
        }

        private static void AssertStatesEqual(BattleState left, BattleState right)
        {
            TestAssert.Equal(left.Tick, right.Tick);
            TestAssert.Equal(StateDigest.Compute(left), StateDigest.Compute(right));
        }

        private static void AssertBooleanArraysEqual(bool[] left, bool[] right)
        {
            TestAssert.Equal(left.Length, right.Length);
            for (int index = 0; index < left.Length; index++) TestAssert.Equal(left[index], right[index]);
        }
    }
}
