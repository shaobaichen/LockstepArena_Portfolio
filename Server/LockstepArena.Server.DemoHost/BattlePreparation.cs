using System;
using System.Net.Sockets;
using System.Diagnostics;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.LiveTcp;
using LockstepArena.Simulation;

namespace LockstepArena.Server.DemoHost
{
    internal sealed class BattlePreparation
    {
        private readonly DemoSession[] _participants;
        private readonly byte[][] _tickets;
        private readonly BattlePreparingEventMessage[] _events;
        private readonly bool[] _reserved;
        private readonly bool[] _used;
        private readonly TcpClient?[] _attachedClients;
        private readonly long _startedTimestamp;
        private readonly TimeSpan _readyTimeout;

        internal BattlePreparation(ulong battleId, BattleState initialState, uint finalStateTick, DemoSession[] participants, byte[][] tickets, BattlePreparingEventMessage[] events, TimeSpan readyTimeout)
        {
            BattleId = battleId;
            InitialState = initialState;
            FinalStateTick = finalStateTick;
            _participants = (DemoSession[])participants.Clone();
            _tickets = new byte[tickets.Length][];
            for (int index = 0; index < tickets.Length; index++) _tickets[index] = (byte[])tickets[index].Clone();
            _events = (BattlePreparingEventMessage[])events.Clone();
            _reserved = new bool[tickets.Length];
            _used = new bool[tickets.Length];
            _attachedClients = new TcpClient?[tickets.Length];
            _startedTimestamp = Stopwatch.GetTimestamp();
            _readyTimeout = readyTimeout;
        }

        internal ulong BattleId { get; }
        internal BattleState InitialState { get; }
        internal uint FinalStateTick { get; }
        internal int ParticipantCount => _participants.Length;
        internal int AttachedCount { get; private set; }
        internal bool IsInvalidated { get; private set; }
        internal TcpSharedBattleSession? SharedSession { get; private set; }
        internal bool HasReportedStatus { get; private set; }
        internal uint LastReportedStateTick { get; private set; }
        internal uint LastReportedNextPublishTick { get; private set; }
        internal bool RequiresBattleReady => InitialState.IsGameplayEnabled;
        internal bool ReadyTimedOut => RequiresBattleReady && Stopwatch.GetElapsedTime(_startedTimestamp) >= _readyTimeout;
        internal bool AllParticipantsReady
        {
            get
            {
                if (AttachedCount != ParticipantCount) return false;
                if (!RequiresBattleReady) return true;
                for (int index = 0; index < _participants.Length; index++)
                    if (!_participants[index].IsBattleReady) return false;
                return true;
            }
        }

        internal DemoSession GetParticipant(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return _participants[slot.Value];
        }

        internal byte[] GetTicket(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return (byte[])_tickets[slot.Value].Clone();
        }

        internal BattlePreparingEventMessage GetPreparingEvent(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return _events[slot.Value].Clone();
        }

        internal bool TryReserveTicket(byte[] candidate, out PlayerSlot slot)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidate));
            if (!IsInvalidated && candidate.Length == 16)
            {
                for (int index = 0; index < _tickets.Length; index++)
                {
                    if (!_used[index] && !_reserved[index] && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(candidate, _tickets[index]))
                    {
                        _reserved[index] = true;
                        slot = new PlayerSlot(index);
                        return true;
                    }
                }
            }

            slot = default;
            return false;
        }

        internal void ReleaseReservation(PlayerSlot slot)
        {
            ValidateSlot(slot);
            if (!_used[slot.Value]) _reserved[slot.Value] = false;
        }

        internal void CommitAttachment(PlayerSlot slot, TcpClient client)
        {
            ValidateSlot(slot);
            if (IsInvalidated || !_reserved[slot.Value] || _used[slot.Value]) throw new InvalidOperationException("Ticket reservation is no longer valid.");
            _attachedClients[slot.Value] = client ?? throw new ArgumentNullException(nameof(client));
            _used[slot.Value] = true;
            _reserved[slot.Value] = false;
            AttachedCount++;
        }

        internal TcpClient GetAttachedClient(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return _attachedClients[slot.Value] ?? throw new InvalidOperationException("Participant is not attached.");
        }

        internal bool IsAttached(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return _attachedClients[slot.Value] is not null;
        }

        internal bool TryGetDisconnectedSlot(out PlayerSlot slot)
        {
            for (int index = 0; index < _attachedClients.Length; index++)
            {
                TcpClient? client = _attachedClients[index];
                if (client is null) continue;
                try
                {
                    Socket socket = client.Client;
                    if (!socket.Poll(0, SelectMode.SelectRead) || socket.Available != 0) continue;
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                slot = new PlayerSlot(index);
                return true;
            }

            slot = default;
            return false;
        }

        internal void Activate(DemoServerOptions options)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            if (IsInvalidated) throw new InvalidOperationException("Preparation is invalidated.");
            if (SharedSession is not null) return;
            if (AttachedCount != ParticipantCount) throw new InvalidOperationException("Every participant must attach first.");
            var bindings = new TcpBattleParticipantBinding[ParticipantCount];
            for (int index = 0; index < bindings.Length; index++)
            {
                var slot = new PlayerSlot(index);
                bindings[index] = new TcpBattleParticipantBinding(GetAttachedClient(slot), InitialState.Roster.GetPlayerId(slot), slot);
            }
            SharedSession = new TcpSharedBattleSession(InitialState, bindings, options.InputDelayTicks, options.MaxFutureTickOffset, options.AuthoritativeHistoryCapacity, options.MaxBattlePayloadLength, options.BattleReceiveBufferLength, options.BattleReceiveOffset, options.BattleReceiveReadCapacity);
        }

        internal void Invalidate()
        {
            if (IsInvalidated) return;
            IsInvalidated = true;
            if (SharedSession is not null)
            {
                SharedSession.Dispose();
                return;
            }
            for (int index = 0; index < _attachedClients.Length; index++) _attachedClients[index]?.Dispose();
        }

        internal bool StatusChanged(uint stateTick, uint nextPublishTick)
        {
            return !HasReportedStatus ||
                LastReportedStateTick != stateTick ||
                LastReportedNextPublishTick != nextPublishTick;
        }

        internal void CommitReportedStatus(uint stateTick, uint nextPublishTick)
        {
            HasReportedStatus = true;
            LastReportedStateTick = stateTick;
            LastReportedNextPublishTick = nextPublishTick;
        }

        private void ValidateSlot(PlayerSlot slot)
        {
            if (slot.Value >= _participants.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
