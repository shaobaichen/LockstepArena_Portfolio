using System;
using System.Net.Sockets;
using LockstepArena.Simulation;

namespace LockstepArena.Server.LiveTcp
{
    public sealed class TcpBattleParticipantBinding
    {
        public TcpBattleParticipantBinding(
            TcpClient connectedClient,
            PlayerId playerId,
            PlayerSlot playerSlot)
        {
            ConnectedClient = connectedClient
                ?? throw new ArgumentNullException(nameof(connectedClient));
            PlayerId = playerId;
            PlayerSlot = playerSlot;
        }

        public TcpClient ConnectedClient { get; }

        public PlayerId PlayerId { get; }

        public PlayerSlot PlayerSlot { get; }
    }
}
