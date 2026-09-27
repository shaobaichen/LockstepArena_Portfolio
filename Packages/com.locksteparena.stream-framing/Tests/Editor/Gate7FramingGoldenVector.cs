using System;
using System.Collections.Generic;

namespace LockstepArena.StreamFraming.Verification
{
    public sealed class Gate7FramingGoldenResult
    {
        public Gate7FramingGoldenResult(
            byte[] framedStream,
            int[] feedBatchSizes,
            byte[][] recoveredPayloads)
        {
            FramedStream = framedStream;
            FeedBatchSizes = feedBatchSizes;
            RecoveredPayloads = recoveredPayloads;
        }

        public byte[] FramedStream { get; }

        public int[] FeedBatchSizes { get; }

        public byte[][] RecoveredPayloads { get; }
    }

    public static class Gate7FramingGoldenVector
    {
        private const int MaxPayloadLength = 64;

        private static readonly int[] SegmentLengths = { 1, 2, 2, 13, 4, 6 };

        public static Gate7FramingGoldenResult Run()
        {
            byte[] framedStream = Concatenate(
                LengthPrefixedFrameEncoder.Encode(new byte[] { 0xDE, 0xAD, 0xBE }, MaxPayloadLength),
                LengthPrefixedFrameEncoder.Encode(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 }, MaxPayloadLength),
                LengthPrefixedFrameEncoder.Encode(
                    new byte[] { 0xFF, 0x00, 0x7F, 0x80, 0x10, 0x20, 0x30, 0x40 },
                    MaxPayloadLength));
            var decoder = new LengthPrefixedFrameDecoder(MaxPayloadLength);
            var recoveredPayloads = new List<byte[]>();
            int[] feedBatchSizes = new int[SegmentLengths.Length];
            byte[] receiveBuffer = new byte[16];
            int streamOffset = 0;

            for (int index = 0; index < SegmentLengths.Length; index++)
            {
                int segmentLength = SegmentLengths[index];
                Array.Fill(receiveBuffer, (byte)0xA5);
                Array.Copy(framedStream, streamOffset, receiveBuffer, 2, segmentLength);

                byte[][] batch = decoder.Feed(receiveBuffer, 2, segmentLength);
                feedBatchSizes[index] = batch.Length;
                recoveredPayloads.AddRange(batch);

                Array.Fill(receiveBuffer, (byte)0x5A);
                streamOffset += segmentLength;
            }

            return new Gate7FramingGoldenResult(
                framedStream,
                feedBatchSizes,
                recoveredPayloads.ToArray());
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
    }
}
