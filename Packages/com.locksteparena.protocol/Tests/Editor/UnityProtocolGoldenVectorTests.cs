using LockstepArena.Protocol.Verification;
using LockstepArena.Simulation;
using NUnit.Framework;

namespace LockstepArena.Protocol.Editor.Tests
{
    public sealed class UnityProtocolGoldenVectorTests
    {
        [Test]
        public void UnityExecutesGate5ProtocolRoundTripGoldenVector()
        {
            Gate5ProtocolGoldenResult result = Gate5ProtocolGoldenVector.Run();

            Assert.That(typeof(ProtocolMapper).Assembly.GetName().Name, Is.EqualTo("LockstepArena.Protocol"));
            Assert.That(result.MappedFramesA.Length, Is.EqualTo(12));
            Assert.That(result.MappedFramesB.Length, Is.EqualTo(12));
            Assert.That(result.DigestsA, Is.EqualTo(result.DigestsB));
            for (int index = 0; index < 12; index++)
            {
                Assert.That(result.SerializedFramesA[index], Is.Not.EqualTo(result.SerializedFramesB[index]));
                AssertCanonicalFrame(result.MappedFramesA[index], index);
                AssertCanonicalFrame(result.MappedFramesB[index], index);
            }

            AssertFinalState(result.FinalStateA);
            AssertFinalState(result.FinalStateB);
            Assert.That(StateDigest.Compute(result.FinalStateA), Is.EqualTo(0x5CFABE84CC00E1C3UL));
            Assert.That(StateDigest.Compute(result.FinalStateB), Is.EqualTo(0x5CFABE84CC00E1C3UL));
        }

        private static void AssertCanonicalFrame(FrameData frame, int expectedTick)
        {
            Assert.That(frame.Tick, Is.EqualTo(checked((uint)expectedTick)));
            Assert.That(frame.InputCount, Is.EqualTo(4));
            for (int slotValue = 0; slotValue < 4; slotValue++)
            {
                Assert.That(
                    frame.GetInput(new PlayerSlot(slotValue)).PlayerSlot.Value,
                    Is.EqualTo(slotValue));
            }
        }

        private static void AssertFinalState(BattleState state)
        {
            Assert.That(state.Tick, Is.EqualTo(12U));
            AssertPlayer(state, 0, 200, 0, 11_001);
            AssertPlayer(state, 1, -200, 0, 22_002);
            AssertPlayer(state, 2, 0, 200, 33_003);
            AssertPlayer(state, 3, 0, -200, 44_004);
        }

        private static void AssertPlayer(
            BattleState state,
            int slotValue,
            int expectedX,
            int expectedZ,
            ushort expectedAim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slotValue));
            Assert.That(player.PositionX, Is.EqualTo(expectedX));
            Assert.That(player.PositionZ, Is.EqualTo(expectedZ));
            Assert.That(player.Aim, Is.EqualTo(expectedAim));
        }
    }
}
