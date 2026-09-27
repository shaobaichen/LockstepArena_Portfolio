using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Google.Protobuf;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.Client.Demo
{
    public sealed class TcpDemoClient : IDisposable
    {
        private readonly DemoClientOptions _options;
        private readonly LengthPrefixedFrameDecoder _decoder;
        private readonly byte[] _receiveBuffer;
        private readonly Queue<byte[]> _outbound = new Queue<byte[]>();
        private readonly Queue<byte[]> _inbound = new Queue<byte[]>();
        private TcpClient? _client;
        private NetworkStream? _stream;
        private byte[]? _sending;
        private int _sendOffset;
        private int _pendingControlBytes;
        private DemoClientPhase _phase;
        private ulong _sessionId;
        private string _nickname = string.Empty;
        private ulong _roomId;
        private string _roomName = string.Empty;
        private string _roomList = string.Empty;
        private string _roomParticipants = string.Empty;
        private DemoRoomSummarySnapshot[] _rooms = Array.Empty<DemoRoomSummarySnapshot>();
        private DemoRoomParticipantSnapshot[] _participants = Array.Empty<DemoRoomParticipantSnapshot>();
        private ulong _roomHostSessionId;
        private uint _roomCapacity;
        private RoomLifecycleMessage _roomLifecycle;
        private ulong _battleId;
        private string _battleRoster = string.Empty;
        private string _lastRejection = string.Empty;
        private ControlRejectReasonMessage _lastRejectionReason;
        private string _lastRejectionDetail = string.Empty;
        private bool _rejectionPending;
        private BattlePreparingEventMessage? _preparing;
        private BattleState? _battleInitialState;
        private BattleState? _lastBattleState;
        private TcpClient? _battleClient;
        private NetworkStream? _battleStream;
        private int _ticketSendOffset;
        private bool _battleAccepted;
        private bool _battleStarted;
        private bool _localBattleSceneReady;
        private bool _battleReadySent;
        private bool _battleAttachmentInitiatedThisPump;
        private PredictedTcpClientBattleRuntime? _battleRuntime;
        private uint _finalInputTick;
        private uint _finalStateTick;
        private uint _serverStateTick;
        private uint _nextPublishTick;
        private bool _latestDirty;
        private int _cumulativeDirtyFrameCount;
        private uint _authoritativeTick;
        private uint _predictedTick;
        private int _pendingPredictionCount;
        private int _pendingAuthoritativeFrameCount;
        private int _replayFrameCount;
        private ulong _authoritativeDigest;
        private ulong _predictedDigest;
        private BattleSettlementEventMessage? _pendingSettlement;
        private bool _settlementVerified;
        private BattleReplaySnapshot? _retainedReplay;
        private BattleSettlementReasonMessage _settlementReason;
        private ulong _winnerPlayerId;
        private uint _slot0RoundWins;
        private uint _slot1RoundWins;
        private string _settlementDetail = string.Empty;
        private bool _exitRequested;
        private bool _disposed;

        public TcpDemoClient(DemoClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _decoder = new LengthPrefixedFrameDecoder(options.MaxControlPayloadLength);
            _receiveBuffer = new byte[options.ControlReceiveBufferLength];
            _phase = DemoClientPhase.Disconnected;
        }

        public DemoClientPhase Phase => _phase;
        public BattleReplaySnapshot? RetainedReplay => _retainedReplay;
        public uint? InputDelayTicks => _preparing?.Bootstrap?.InputDelayTicks;
        public BattleState? PredictedBattleState => _battleRuntime?.PredictedState ?? _lastBattleState ?? _battleInitialState;
        public BattleState? AuthoritativeBattleState => _battleRuntime?.AuthoritativeState ?? _lastBattleState ?? _battleInitialState;
        public PlayerSlot? LocalPlayerSlot => _preparing is null
            ? (PlayerSlot?)null
            : new PlayerSlot(checked((int)_preparing.LocalPlayerSlot));

        public bool TryConsumeRejection(
            out ControlRejectReasonMessage reason,
            out string detail)
        {
            if (!_rejectionPending)
            {
                reason = ControlRejectReasonMessage.ControlRejectReasonUnspecified;
                detail = string.Empty;
                return false;
            }

            reason = _lastRejectionReason;
            detail = _lastRejectionDetail;
            _rejectionPending = false;
            return true;
        }

        public DemoClientSnapshot Snapshot
        {
            get
            {
                RefreshBattleDiagnostics();
                return new DemoClientSnapshot(
                    _phase,
                    _sessionId,
                    _nickname,
                    _roomId,
                    _roomName,
                    _roomList,
                    _roomParticipants,
                    _battleId,
                    _battleRoster,
                    _lastRejection,
                    _serverStateTick,
                    _nextPublishTick,
                    _authoritativeTick,
                    _predictedTick,
                    _pendingPredictionCount,
                    _pendingAuthoritativeFrameCount,
                    _replayFrameCount,
                    _latestDirty,
                    _cumulativeDirtyFrameCount,
                    _authoritativeDigest,
                    _predictedDigest,
                    _settlementVerified,
                    _settlementReason,
                    _winnerPlayerId,
                    _slot0RoundWins,
                    _slot1RoundWins,
                    _settlementDetail,
                    _rooms,
                    _participants,
                    _roomHostSessionId,
                    _roomCapacity,
                    _roomLifecycle);
            }
        }

        public void BeginConnect()
        {
            RequirePhase(DemoClientPhase.Disconnected);
            _phase = DemoClientPhase.ConnectingControl;
            try
            {
                _client = BeginConnect(_options.ServerAddress, _options.ControlPort);
            }
            catch
            {
                _phase = DemoClientPhase.Faulted;
                throw;
            }
        }

        public void EnterSession(string nickname)
        {
            RequirePhase(DemoClientPhase.AwaitingSessionEntry);
            Queue(new ClientControlCommandMessage { EnterSession = new EnterSessionCommandMessage { Nickname = nickname } });
        }

        public void RequestRoomList()
        {
            RequireAnyPhase(DemoClientPhase.Lobby, DemoClientPhase.Room);
            Queue(new ClientControlCommandMessage { RequestRoomList = new RequestRoomListCommandMessage() });
        }

        public void CreateRoom(string roomName, int capacity)
        {
            RequirePhase(DemoClientPhase.Lobby);
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Queue(new ClientControlCommandMessage { CreateRoom = new CreateRoomCommandMessage { RoomName = roomName, Capacity = checked((uint)capacity) } });
        }

        public void JoinRoom(ulong roomId)
        {
            RequirePhase(DemoClientPhase.Lobby);
            Queue(new ClientControlCommandMessage { JoinRoom = new JoinRoomCommandMessage { RoomId = roomId } });
        }

        public void LeaveRoom()
        {
            RequirePhase(DemoClientPhase.Room);
            Queue(new ClientControlCommandMessage { LeaveRoom = new LeaveRoomCommandMessage() });
        }

        public void SetReady(bool isReady)
        {
            RequirePhase(DemoClientPhase.Room);
            Queue(new ClientControlCommandMessage { SetReady = new SetReadyCommandMessage { IsReady = isReady } });
        }

        public void StartBattle()
        {
            RequirePhase(DemoClientPhase.Room);
            Queue(new ClientControlCommandMessage { StartBattle = new StartBattleCommandMessage() });
        }

        public void MarkBattleSceneReady()
        {
            RequirePhase(DemoClientPhase.PreparingBattle);
            if (_options.BattleDefinition is null)
                throw new InvalidOperationException("BattleReady is only used by gameplay battles.");
            _localBattleSceneReady = true;
            TryQueueBattleReady();
        }

        public void ReturnToLobby()
        {
            RequirePhase(DemoClientPhase.Settlement);
            Queue(new ClientControlCommandMessage { ReturnToLobby = new ReturnToLobbyCommandMessage() });
            _retainedReplay = null;
        }

        public void Exit()
        {
            if (_phase == DemoClientPhase.Disposed) throw new ObjectDisposedException(nameof(TcpDemoClient));
            if (_phase == DemoClientPhase.Faulted) throw new InvalidOperationException("The client is faulted.");
            if (_phase == DemoClientPhase.Disconnected || _exitRequested) throw new InvalidOperationException("Exit is not available in the current client state.");
            Queue(new ClientControlCommandMessage { ExitSession = new ExitSessionCommandMessage() });
            _exitRequested = true;
        }

        public DemoClientPumpResult PumpOnce(LocalInputSample? localInput)
        {
            ThrowIfUnavailable();
            try
            {
                if (_phase == DemoClientPhase.ConnectingControl)
                {
                    if (TryCompleteConnect(_client ?? throw new InvalidOperationException("Control client is missing."), out NetworkStream? stream))
                    {
                        _stream = stream;
                        _phase = DemoClientPhase.AwaitingSessionEntry;
                    }
                    return new DemoClientPumpResult(0, 0, 0, false);
                }

                _battleAttachmentInitiatedThisPump = false;
                int sent = PumpSend();
                if (_exitRequested && _pendingControlBytes == 0)
                {
                    CompleteExit();
                    return new DemoClientPumpResult(sent, 0, 0, false);
                }
                int processed = PumpReceive();
                if (!_battleAttachmentInitiatedThisPump) ProgressBattleAttachment();
                int authority = 0;
                bool prediction = false;
                if (_battleRuntime is not null &&
                    (_phase == DemoClientPhase.InBattle ||
                     _phase == DemoClientPhase.SettlementPendingAuthority) &&
                    _battleRuntime.AuthoritativeState.Tick < _finalStateTick)
                {
                    LocalInputSample? acceptedInput =
                        _phase == DemoClientPhase.InBattle &&
                        _battleRuntime.PredictedState.Tick <= _finalInputTick
                            ? localInput
                            : null;
                    PredictedClientUpdateResult result = _battleRuntime.Update(acceptedInput);
                    authority = result.ReconciledAuthoritativeFrameCount;
                    prediction = result.LocalPredictionSent;
                    _latestDirty = result.DirtyFrameCount > 0;
                    _cumulativeDirtyFrameCount = checked(
                        _cumulativeDirtyFrameCount + result.DirtyFrameCount);
                    RefreshBattleDiagnostics();
                    TryVerifyPendingSettlement();
                }
                return new DemoClientPumpResult(sent, processed, authority, prediction);
            }
            catch
            {
                _phase = DemoClientPhase.Faulted;
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _phase = DemoClientPhase.Disposed;
            try
            {
                if (_battleRuntime is not null) _battleRuntime.Dispose();
                else
                {
                    _battleStream?.Dispose();
                    _battleClient?.Dispose();
                }
            }
            finally
            {
                try { _stream?.Dispose(); }
                finally { _client?.Dispose(); }
            }
        }

        private void Queue(ClientControlCommandMessage command)
        {
            byte[] framed = ClientControlProtocol.Frame(command, _options.MaxControlPayloadLength);
            if ((long)_pendingControlBytes + framed.Length > _options.MaxPendingControlBytes) throw new InvalidOperationException("The pending control send capacity is full.");
            _outbound.Enqueue(framed);
            _pendingControlBytes += framed.Length;
        }

        private int PumpSend()
        {
            if (_client is null || _stream is null || !_client.Client.Poll(0, SelectMode.SelectWrite)) return 0;
            if (_sending is null)
            {
                if (_outbound.Count == 0) return 0;
                _sending = _outbound.Dequeue();
                _sendOffset = 0;
            }

            int count = Math.Min(_options.MaxControlSendBytesPerPump, _sending.Length - _sendOffset);
            _stream.Write(_sending, _sendOffset, count);
            _sendOffset += count;
            _pendingControlBytes -= count;
            if (_sendOffset == _sending.Length)
            {
                _sending = null;
                _sendOffset = 0;
            }

            return count;
        }

        private int PumpReceive()
        {
            if (_client is null || _stream is null) return 0;
            if (_client.Client.Poll(0, SelectMode.SelectRead))
            {
                if (_client.Client.Available == 0) throw new EndOfStreamException("Control connection ended.");
                int read = _stream.Read(_receiveBuffer, _options.ControlReceiveOffset, _options.ControlReceiveReadCapacity);
                if (read == 0) throw new EndOfStreamException("Control connection ended.");
                byte[][] payloads = _decoder.Feed(_receiveBuffer, _options.ControlReceiveOffset, read);
                for (int index = 0; index < payloads.Length; index++) _inbound.Enqueue(payloads[index]);
            }

            int processed = 0;
            while (processed < _options.MaxControlMessagesPerPump && _inbound.Count > 0)
            {
                ProcessEvent(ServerControlEventMessage.Parser.ParseFrom(_inbound.Dequeue()));
                processed++;
            }
            return processed;
        }

        private void ProcessEvent(ServerControlEventMessage message)
        {
            switch (message.EventCase)
            {
                case ServerControlEventMessage.EventOneofCase.SessionEntered:
                    _sessionId = message.SessionEntered.SessionId;
                    _nickname = message.SessionEntered.Nickname;
                    _phase = DemoClientPhase.Lobby;
                    break;
                case ServerControlEventMessage.EventOneofCase.RoomSnapshot:
                    _roomId = message.RoomSnapshot.RoomId;
                    _roomName = message.RoomSnapshot.RoomName;
                    _roomParticipants = FormatRoomParticipants(message.RoomSnapshot);
                    _participants = MapRoomParticipants(message.RoomSnapshot);
                    _roomHostSessionId = message.RoomSnapshot.HostSessionId;
                    _roomCapacity = message.RoomSnapshot.Capacity;
                    _roomLifecycle = message.RoomSnapshot.Lifecycle;
                    _phase = DemoClientPhase.Room;
                    break;
                case ServerControlEventMessage.EventOneofCase.BattlePreparing:
                    BeginBattleAttachment(message.BattlePreparing);
                    _phase = DemoClientPhase.PreparingBattle;
                    break;
                case ServerControlEventMessage.EventOneofCase.BattleStarted:
                    if (message.BattleStarted.BattleId != _battleId) throw new InvalidDataException("BattleStarted does not match the prepared battle.");
                    _battleStarted = true;
                    TryActivateBattleRuntime();
                    break;
                case ServerControlEventMessage.EventOneofCase.BattleSettlement:
                    ReceiveSettlement(message.BattleSettlement);
                    break;
                case ServerControlEventMessage.EventOneofCase.CommandRejected:
                    _lastRejectionReason = message.CommandRejected.Reason;
                    _lastRejectionDetail = message.CommandRejected.Detail;
                    _lastRejection = FormatRejection(_lastRejectionReason);
                    _rejectionPending = true;
                    break;
                case ServerControlEventMessage.EventOneofCase.LobbyEntered:
                    ClearRoomAndBattlePresentation();
                    _phase = DemoClientPhase.Lobby;
                    Queue(new ClientControlCommandMessage { RequestRoomList = new RequestRoomListCommandMessage() });
                    break;
                case ServerControlEventMessage.EventOneofCase.RoomList:
                    _roomList = FormatRoomList(message.RoomList);
                    _rooms = MapRoomList(message.RoomList);
                    break;
                case ServerControlEventMessage.EventOneofCase.BattleStatus:
                    ReceiveBattleStatus(message.BattleStatus);
                    break;
                default:
                    throw new InvalidDataException("Control event union is missing.");
            }
        }

        private static string FormatRejection(ControlRejectReasonMessage reason)
        {
            return reason switch
            {
                ControlRejectReasonMessage.ControlRejectReasonNotHost => "NOT_HOST",
                ControlRejectReasonMessage.ControlRejectReasonNotReady => "NOT_READY",
                _ => reason.ToString(),
            };
        }

        private void BeginBattleAttachment(BattlePreparingEventMessage preparing)
        {
            _retainedReplay = null;
            if (preparing is null || preparing.Bootstrap is null) throw new InvalidDataException("Battle preparation is incomplete.");
            if (preparing.BattleId == 0 || preparing.BattleId != preparing.Bootstrap.BattleId) throw new InvalidDataException("Battle preparation identity is invalid.");
            if (preparing.BattleTicket.Length != 16) throw new InvalidDataException("Battle ticket must contain exactly 16 bytes.");
            if (preparing.BattlePort == 0 || preparing.BattlePort > ushort.MaxValue) throw new InvalidDataException("Battle port is outside the TCP port range.");
            if (preparing.LocalPlayerSlot > int.MaxValue) throw new InvalidDataException("Local player slot exceeds the Domain range.");
            var localId = new PlayerId(preparing.LocalPlayerId);
            var localSlot = new PlayerSlot(checked((int)preparing.LocalPlayerSlot));
            BattleState initialState;
            if (_options.BattleDefinition is null)
            {
                initialState = ProtocolMapper.ToDomainBattleBootstrap(preparing.Bootstrap, localId, localSlot);
            }
            else
            {
                ulong localHash = BattleConfigHash.Compute(_options.BattleDefinition);
                if (preparing.Bootstrap.BattleConfigHash != localHash)
                {
                    _preparing = preparing.Clone();
                    _battleId = preparing.BattleId;
                    Queue(new ClientControlCommandMessage
                    {
                        BattleReady = new BattleReadyCommandMessage
                        {
                            BattleId = preparing.BattleId,
                            BattleConfigHash = localHash,
                        },
                    });
                    _battleReadySent = true;
                    return;
                }
                initialState = ProtocolMapper.ToDomainGameplayBattleBootstrap(
                    preparing.Bootstrap, localId, localSlot, _options.BattleDefinition);
            }
            uint finalStateTick = preparing.Bootstrap.FinalStateTick;
            if (finalStateTick == 0U) throw new InvalidDataException("Final battle Tick must be positive.");
            TcpClient battleClient = BeginConnect(_options.ServerAddress, checked((int)preparing.BattlePort));
            _battleClient = battleClient;
            _battleStream = null;
            _battleAttachmentInitiatedThisPump = true;
            _preparing = preparing.Clone();
            _battleInitialState = initialState;
            _lastBattleState = initialState;
            _battleRoster = FormatBattleRoster(initialState.Roster);
            _battleId = preparing.BattleId;
            _finalStateTick = finalStateTick;
            _finalInputTick = _finalStateTick - 1U;
            _serverStateTick = initialState.Tick;
            _nextPublishTick = initialState.Tick;
            _authoritativeTick = initialState.Tick;
            _predictedTick = initialState.Tick;
            _authoritativeDigest = StateDigest.Compute(initialState);
            _predictedDigest = _authoritativeDigest;
            _latestDirty = false;
            _cumulativeDirtyFrameCount = 0;
            _pendingSettlement = null;
            _settlementVerified = false;
            _ticketSendOffset = 0;
            _battleAccepted = false;
            _battleStarted = false;
            _localBattleSceneReady = false;
            _battleReadySent = false;
        }

        private void ProgressBattleAttachment()
        {
            if (_battleClient is null || _preparing is null || _battleRuntime is not null) return;
            if (_battleStream is null)
            {
                if (TryCompleteConnect(_battleClient, out NetworkStream? stream)) _battleStream = stream;
                return;
            }
            if (_ticketSendOffset < 16)
            {
                if (!_battleClient.Client.Poll(0, SelectMode.SelectWrite)) return;
                byte[] ticket = _preparing.BattleTicket.ToByteArray();
                int sent = _battleClient.Client.Send(ticket, _ticketSendOffset, ticket.Length - _ticketSendOffset, SocketFlags.None);
                if (sent <= 0) throw new EndOfStreamException("Battle ticket send ended early.");
                _ticketSendOffset += sent;
                return;
            }
            if (!_battleAccepted && _battleClient.Client.Poll(0, SelectMode.SelectRead))
            {
                if (_battleClient.Client.Available == 0) throw new EndOfStreamException("Battle attachment ended before acceptance.");
                int value = _battleStream.ReadByte();
                if (value != 0x01) throw new InvalidDataException("Battle attachment acceptance byte is invalid.");
                _battleAccepted = true;
                TryQueueBattleReady();
                TryActivateBattleRuntime();
            }
        }

        private void TryQueueBattleReady()
        {
            if (!_battleAccepted || !_localBattleSceneReady || _battleReadySent ||
                _preparing is null || _options.BattleDefinition is null) return;
            Queue(new ClientControlCommandMessage
            {
                BattleReady = new BattleReadyCommandMessage
                {
                    BattleId = _preparing.BattleId,
                    BattleConfigHash = BattleConfigHash.Compute(_options.BattleDefinition),
                },
            });
            _battleReadySent = true;
        }

        private static TcpClient BeginConnect(IPAddress address, int port)
        {
            var client = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                client.Client.Blocking = false;
                try
                {
                    client.Connect(address, port);
                }
                catch (SocketException exception) when (IsConnectInProgress(exception.SocketErrorCode))
                {
                }
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        private static bool TryCompleteConnect(TcpClient client, out NetworkStream? stream)
        {
            Socket socket = client.Client;
            if (!socket.Poll(0, SelectMode.SelectWrite) && !socket.Poll(0, SelectMode.SelectError))
            {
                stream = null;
                return false;
            }

            int error = (int)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
            if (error != 0) throw new SocketException(error);
            socket.Blocking = true;
            stream = client.GetStream();
            return true;
        }

        private static bool IsConnectInProgress(SocketError error)
        {
            return error == SocketError.WouldBlock ||
                error == SocketError.InProgress ||
                error == SocketError.AlreadyInProgress;
        }

        private void TryActivateBattleRuntime()
        {
            if (!_battleAccepted || !_battleStarted || _battleRuntime is not null) return;
            TcpClient client = _battleClient ?? throw new InvalidOperationException("Battle client is missing.");
            BattlePreparingEventMessage preparing = _preparing ?? throw new InvalidOperationException("Battle preparation is missing.");
            BattleState initialState = _battleInitialState ?? throw new InvalidOperationException("Battle initial state is missing.");
            var localId = new PlayerId(preparing.LocalPlayerId);
            var localSlot = new PlayerSlot(checked((int)preparing.LocalPlayerSlot));
            _battleRuntime = new PredictedTcpClientBattleRuntime(client, initialState, localId, localSlot, _options.MaxPredictionTicks, _options.MaxAuthoritativeFramesPerUpdate, _options.MaxPendingAuthoritativeFrames, _options.MaxReplayFrames, _options.MaxBattlePayloadLength, _options.BattleReceiveBufferLength, _options.BattleReceiveOffset, _options.BattleReceiveReadCapacity);
            _battleClient = null;
            _battleStream = null;
            _phase = DemoClientPhase.InBattle;
        }

        private void ReceiveBattleStatus(BattleStatusEventMessage status)
        {
            if (status.BattleId != _battleId) throw new InvalidDataException("Battle status does not match the active battle.");
            if (status.ServerStateTick > _finalStateTick) throw new InvalidDataException("Server battle status exceeds the final Tick.");
            _serverStateTick = status.ServerStateTick;
            _nextPublishTick = status.NextPublishTick;
        }

        private void ReceiveSettlement(BattleSettlementEventMessage settlement)
        {
            if (settlement.BattleId != _battleId) throw new InvalidDataException("Settlement does not match the active battle.");
            _settlementReason = settlement.Reason;
            _winnerPlayerId = settlement.WinnerPlayerId;
            _slot0RoundWins = settlement.Slot0RoundWins;
            _slot1RoundWins = settlement.Slot1RoundWins;
            _settlementDetail = settlement.Detail;
            if (settlement.Reason == BattleSettlementReasonMessage.BattleSettlementReasonAborted)
            {
                if (settlement.FinalState is not null) throw new InvalidDataException("Aborted settlement cannot claim a final state.");
                _pendingSettlement = null;
                _settlementVerified = false;
                _retainedReplay = null;
                DisposeBattleRuntime();
                _phase = DemoClientPhase.Settlement;
                return;
            }

            bool tickLimit = settlement.Reason == BattleSettlementReasonMessage.BattleSettlementReasonTickLimitReached;
            bool matchCompleted = settlement.Reason == BattleSettlementReasonMessage.BattleSettlementReasonMatchCompleted;
            bool forfeit = settlement.Reason == BattleSettlementReasonMessage.BattleSettlementReasonDisconnectForfeit;
            if ((!tickLimit && !matchCompleted && !forfeit) || settlement.FinalState is null)
            {
                throw new InvalidDataException("Normal settlement is incomplete.");
            }

            if (tickLimit && settlement.FinalState.Tick != _finalStateTick)
            {
                throw new InvalidDataException("Settlement Tick does not match the battle limit.");
            }

            if (!tickLimit)
            {
                if (settlement.FinalState.Tick > _finalStateTick)
                    throw new InvalidDataException("Settlement Tick exceeds the battle safety limit.");
                _finalStateTick = settlement.FinalState.Tick;
                _finalInputTick = _finalStateTick == 0U ? 0U : _finalStateTick - 1U;
            }

            PredictedTcpClientBattleRuntime runtime = _battleRuntime ?? throw new InvalidDataException("Battle runtime is unavailable for settlement.");
            if (runtime.AuthoritativeState.Tick > _finalStateTick)
            {
                throw new InvalidDataException("Authoritative state advanced beyond the final Tick.");
            }

            _pendingSettlement = settlement.Clone();
            if (runtime.AuthoritativeState.Tick < _finalStateTick)
            {
                _phase = DemoClientPhase.SettlementPendingAuthority;
                return;
            }

            VerifyPendingSettlement();
        }

        private void TryVerifyPendingSettlement()
        {
            if (_pendingSettlement is null || _battleRuntime is null) return;
            if (_battleRuntime.AuthoritativeState.Tick > _finalStateTick)
            {
                throw new InvalidDataException("Authoritative state advanced beyond the final Tick.");
            }
            if (_battleRuntime.AuthoritativeState.Tick == _finalStateTick) VerifyPendingSettlement();
        }

        private void VerifyPendingSettlement()
        {
            PredictedTcpClientBattleRuntime runtime = _battleRuntime ?? throw new InvalidDataException("Battle runtime is unavailable for settlement.");
            BattleSettlementEventMessage settlement = _pendingSettlement ?? throw new InvalidDataException("Settlement is unavailable.");
            FinalBattleStateMessage wire = settlement.FinalState ?? throw new InvalidDataException("Settlement final state is missing.");
            BattleState authority = runtime.AuthoritativeState;
            BattleState predicted = runtime.PredictedState;
            bool forfeit = settlement.Reason ==
                BattleSettlementReasonMessage.BattleSettlementReasonDisconnectForfeit;
            bool matchCompleted = settlement.Reason ==
                BattleSettlementReasonMessage.BattleSettlementReasonMatchCompleted;
            bool converged = matchCompleted
                ? predicted.Tick >= _finalStateTick && GameplayTerminalValuesMatch(authority, predicted)
                : predicted.Tick == _finalStateTick &&
                    BattleStateValueComparer.HaveSameValue(authority, predicted);
            if (authority.Tick != _finalStateTick ||
                (!forfeit && (runtime.PendingAuthoritativeFrameCount != 0 || !converged)))
            {
                throw new InvalidDataException("Client battle state has not converged at settlement.");
            }

            BattleState replay = runtime.ReconstructAuthoritativeState();
            if (!BattleStateValueComparer.HaveSameValue(authority, replay) || wire.PlayerStates.Count != authority.PlayerCount)
            {
                throw new InvalidDataException("Settlement Replay or player count does not match authority.");
            }

            for (int index = 0; index < authority.PlayerCount; index++)
            {
                var slot = new PlayerSlot(index);
                SettlementPlayerStateMessage player = wire.PlayerStates[index];
                PlayerState expected = authority.GetPlayerState(slot);
                if (player.PlayerSlot != checked((uint)index) ||
                    player.PlayerId != authority.Roster.GetPlayerId(slot).Value ||
                    player.PositionX != expected.PositionX ||
                    player.PositionZ != expected.PositionZ ||
                    player.Aim > ushort.MaxValue ||
                    player.Aim != expected.Aim)
                {
                    throw new InvalidDataException("Settlement player state does not match authority.");
                }
            }

            ulong digest = StateDigest.Compute(authority);
            if (wire.Tick != authority.Tick || wire.StateDigest != digest)
            {
                throw new InvalidDataException("Settlement digest does not match authority.");
            }

            if (authority.IsGameplayEnabled)
            {
                uint slot0Wins = checked((uint)authority.GetPlayerState(new PlayerSlot(0)).RoundWins);
                uint slot1Wins = checked((uint)authority.GetPlayerState(new PlayerSlot(1)).RoundWins);
                if (settlement.Slot0RoundWins != slot0Wins || settlement.Slot1RoundWins != slot1Wins)
                    throw new InvalidDataException("Settlement round score does not match authority.");
                if (settlement.Reason == BattleSettlementReasonMessage.BattleSettlementReasonMatchCompleted)
                {
                    if (authority.Phase != BattlePhase.MatchEnded || !authority.MatchWinnerSlot.HasValue ||
                        settlement.WinnerPlayerId != authority.Roster.GetPlayerId(authority.MatchWinnerSlot.Value).Value)
                        throw new InvalidDataException("Completed-match winner does not match authority.");
                }
                else if (forfeit && !ContainsPlayerId(authority, settlement.WinnerPlayerId))
                {
                    throw new InvalidDataException("Forfeit winner is not in the battle roster.");
                }
            }

            BattleState retainedState = forfeit ? authority : predicted;
            RefreshBattleDiagnostics();
            _pendingSettlement = null;
            _settlementVerified = true;
            _retainedReplay = runtime.CaptureReplaySnapshot();
            DisposeBattleRuntime();
            _lastBattleState = retainedState;
            _phase = DemoClientPhase.Settlement;
        }

        private void RefreshBattleDiagnostics()
        {
            if (_battleRuntime is null) return;
            _authoritativeTick = _battleRuntime.AuthoritativeState.Tick;
            _predictedTick = _battleRuntime.PredictedState.Tick;
            _pendingPredictionCount = _battleRuntime.PendingPredictionCount;
            _pendingAuthoritativeFrameCount = _battleRuntime.PendingAuthoritativeFrameCount;
            _replayFrameCount = _battleRuntime.ReplayFrameCount;
            _authoritativeDigest = StateDigest.Compute(_battleRuntime.AuthoritativeState);
            _predictedDigest = StateDigest.Compute(_battleRuntime.PredictedState);
            _lastBattleState = _battleRuntime.PredictedState;
        }

        private void DisposeBattleRuntime()
        {
            RefreshBattleDiagnostics();
            if (_battleRuntime is not null)
            {
                _battleRuntime.Dispose();
                _battleRuntime = null;
            }
            else
            {
                _battleStream?.Dispose();
                _battleClient?.Dispose();
            }
            _battleStream = null;
            _battleClient = null;
        }

        private void ClearRoomAndBattlePresentation()
        {
            _retainedReplay = null;
            DisposeBattleRuntime();
            _roomId = 0;
            _roomName = string.Empty;
            _roomParticipants = string.Empty;
            _participants = Array.Empty<DemoRoomParticipantSnapshot>();
            _roomHostSessionId = 0;
            _roomCapacity = 0;
            _roomLifecycle = RoomLifecycleMessage.RoomLifecycleUnspecified;
            _battleId = 0;
            _battleRoster = string.Empty;
            _preparing = null;
            _battleInitialState = null;
            _lastBattleState = null;
            _pendingSettlement = null;
            _settlementVerified = false;
            _settlementReason = BattleSettlementReasonMessage.BattleSettlementReasonUnspecified;
            _winnerPlayerId = 0;
            _slot0RoundWins = 0;
            _slot1RoundWins = 0;
            _settlementDetail = string.Empty;
            _localBattleSceneReady = false;
            _battleReadySent = false;
        }

        private void CompleteExit()
        {
            _retainedReplay = null;
            DisposeBattleRuntime();
            _stream?.Dispose();
            _client?.Dispose();
            _stream = null;
            _client = null;
            _sending = null;
            _sendOffset = 0;
            _exitRequested = false;
            _phase = DemoClientPhase.Disconnected;
        }

        private static bool ContainsPlayerId(BattleState state, ulong playerId)
        {
            for (int index = 0; index < state.PlayerCount; index++)
                if (state.Roster.GetPlayerId(new PlayerSlot(index)).Value == playerId) return true;
            return false;
        }

        private static bool GameplayTerminalValuesMatch(BattleState authority, BattleState predicted)
        {
            if (!authority.IsGameplayEnabled || !predicted.IsGameplayEnabled ||
                authority.Phase != BattlePhase.MatchEnded || predicted.Phase != BattlePhase.MatchEnded ||
                !authority.Roster.HasSameStructure(predicted.Roster) ||
                authority.MatchWinnerSlot != predicted.MatchWinnerSlot ||
                authority.RoundResult != predicted.RoundResult ||
                authority.PhaseTicksRemaining != predicted.PhaseTicksRemaining ||
                authority.RoundTicksRemaining != predicted.RoundTicksRemaining ||
                authority.NextProjectileId != predicted.NextProjectileId ||
                authority.ProjectileCount != predicted.ProjectileCount ||
                BattleConfigHash.Compute(authority.Definition!) != BattleConfigHash.Compute(predicted.Definition!))
                return false;
            for (int index = 0; index < authority.PlayerCount; index++)
                if (!authority.GetPlayerState(new PlayerSlot(index)).Equals(
                    predicted.GetPlayerState(new PlayerSlot(index)))) return false;
            for (int index = 0; index < authority.ProjectileCount; index++)
                if (authority.GetProjectile(index) != predicted.GetProjectile(index)) return false;
            return true;
        }

        private static string FormatRoomList(RoomListEventMessage roomList)
        {
            var result = new StringBuilder();
            for (int index = 0; index < roomList.Rooms.Count; index++)
            {
                if (index > 0) result.Append(" | ");
                RoomSummaryMessage room = roomList.Rooms[index];
                result.Append("Room").Append(room.RoomId).Append(' ')
                    .Append(room.RoomName).Append(" host=").Append(room.HostNickname)
                    .Append(' ').Append(room.ParticipantCount).Append('/')
                    .Append(room.Capacity).Append(' ').Append(room.Lifecycle);
            }
            return result.ToString();
        }

        private static DemoRoomSummarySnapshot[] MapRoomList(RoomListEventMessage roomList)
        {
            var result = new DemoRoomSummarySnapshot[roomList.Rooms.Count];
            for (int index = 0; index < result.Length; index++)
            {
                RoomSummaryMessage room = roomList.Rooms[index];
                result[index] = new DemoRoomSummarySnapshot(
                    room.RoomId,
                    room.RoomName,
                    room.HostNickname,
                    room.ParticipantCount,
                    room.Capacity,
                    room.Lifecycle);
            }
            return result;
        }

        private static string FormatRoomParticipants(RoomSnapshotEventMessage room)
        {
            var result = new StringBuilder();
            for (int index = 0; index < room.Participants.Count; index++)
            {
                if (index > 0) result.Append(" | ");
                RoomParticipantMessage participant = room.Participants[index];
                result.Append(participant.JoinOrdinal).Append(':').Append(participant.Nickname)
                    .Append(" PlayerId").Append(participant.SessionId);
                if (participant.IsHost) result.Append(" host");
                result.Append(participant.IsReady ? " ready" : " unready");
            }
            return result.ToString();
        }

        private static DemoRoomParticipantSnapshot[] MapRoomParticipants(RoomSnapshotEventMessage room)
        {
            var result = new DemoRoomParticipantSnapshot[room.Participants.Count];
            for (int index = 0; index < result.Length; index++)
            {
                RoomParticipantMessage participant = room.Participants[index];
                result[index] = new DemoRoomParticipantSnapshot(
                    participant.SessionId,
                    participant.Nickname,
                    participant.IsHost,
                    participant.IsReady,
                    participant.JoinOrdinal);
            }
            return result;
        }

        private static string FormatBattleRoster(ActiveRoster roster)
        {
            var result = new StringBuilder();
            for (int index = 0; index < roster.Count; index++)
            {
                if (index > 0) result.Append(" | ");
                result.Append("Slot").Append(index).Append("/PlayerId")
                    .Append(roster.GetPlayerId(new PlayerSlot(index)).Value);
            }
            return result.ToString();
        }

        private void RequirePhase(DemoClientPhase phase)
        {
            ThrowIfUnavailable();
            if (_phase != phase) throw new InvalidOperationException("Command is invalid for the current client phase.");
        }

        private void RequireAnyPhase(DemoClientPhase first, DemoClientPhase second)
        {
            ThrowIfUnavailable();
            if (_phase != first && _phase != second) throw new InvalidOperationException("Command is invalid for the current client phase.");
        }

        private void ThrowIfUnavailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpDemoClient));
            if (_phase == DemoClientPhase.Faulted) throw new InvalidOperationException("The client is faulted.");
        }
    }
}
