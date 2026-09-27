using System;
using System.Collections.Generic;
using LockstepArena.StreamFraming.Verification;

namespace LockstepArena.StreamFraming.Tests
{
    internal static class StreamSegmentationTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(PrefixCanSplitAcrossFourFeeds), PrefixCanSplitAcrossFourFeeds),
            new TestCase(nameof(PayloadCanSplitAcrossFeeds), PayloadCanSplitAcrossFeeds),
            new TestCase(nameof(OneSegmentCanContainSeveralCompleteFrames), OneSegmentCanContainSeveralCompleteFrames),
            new TestCase(nameof(PayloadTailAndNextCompleteFrameCanShareSegment), PayloadTailAndNextCompleteFrameCanShareSegment),
            new TestCase(nameof(ArbitrarySegmentsRecoverApprovedAbcSequence), ArbitrarySegmentsRecoverApprovedAbcSequence),
            new TestCase(nameof(DifferentSegmentationsRecoverIdenticalPayloadSequence), DifferentSegmentationsRecoverIdenticalPayloadSequence),
            new TestCase(nameof(ZeroLengthFrameBetweenNonEmptyFramesIsRecovered), ZeroLengthFrameBetweenNonEmptyFramesIsRecovered),
            new TestCase(nameof(ReusedReceiveBufferCannotMutatePartialState), ReusedReceiveBufferCannotMutatePartialState),
            new TestCase(nameof(ReturnedBatchAndPayloadsAreIndependentlyOwned), ReturnedBatchAndPayloadsAreIndependentlyOwned),
        };

        private static readonly byte[] PayloadA = { 0xDE, 0xAD, 0xBE };
        private static readonly byte[] PayloadB = { 0x00, 0x01, 0x02, 0x03, 0x04 };
        private static readonly byte[] PayloadC = { 0xFF, 0x00, 0x7F, 0x80, 0x10, 0x20, 0x30, 0x40 };

        private static void PrefixCanSplitAcrossFourFeeds()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            byte[] frame = LengthPrefixedFrameEncoder.Encode(new byte[] { 0x77 }, 64);
            for (int index = 0; index < 4; index++)
            {
                TestAssert.Equal(0, decoder.Feed(frame, index, 1).Length);
            }

            byte[][] actual = decoder.Feed(frame, 4, 1);
            TestAssert.Equal(1, actual.Length);
            TestAssert.SequenceEqual(new byte[] { 0x77 }, actual[0]);
        }

        private static void PayloadCanSplitAcrossFeeds()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            byte[] frame = LengthPrefixedFrameEncoder.Encode(new byte[] { 1, 2, 3, 4 }, 64);

            TestAssert.Equal(0, decoder.Feed(frame, 0, 6).Length);
            byte[][] actual = decoder.Feed(frame, 6, 2);

            TestAssert.Equal(1, actual.Length);
            TestAssert.SequenceEqual(new byte[] { 1, 2, 3, 4 }, actual[0]);
        }

        private static void OneSegmentCanContainSeveralCompleteFrames()
        {
            byte[] stream = Concatenate(
                LengthPrefixedFrameEncoder.Encode(PayloadA, 64),
                LengthPrefixedFrameEncoder.Encode(PayloadB, 64),
                LengthPrefixedFrameEncoder.Encode(PayloadC, 64));
            var decoder = new LengthPrefixedFrameDecoder(64);

            byte[][] actual = decoder.Feed(stream, 0, stream.Length);

            AssertAbc(actual);
        }

        private static void PayloadTailAndNextCompleteFrameCanShareSegment()
        {
            byte[] first = LengthPrefixedFrameEncoder.Encode(PayloadA, 64);
            byte[] second = LengthPrefixedFrameEncoder.Encode(PayloadB, 64);
            byte[] stream = Concatenate(first, second);
            var decoder = new LengthPrefixedFrameDecoder(64);

            TestAssert.Equal(0, decoder.Feed(stream, 0, 5).Length);
            byte[][] actual = decoder.Feed(stream, 5, stream.Length - 5);

            TestAssert.Equal(2, actual.Length);
            TestAssert.SequenceEqual(PayloadA, actual[0]);
            TestAssert.SequenceEqual(PayloadB, actual[1]);
        }

        private static void ArbitrarySegmentsRecoverApprovedAbcSequence()
        {
            Gate7FramingGoldenResult golden = Gate7FramingGoldenVector.Run();

            TestAssert.SequenceEqual(
                new byte[]
                {
                    0x00, 0x00, 0x00, 0x03, 0xDE, 0xAD, 0xBE,
                    0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x02, 0x03, 0x04,
                    0x00, 0x00, 0x00, 0x08, 0xFF, 0x00, 0x7F, 0x80,
                    0x10, 0x20, 0x30, 0x40,
                },
                golden.FramedStream);
            TestAssert.Equal(6, golden.FeedBatchSizes.Length);
            TestAssert.Equal(0, golden.FeedBatchSizes[0]);
            TestAssert.Equal(0, golden.FeedBatchSizes[1]);
            TestAssert.Equal(0, golden.FeedBatchSizes[2]);
            TestAssert.Equal(2, golden.FeedBatchSizes[3]);
            TestAssert.Equal(0, golden.FeedBatchSizes[4]);
            TestAssert.Equal(1, golden.FeedBatchSizes[5]);
            AssertAbc(golden.RecoveredPayloads);
        }

        private static void DifferentSegmentationsRecoverIdenticalPayloadSequence()
        {
            byte[] stream = CreateAbcStream();
            byte[][] split = DecodeSegments(stream, new int[] { 1, 2, 2, 13, 4, 6 });
            byte[][] whole = DecodeSegments(stream, new int[] { stream.Length });

            AssertSamePayloadSequence(split, whole);
        }

        private static void ZeroLengthFrameBetweenNonEmptyFramesIsRecovered()
        {
            byte[] stream = Concatenate(
                LengthPrefixedFrameEncoder.Encode(PayloadA, 64),
                LengthPrefixedFrameEncoder.Encode(Array.Empty<byte>(), 64),
                LengthPrefixedFrameEncoder.Encode(PayloadB, 64));
            var decoder = new LengthPrefixedFrameDecoder(64);

            byte[][] actual = decoder.Feed(stream, 0, stream.Length);

            TestAssert.Equal(3, actual.Length);
            TestAssert.SequenceEqual(PayloadA, actual[0]);
            TestAssert.Equal(0, actual[1].Length);
            TestAssert.SequenceEqual(PayloadB, actual[2]);
        }

        private static void ReusedReceiveBufferCannotMutatePartialState()
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            byte[] frame = LengthPrefixedFrameEncoder.Encode(PayloadA, 64);
            byte[] receiveBuffer = new byte[12];
            Array.Copy(frame, 0, receiveBuffer, 2, 5);
            TestAssert.Equal(0, decoder.Feed(receiveBuffer, 2, 5).Length);
            Array.Fill(receiveBuffer, (byte)0x00);
            Array.Copy(frame, 5, receiveBuffer, 2, frame.Length - 5);

            byte[][] actual = decoder.Feed(receiveBuffer, 2, frame.Length - 5);

            TestAssert.Equal(1, actual.Length);
            TestAssert.SequenceEqual(PayloadA, actual[0]);
        }

        private static void ReturnedBatchAndPayloadsAreIndependentlyOwned()
        {
            byte[] payload = { 0x11, 0x22 };
            byte[] stream = Concatenate(
                LengthPrefixedFrameEncoder.Encode(payload, 64),
                LengthPrefixedFrameEncoder.Encode(payload, 64));
            var decoder = new LengthPrefixedFrameDecoder(64);

            byte[][] actual = decoder.Feed(stream, 0, stream.Length);
            stream[4] = 0xFF;
            actual[0][0] = 0xEE;
            actual[0] = Array.Empty<byte>();

            TestAssert.SequenceEqual(new byte[] { 0x11, 0x22 }, actual[1]);
            TestAssert.Equal(0, decoder.Feed(Array.Empty<byte>(), 0, 0).Length);
        }

        private static byte[] CreateAbcStream()
        {
            return Concatenate(
                LengthPrefixedFrameEncoder.Encode(PayloadA, 64),
                LengthPrefixedFrameEncoder.Encode(PayloadB, 64),
                LengthPrefixedFrameEncoder.Encode(PayloadC, 64));
        }

        private static byte[][] DecodeSegments(byte[] stream, int[] segmentLengths)
        {
            var decoder = new LengthPrefixedFrameDecoder(64);
            var payloads = new List<byte[]>();
            int offset = 0;
            foreach (int segmentLength in segmentLengths)
            {
                byte[][] batch = decoder.Feed(stream, offset, segmentLength);
                payloads.AddRange(batch);
                offset += segmentLength;
            }

            TestAssert.Equal(stream.Length, offset);
            return payloads.ToArray();
        }

        private static byte[] Concatenate(params byte[][] values)
        {
            int length = 0;
            foreach (byte[] value in values)
            {
                length += value.Length;
            }

            byte[] result = new byte[length];
            int offset = 0;
            foreach (byte[] value in values)
            {
                Array.Copy(value, 0, result, offset, value.Length);
                offset += value.Length;
            }

            return result;
        }

        private static void AssertAbc(byte[][] actual)
        {
            TestAssert.Equal(3, actual.Length);
            TestAssert.SequenceEqual(PayloadA, actual[0]);
            TestAssert.SequenceEqual(PayloadB, actual[1]);
            TestAssert.SequenceEqual(PayloadC, actual[2]);
        }

        private static void AssertSamePayloadSequence(byte[][] expected, byte[][] actual)
        {
            TestAssert.Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                TestAssert.SequenceEqual(expected[index], actual[index]);
            }
        }
    }
}
