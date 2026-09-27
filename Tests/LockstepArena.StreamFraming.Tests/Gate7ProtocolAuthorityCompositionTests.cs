using LockstepArena.Simulation;

namespace LockstepArena.StreamFraming.Tests
{
    internal static class Gate7ProtocolAuthorityCompositionTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(FramedSubmissionsDriveGapFillPublicationOfTicks100Through102), FramedSubmissionsDriveGapFillPublicationOfTicks100Through102),
            new TestCase(nameof(FramedAuthoritativePayloadsMatchApprovedPerTickClientDigests), FramedAuthoritativePayloadsMatchApprovedPerTickClientDigests),
            new TestCase(nameof(FramedServerAndClientReachApprovedFinalStateAndDigest), FramedServerAndClientReachApprovedFinalStateAndDigest),
            new TestCase(nameof(DifferentBidirectionalSegmentationsProduceSameAuthoritativeDomainSequence), DifferentBidirectionalSegmentationsProduceSameAuthoritativeDomainSequence),
        };

        private static void FramedSubmissionsDriveGapFillPublicationOfTicks100Through102()
        {
            var actual = Gate7ProtocolAuthorityFramingGoldenVector.RunPrimarySegmentation();

            TestAssert.Equal(12, actual.SubmissionPayloads.Length);
            AssertPayloadSequenceEqual(actual.SubmissionPayloads, actual.RecoveredSubmissionPayloads);
            TestAssert.Equal(11, actual.PreGapOutputLengths.Length);
            for (int index = 0; index < actual.PreGapOutputLengths.Length; index++)
            {
                TestAssert.Equal(0, actual.PreGapOutputLengths[index]);
            }

            TestAssert.Equal(3, actual.AuthoritativePayloads.Length);
            AssertPayloadSequenceEqual(actual.AuthoritativePayloads, actual.RecoveredAuthoritativePayloads);
            TestAssert.Equal(100U, actual.AuthoritativeFrames[0].Tick);
            TestAssert.Equal(101U, actual.AuthoritativeFrames[1].Tick);
            TestAssert.Equal(102U, actual.AuthoritativeFrames[2].Tick);
            TestAssert.Equal(103U, actual.NextPublishTick);
        }

        private static void FramedAuthoritativePayloadsMatchApprovedPerTickClientDigests()
        {
            var actual = Gate7ProtocolAuthorityFramingGoldenVector.RunPrimarySegmentation();

            TestAssert.Equal(3, actual.ClientDigests.Length);
            TestAssert.Equal(0xD95809E1EB5CDDAAUL, actual.ClientDigests[0]);
            TestAssert.Equal(0xA96B83267DD72A7DUL, actual.ClientDigests[1]);
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, actual.ClientDigests[2]);
        }

        private static void FramedServerAndClientReachApprovedFinalStateAndDigest()
        {
            var actual = Gate7ProtocolAuthorityFramingGoldenVector.RunPrimarySegmentation();

            AssertApprovedFinalState(actual.ServerState);
            AssertApprovedFinalState(actual.ClientState);
            AssertStateEqual(actual.ServerState, actual.ClientState);
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, StateDigest.Compute(actual.ServerState));
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, StateDigest.Compute(actual.ClientState));
        }

        private static void DifferentBidirectionalSegmentationsProduceSameAuthoritativeDomainSequence()
        {
            var primary = Gate7ProtocolAuthorityFramingGoldenVector.RunPrimarySegmentation();
            var alternate = Gate7ProtocolAuthorityFramingGoldenVector.RunAlternateSegmentation();

            AssertPayloadSequenceEqual(primary.SubmissionPayloads, alternate.SubmissionPayloads);
            AssertPayloadSequenceEqual(primary.AuthoritativePayloads, alternate.AuthoritativePayloads);
            TestAssert.Equal(primary.AuthoritativeFrames.Length, alternate.AuthoritativeFrames.Length);
            for (int index = 0; index < primary.AuthoritativeFrames.Length; index++)
            {
                AssertFrameEqual(primary.AuthoritativeFrames[index], alternate.AuthoritativeFrames[index]);
                TestAssert.Equal(primary.ClientDigests[index], alternate.ClientDigests[index]);
            }

            AssertStateEqual(primary.ServerState, alternate.ServerState);
            AssertStateEqual(primary.ClientState, alternate.ClientState);
        }

        private static void AssertApprovedFinalState(BattleState actual)
        {
            TestAssert.Equal(103U, actual.Tick);
            AssertPlayer(actual, 0, -300, 100, 10_102);
            AssertPlayer(actual, 1, 300, -100, 20_102);
            AssertPlayer(actual, 2, 100, -300, 30_102);
            AssertPlayer(actual, 3, -100, 300, 40_102);
        }

        private static void AssertPlayer(
            BattleState state,
            int slotValue,
            int expectedX,
            int expectedZ,
            int expectedAim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slotValue));
            TestAssert.Equal(expectedX, player.PositionX);
            TestAssert.Equal(expectedZ, player.PositionZ);
            TestAssert.Equal(checked((ushort)expectedAim), player.Aim);
        }

        private static void AssertFrameEqual(FrameData expected, FrameData actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.Equal(true, expected.Roster.HasSameStructure(actual.Roster));
            TestAssert.Equal(expected.InputCount, actual.InputCount);
            for (int slotValue = 0; slotValue < expected.InputCount; slotValue++)
            {
                PlayerSlot slot = new PlayerSlot(slotValue);
                InputFrame expectedInput = expected.GetInput(slot);
                InputFrame actualInput = actual.GetInput(slot);
                TestAssert.Equal(expectedInput.Tick, actualInput.Tick);
                TestAssert.Equal(expectedInput.PlayerSlot, actualInput.PlayerSlot);
                TestAssert.Equal(expectedInput.MoveX, actualInput.MoveX);
                TestAssert.Equal(expectedInput.MoveZ, actualInput.MoveZ);
                TestAssert.Equal(expectedInput.Aim, actualInput.Aim);
            }
        }

        private static void AssertStateEqual(BattleState expected, BattleState actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.Equal(true, expected.Roster.HasSameStructure(actual.Roster));
            TestAssert.Equal(expected.PlayerCount, actual.PlayerCount);
            for (int slotValue = 0; slotValue < expected.PlayerCount; slotValue++)
            {
                PlayerSlot slot = new PlayerSlot(slotValue);
                PlayerState expectedPlayer = expected.GetPlayerState(slot);
                PlayerState actualPlayer = actual.GetPlayerState(slot);
                TestAssert.Equal(expectedPlayer.PositionX, actualPlayer.PositionX);
                TestAssert.Equal(expectedPlayer.PositionZ, actualPlayer.PositionZ);
                TestAssert.Equal(expectedPlayer.Aim, actualPlayer.Aim);
            }
        }

        private static void AssertPayloadSequenceEqual(byte[][] expected, byte[][] actual)
        {
            TestAssert.Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                TestAssert.SequenceEqual(expected[index], actual[index]);
            }
        }
    }
}
