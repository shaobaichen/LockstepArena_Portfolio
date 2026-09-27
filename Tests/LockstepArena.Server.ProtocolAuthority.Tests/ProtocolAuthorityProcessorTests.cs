using System;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.ProtocolAuthority.Tests
{
    internal static class ProcessorBootstrapTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(ConstructorRejectsNullInitialState), ConstructorRejectsNullInitialState),
            new TestCase(nameof(ConstructorDelegatesInvalidHistoryCapacity), ConstructorDelegatesInvalidHistoryCapacity),
            new TestCase(nameof(ConstructorBootstrapsExactServerState), ConstructorBootstrapsExactServerState),
            new TestCase(nameof(ConstructorStartsAuthorityAtInitialStateTick), ConstructorStartsAuthorityAtInitialStateTick),
        };

        private static void ConstructorRejectsNullInitialState()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => new ProtocolAuthorityProcessor(null!, 2U, 3));
        }

        private static void ConstructorDelegatesInvalidHistoryCapacity()
        {
            BattleState initialState = CreateInitialState();
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new ProtocolAuthorityProcessor(initialState, 2U, 0));
        }

        private static void ConstructorBootstrapsExactServerState()
        {
            BattleState initialState = CreateInitialState();
            var processor = new ProtocolAuthorityProcessor(initialState, 2U, 3);

            TestAssert.Same(initialState, processor.ServerState);
            TestAssert.Same(initialState.Roster, processor.ServerState.Roster);
        }

        private static void ConstructorStartsAuthorityAtInitialStateTick()
        {
            BattleState initialState = CreateInitialState();
            var processor = new ProtocolAuthorityProcessor(initialState, 2U, 3);

            TestAssert.Equal(100U, processor.ServerState.Tick);
            TestAssert.Equal(100U, processor.NextPublishTick);
        }

        internal static BattleState CreateInitialState()
        {
            var roster = new ActiveRoster(new[]
            {
                new PlayerId(91UL),
                new PlayerId(17UL),
            });
            return new BattleState(100U, roster, new[]
            {
                new PlayerState(-100, 0, 1_000),
                new PlayerState(100, 0, 2_000),
            });
        }
    }

    internal static class ProtocolAuthorityPublicationTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(TwoPlayerIncompleteFrameReturnsEmpty), TwoPlayerIncompleteFrameReturnsEmpty),
            new TestCase(nameof(TwoPlayerCompletionReturnsOnePayload), TwoPlayerCompletionReturnsOnePayload),
            new TestCase(nameof(ThreePlayerCompleteFutureFrameWaitsForGap), ThreePlayerCompleteFutureFrameWaitsForGap),
            new TestCase(nameof(GapFillReturnsContinuousPayloadTicks), GapFillReturnsContinuousPayloadTicks),
            new TestCase(nameof(SuccessfulBatchCatchesServerStateUpToAuthority), SuccessfulBatchCatchesServerStateUpToAuthority),
            new TestCase(nameof(NonEmptyOutputOwnsItsOuterContainer), NonEmptyOutputOwnsItsOuterContainer),
            new TestCase(nameof(PayloadBuffersAreDistinctAndCallerMutationCannotAffectServerState), PayloadBuffersAreDistinctAndCallerMutationCannotAffectServerState),
        };

        private static void TwoPlayerIncompleteFrameReturnsEmpty()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(2);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);

            byte[][] output = ProtocolAuthorityTestData.Submit(processor, roster, 100U, 0);

            TestAssert.Equal(0, output.Length);
            TestAssert.Equal(100U, processor.ServerState.Tick);
            TestAssert.Equal(100U, processor.NextPublishTick);
        }

        private static void TwoPlayerCompletionReturnsOnePayload()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(2);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);
            ProtocolAuthorityTestData.Submit(processor, roster, 100U, 0);

            byte[][] output = ProtocolAuthorityTestData.Submit(processor, roster, 100U, 1);

            TestAssert.Equal(1, output.Length);
            ActiveRoster clientRoster = ProtocolAuthorityTestData.CreateRoster(2);
            FrameData frame = ProtocolAuthorityTestData.ParseFrame(output[0], clientRoster);
            TestAssert.Equal(100U, frame.Tick);
            ProtocolAuthorityTestData.AssertCanonicalSlots(frame);
        }

        private static void ThreePlayerCompleteFutureFrameWaitsForGap()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(3);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);

            byte[][] output = ProtocolAuthorityTestData.CompleteTick(processor, roster, 101U);

            TestAssert.Equal(0, output.Length);
            TestAssert.Equal(100U, processor.NextPublishTick);
            TestAssert.Equal(100U, processor.ServerState.Tick);
        }

        private static void GapFillReturnsContinuousPayloadTicks()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(3);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);
            ProtocolAuthorityTestData.CompleteTick(processor, roster, 101U);

            byte[][] output = ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U);

            TestAssert.Equal(2, output.Length);
            ActiveRoster clientRoster = ProtocolAuthorityTestData.CreateRoster(3);
            TestAssert.Equal(100U, ProtocolAuthorityTestData.ParseFrame(output[0], clientRoster).Tick);
            TestAssert.Equal(101U, ProtocolAuthorityTestData.ParseFrame(output[1], clientRoster).Tick);
        }

        private static void SuccessfulBatchCatchesServerStateUpToAuthority()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(3);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);
            ProtocolAuthorityTestData.CompleteTick(processor, roster, 101U);

            ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U);

            TestAssert.Equal(102U, processor.ServerState.Tick);
            TestAssert.Equal(102U, processor.NextPublishTick);
        }

        private static void NonEmptyOutputOwnsItsOuterContainer()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(2);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);

            byte[][] first = ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U);
            byte[][] second = ProtocolAuthorityTestData.CompleteTick(processor, roster, 101U);

            TestAssert.Equal(1, first.Length);
            TestAssert.Equal(1, second.Length);
            TestAssert.True(!ReferenceEquals(first, second));
        }

        private static void PayloadBuffersAreDistinctAndCallerMutationCannotAffectServerState()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(2);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);
            ProtocolAuthorityTestData.CompleteTick(processor, roster, 101U);

            byte[][] output = ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U);
            ulong digestBeforeMutation = StateDigest.Compute(processor.ServerState);

            TestAssert.Equal(2, output.Length);
            TestAssert.True(!ReferenceEquals(output[0], output[1]));
            output[0][0] ^= 0xFF;
            output[0] = output[1];

            TestAssert.Equal(digestBeforeMutation, StateDigest.Compute(processor.ServerState));
            TestAssert.Equal(102U, processor.ServerState.Tick);
        }
    }

    internal static class ProtocolAuthorityTestData
    {
        internal static ProtocolAuthorityProcessor CreateProcessor(
            ActiveRoster roster,
            uint tick,
            uint maxFutureTickOffset)
        {
            var states = new PlayerState[roster.Count];
            for (int index = 0; index < states.Length; index++)
            {
                states[index] = new PlayerState(index * 100, -(index * 100), checked((ushort)(1_000 + index)));
            }

            return new ProtocolAuthorityProcessor(
                new BattleState(tick, roster, states),
                maxFutureTickOffset,
                5);
        }

        internal static ActiveRoster CreateRoster(int playerCount)
        {
            var playerIds = new PlayerId[playerCount];
            for (int index = 0; index < playerIds.Length; index++)
            {
                playerIds[index] = new PlayerId(checked((ulong)(10_000 - (index * 17))));
            }

            return new ActiveRoster(playerIds);
        }

        internal static byte[][] CompleteTick(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster,
            uint tick,
            int firstSlot = 0)
        {
            byte[][] output = Array.Empty<byte[]>();
            for (int slotValue = firstSlot; slotValue < roster.Count; slotValue++)
            {
                byte[][] candidate = Submit(processor, roster, tick, slotValue);
                if (candidate.Length > 0)
                {
                    output = candidate;
                }
            }

            return output;
        }

        internal static byte[][] Submit(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster,
            uint tick,
            int slotValue,
            PlayerId? submittedPlayerId = null)
        {
            PlayerSlot slot = new PlayerSlot(slotValue);
            InputFrame input = CreateInput(tick, slotValue);
            PlayerId playerId = submittedPlayerId ?? roster.GetPlayerId(slot);
            byte[] payload = ProtocolMapper.ToWire(playerId, input).ToByteArray();
            return processor.SubmitPlayerInputPayload(payload);
        }

        internal static InputFrame CreateInput(uint tick, int slotValue)
        {
            return new InputFrame(
                tick,
                new PlayerSlot(slotValue),
                0,
                0,
                checked((ushort)(10_000 + slotValue)));
        }

        internal static FrameData ParseFrame(byte[] payload, ActiveRoster expectedRoster)
        {
            AuthoritativeFrameMessage wire = AuthoritativeFrameMessage.Parser.ParseFrom(payload);
            return ProtocolMapper.ToDomain(wire, expectedRoster);
        }

        internal static void AssertCanonicalSlots(FrameData frame)
        {
            for (int slotValue = 0; slotValue < frame.InputCount; slotValue++)
            {
                TestAssert.Equal(slotValue, frame.GetInput(new PlayerSlot(slotValue)).PlayerSlot.Value);
            }
        }
    }

    internal static class ProtocolAuthorityDeterminismTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(GapFillPublishesTicks100Through102AsIndependentPayloads), GapFillPublishesTicks100Through102AsIndependentPayloads),
            new TestCase(nameof(ClientDigestsMatchApprovedPerTickOracles), ClientDigestsMatchApprovedPerTickOracles),
            new TestCase(nameof(ServerAndClientMatchApprovedFinalStateAndDigest), ServerAndClientMatchApprovedFinalStateAndDigest),
            new TestCase(nameof(DifferentSubmissionArrivalOrdersProduceSameAuthoritativeDomainSequence), DifferentSubmissionArrivalOrdersProduceSameAuthoritativeDomainSequence),
        };

        private static void GapFillPublishesTicks100Through102AsIndependentPayloads()
        {
            Gate6GapFillGoldenResult result = Gate6GapFillGoldenVector.RunApprovedArrivalOrder();

            foreach (int outputLength in result.PreGapOutputLengths)
            {
                TestAssert.Equal(0, outputLength);
            }

            TestAssert.Equal(3, result.AuthoritativePayloads.Length);
            TestAssert.True(!ReferenceEquals(result.AuthoritativePayloads[0], result.AuthoritativePayloads[1]));
            TestAssert.True(!ReferenceEquals(result.AuthoritativePayloads[1], result.AuthoritativePayloads[2]));
            for (int index = 0; index < result.AuthoritativeFrames.Length; index++)
            {
                TestAssert.Equal(checked((uint)(100 + index)), result.AuthoritativeFrames[index].Tick);
            }
        }

        private static void ClientDigestsMatchApprovedPerTickOracles()
        {
            Gate6GapFillGoldenResult result = Gate6GapFillGoldenVector.RunApprovedArrivalOrder();
            ulong[] expected =
            {
                0xD95809E1EB5CDDAAUL,
                0xA96B83267DD72A7DUL,
                0x386C4BB11A7EB7E0UL,
            };

            TestAssert.Equal(expected.Length, result.ClientDigests.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                TestAssert.Equal(expected[index], result.ClientDigests[index]);
            }
        }

        private static void ServerAndClientMatchApprovedFinalStateAndDigest()
        {
            Gate6GapFillGoldenResult result = Gate6GapFillGoldenVector.RunApprovedArrivalOrder();
            PlayerState[] expectedPlayers =
            {
                new PlayerState(-300, 100, 10_102),
                new PlayerState(300, -100, 20_102),
                new PlayerState(100, -300, 30_102),
                new PlayerState(-100, 300, 40_102),
            };

            TestAssert.Equal(103U, result.ServerState.Tick);
            TestAssert.Equal(103U, result.ClientState.Tick);
            TestAssert.Equal(103U, result.NextPublishTick);
            TestAssert.True(result.ServerState.Roster.HasSameStructure(result.ClientState.Roster));
            AssertState(result.ServerState, expectedPlayers);
            AssertState(result.ClientState, expectedPlayers);
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, StateDigest.Compute(result.ServerState));
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, StateDigest.Compute(result.ClientState));
        }

        private static void DifferentSubmissionArrivalOrdersProduceSameAuthoritativeDomainSequence()
        {
            Gate6GapFillGoldenResult approved = Gate6GapFillGoldenVector.RunApprovedArrivalOrder();
            Gate6GapFillGoldenResult alternate = Gate6GapFillGoldenVector.RunAlternateArrivalOrder();

            TestAssert.Equal(approved.AuthoritativeFrames.Length, alternate.AuthoritativeFrames.Length);
            for (int frameIndex = 0; frameIndex < approved.AuthoritativeFrames.Length; frameIndex++)
            {
                FrameData left = approved.AuthoritativeFrames[frameIndex];
                FrameData right = alternate.AuthoritativeFrames[frameIndex];
                TestAssert.Equal(left.Tick, right.Tick);
                TestAssert.True(left.Roster.HasSameStructure(right.Roster));
                for (int slotValue = 0; slotValue < left.InputCount; slotValue++)
                {
                    PlayerSlot slot = new PlayerSlot(slotValue);
                    InputFrame leftInput = left.GetInput(slot);
                    InputFrame rightInput = right.GetInput(slot);
                    TestAssert.Equal(leftInput.Tick, rightInput.Tick);
                    TestAssert.Equal(leftInput.PlayerSlot, rightInput.PlayerSlot);
                    TestAssert.Equal(leftInput.MoveX, rightInput.MoveX);
                    TestAssert.Equal(leftInput.MoveZ, rightInput.MoveZ);
                    TestAssert.Equal(leftInput.Aim, rightInput.Aim);
                }
            }

            AssertStateEqual(approved.ServerState, alternate.ServerState);
            TestAssert.Equal(
                StateDigest.Compute(approved.ServerState),
                StateDigest.Compute(alternate.ServerState));
        }

        private static void AssertState(BattleState actual, PlayerState[] expectedPlayers)
        {
            TestAssert.Equal(expectedPlayers.Length, actual.PlayerCount);
            for (int slotValue = 0; slotValue < expectedPlayers.Length; slotValue++)
            {
                PlayerState expected = expectedPlayers[slotValue];
                PlayerState player = actual.GetPlayerState(new PlayerSlot(slotValue));
                TestAssert.Equal(expected.PositionX, player.PositionX);
                TestAssert.Equal(expected.PositionZ, player.PositionZ);
                TestAssert.Equal(expected.Aim, player.Aim);
            }
        }

        private static void AssertStateEqual(BattleState left, BattleState right)
        {
            TestAssert.Equal(left.Tick, right.Tick);
            TestAssert.True(left.Roster.HasSameStructure(right.Roster));
            TestAssert.Equal(left.PlayerCount, right.PlayerCount);
            for (int slotValue = 0; slotValue < left.PlayerCount; slotValue++)
            {
                PlayerSlot slot = new PlayerSlot(slotValue);
                PlayerState leftPlayer = left.GetPlayerState(slot);
                PlayerState rightPlayer = right.GetPlayerState(slot);
                TestAssert.Equal(leftPlayer.PositionX, rightPlayer.PositionX);
                TestAssert.Equal(leftPlayer.PositionZ, rightPlayer.PositionZ);
                TestAssert.Equal(leftPlayer.Aim, rightPlayer.Aim);
            }
        }
    }
}
