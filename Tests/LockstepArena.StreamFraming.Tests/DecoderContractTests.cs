using System;

namespace LockstepArena.StreamFraming.Tests
{
    internal static class DecoderContractTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(DecoderRejectsMaxBelowMinimum), DecoderRejectsMaxBelowMinimum),
            new TestCase(nameof(DecoderRejectsMaxAboveAllocationBoundary), DecoderRejectsMaxAboveAllocationBoundary),
            new TestCase(nameof(NullBufferTakesPriorityAndPreservesPartialPrefix), NullBufferTakesPriorityAndPreservesPartialPrefix),
            new TestCase(nameof(InvalidOffsetPreservesPartialPrefix), InvalidOffsetPreservesPartialPrefix),
            new TestCase(nameof(InvalidCountPreservesPartialPayload), InvalidCountPreservesPartialPayload),
            new TestCase(nameof(ZeroCountReturnsEmptyAndPreservesPartialPayload), ZeroCountReturnsEmptyAndPreservesPartialPayload),
            new TestCase(nameof(FeedConsumesOnlyOffsetCountSegment), FeedConsumesOnlyOffsetCountSegment),
            new TestCase(nameof(HealthyEmptySegmentReturnsEmptyBatch), HealthyEmptySegmentReturnsEmptyBatch),
        };

        private static void DecoderRejectsMaxBelowMinimum()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new LengthPrefixedFrameDecoder(0));
        }

        private static void DecoderRejectsMaxAboveAllocationBoundary()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new LengthPrefixedFrameDecoder(int.MaxValue - 3));
        }

        private static void NullBufferTakesPriorityAndPreservesPartialPrefix()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            TestAssert.Equal(0, decoder.Feed(new byte[] { 0x00, 0x00 }, 0, 2).Length);

            TestAssert.Throws<ArgumentNullException>(() => decoder.Feed(null!, -1, -1));

            byte[][] actual = decoder.Feed(new byte[] { 0x00, 0x01, 0xAA }, 0, 3);
            TestAssert.Equal(1, actual.Length);
            TestAssert.SequenceEqual(new byte[] { 0xAA }, actual[0]);
        }

        private static void InvalidOffsetPreservesPartialPrefix()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            TestAssert.Equal(0, decoder.Feed(new byte[] { 0x00 }, 0, 1).Length);

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => decoder.Feed(new byte[4], -1, 1));

            byte[][] actual = decoder.Feed(new byte[] { 0x00, 0x00, 0x01, 0x55 }, 0, 4);
            TestAssert.Equal(1, actual.Length);
            TestAssert.SequenceEqual(new byte[] { 0x55 }, actual[0]);
        }

        private static void InvalidCountPreservesPartialPayload()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            byte[] first = { 0x00, 0x00, 0x00, 0x03, 0x10 };
            TestAssert.Equal(0, decoder.Feed(first, 0, first.Length).Length);

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => decoder.Feed(new byte[2], 0, 3));

            byte[][] actual = decoder.Feed(new byte[] { 0x20, 0x30 }, 0, 2);
            TestAssert.Equal(1, actual.Length);
            TestAssert.SequenceEqual(new byte[] { 0x10, 0x20, 0x30 }, actual[0]);
        }

        private static void ZeroCountReturnsEmptyAndPreservesPartialPayload()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            byte[] first = { 0x00, 0x00, 0x00, 0x02, 0xA0 };
            TestAssert.Equal(0, decoder.Feed(first, 0, first.Length).Length);

            TestAssert.Equal(0, decoder.Feed(new byte[] { 0xFF }, 1, 0).Length);

            byte[][] actual = decoder.Feed(new byte[] { 0xB0 }, 0, 1);
            TestAssert.Equal(1, actual.Length);
            TestAssert.SequenceEqual(new byte[] { 0xA0, 0xB0 }, actual[0]);
        }

        private static void FeedConsumesOnlyOffsetCountSegment()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            byte[] buffer = { 0xFF, 0x00, 0x00, 0x00, 0x01, 0x42, 0xEE };

            byte[][] actual = decoder.Feed(buffer, 1, 5);

            TestAssert.Equal(1, actual.Length);
            TestAssert.SequenceEqual(new byte[] { 0x42 }, actual[0]);
            TestAssert.Equal(0, decoder.Feed(Array.Empty<byte>(), 0, 0).Length);
        }

        private static void HealthyEmptySegmentReturnsEmptyBatch()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);

            byte[][] actual = decoder.Feed(Array.Empty<byte>(), 0, 0);

            TestAssert.Equal(0, actual.Length);
        }
    }
}
