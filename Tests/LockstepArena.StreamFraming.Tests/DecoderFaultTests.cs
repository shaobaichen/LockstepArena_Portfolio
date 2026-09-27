using System;
using System.IO;

namespace LockstepArena.StreamFraming.Tests
{
    internal static class DecoderFaultTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(OversizeIsRejectedAsSoonAsPrefixCompletes), OversizeIsRejectedAsSoonAsPrefixCompletes),
            new TestCase(nameof(UintMaxLengthIsRejectedBeforeNarrowingOrAllocation), UintMaxLengthIsRejectedBeforeNarrowingOrAllocation),
            new TestCase(nameof(ValidFrameBeforeOversizeInSameFeedReturnsNoPartialBatch), ValidFrameBeforeOversizeInSameFeedReturnsNoPartialBatch),
            new TestCase(nameof(FaultedDecoderRejectsBeforeNullAndRangeValidation), FaultedDecoderRejectsBeforeNullAndRangeValidation),
        };

        private static void OversizeIsRejectedAsSoonAsPrefixCompletes()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            byte[] prefix = { 0x00, 0x00, 0x00, 0x41 };
            TestAssert.Equal(0, decoder.Feed(prefix, 0, 3).Length);

            TestAssert.Throws<InvalidDataException>(() => decoder.Feed(prefix, 3, 1));
            TestAssert.Throws<InvalidOperationException>(
                () => decoder.Feed(Array.Empty<byte>(), 0, 0));
        }

        private static void UintMaxLengthIsRejectedBeforeNarrowingOrAllocation()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);

            TestAssert.Throws<InvalidDataException>(
                () => decoder.Feed(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4));
            TestAssert.Throws<InvalidOperationException>(
                () => decoder.Feed(Array.Empty<byte>(), 0, 0));
        }

        private static void ValidFrameBeforeOversizeInSameFeedReturnsNoPartialBatch()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            byte[] input =
            {
                0x00, 0x00, 0x00, 0x01, 0xAA,
                0x00, 0x00, 0x00, 0x41,
            };
            byte[][]? returned = null;

            try
            {
                returned = decoder.Feed(input, 0, input.Length);
                throw new InvalidOperationException("Expected InvalidDataException but no exception was thrown.");
            }
            catch (InvalidDataException)
            {
            }

            TestAssert.Equal<byte[][]?>(null, returned);
            TestAssert.Throws<InvalidOperationException>(
                () => decoder.Feed(Array.Empty<byte>(), 0, 0));
        }

        private static void FaultedDecoderRejectsBeforeNullAndRangeValidation()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            TestAssert.Throws<InvalidDataException>(
                () => decoder.Feed(new byte[] { 0x00, 0x00, 0x00, 0x41 }, 0, 4));

            TestAssert.Throws<InvalidOperationException>(() => decoder.Feed(null!, -1, -1));
        }
    }
}
