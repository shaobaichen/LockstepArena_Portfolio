using System;
using LockstepArena.Protocol.Wire;

namespace LockstepArena.Client.Demo
{
    public sealed class DemoRoomSummarySnapshot
    {
        public DemoRoomSummarySnapshot(
            ulong roomId,
            string roomName,
            string hostNickname,
            uint participantCount,
            uint capacity,
            RoomLifecycleMessage lifecycle)
        {
            RoomId = roomId;
            RoomName = roomName ?? throw new ArgumentNullException(nameof(roomName));
            HostNickname = hostNickname ?? throw new ArgumentNullException(nameof(hostNickname));
            ParticipantCount = participantCount;
            Capacity = capacity;
            Lifecycle = lifecycle;
        }

        public ulong RoomId { get; }
        public string RoomName { get; }
        public string HostNickname { get; }
        public uint ParticipantCount { get; }
        public uint Capacity { get; }
        public RoomLifecycleMessage Lifecycle { get; }
    }
}
