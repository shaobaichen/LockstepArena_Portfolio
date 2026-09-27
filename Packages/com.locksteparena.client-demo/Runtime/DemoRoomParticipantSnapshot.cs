using System;

namespace LockstepArena.Client.Demo
{
    public sealed class DemoRoomParticipantSnapshot
    {
        public DemoRoomParticipantSnapshot(
            ulong sessionId,
            string nickname,
            bool isHost,
            bool isReady,
            uint joinOrdinal)
        {
            SessionId = sessionId;
            Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
            IsHost = isHost;
            IsReady = isReady;
            JoinOrdinal = joinOrdinal;
        }

        public ulong SessionId { get; }
        public string Nickname { get; }
        public bool IsHost { get; }
        public bool IsReady { get; }
        public uint JoinOrdinal { get; }
    }
}
