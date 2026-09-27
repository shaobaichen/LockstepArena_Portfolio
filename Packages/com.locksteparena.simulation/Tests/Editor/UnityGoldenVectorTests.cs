using LockstepArena.Simulation.Verification;
using NUnit.Framework;

namespace LockstepArena.Simulation.Editor.Tests
{
    public sealed class UnityGoldenVectorTests
    {
        [Test]
        public void UnityExecutesApprovedGoldenVector()
        {
            Gate3GoldenVectorResult result = Gate3GoldenVector.Run();

            Assert.That(
                typeof(BattleSimulation).Assembly.GetName().Name,
                Is.EqualTo("LockstepArena.Simulation"));
            Assert.That(result.State.Tick, Is.EqualTo(1_000U));
            Assert.That(result.State.PlayerCount, Is.EqualTo(4));
            AssertPlayer(result.State, 0, 0x0102030405060708UL, 0, -3_000, 13_086);
            AssertPlayer(result.State, 1, 0x000000000000002AUL, 0, 3_000, 8_699);
            AssertPlayer(result.State, 2, 0xFFEEDDCCBBAA0099UL, -2_500, -2_000, 51_320);
            AssertPlayer(result.State, 3, 0x00000000000F4243UL, 2_500, 2_000, 62_539);
            Assert.That(result.Digest, Is.EqualTo(0x89A7DD66F8D9E871UL));
        }

        private static void AssertPlayer(
            BattleState state,
            int slotValue,
            ulong expectedPlayerId,
            int expectedX,
            int expectedZ,
            ushort expectedAim)
        {
            PlayerSlot slot = new PlayerSlot(slotValue);
            PlayerState player = state.GetPlayerState(slot);
            Assert.That(state.Roster.GetPlayerId(slot).Value, Is.EqualTo(expectedPlayerId));
            Assert.That(player.PositionX, Is.EqualTo(expectedX));
            Assert.That(player.PositionZ, Is.EqualTo(expectedZ));
            Assert.That(player.Aim, Is.EqualTo(expectedAim));
        }
    }
}
