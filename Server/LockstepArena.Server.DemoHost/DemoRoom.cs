using System;
using System.Collections.Generic;

namespace LockstepArena.Server.DemoHost
{
    internal enum DemoRoomLifecycle
    {
        Open,
        PreparingBattle,
        InBattle,
        Settled,
        Removed
    }

    internal sealed class DemoRoom
    {
        private readonly List<DemoSession> _participants;

        internal DemoRoom(ulong roomId, string roomName, ulong hostSessionId, int capacity, DemoSession host)
        {
            RoomId = roomId;
            RoomName = roomName;
            HostSessionId = hostSessionId;
            Capacity = capacity;
            Lifecycle = DemoRoomLifecycle.Open;
            _participants = new List<DemoSession> { host };
        }

        internal ulong RoomId { get; }
        internal string RoomName { get; }
        internal ulong HostSessionId { get; }
        internal int Capacity { get; }
        internal DemoRoomLifecycle Lifecycle { get; set; }
        internal int ParticipantCount => _participants.Count;
        internal BattlePreparation? Preparation { get; set; }

        internal DemoSession GetParticipant(int index)
        {
            if ((uint)index >= (uint)_participants.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _participants[index];
        }

        internal int IndexOf(ulong sessionId)
        {
            for (int index = 0; index < _participants.Count; index++)
            {
                if (_participants[index].SessionId == sessionId) return index;
            }

            return -1;
        }

        internal void Add(DemoSession session)
        {
            _participants.Add(session);
        }

        internal void RemoveAt(int index)
        {
            _participants.RemoveAt(index);
            for (int next = index; next < _participants.Count; next++) _participants[next].JoinOrdinal = next;
        }

        internal DemoSession[] CopyParticipants()
        {
            return _participants.ToArray();
        }
    }
}
