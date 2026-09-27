using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.Server.DemoHost
{
    public sealed class TcpDemoServer : IDisposable
    {
        private readonly DemoServerOptions _options;
        private readonly List<DemoSession> _sessions = new List<DemoSession>();
        private readonly List<DemoRoom> _rooms = new List<DemoRoom>();
        private readonly TcpListener _controlListener;
        private readonly TcpListener _battleListener;
        private readonly List<ControlConnection> _controlConnections = new List<ControlConnection>();
        private readonly List<BattleAttachment> _battleAttachments = new List<BattleAttachment>();
        private ulong _nextSessionId = 1;
        private ulong _nextRoomId = 1;
        private ulong _nextBattleId = 1;
        private ulong _nextControlAcceptOrdinal = 1;
        private bool _disposed;

        public TcpDemoServer(DemoServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            var controlListener = new TcpListener(options.BindAddress, options.ControlPort);
            var battleListener = new TcpListener(options.BindAddress, options.BattlePort);
            try
            {
                controlListener.Start();
                battleListener.Start();
            }
            catch
            {
                controlListener.Stop();
                battleListener.Stop();
                throw;
            }

            _controlListener = controlListener;
            _battleListener = battleListener;
            ControlPort = ((IPEndPoint)controlListener.LocalEndpoint).Port;
            BattlePort = ((IPEndPoint)battleListener.LocalEndpoint).Port;
            BindAddress = options.BindAddress;
        }

        public int ControlPort { get; }
        public int BattlePort { get; }
        public IPAddress BindAddress { get; }
        public int SessionCount => _sessions.Count;
        public int RoomCount => _rooms.Count;

        public DemoServerPumpResult PumpOnce()
        {
            ThrowIfDisposed();
            int accepted = AcceptControlOnce();
            int acceptedBattle = AcceptBattleOnce();
            int processed = 0;
            ControlConnection[] connections = _controlConnections.ToArray();
            Array.Sort(connections, CompareControlConnections);
            for (int index = 0; index < connections.Length; index++)
            {
                ControlConnection connection = connections[index];
                if (connection.Closed) continue;
                try
                {
                    connection.PumpSend(_options.MaxControlSendBytesPerPump);
                    connection.PumpRead();
                    int remaining = _options.MaxControlMessagesPerPump;
                    while (remaining > 0 && !connection.Closed && connection.TryPeekPayload(out byte[]? payload))
                    {
                        try
                        {
                            ProcessCommand(connection, payload!);
                            connection.DequeuePayload(payload!);
                            processed++;
                            remaining--;
                        }
                        catch (ControlCapacityException)
                        {
                            break;
                        }
                    }
                }
                catch
                {
                    CloseControl(connection);
                }
            }

            DrainAllSessionEvents();
            ProgressBattleAttachments();
            ProgressBattlePreparations();
            int published = PumpBattles(out int completed, out int aborted);
            DrainAllSessionEvents();
            return new DemoServerPumpResult(accepted, acceptedBattle, processed, published, completed, aborted);
        }

        internal DemoSession EnterSession(string nickname)
        {
            return EnterSession(nickname, null);
        }

        private DemoSession EnterSession(string nickname, ControlConnection? destination)
        {
            ThrowIfDisposed();
            string normalized = ValidateName(nickname, 32, nameof(nickname));
            if (_sessions.Count >= _options.MaxSessions) throw new InvalidOperationException("Session capacity is full.");
            for (int index = 0; index < _sessions.Count; index++)
            {
                if (string.Equals(_sessions[index].Nickname, normalized, StringComparison.Ordinal)) throw new InvalidOperationException("Nickname is already active.");
            }

            ulong id = PeekNext(_nextSessionId, "Session identifiers are exhausted.");
            var session = new DemoSession(
                id,
                normalized,
                _options.MaxControlPayloadLength,
                _options.MaxPendingControlBytesPerSession);
            var events = new ControlEventBatch(this);
            events.Add(session, new ServerControlEventMessage
            {
                SessionEntered = new SessionEnteredEventMessage { SessionId = id, Nickname = normalized },
            }, destination);
            events.Preflight();
            _sessions.Add(session);
            CommitNext(ref _nextSessionId, id);
            events.Commit();
            return session;
        }

        internal DemoRoom CreateRoom(ulong sessionId, string roomName, int capacity)
        {
            ThrowIfDisposed();
            DemoSession host = GetSession(sessionId);
            RequirePhase(host, DemoSessionPhase.Lobby);
            string normalized = ValidateName(roomName, 48, nameof(roomName));
            if (capacity < 2 || capacity > _options.MaxRoomCapacity) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (_rooms.Count >= _options.MaxRooms) throw new InvalidOperationException("Room capacity is full.");
            ulong id = PeekNext(_nextRoomId, "Room identifiers are exhausted.");

            var room = new DemoRoom(id, normalized, sessionId, capacity, host);
            DemoSession[] participants = { host };
            var events = new ControlEventBatch(this);
            AddSnapshotEvents(events, room, participants, null, false);
            events.Preflight();
            host.RoomId = id;
            host.JoinOrdinal = 0;
            host.IsReady = false;
            host.Phase = DemoSessionPhase.InRoom;
            _rooms.Add(room);
            CommitNext(ref _nextRoomId, id);
            events.Commit();
            return room;
        }

        internal RoomSummaryMessage[] GetRoomSummaries()
        {
            ThrowIfDisposed();
            var result = new RoomSummaryMessage[_rooms.Count];
            for (int index = 0; index < _rooms.Count; index++)
            {
                DemoRoom room = _rooms[index];
                result[index] = new RoomSummaryMessage
                {
                    RoomId = room.RoomId,
                    RoomName = room.RoomName,
                    HostNickname = room.GetParticipant(0).Nickname,
                    ParticipantCount = checked((uint)room.ParticipantCount),
                    Capacity = checked((uint)room.Capacity),
                    Lifecycle = ServerControlProtocol.ToWire(room.Lifecycle),
                };
            }

            return result;
        }

        internal void JoinRoom(ulong sessionId, ulong roomId)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            RequirePhase(session, DemoSessionPhase.Lobby);
            DemoRoom room = GetRoom(roomId);
            if (room.Lifecycle != DemoRoomLifecycle.Open) throw new InvalidOperationException("Room is not open.");
            if (room.IndexOf(sessionId) >= 0) throw new InvalidOperationException("Session is already in the room.");
            if (room.ParticipantCount >= room.Capacity) throw new InvalidOperationException("Room is full.");

            DemoSession[] participants = AppendParticipant(room.CopyParticipants(), session);
            var events = new ControlEventBatch(this);
            AddSnapshotEvents(events, room, participants, null, false);
            events.Preflight();
            session.RoomId = roomId;
            session.JoinOrdinal = room.ParticipantCount;
            session.IsReady = false;
            session.Phase = DemoSessionPhase.InRoom;
            room.Add(session);
            events.Commit();
        }

        internal void SetReady(ulong sessionId, bool isReady)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            RequirePhase(session, DemoSessionPhase.InRoom);
            DemoRoom room = GetRoom(session.RoomId);
            if (room.Lifecycle != DemoRoomLifecycle.Open) throw new InvalidOperationException("Room is not open.");
            DemoSession[] participants = room.CopyParticipants();
            var events = new ControlEventBatch(this);
            AddSnapshotEvents(events, room, participants, session, isReady);
            events.Preflight();
            session.IsReady = isReady;
            events.Commit();
        }

        internal void LeaveRoom(ulong sessionId)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            RequirePhase(session, DemoSessionPhase.InRoom);
            DemoRoom room = GetRoom(session.RoomId);
            if (room.Lifecycle != DemoRoomLifecycle.Open) throw new InvalidOperationException("Room membership is frozen.");
            if (session.SessionId == room.HostSessionId)
            {
                RemoveOpenHostRoom(room, session.SessionId, true);
                return;
            }

            int participantIndex = room.IndexOf(sessionId);
            if (participantIndex < 0) throw new InvalidOperationException("Session is not in its room.");
            DemoSession[] survivors = RemoveParticipant(room.CopyParticipants(), participantIndex);
            var events = new ControlEventBatch(this);
            events.Add(session, CreateLobbyEntered());
            AddSnapshotEvents(events, room, survivors, null, false);
            events.Preflight();
            room.RemoveAt(participantIndex);
            MoveToLobbyState(session);
            events.Commit();
        }

        internal BattlePreparation StartBattle(ulong sessionId)
        {
            ThrowIfDisposed();
            DemoSession requester = GetSession(sessionId);
            RequirePhase(requester, DemoSessionPhase.InRoom);
            DemoRoom room = GetRoom(requester.RoomId);
            if (room.Lifecycle != DemoRoomLifecycle.Open) throw new InvalidOperationException("Room is not open.");
            if (room.HostSessionId != sessionId) throw new InvalidOperationException("Only the host may start.");
            if (room.ParticipantCount != room.Capacity) throw new InvalidOperationException("Room is not full.");
            DemoSession[] participants = room.CopyParticipants();
            for (int index = 0; index < participants.Length; index++)
            {
                if (!participants[index].IsReady) throw new InvalidOperationException("Every participant must be ready.");
                if (participants[index].Phase != DemoSessionPhase.InRoom) throw new InvalidOperationException("Participant control session is not active.");
            }

            if (room.Preparation is not null) throw new InvalidOperationException("Room is already prepared.");
            ulong battleId = PeekNext(_nextBattleId, "Battle identifiers are exhausted.");
            ulong finalTickWide = _options.BattleDurationTicks;
            if (finalTickWide > uint.MaxValue) throw new InvalidOperationException("Battle duration exceeds Tick range.");
            if (_options.SpawnStateCount < participants.Length) throw new InvalidOperationException("Spawn configuration is incomplete.");

            var playerIds = new PlayerId[participants.Length];
            for (int index = 0; index < participants.Length; index++)
            {
                playerIds[index] = new PlayerId(participants[index].SessionId);
            }

            var roster = new ActiveRoster(playerIds);
            BattleState initialState;
            if (_options.BattleDefinition is not null)
            {
                initialState = BattleState.CreateGameplayInitial(roster, _options.BattleDefinition);
            }
            else
            {
                var states = new PlayerState[participants.Length];
                for (int index = 0; index < participants.Length; index++)
                    states[index] = _options.GetSpawnState(new PlayerSlot(index));
                initialState = BattleState.CreateInitial(roster, states);
            }
            uint finalStateTick = checked(initialState.Tick + _options.BattleDurationTicks);
            BattleBootstrapMessage bootstrap = ProtocolMapper.ToWireBattleBootstrap(battleId, initialState, _options.BattleDurationTicks, _options.InputDelayTicks, finalStateTick);
            var tickets = new byte[participants.Length][];
            var events = new BattlePreparingEventMessage[participants.Length];
            for (int index = 0; index < participants.Length; index++)
            {
                byte[] ticket = CreateUniqueTicket(tickets, index);
                tickets[index] = ticket;
                events[index] = new BattlePreparingEventMessage
                {
                    RoomId = room.RoomId,
                    BattleId = battleId,
                    BattleTicket = ByteString.CopyFrom(ticket),
                    BattlePort = checked((uint)BattlePort),
                    LocalPlayerId = participants[index].SessionId,
                    LocalPlayerSlot = checked((uint)index),
                    Bootstrap = bootstrap.Clone(),
                };
            }

            var preparation = new BattlePreparation(battleId, initialState, finalStateTick, participants, tickets, events, _options.BattleReadyTimeout);
            var outbound = new ControlEventBatch(this);
            for (int index = 0; index < participants.Length; index++)
            {
                outbound.Add(participants[index], new ServerControlEventMessage { BattlePreparing = events[index].Clone() });
            }
            outbound.Preflight();
            for (int index = 0; index < participants.Length; index++)
            {
                participants[index].Phase = DemoSessionPhase.PreparingBattle;
                participants[index].IsBattleReady = false;
            }

            room.Preparation = preparation;
            room.Lifecycle = DemoRoomLifecycle.PreparingBattle;
            CommitNext(ref _nextBattleId, battleId);
            outbound.Commit();
            return preparation;
        }

        internal DemoRoom GetRoom(ulong roomId)
        {
            return FindRoom(roomId) ?? throw new InvalidOperationException("Room was not found.");
        }

        private DemoRoom? FindRoom(ulong roomId)
        {
            for (int index = 0; index < _rooms.Count; index++)
                if (_rooms[index].RoomId == roomId) return _rooms[index];
            return null;
        }

        internal void MarkBattleReady(ulong sessionId, ulong battleId, ulong battleConfigHash)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            RequirePhase(session, DemoSessionPhase.PreparingBattle);
            DemoRoom room = GetRoom(session.RoomId);
            BattlePreparation preparation = room.Preparation ?? throw new InvalidOperationException("Battle preparation is missing.");
            if (preparation.BattleId != battleId) throw new InvalidOperationException("BattleReady does not match the prepared battle.");
            if (!preparation.InitialState.IsGameplayEnabled) throw new InvalidOperationException("BattleReady is not used by this battle.");
            ulong expectedHash = BattleConfigHash.Compute(preparation.InitialState.Definition!);
            if (battleConfigHash != expectedHash)
            {
                AbortPreparationToLobby(room, "Battle configuration mismatch.", null);
                return;
            }

            var slot = new PlayerSlot(session.JoinOrdinal);
            if (!ReferenceEquals(preparation.GetParticipant(slot), session) || !preparation.IsAttached(slot))
                throw new InvalidOperationException("Battle connection must attach before BattleReady.");
            session.IsBattleReady = true;
            TryActivatePreparation(room);
        }

        internal DemoSession GetSession(ulong sessionId)
        {
            for (int index = 0; index < _sessions.Count; index++) if (_sessions[index].SessionId == sessionId) return _sessions[index];
            throw new InvalidOperationException("Session was not found.");
        }

        internal void CloseSession(ulong sessionId)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            if (session.Phase == DemoSessionPhase.PreparingBattle)
            {
                AbortPreparationToLobby(GetRoom(session.RoomId), "A participant disconnected during battle preparation.", session);
            }
            else if (session.Phase == DemoSessionPhase.InBattle)
            {
                DemoRoom room = GetRoom(session.RoomId);
                if (room.Preparation?.InitialState.IsGameplayEnabled == true)
                    CompleteForfeit(room, new PlayerSlot(session.JoinOrdinal),
                        "A participant control connection ended.", session);
                else
                    AbortRoom(room, "A participant control connection ended.", session);
            }
            else if (session.Phase == DemoSessionPhase.Settlement)
            {
                if (session.RoomId != 0) RemoveSettledParticipant(session);
            }
            else if (session.Phase == DemoSessionPhase.InRoom)
            {
                DemoRoom room = GetRoom(session.RoomId);
                if (room.Lifecycle == DemoRoomLifecycle.Open && room.HostSessionId == sessionId) RemoveOpenHostRoom(room, sessionId, false);
                else if (room.Lifecycle == DemoRoomLifecycle.Open)
                {
                    int index = room.IndexOf(sessionId);
                    if (index >= 0) RemoveOpenParticipant(room, session, false);
                }
            }

            session.Phase = DemoSessionPhase.Closed;
            _sessions.Remove(session);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _controlListener.Stop();
            _battleListener.Stop();
            for (int index = 0; index < _controlConnections.Count; index++) _controlConnections[index].Dispose();
            _controlConnections.Clear();
            for (int index = 0; index < _battleAttachments.Count; index++) _battleAttachments[index].Dispose();
            _battleAttachments.Clear();
            for (int index = 0; index < _rooms.Count; index++) _rooms[index].Preparation?.Invalidate();
            _rooms.Clear();
            _sessions.Clear();
        }

        private int AcceptControlOnce()
        {
            if (_controlConnections.Count >= _options.MaxSessions || !_controlListener.Server.Poll(0, SelectMode.SelectRead)) return 0;
            ulong acceptOrdinal = PeekNext(_nextControlAcceptOrdinal, "Control connection accept ordinals are exhausted.");
            TcpClient client = _controlListener.AcceptTcpClient();
            if (client.Client.AddressFamily != AddressFamily.InterNetwork)
            {
                client.Dispose();
                return 0;
            }

            _controlConnections.Add(new ControlConnection(client, _options, acceptOrdinal));
            CommitNext(ref _nextControlAcceptOrdinal, acceptOrdinal);
            return 1;
        }

        private static int CompareControlConnections(ControlConnection left, ControlConnection right)
        {
            DemoSession? leftSession = left.Session;
            DemoSession? rightSession = right.Session;
            if (leftSession is null && rightSession is null)
            {
                return left.AcceptOrdinal.CompareTo(right.AcceptOrdinal);
            }
            if (leftSession is null) return -1;
            if (rightSession is null) return 1;
            return leftSession.SessionId.CompareTo(rightSession.SessionId);
        }

        private int AcceptBattleOnce()
        {
            if (!_battleListener.Server.Poll(0, SelectMode.SelectRead)) return 0;
            TcpClient client = _battleListener.AcceptTcpClient();
            if (client.Client.AddressFamily != AddressFamily.InterNetwork)
            {
                client.Dispose();
                return 0;
            }

            _battleAttachments.Add(new BattleAttachment(client));
            return 1;
        }

        private void ProgressBattleAttachments()
        {
            BattleAttachment[] attachments = _battleAttachments.ToArray();
            for (int index = 0; index < attachments.Length; index++)
            {
                BattleAttachment attachment = attachments[index];
                if (attachment.Closed) continue;
                try
                {
                    if (!attachment.Progress(this)) continue;
                    _battleAttachments.Remove(attachment);
                    BattlePreparation preparation = attachment.Preparation!;
                    preparation.CommitAttachment(attachment.Slot, attachment.DetachClient());
                    TryActivatePreparation(GetRoomForPreparation(preparation));
                }
                catch
                {
                    attachment.ReleaseReservation();
                    attachment.Dispose();
                    _battleAttachments.Remove(attachment);
                }
            }
        }

        private void ProgressBattlePreparations()
        {
            DemoRoom[] rooms = _rooms.ToArray();
            for (int index = 0; index < rooms.Length; index++)
            {
                DemoRoom room = rooms[index];
                BattlePreparation? preparation = room.Preparation;
                if (room.Lifecycle == DemoRoomLifecycle.PreparingBattle &&
                    preparation is not null && preparation.ReadyTimedOut)
                    AbortPreparationToLobby(room, "BattleReady timed out.", null);
            }
        }

        private void TryActivatePreparation(DemoRoom room)
        {
            BattlePreparation preparation = room.Preparation ?? throw new InvalidOperationException("Battle preparation is missing.");
            if (!preparation.AllParticipantsReady || preparation.SharedSession is not null) return;
            DemoSession[] participants = room.CopyParticipants();
            var events = new ControlEventBatch(this);
            for (int index = 0; index < participants.Length; index++)
                events.Add(participants[index], new ServerControlEventMessage
                {
                    BattleStarted = new BattleStartedEventMessage { BattleId = preparation.BattleId },
                });
            events.Preflight();
            preparation.Activate(_options);
            room.Lifecycle = DemoRoomLifecycle.InBattle;
            for (int index = 0; index < participants.Length; index++)
                participants[index].Phase = DemoSessionPhase.InBattle;
            events.Commit();
        }

        private void AbortPreparationToLobby(DemoRoom room, string detail, DemoSession? disconnected)
        {
            BattlePreparation? preparation = room.Preparation;
            DemoSession[] participants = room.CopyParticipants();
            preparation?.Invalidate();
            for (int index = _battleAttachments.Count - 1; index >= 0; index--)
            {
                if (!ReferenceEquals(_battleAttachments[index].Preparation, preparation)) continue;
                _battleAttachments[index].Dispose();
                _battleAttachments.RemoveAt(index);
            }
            room.Lifecycle = DemoRoomLifecycle.Removed;
            _rooms.Remove(room);
            for (int index = 0; index < participants.Length; index++)
            {
                DemoSession participant = participants[index];
                if (participant.Phase == DemoSessionPhase.Closed || ReferenceEquals(participant, disconnected)) continue;
                MoveToLobbyState(participant);
                TryNotifyAfterCleanup(participant, CreateLobbyEntered());
            }
            _ = detail;
        }

        private int PumpBattles(out int completed, out int aborted)
        {
            int published = 0;
            completed = 0;
            aborted = 0;
            DemoRoom[] rooms = _rooms.ToArray();
            for (int index = 0; index < rooms.Length; index++)
            {
                DemoRoom room = rooms[index];
                if (room.Lifecycle != DemoRoomLifecycle.InBattle || room.Preparation?.SharedSession is null) continue;
                try
                {
                    BattlePreparation preparation = room.Preparation;
                    if (preparation.TryGetDisconnectedSlot(out PlayerSlot disconnectedSlot))
                    {
                        if (preparation.InitialState.IsGameplayEnabled)
                        {
                            CompleteForfeit(room, disconnectedSlot,
                                "A participant battle connection ended.", null);
                            completed++;
                        }
                        else
                        {
                            AbortRoom(room, "Battle session failed.");
                            aborted++;
                        }
                        continue;
                    }

                    published = checked(published + preparation.SharedSession.PumpOnce());
                    uint stateTick = preparation.SharedSession.ServerState.Tick;
                    uint nextPublishTick = preparation.SharedSession.NextPublishTick;
                    if (stateTick > preparation.FinalStateTick)
                    {
                        throw new InvalidOperationException("Battle state advanced beyond the final Tick.");
                    }
                    if (preparation.StatusChanged(stateTick, nextPublishTick))
                    {
                        QueueBattleStatus(room, preparation.BattleId, stateTick, nextPublishTick);
                        preparation.CommitReportedStatus(stateTick, nextPublishTick);
                    }
                    if (preparation.SharedSession.ServerState.IsGameplayEnabled &&
                        preparation.SharedSession.ServerState.Phase == BattlePhase.MatchEnded)
                    {
                        CompleteBattle(room, preparation.SharedSession.ServerState,
                            BattleSettlementReasonMessage.BattleSettlementReasonMatchCompleted,
                            "Match completed.");
                        completed++;
                    }
                    else if (stateTick == preparation.FinalStateTick)
                    {
                        CompleteBattle(room, preparation.SharedSession.ServerState,
                            BattleSettlementReasonMessage.BattleSettlementReasonTickLimitReached,
                            "Battle safety Tick limit reached.");
                        completed++;
                    }
                }
                catch
                {
                    AbortRoom(room, "Battle session failed.");
                    aborted++;
                }
            }
            return published;
        }

        private void QueueBattleStatus(DemoRoom room, ulong battleId, uint stateTick, uint nextPublishTick)
        {
            DemoSession[] participants = room.CopyParticipants();
            var events = new ControlEventBatch(this);
            for (int index = 0; index < participants.Length; index++)
            {
                events.Add(participants[index], new ServerControlEventMessage
                {
                    BattleStatus = new BattleStatusEventMessage
                    {
                        BattleId = battleId,
                        ServerStateTick = stateTick,
                        NextPublishTick = nextPublishTick,
                    },
                });
            }
            events.Preflight();
            events.Commit();
        }

        private void CompleteBattle(
            DemoRoom room,
            BattleState state,
            BattleSettlementReasonMessage reason,
            string detail,
            PlayerSlot? forcedWinnerSlot = null,
            DemoSession? disconnected = null)
        {
            BattlePreparation preparation = room.Preparation ?? throw new InvalidOperationException("Battle preparation is missing.");
            var finalState = new FinalBattleStateMessage
            {
                Tick = state.Tick,
                StateDigest = StateDigest.Compute(state),
            };
            for (int index = 0; index < state.PlayerCount; index++)
            {
                var slot = new PlayerSlot(index);
                PlayerState player = state.GetPlayerState(slot);
                finalState.PlayerStates.Add(new SettlementPlayerStateMessage
                {
                    PlayerSlot = checked((uint)index),
                    PlayerId = state.Roster.GetPlayerId(slot).Value,
                    PositionX = player.PositionX,
                    PositionZ = player.PositionZ,
                    Aim = player.Aim,
                });
            }

            PlayerSlot? winnerSlot = forcedWinnerSlot ?? state.MatchWinnerSlot;
            ulong winnerPlayerId = winnerSlot.HasValue
                ? state.Roster.GetPlayerId(winnerSlot.Value).Value
                : 0UL;
            uint slot0RoundWins = state.PlayerCount > 0
                ? checked((uint)state.GetPlayerState(new PlayerSlot(0)).RoundWins)
                : 0U;
            uint slot1RoundWins = state.PlayerCount > 1
                ? checked((uint)state.GetPlayerState(new PlayerSlot(1)).RoundWins)
                : 0U;

            DemoSession[] participants = room.CopyParticipants();
            var events = new ControlEventBatch(this);
            for (int index = 0; index < participants.Length; index++)
            {
                if (ReferenceEquals(participants[index], disconnected)) continue;
                events.Add(participants[index], new ServerControlEventMessage
                {
                    BattleSettlement = new BattleSettlementEventMessage
                    {
                        BattleId = preparation.BattleId,
                        Reason = reason,
                        FinalState = finalState.Clone(),
                        Detail = detail,
                        WinnerPlayerId = winnerPlayerId,
                        Slot0RoundWins = slot0RoundWins,
                        Slot1RoundWins = slot1RoundWins,
                    },
                });
            }
            events.Preflight();
            room.Lifecycle = DemoRoomLifecycle.Settled;
            for (int index = 0; index < participants.Length; index++)
            {
                participants[index].Phase = DemoSessionPhase.Settlement;
            }
            events.Commit();
        }

        private void CompleteForfeit(
            DemoRoom room,
            PlayerSlot disconnectedSlot,
            string detail,
            DemoSession? disconnected)
        {
            BattlePreparation preparation = room.Preparation ??
                throw new InvalidOperationException("Battle preparation is missing.");
            if (preparation.SharedSession is null)
                throw new InvalidOperationException("Battle session is not active.");
            if (disconnectedSlot.Value >= preparation.InitialState.PlayerCount)
                throw new ArgumentOutOfRangeException(nameof(disconnectedSlot));
            var winnerSlot = new PlayerSlot(disconnectedSlot.Value == 0 ? 1 : 0);
            CompleteBattle(room, preparation.SharedSession.ServerState,
                BattleSettlementReasonMessage.BattleSettlementReasonDisconnectForfeit,
                detail, winnerSlot, disconnected);
        }

        private bool TryReserveTicket(byte[] ticket, out BattlePreparation? preparation, out PlayerSlot slot)
        {
            for (int index = 0; index < _rooms.Count; index++)
            {
                BattlePreparation? candidate = _rooms[index].Preparation;
                if (candidate is not null && candidate.TryReserveTicket(ticket, out slot))
                {
                    preparation = candidate;
                    return true;
                }
            }
            preparation = null;
            slot = default;
            return false;
        }

        private DemoRoom GetRoomForPreparation(BattlePreparation preparation)
        {
            for (int index = 0; index < _rooms.Count; index++) if (ReferenceEquals(_rooms[index].Preparation, preparation)) return _rooms[index];
            throw new InvalidOperationException("Preparation no longer belongs to an active room.");
        }

        private void AbortRoom(DemoRoom room, string detail, DemoSession? disconnected = null)
        {
            BattlePreparation? preparation = room.Preparation;
            DemoSession[] participants = room.CopyParticipants();
            preparation?.Invalidate();
            for (int index = _battleAttachments.Count - 1; index >= 0; index--)
            {
                if (ReferenceEquals(_battleAttachments[index].Preparation, preparation))
                {
                    _battleAttachments[index].Dispose();
                    _battleAttachments.RemoveAt(index);
                }
            }
            room.Lifecycle = DemoRoomLifecycle.Removed;
            _rooms.Remove(room);
            for (int index = 0; index < participants.Length; index++)
            {
                DemoSession session = participants[index];
                if (session.Phase == DemoSessionPhase.Closed) continue;
                session.RoomId = 0;
                session.JoinOrdinal = 0;
                session.IsReady = false;
                session.Phase = DemoSessionPhase.Settlement;
                if (ReferenceEquals(session, disconnected)) continue;
                TryNotifyAfterCleanup(session, new ServerControlEventMessage
                {
                    BattleSettlement = new BattleSettlementEventMessage
                    {
                        BattleId = preparation?.BattleId ?? 0,
                        Reason = BattleSettlementReasonMessage.BattleSettlementReasonAborted,
                        Detail = detail,
                    },
                });
            }
        }

        private void ProcessCommand(ControlConnection connection, byte[] payload)
        {
            ClientControlCommandMessage command = ClientControlCommandMessage.Parser.ParseFrom(payload);
            if (command.CommandCase == ClientControlCommandMessage.CommandOneofCase.None) throw new InvalidDataException("Control command union is missing.");
            try
            {
                switch (command.CommandCase)
                {
                    case ClientControlCommandMessage.CommandOneofCase.EnterSession:
                        if (connection.Session is not null) throw new InvalidOperationException("Session was already entered.");
                        connection.Session = EnterSession(command.EnterSession.Nickname, connection);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.RequestRoomList:
                        QueueSessionEvent(RequireNamed(connection), new ServerControlEventMessage { RoomList = CreateRoomList() });
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.CreateRoom:
                        DemoSession creator = RequireNamed(connection);
                        CreateRoom(creator.SessionId, command.CreateRoom.RoomName, checked((int)command.CreateRoom.Capacity));
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.JoinRoom:
                        ProcessJoinRoom(connection, command.JoinRoom.RoomId);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.LeaveRoom:
                        DemoSession leaver = RequireNamed(connection);
                        LeaveRoom(leaver.SessionId);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.SetReady:
                        DemoSession ready = RequireNamed(connection);
                        SetReady(ready.SessionId, command.SetReady.IsReady);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.StartBattle:
                        ProcessStartBattle(connection);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.ReturnToLobby:
                        ReturnSettledSession(RequireNamed(connection));
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.ExitSession:
                        CloseControl(connection);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.BattleReady:
                        DemoSession battleReady = RequireNamed(connection);
                        MarkBattleReady(battleReady.SessionId, command.BattleReady.BattleId, command.BattleReady.BattleConfigHash);
                        break;
                    default:
                        throw new InvalidDataException("Unknown control command.");
                }
            }
            catch (ControlCapacityException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                QueueRejection(connection, ControlRejectReasonMessage.ControlRejectReasonInvalidValue, exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                QueueRejection(connection, ControlRejectReasonMessage.ControlRejectReasonInvalidPhase, exception.Message);
            }
        }

        private void DrainAllSessionEvents()
        {
            for (int index = 0; index < _controlConnections.Count; index++)
            {
                ControlConnection connection = _controlConnections[index];
                DemoSession? session = connection.Session;
                if (connection.Closed || session is null) continue;
                while (session.TryPeekEvent(out PendingControlEvent? candidate))
                {
                    if (!connection.TryQueue(candidate!.FramedBytes)) break;
                    session.DequeueEvent(candidate);
                }
            }
        }

        private void CloseControl(ControlConnection connection)
        {
            if (connection.Closed) return;
            DemoSession? session = connection.Session;
            connection.Dispose();
            _controlConnections.Remove(connection);
            if (session is not null && session.Phase != DemoSessionPhase.Closed) CloseSession(session.SessionId);
        }

        private static DemoSession RequireNamed(ControlConnection connection)
        {
            return connection.Session ?? throw new InvalidOperationException("Session entry is required.");
        }

        private RoomListEventMessage CreateRoomList()
        {
            var result = new RoomListEventMessage();
            result.Rooms.Add(GetRoomSummaries());
            return result;
        }

        private void ProcessJoinRoom(ControlConnection connection, ulong roomId)
        {
            DemoSession joiner = RequireNamed(connection);
            RequirePhase(joiner, DemoSessionPhase.Lobby);
            DemoRoom? room = FindRoom(roomId);
            if (room is null || room.Lifecycle != DemoRoomLifecycle.Open)
            {
                QueueRejection(
                    connection,
                    ControlRejectReasonMessage.ControlRejectReasonRoomNotFound,
                    "Room was not found.");
                return;
            }

            if (room.ParticipantCount >= room.Capacity)
            {
                QueueRejection(
                    connection,
                    ControlRejectReasonMessage.ControlRejectReasonRoomFull,
                    "Room is full.");
                return;
            }

            JoinRoom(joiner.SessionId, roomId);
        }

        private void ProcessStartBattle(ControlConnection connection)
        {
            DemoSession starter = RequireNamed(connection);
            RequirePhase(starter, DemoSessionPhase.InRoom);
            DemoRoom room = GetRoom(starter.RoomId);
            if (room.HostSessionId != starter.SessionId)
            {
                QueueRejection(connection, ControlRejectReasonMessage.ControlRejectReasonNotHost, "Only the host may start.");
                return;
            }

            if (room.ParticipantCount != room.Capacity)
            {
                QueueRejection(connection, ControlRejectReasonMessage.ControlRejectReasonNotReady, "The room is not full.");
                return;
            }

            for (int index = 0; index < room.ParticipantCount; index++)
            {
                if (room.GetParticipant(index).IsReady) continue;
                QueueRejection(connection, ControlRejectReasonMessage.ControlRejectReasonNotReady, "Every participant must be ready.");
                return;
            }

            StartBattle(starter.SessionId);
        }

        private void QueueRejection(ControlConnection connection, ControlRejectReasonMessage reason, string detail)
        {
            if (connection.Session is null) throw new InvalidDataException(detail);
            QueueSessionEvent(connection.Session, new ServerControlEventMessage
            {
                CommandRejected = new CommandRejectedEventMessage { Reason = reason, Detail = detail },
            });
        }

        private void RemoveOpenHostRoom(DemoRoom room, ulong hostSessionId, bool notifyHost)
        {
            DemoSession[] participants = room.CopyParticipants();
            ControlEventBatch? events = null;
            if (notifyHost)
            {
                events = new ControlEventBatch(this);
                for (int index = 0; index < participants.Length; index++)
                {
                    DemoSession participant = participants[index];
                    if (participant.Phase != DemoSessionPhase.Closed) events.Add(participant, CreateLobbyEntered());
                }
                events.Preflight();
            }
            room.Lifecycle = DemoRoomLifecycle.Removed;
            _rooms.Remove(room);
            for (int index = 0; index < participants.Length; index++)
            {
                if (participants[index].Phase != DemoSessionPhase.Closed) MoveToLobbyState(participants[index]);
            }
            if (notifyHost) events!.Commit();
            else
            {
                for (int index = 0; index < participants.Length; index++)
                {
                    DemoSession participant = participants[index];
                    if (participant.Phase != DemoSessionPhase.Closed && participant.SessionId != hostSessionId)
                    {
                        TryNotifyAfterCleanup(participant, CreateLobbyEntered());
                    }
                }
            }
        }

        private void TryNotifyAfterCleanup(DemoSession session, ServerControlEventMessage message)
        {
            try
            {
                var events = new ControlEventBatch(this);
                events.Add(session, message);
                events.Preflight();
                events.Commit();
            }
            catch (ControlCapacityException)
            {
                ControlConnection? connection = FindControlConnection(session);
                if (connection is not null)
                {
                    connection.Dispose();
                    _controlConnections.Remove(connection);
                }
                session.Phase = DemoSessionPhase.Closed;
                _sessions.Remove(session);
            }
        }

        private void ReturnSettledSession(DemoSession session)
        {
            RequirePhase(session, DemoSessionPhase.Settlement);
            var events = new ControlEventBatch(this);
            events.Add(session, new ServerControlEventMessage { LobbyEntered = new LobbyEnteredEventMessage() });
            events.Preflight();
            if (session.RoomId != 0) RemoveSettledParticipant(session);
            MoveToLobbyState(session);
            events.Commit();
        }

        private void RemoveSettledParticipant(DemoSession session)
        {
            DemoRoom room = GetRoom(session.RoomId);
            if (room.Lifecycle != DemoRoomLifecycle.Settled) throw new InvalidOperationException("Room is not settled.");
            int index = room.IndexOf(session.SessionId);
            if (index < 0) throw new InvalidOperationException("Session is not in its settled room.");
            room.RemoveAt(index);
            if (room.ParticipantCount == 0)
            {
                room.Preparation?.Invalidate();
                room.Lifecycle = DemoRoomLifecycle.Removed;
                _rooms.Remove(room);
            }
        }

        private static void MoveToLobbyState(DemoSession session)
        {
            session.RoomId = 0;
            session.JoinOrdinal = 0;
            session.IsReady = false;
            session.IsBattleReady = false;
            session.Phase = DemoSessionPhase.Lobby;
        }

        private static string ValidateName(string value, int maximumUtf8Bytes, string parameterName)
        {
            if (value is null) throw new ArgumentNullException(parameterName);
            string normalized = value.Trim();
            if (normalized.Length == 0) throw new ArgumentException("Name cannot be empty.", parameterName);
            for (int index = 0; index < normalized.Length; index++) if (char.IsControl(normalized[index])) throw new ArgumentException("Name cannot contain control characters.", parameterName);
            int byteCount = Encoding.UTF8.GetByteCount(normalized);
            if (byteCount < 1 || byteCount > maximumUtf8Bytes) throw new ArgumentOutOfRangeException(parameterName);
            return normalized;
        }

        private static ulong PeekNext(ulong value, string message)
        {
            if (value == 0) throw new InvalidOperationException(message);
            return value;
        }

        private static void CommitNext(ref ulong counter, ulong issued)
        {
            counter = issued == ulong.MaxValue ? 0 : issued + 1;
        }

        private static byte[] CreateUniqueTicket(byte[][] preceding, int count)
        {
            while (true)
            {
                var ticket = new byte[16];
                RandomNumberGenerator.Fill(ticket);
                bool duplicate = false;
                for (int index = 0; index < count && !duplicate; index++) duplicate = CryptographicOperations.FixedTimeEquals(ticket, preceding[index]);
                if (!duplicate) return ticket;
            }
        }

        private static void RequirePhase(DemoSession session, DemoSessionPhase phase)
        {
            if (session.Phase != phase) throw new InvalidOperationException("Command is invalid for the current session phase.");
        }

        private static DemoSession[] AppendParticipant(DemoSession[] participants, DemoSession participant)
        {
            var result = new DemoSession[participants.Length + 1];
            Array.Copy(participants, result, participants.Length);
            result[result.Length - 1] = participant;
            return result;
        }

        private static DemoSession[] RemoveParticipant(DemoSession[] participants, int removeIndex)
        {
            var result = new DemoSession[participants.Length - 1];
            if (removeIndex > 0) Array.Copy(participants, 0, result, 0, removeIndex);
            if (removeIndex < result.Length) Array.Copy(participants, removeIndex + 1, result, removeIndex, result.Length - removeIndex);
            return result;
        }

        private static ServerControlEventMessage CreateLobbyEntered()
        {
            return new ServerControlEventMessage { LobbyEntered = new LobbyEnteredEventMessage() };
        }

        private static RoomSnapshotEventMessage CreateSnapshot(
            DemoRoom room,
            DemoSession[] participants,
            DemoSession? readyOverride,
            bool readyValue)
        {
            var message = new RoomSnapshotEventMessage
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                HostSessionId = room.HostSessionId,
                Capacity = checked((uint)room.Capacity),
                Lifecycle = ServerControlProtocol.ToWire(room.Lifecycle),
            };
            for (int index = 0; index < participants.Length; index++)
            {
                DemoSession participant = participants[index];
                message.Participants.Add(new RoomParticipantMessage
                {
                    SessionId = participant.SessionId,
                    Nickname = participant.Nickname,
                    IsHost = participant.SessionId == room.HostSessionId,
                    IsReady = ReferenceEquals(participant, readyOverride) ? readyValue : participant.IsReady,
                    JoinOrdinal = checked((uint)index),
                });
            }

            return message;
        }

        private static void AddSnapshotEvents(
            ControlEventBatch events,
            DemoRoom room,
            DemoSession[] participants,
            DemoSession? readyOverride,
            bool readyValue)
        {
            RoomSnapshotEventMessage candidate = CreateSnapshot(room, participants, readyOverride, readyValue);
            for (int index = 0; index < participants.Length; index++)
            {
                events.Add(participants[index], new ServerControlEventMessage { RoomSnapshot = candidate.Clone() });
            }
        }

        private void QueueSessionEvent(DemoSession session, ServerControlEventMessage message)
        {
            var events = new ControlEventBatch(this);
            events.Add(session, message);
            events.Preflight();
            events.Commit();
        }

        private void RemoveOpenParticipant(DemoRoom room, DemoSession session, bool notifyLeaving)
        {
            int participantIndex = room.IndexOf(session.SessionId);
            if (participantIndex < 0) throw new InvalidOperationException("Session is not in its room.");
            DemoSession[] survivors = RemoveParticipant(room.CopyParticipants(), participantIndex);
            var events = new ControlEventBatch(this);
            if (notifyLeaving) events.Add(session, CreateLobbyEntered());
            AddSnapshotEvents(events, room, survivors, null, false);
            events.Preflight();
            room.RemoveAt(participantIndex);
            if (notifyLeaving) MoveToLobbyState(session);
            events.Commit();
        }

        private ControlConnection? FindControlConnection(DemoSession session)
        {
            for (int index = 0; index < _controlConnections.Count; index++)
            {
                ControlConnection candidate = _controlConnections[index];
                if (!candidate.Closed && ReferenceEquals(candidate.Session, session)) return candidate;
            }
            return null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpDemoServer));
        }

        private sealed class ControlEventBatch
        {
            private readonly TcpDemoServer _server;
            private readonly List<ControlEventEntry> _entries = new List<ControlEventEntry>();

            internal ControlEventBatch(TcpDemoServer server)
            {
                _server = server;
            }

            internal void Add(DemoSession session, ServerControlEventMessage message, ControlConnection? destination = null)
            {
                _entries.Add(new ControlEventEntry(session, session.Prepare(message), destination));
            }

            internal void Preflight()
            {
                for (int index = 0; index < _entries.Count; index++)
                {
                    DemoSession session = _entries[index].Session;
                    bool first = true;
                    long additionalBytes = 0;
                    ControlConnection? destination = _entries[index].Destination;
                    for (int other = 0; other < _entries.Count; other++)
                    {
                        if (!ReferenceEquals(_entries[other].Session, session)) continue;
                        if (other < index) first = false;
                        additionalBytes = checked(additionalBytes + _entries[other].Event.FramedBytes.Length);
                        destination ??= _entries[other].Destination;
                    }
                    if (!first) continue;
                    destination ??= _server.FindControlConnection(session);
                    session.EnsureCapacity(additionalBytes, destination?.PendingBytes ?? 0);
                }
            }

            internal void Commit()
            {
                for (int index = 0; index < _entries.Count; index++)
                {
                    _entries[index].Session.Commit(_entries[index].Event);
                }
            }
        }

        private sealed class ControlEventEntry
        {
            internal ControlEventEntry(DemoSession session, PendingControlEvent @event, ControlConnection? destination)
            {
                Session = session;
                Event = @event;
                Destination = destination;
            }

            internal DemoSession Session { get; }
            internal PendingControlEvent Event { get; }
            internal ControlConnection? Destination { get; }
        }

        private sealed class ControlConnection : IDisposable
        {
            private readonly TcpClient _client;
            private readonly NetworkStream _stream;
            private readonly LengthPrefixedFrameDecoder _decoder;
            private readonly byte[] _receiveBuffer;
            private readonly int _receiveOffset;
            private readonly int _receiveCapacity;
            private readonly int _maxPendingBytes;
            private readonly Queue<byte[]> _incoming = new Queue<byte[]>();
            private readonly Queue<byte[]> _outgoing = new Queue<byte[]>();
            private byte[]? _sending;
            private int _sendOffset;
            private int _pendingBytes;

            internal ControlConnection(TcpClient client, DemoServerOptions options, ulong acceptOrdinal)
            {
                _client = client;
                _stream = client.GetStream();
                _decoder = new LengthPrefixedFrameDecoder(options.MaxControlPayloadLength);
                _receiveBuffer = new byte[options.ControlReceiveBufferLength];
                _receiveOffset = options.ControlReceiveOffset;
                _receiveCapacity = options.ControlReceiveReadCapacity;
                _maxPendingBytes = options.MaxPendingControlBytesPerSession;
                AcceptOrdinal = acceptOrdinal;
            }

            internal DemoSession? Session { get; set; }
            internal ulong AcceptOrdinal { get; }
            internal int PendingBytes => _pendingBytes;
            internal bool Closed { get; private set; }

            internal void PumpSend(int maximumBytes)
            {
                if (!_client.Client.Poll(0, SelectMode.SelectWrite)) return;
                if (_sending is null)
                {
                    if (_outgoing.Count == 0) return;
                    _sending = _outgoing.Dequeue();
                    _sendOffset = 0;
                }

                int count = Math.Min(maximumBytes, _sending.Length - _sendOffset);
                _stream.Write(_sending, _sendOffset, count);
                _sendOffset += count;
                _pendingBytes -= count;
                if (_sendOffset == _sending.Length)
                {
                    _sending = null;
                    _sendOffset = 0;
                }
            }

            internal bool PumpRead()
            {
                if (!_client.Client.Poll(0, SelectMode.SelectRead)) return false;
                if (_client.Client.Available == 0) throw new EndOfStreamException("Control connection ended.");
                int read = _stream.Read(_receiveBuffer, _receiveOffset, _receiveCapacity);
                if (read == 0) throw new EndOfStreamException("Control connection ended.");
                byte[][] payloads = _decoder.Feed(_receiveBuffer, _receiveOffset, read);
                for (int index = 0; index < payloads.Length; index++) _incoming.Enqueue(payloads[index]);
                return true;
            }

            internal bool TryPeekPayload(out byte[]? payload)
            {
                if (_incoming.Count == 0)
                {
                    payload = null;
                    return false;
                }
                payload = _incoming.Peek();
                return true;
            }

            internal void DequeuePayload(byte[] payload)
            {
                if (_incoming.Count == 0 || !ReferenceEquals(_incoming.Peek(), payload))
                {
                    throw new InvalidOperationException("The control payload is not at the queue head.");
                }
                _incoming.Dequeue();
            }

            internal bool TryQueue(byte[] framed)
            {
                if ((long)_pendingBytes + framed.Length > _maxPendingBytes) return false;
                _outgoing.Enqueue(framed);
                _pendingBytes += framed.Length;
                return true;
            }

            public void Dispose()
            {
                if (Closed) return;
                Closed = true;
                try { _stream.Dispose(); }
                finally { _client.Dispose(); }
            }
        }

        private sealed class BattleAttachment : IDisposable
        {
            private readonly byte[] _ticket = new byte[16];
            private TcpClient? _client;
            private readonly NetworkStream _stream;
            private int _ticketOffset;

            internal BattleAttachment(TcpClient client)
            {
                _client = client;
                _stream = client.GetStream();
            }

            internal BattlePreparation? Preparation { get; private set; }
            internal PlayerSlot Slot { get; private set; }
            internal bool Closed { get; private set; }

            internal bool Progress(TcpDemoServer server)
            {
                TcpClient client = _client ?? throw new InvalidOperationException("Attachment client ownership was transferred.");
                if (Preparation is null)
                {
                    if (!client.Client.Poll(0, SelectMode.SelectRead)) return false;
                    if (client.Client.Available == 0) throw new EndOfStreamException("Battle ticket ended early.");
                    int read = _stream.Read(_ticket, _ticketOffset, _ticket.Length - _ticketOffset);
                    if (read == 0) throw new EndOfStreamException("Battle ticket ended early.");
                    _ticketOffset += read;
                    if (_ticketOffset == _ticket.Length)
                    {
                        if (!server.TryReserveTicket(_ticket, out BattlePreparation? preparation, out PlayerSlot slot)) throw new InvalidDataException("Battle ticket is invalid.");
                        Preparation = preparation;
                        Slot = slot;
                    }
                    return false;
                }

                if (!client.Client.Poll(0, SelectMode.SelectWrite)) return false;
                _stream.WriteByte(0x01);
                return true;
            }

            internal TcpClient DetachClient()
            {
                TcpClient result = _client ?? throw new InvalidOperationException("Attachment ownership was already transferred.");
                _client = null;
                return result;
            }

            internal void ReleaseReservation()
            {
                Preparation?.ReleaseReservation(Slot);
            }

            public void Dispose()
            {
                if (Closed) return;
                Closed = true;
                try { _stream.Dispose(); }
                finally
                {
                    _client?.Dispose();
                    _client = null;
                }
            }
        }
    }
}
