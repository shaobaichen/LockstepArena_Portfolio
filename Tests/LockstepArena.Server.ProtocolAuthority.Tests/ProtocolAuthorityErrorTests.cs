using System;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.ProtocolAuthority.Tests
{
    internal static class ProtocolAuthorityRejectionTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(NullPayloadThrowsArgumentNullWithoutFault), NullPayloadThrowsArgumentNullWithoutFault),
            new TestCase(nameof(MalformedPayloadPreservesParserExceptionWithoutFault), MalformedPayloadPreservesParserExceptionWithoutFault),
            new TestCase(nameof(InvalidMappingPreservesProtocolMappingExceptionWithoutFault), InvalidMappingPreservesProtocolMappingExceptionWithoutFault),
            new TestCase(nameof(OldTickPreservesCoordinatorExceptionWithoutFault), OldTickPreservesCoordinatorExceptionWithoutFault),
            new TestCase(nameof(FutureWindowPreservesCoordinatorExceptionWithoutFault), FutureWindowPreservesCoordinatorExceptionWithoutFault),
            new TestCase(nameof(PlayerIdSlotMismatchPreservesCoordinatorExceptionWithoutFault), PlayerIdSlotMismatchPreservesCoordinatorExceptionWithoutFault),
            new TestCase(nameof(DuplicateSubmissionPreservesAcceptedInputWithoutFault), DuplicateSubmissionPreservesAcceptedInputWithoutFault),
        };

        private static void NullPayloadThrowsArgumentNullWithoutFault()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateSubject();
            TestAssert.Throws<ArgumentNullException>(() => processor.SubmitPlayerInputPayload(null!));
            AssertStillPublishes(processor, roster);
        }

        private static void MalformedPayloadPreservesParserExceptionWithoutFault()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateSubject();
            TestAssert.Throws<InvalidProtocolBufferException>(
                () => processor.SubmitPlayerInputPayload(new byte[] { 0x12, 0x05, 0x01 }));
            AssertStillPublishes(processor, roster);
        }

        private static void InvalidMappingPreservesProtocolMappingExceptionWithoutFault()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateSubject();
            var wire = new PlayerInputSubmissionMessage
            {
                SubmittedPlayerId = roster.GetPlayerId(new PlayerSlot(0)).Value,
                Input = new InputFrameMessage
                {
                    Tick = 100U,
                    PlayerSlot = 0U,
                    MoveX = 2,
                    MoveZ = 0,
                    Aim = 100U,
                },
            };

            TestAssert.Throws<ProtocolMappingException>(
                () => processor.SubmitPlayerInputPayload(wire.ToByteArray()));
            AssertStillPublishes(processor, roster);
        }

        private static void OldTickPreservesCoordinatorExceptionWithoutFault()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateSubject();
            byte[] payload = ProtocolMapper.ToWire(
                roster.GetPlayerId(new PlayerSlot(0)),
                ProtocolAuthorityTestData.CreateInput(99U, 0)).ToByteArray();

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => processor.SubmitPlayerInputPayload(payload));
            AssertStillPublishes(processor, roster);
        }

        private static void FutureWindowPreservesCoordinatorExceptionWithoutFault()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateSubject();
            byte[] payload = ProtocolMapper.ToWire(
                roster.GetPlayerId(new PlayerSlot(0)),
                ProtocolAuthorityTestData.CreateInput(102U, 0)).ToByteArray();

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => processor.SubmitPlayerInputPayload(payload));
            AssertStillPublishes(processor, roster);
        }

        private static void PlayerIdSlotMismatchPreservesCoordinatorExceptionWithoutFault()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateSubject();
            byte[] payload = ProtocolMapper.ToWire(
                roster.GetPlayerId(new PlayerSlot(1)),
                ProtocolAuthorityTestData.CreateInput(100U, 0)).ToByteArray();

            TestAssert.Throws<ArgumentException>(() => processor.SubmitPlayerInputPayload(payload));
            AssertStillPublishes(processor, roster);
        }

        private static void DuplicateSubmissionPreservesAcceptedInputWithoutFault()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateSubject();
            ProtocolAuthorityTestData.Submit(processor, roster, 100U, 0);

            TestAssert.Throws<InvalidOperationException>(
                () => ProtocolAuthorityTestData.Submit(processor, roster, 100U, 0));

            byte[][] output = ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U, 1);
            TestAssert.Equal(1, output.Length);
            TestAssert.Equal(101U, processor.NextPublishTick);
        }

        private static (ProtocolAuthorityProcessor Processor, ActiveRoster Roster) CreateSubject()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(2);
            return (ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U), roster);
        }

        private static void AssertStillPublishes(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster)
        {
            byte[][] output = ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U);
            TestAssert.Equal(1, output.Length);
            TestAssert.Equal(101U, processor.NextPublishTick);
        }
    }
}
