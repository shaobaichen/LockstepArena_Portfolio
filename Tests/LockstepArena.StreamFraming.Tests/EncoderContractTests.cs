using System;

namespace LockstepArena.StreamFraming.Tests
{
    internal static class EncoderContractTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(EncoderRejectsMaxBelowMinimumBeforePayloadValidation), EncoderRejectsMaxBelowMinimumBeforePayloadValidation),
            new TestCase(nameof(EncoderRejectsMaxAboveAllocationBoundary), EncoderRejectsMaxAboveAllocationBoundary),
            new TestCase(nameof(EncoderRejectsNullPayload), EncoderRejectsNullPayload),
            new TestCase(nameof(EncoderRejectsPayloadAboveConfiguredMaximum), EncoderRejectsPayloadAboveConfiguredMaximum),
            new TestCase(nameof(EncoderWritesFourByteBigEndianLength), EncoderWritesFourByteBigEndianLength),
            new TestCase(nameof(EncoderCopiesPayloadIntoIndependentFrame), EncoderCopiesPayloadIntoIndependentFrame),
            new TestCase(nameof(EncoderAllowsZeroLengthPayload), EncoderAllowsZeroLengthPayload),
        };

        private static void EncoderRejectsMaxBelowMinimumBeforePayloadValidation()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => LengthPrefixedFrameEncoder.Encode(null!, 0));
        }

        private static void EncoderRejectsMaxAboveAllocationBoundary()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => LengthPrefixedFrameEncoder.Encode(Array.Empty<byte>(), int.MaxValue - 3));
        }

        private static void EncoderRejectsNullPayload()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => LengthPrefixedFrameEncoder.Encode(null!, 64));
        }

        private static void EncoderRejectsPayloadAboveConfiguredMaximum()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => LengthPrefixedFrameEncoder.Encode(new byte[4], 3));
        }

        private static void EncoderWritesFourByteBigEndianLength()
        {
            byte[] actual = LengthPrefixedFrameEncoder.Encode(new byte[] { 0xAA, 0xBB, 0xCC }, 64);

            TestAssert.SequenceEqual(
                new byte[] { 0x00, 0x00, 0x00, 0x03, 0xAA, 0xBB, 0xCC },
                actual);
        }

        private static void EncoderCopiesPayloadIntoIndependentFrame()
        {
            byte[] payload = { 0x10, 0x20 };
            byte[] frame = LengthPrefixedFrameEncoder.Encode(payload, 64);

            payload[0] = 0xFF;

            TestAssert.Equal((byte)0x10, frame[4]);
        }

        private static void EncoderAllowsZeroLengthPayload()
        {
            byte[] actual = LengthPrefixedFrameEncoder.Encode(Array.Empty<byte>(), 64);

            TestAssert.SequenceEqual(new byte[] { 0x00, 0x00, 0x00, 0x00 }, actual);
        }
    }
}
