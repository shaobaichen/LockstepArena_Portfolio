using System;
using System.Collections.Generic;
using Google.Protobuf;
using LockstepArena.Protocol.Wire;
using LockstepArena.StreamFraming;

namespace LockstepArena.Server.DemoHost
{
    internal enum DemoSessionPhase
    {
        Lobby,
        InRoom,
        PreparingBattle,
        InBattle,
        Settlement,
        Closed
    }

    internal sealed class DemoSession
    {
        private readonly List<PendingControlEvent> _events = new List<PendingControlEvent>();
        private readonly int _maxControlPayloadLength;
        private readonly int _maxPendingControlBytes;
        private int _pendingControlBytes;

        internal DemoSession(ulong sessionId, string nickname, int maxControlPayloadLength, int maxPendingControlBytes)
        {
            SessionId = sessionId;
            Nickname = nickname;
            _maxControlPayloadLength = maxControlPayloadLength;
            _maxPendingControlBytes = maxPendingControlBytes;
            Phase = DemoSessionPhase.Lobby;
        }

        internal ulong SessionId { get; }
        internal string Nickname { get; }
        internal DemoSessionPhase Phase { get; set; }
        internal ulong RoomId { get; set; }
        internal int JoinOrdinal { get; set; }
        internal bool IsReady { get; set; }
        internal bool IsBattleReady { get; set; }
        internal int EventCount => _events.Count;
        internal int PendingControlBytes => _pendingControlBytes;
        internal ServerControlEventMessage LastEvent => _events.Count == 0
            ? throw new InvalidOperationException("Session has no events.")
            : _events[_events.Count - 1].Message;

        internal void Queue(ServerControlEventMessage message)
        {
            PendingControlEvent candidate = Prepare(message);
            EnsureCapacity(candidate.FramedBytes.Length, 0);
            Commit(candidate);
        }

        internal PendingControlEvent Prepare(ServerControlEventMessage message)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            byte[] framed = LengthPrefixedFrameEncoder.Encode(
                message.ToByteArray(),
                _maxControlPayloadLength);
            return new PendingControlEvent(message, framed);
        }

        internal void EnsureCapacity(long additionalBytes, int destinationPendingBytes)
        {
            if ((long)_pendingControlBytes + destinationPendingBytes + additionalBytes > _maxPendingControlBytes)
            {
                throw new ControlCapacityException();
            }
        }

        internal void Commit(PendingControlEvent candidate)
        {
            _events.Add(candidate);
            _pendingControlBytes = checked(_pendingControlBytes + candidate.FramedBytes.Length);
        }

        internal void ClearEvents()
        {
            _events.Clear();
            _pendingControlBytes = 0;
        }

        internal bool TryPeekEvent(out PendingControlEvent? candidate)
        {
            if (_events.Count == 0)
            {
                candidate = null;
                return false;
            }

            candidate = _events[0];
            return true;
        }

        internal void DequeueEvent(PendingControlEvent candidate)
        {
            if (_events.Count == 0 || !ReferenceEquals(_events[0], candidate))
            {
                throw new InvalidOperationException("The pending control event is not at the queue head.");
            }

            _events.RemoveAt(0);
            _pendingControlBytes -= candidate.FramedBytes.Length;
        }
    }

    internal sealed class PendingControlEvent
    {
        internal PendingControlEvent(ServerControlEventMessage message, byte[] framedBytes)
        {
            Message = message;
            FramedBytes = framedBytes;
        }

        internal ServerControlEventMessage Message { get; }
        internal byte[] FramedBytes { get; }
    }

    internal sealed class ControlCapacityException : InvalidOperationException
    {
        internal ControlCapacityException()
            : base("Pending control send capacity is full.")
        {
        }
    }
}
