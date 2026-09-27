using LockstepArena.Client.Prediction.Verification;
using LockstepArena.Simulation;
using NUnit.Framework;

namespace LockstepArena.Client.Prediction.Editor.Tests
{
    public sealed class UnityClientPredictionGoldenTests
    {
        [Test]
        public void UnityExecutesCorrectPredictionGolden()
        {
            Gate12PredictionGoldenResult result = Gate12PredictionGoldenVector.RunCorrect();

            Assert.That(result.DirtyResults, Is.EqualTo(new[] { false, false, false }));
            Assert.That(result.PendingCountsAfterReconcile, Is.EqualTo(new[] { 2, 1, 0 }));
            AssertState(result.PredictedStatesAfterPredict[0], 101U, 0xD95809E1EB5CDDAAUL, -200, 0, 10100, 200, 0, 20100, 0, -200, 30100, 0, 200, 40100);
            AssertState(result.PredictedStatesAfterPredict[1], 102U, 0xA96B83267DD72A7DUL, -200, 100, 10101, 200, -100, 20101, 100, -200, 30101, -100, 200, 40101);
            AssertState(result.AuthoritativeStatesAfterReconcile[2], 103U, 0x386C4BB11A7EB7E0UL, -300, 100, 10102, 300, -100, 20102, 100, -300, 30102, -100, 300, 40102);
            AssertState(result.PredictedStatesAfterReconcile[2], 103U, 0x386C4BB11A7EB7E0UL, -300, 100, 10102, 300, -100, 20102, 100, -300, 30102, -100, 300, 40102);
        }

        [Test]
        public void UnityExecutesDirtyRollbackGolden()
        {
            Gate12PredictionGoldenResult result = Gate12PredictionGoldenVector.RunWrong();

            Assert.That(result.DirtyResults, Is.EqualTo(new[] { false, true, false }));
            Assert.That(result.PendingCountsAfterReconcile, Is.EqualTo(new[] { 2, 1, 0 }));
            AssertState(result.PredictedStatesAfterPredict[1], 102U, 0x8506E4507001B972UL, -200, 100, 10101, 200, -100, 20101, -100, -200, 30101, -100, 200, 40101);
            AssertState(result.PredictedStatesAfterPredict[2], 103U, 0x7D0D3A230618500FUL, -300, 100, 10102, 300, -100, 20102, -100, -300, 30102, -100, 300, 40102);
            AssertState(result.AuthoritativeStatesAfterReconcile[1], 102U, 0xA96B83267DD72A7DUL, -200, 100, 10101, 200, -100, 20101, 100, -200, 30101, -100, 200, 40101);
            AssertState(result.PredictedStatesAfterReconcile[1], 103U, 0x386C4BB11A7EB7E0UL, -300, 100, 10102, 300, -100, 20102, 100, -300, 30102, -100, 300, 40102);
            AssertState(result.AuthoritativeStatesAfterReconcile[2], 103U, 0x386C4BB11A7EB7E0UL, -300, 100, 10102, 300, -100, 20102, 100, -300, 30102, -100, 300, 40102);
        }

        private static void AssertState(
            BattleState state,
            uint tick,
            ulong digest,
            int x0,
            int z0,
            ushort aim0,
            int x1,
            int z1,
            ushort aim1,
            int x2,
            int z2,
            ushort aim2,
            int x3,
            int z3,
            ushort aim3)
        {
            Assert.That(state.Tick, Is.EqualTo(tick));
            Assert.That(StateDigest.Compute(state), Is.EqualTo(digest));
            AssertPlayer(state, 0, x0, z0, aim0);
            AssertPlayer(state, 1, x1, z1, aim1);
            AssertPlayer(state, 2, x2, z2, aim2);
            AssertPlayer(state, 3, x3, z3, aim3);
        }

        private static void AssertPlayer(BattleState state, int slot, int x, int z, ushort aim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slot));
            Assert.That(player.PositionX, Is.EqualTo(x));
            Assert.That(player.PositionZ, Is.EqualTo(z));
            Assert.That(player.Aim, Is.EqualTo(aim));
        }
    }
}
