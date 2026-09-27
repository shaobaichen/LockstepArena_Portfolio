using System;
using System.Collections.Generic;
using System.IO;

namespace LockstepArena.StreamFraming
{
    public sealed class LengthPrefixedFrameDecoder
    {
        private readonly int _maxPayloadLength;
        private readonly byte[] _prefix = new byte[4];
        private int _prefixCount;
        private byte[]? _payload;
        private int _payloadCount;
        private bool _faulted;

        public LengthPrefixedFrameDecoder(int maxPayloadLength)
        {
            if (maxPayloadLength < 1 || maxPayloadLength > int.MaxValue - 4)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPayloadLength));
            }

            _maxPayloadLength = maxPayloadLength;
        }

        public byte[][] Feed(byte[] buffer, int offset, int count)
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The decoder is faulted.");
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0 || count > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0)
            {
                return Array.Empty<byte[]>();
            }

            List<byte[]>? completed = null;
            int end = offset + count;
            while (offset < end)
            {
                if (_payload == null)
                {
                    int prefixBytes = Math.Min(4 - _prefixCount, end - offset);
                    Array.Copy(buffer, offset, _prefix, _prefixCount, prefixBytes);
                    offset += prefixBytes;
                    _prefixCount += prefixBytes;
                    if (_prefixCount < 4)
                    {
                        continue;
                    }

                    uint declaredLength =
                        ((uint)_prefix[0] << 24) |
                        ((uint)_prefix[1] << 16) |
                        ((uint)_prefix[2] << 8) |
                        _prefix[3];
                    _prefixCount = 0;
                    if (declaredLength > (uint)_maxPayloadLength)
                    {
                        _faulted = true;
                        throw new InvalidDataException("Declared payload length exceeds the configured maximum.");
                    }

                    int payloadLength = checked((int)declaredLength);
                    if (payloadLength == 0)
                    {
                        completed ??= new List<byte[]>();
                        completed.Add(Array.Empty<byte>());
                        continue;
                    }

                    _payload = new byte[payloadLength];
                    _payloadCount = 0;
                }

                int payloadBytes = Math.Min(_payload.Length - _payloadCount, end - offset);
                Array.Copy(buffer, offset, _payload, _payloadCount, payloadBytes);
                offset += payloadBytes;
                _payloadCount += payloadBytes;
                if (_payloadCount == _payload.Length)
                {
                    completed ??= new List<byte[]>();
                    completed.Add(_payload);
                    _payload = null;
                    _payloadCount = 0;
                }
            }

            return completed == null ? Array.Empty<byte[]>() : completed.ToArray();
        }
    }
}
