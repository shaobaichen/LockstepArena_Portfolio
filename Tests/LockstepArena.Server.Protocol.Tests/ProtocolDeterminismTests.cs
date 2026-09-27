using System;
using LockstepArena.Protocol.Verification;
using LockstepArena.Simulation;

namespace LockstepArena.Server.Protocol.Tests
{
    internal static class ProtocolDeterminismTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(DifferentRepeatedInputOrderProducesDifferentBytes), DifferentRepeatedInputOrderProducesDifferentBytes),
            new TestCase(nameof(EquivalentMappedFramesProduceEqualDomainDigests), EquivalentMappedFramesProduceEqualDomainDigests),
            new TestCase(nameof(TwelveTickRoundTripMatchesApprovedGolden), TwelveTickRoundTripMatchesApprovedGolden),
        };

        private static void DifferentRepeatedInputOrderProducesDifferentBytes()
        {
            Gate5ProtocolGoldenResult result = Gate5ProtocolGoldenVector.Run();
            TestAssert.Equal(12, result.SerializedFramesA.Length);
            TestAssert.Equal(12, result.SerializedFramesB.Length);

            for (int index = 0; index < 12; index++)
            {
                TestAssert.True(!BytesEqual(result.SerializedFramesA[index], result.SerializedFramesB[index]));
            }
        }

        private static void EquivalentMappedFramesProduceEqualDomainDigests()
        {
            Gate5ProtocolGoldenResult result = Gate5ProtocolGoldenVector.Run();
            TestAssert.Equal(12, result.MappedFramesA.Length);
            TestAssert.Equal(12, result.MappedFramesB.Length);
            TestAssert.Equal(12, result.DigestsA.Length);
            TestAssert.Equal(12, result.DigestsB.Length);

            for (int tick = 0; tick < 12; tick++)
            {
                AssertCanonicalFrame(result.MappedFramesA[tick], tick);
                AssertCanonicalFrame(result.MappedFramesB[tick], tick);
                TestAssert.Equal(result.DigestsA[tick], result.DigestsB[tick]);
            }
        }

        private static void TwelveTickRoundTripMatchesApprovedGolden()
        {
            Gate5ProtocolGoldenResult result = Gate5ProtocolGoldenVector.Run();
            AssertFinalState(result.FinalStateA);
            AssertFinalState(result.FinalStateB);
            TestAssert.Equal(0x5CFABE84CC00E1C3UL, StateDigest.Compute(result.FinalStateA));
            TestAssert.Equal(0x5CFABE84CC00E1C3UL, StateDigest.Compute(result.FinalStateB));
        }

        private static void AssertCanonicalFrame(FrameData frame, int expectedTick)
        {
            TestAssert.Equal(checked((uint)expectedTick), frame.Tick);
            TestAssert.Equal(4, frame.InputCount);
            for (int slotValue = 0; slotValue < 4; slotValue++)
            {
                TestAssert.Equal(slotValue, frame.GetInput(new PlayerSlot(slotValue)).PlayerSlot.Value);
            }
        }

        private static void AssertFinalState(BattleState state)
        {
            TestAssert.Equal(12U, state.Tick);
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
            TestAssert.Equal(expectedX, player.PositionX);
            TestAssert.Equal(expectedZ, player.PositionZ);
            TestAssert.Equal(expectedAim, player.Aim);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
