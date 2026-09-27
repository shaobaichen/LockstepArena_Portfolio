using System;

namespace LockstepArena.StreamFraming
{
    public static class LengthPrefixedFrameEncoder
    {
        public static byte[] Encode(byte[] payload, int maxPayloadLength)
        {
            if (maxPayloadLength < 1 || maxPayloadLength > int.MaxValue - 4)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPayloadLength));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (payload.Length > maxPayloadLength)
            {
                throw new ArgumentOutOfRangeException(nameof(payload));
            }

            byte[] frame = new byte[4 + payload.Length];
            uint length = checked((uint)payload.Length);
            frame[0] = (byte)(length >> 24);
            frame[1] = (byte)(length >> 16);
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
            Array.Copy(payload, 0, frame, 4, payload.Length);
            return frame;
        }
    }
}
