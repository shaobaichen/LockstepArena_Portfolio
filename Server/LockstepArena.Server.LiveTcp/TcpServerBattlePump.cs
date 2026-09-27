using System;
using System.IO;
using System.Net.Sockets;
using LockstepArena.Server.ProtocolAuthority;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.Server.LiveTcp
{
    public sealed class TcpServerBattlePump : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly LengthPrefixedFrameDecoder _decoder;
        private readonly ProtocolAuthorityProcessor _processor;
        private readonly byte[] _receiveBuffer;
        private readonly int _receiveOffset;
        private readonly int _receiveReadCapacity;
        private readonly int _maxPayloadLength;
        private bool _disposed;
        private bool _faulted;

        public TcpServerBattlePump(
            TcpClient connectedClient,
            BattleState initialState,
            uint inputDelayTicks,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity,
            int maxPayloadLength,
            int receiveBufferLength,
            int receiveOffset,
            int receiveReadCapacity)
        {
            if (connectedClient is null)
            {
                throw new ArgumentNullException(nameof(connectedClient));
            }

            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (receiveBufferLength < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveBufferLength));
            }

            if (receiveOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveOffset));
            }

            if (receiveReadCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveReadCapacity));
            }

            if (receiveReadCapacity > receiveBufferLength ||
                receiveOffset > receiveBufferLength - receiveReadCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveOffset));
            }

            var decoder = new LengthPrefixedFrameDecoder(maxPayloadLength);
            if (connectedClient.Client.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("The connected client must use IPv4.", nameof(connectedClient));
            }

            if (!connectedClient.Connected)
            {
                throw new InvalidOperationException("The TCP client must already be connected.");
            }

            NetworkStream stream = connectedClient.GetStream();
            var receiveBuffer = new byte[receiveBufferLength];
            var processor = new ProtocolAuthorityProcessor(
                initialState,
                inputDelayTicks,
                maxFutureTickOffset,
                authoritativeHistoryCapacity);

            _client = connectedClient;
            _stream = stream;
            _decoder = decoder;
            _processor = processor;
            _receiveBuffer = receiveBuffer;
            _receiveOffset = receiveOffset;
            _receiveReadCapacity = receiveReadCapacity;
            _maxPayloadLength = maxPayloadLength;
        }

        public BattleState ServerState => _processor.ServerState;

        public uint NextPublishTick => _processor.NextPublishTick;

        public int PumpOnce()
        {
            ThrowIfDisposed();
            if (_faulted)
            {
                throw new InvalidOperationException("The TCP server battle pump is faulted.");
            }

            try
            {
                int written = 0;
                if (_client.Client.Poll(0, SelectMode.SelectRead))
                {
                    if (_client.Client.Available == 0)
                    {
                        throw new EndOfStreamException("The TCP stream ended before battle completion.");
                    }

                    int bytesRead = _stream.Read(
                        _receiveBuffer,
                        _receiveOffset,
                        _receiveReadCapacity);
                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException("The TCP stream ended before battle completion.");
                    }

                    byte[][] submissions = _decoder.Feed(
                        _receiveBuffer,
                        _receiveOffset,
                        bytesRead);
                    for (int index = 0; index < submissions.Length; index++)
                    {
                        written = checked(written + WritePayloads(
                            _processor.SubmitPlayerInputPayload(submissions[index])));
                    }
                }

                written = checked(written + WritePayloads(_processor.PollAuthority()));
                return written;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _stream.Dispose();
            }
            finally
            {
                _client.Dispose();
            }
        }

        private int WritePayloads(byte[][] payloads)
        {
            int written = 0;
            for (int index = 0; index < payloads.Length; index++)
            {
                byte[] framed = LengthPrefixedFrameEncoder.Encode(
                    payloads[index],
                    _maxPayloadLength);
                _stream.Write(framed, 0, framed.Length);
                written++;
            }

            return written;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TcpServerBattlePump));
            }
        }
    }
}
