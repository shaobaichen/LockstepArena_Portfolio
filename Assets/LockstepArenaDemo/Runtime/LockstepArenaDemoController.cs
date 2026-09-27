#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LockstepArena.Client.Demo;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LockstepArena.Demo
{
    public sealed class LockstepArenaDemoController : MonoBehaviour
    {
        private const string LobbySceneName = "LobbyScene";
        private const string BattleSceneName = "BattleScene";
        private const int ClientPredictionCapacity = 8;
        private static LockstepArenaDemoController? _instance;

        [SerializeField] private int controlPort = 46000;
        [SerializeField] private int battlePort = 46001;

        private readonly BattleDefinition _definition = BattleDefinition.CreateDefault();
        private readonly LanServerLauncher _lanServerLauncher = new LanServerLauncher();
        private TcpDemoClient? _client;
        private BattlePresenter? _presenter;
        private BattleHudView? _hud;
        private BattleReplayPlayer? _replayPlayer;
        private string _lastError = string.Empty;
        private string? _pendingNickname;
        private string? _pendingHostRoomName;
        private bool _sessionEntrySent;
        private bool _hostRoomCreateSent;
        private int _shellFlowGeneration;
        private bool _sceneReadySent;
        private bool _debugVisible;
        private double _predictionSeconds;
        private string _aimDiagnostic = "Aim: no gameplay input sampled";

        public DemoClientSnapshot? ClientSnapshot => _client?.Snapshot;
        public ShellConnectionStage ShellStage { get; private set; } = ShellConnectionStage.Idle;
        public string UserFacingError { get; private set; } = string.Empty;
        public string HostedLanAddress { get; private set; } = string.Empty;
        public bool OwnsLocalServer => _lanServerLauncher.OwnsServer;
        public bool IsReplayMode => _replayPlayer is not null;
        public BattleReplayPlayer? ReplayPlayer => _replayPlayer;
        public bool CanWatchReplay => _client?.Phase == DemoClientPhase.Settlement &&
            _client.Snapshot.SettlementVerified && _client.RetainedReplay is not null &&
            _client.RetainedReplay.AuthoritativeFrames.Count > 0;

        private void Awake()
        {
            if (_instance is not null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Application.runInBackground = true;
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_instance != this) return;
            Keyboard? keyboard = Keyboard.current;
            if (keyboard is not null && keyboard.f1Key.wasPressedThisFrame) _debugVisible = !_debugVisible;
            try
            {
                SynchronizeScene();
                if (IsReplayMode && !CanWatchReplay) ExitReplay();
                if (_client is null || _client.Phase == DemoClientPhase.Disconnected ||
                    _client.Phase == DemoClientPhase.Disposed) return;
                if (_client.Phase == DemoClientPhase.Faulted)
                {
                    FailShell(new InvalidOperationException("The network connection failed."));
                    return;
                }
                AdvanceShellFlow();
                if (IsReplayMode && keyboard is not null)
                {
                    if (keyboard.escapeKey.wasPressedThisFrame) ExitReplay();
                    else if (keyboard.spaceKey.wasPressedThisFrame) _replayPlayer!.TogglePause();
                }
                LocalInputSample? input = CollectGameplayInput(Time.unscaledDeltaTime);
                _client.PumpOnce(input);
                AdvanceShellFlow();
                SynchronizeScene();
                AdvanceBattlePresentation(Time.unscaledDeltaTime);
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
                if (_client is null || !IsBattlePhase(_client.Phase))
                {
                    FailShell(exception);
                }
            }
        }

        private LocalInputSample? CollectGameplayInput(double elapsedSeconds)
        {
            if (IsReplayMode || _client?.Phase != DemoClientPhase.InBattle)
            {
                _predictionSeconds = 0d;
                return null;
            }
            return ConsumePredictionTick(elapsedSeconds) ? ReadGameplayInput() : null;
        }

        private void AdvanceBattlePresentation(double elapsedSeconds)
        {
            if (IsReplayMode && !CanWatchReplay) ExitReplay();
            if (_replayPlayer is not null)
            {
                _replayPlayer.AdvanceTime(elapsedSeconds);
                EnsurePresenter().Present(_replayPlayer.CurrentState, null);
                EnsureHud().PresentReplay(_replayPlayer, ExitReplay);
            }
            else if (_client is not null && IsBattlePhase(_client.Phase) && _client.PredictedBattleState is not null)
            {
                BattleState state = _client.PredictedBattleState;
                ushort? visualAim = _client.Phase == DemoClientPhase.InBattle &&
                    _client.LocalPlayerSlot.HasValue ? GetCurrentAim() : null;
                EnsurePresenter().Present(state, _client.LocalPlayerSlot, visualAim);
                EnsureHud().Present(state, _client.Phase, _client.Snapshot, ReturnToLobby, CanWatchReplay, WatchReplay);
            }
            else return;
            _lastError = string.Empty;
        }

        public void WatchReplay()
        {
            if (!CanWatchReplay) return;
            _presenter?.Clear();
            _predictionSeconds = 0d;
            _replayPlayer = new BattleReplayPlayer(_client!.RetainedReplay!);
        }

        public void ExitReplay()
        {
            _replayPlayer = null;
            _presenter?.Clear();
        }

        private void AdvanceShellFlow()
        {
            if (_client is null) return;

            if (_client.Phase == DemoClientPhase.AwaitingSessionEntry && _pendingNickname is not null)
            {
                if (!_sessionEntrySent)
                {
                    _client.EnterSession(_pendingNickname);
                    _sessionEntrySent = true;
                    ShellStage = ShellConnectionStage.EnteringSession;
                }
                else if (_client.Snapshot.LastRejection.Length > 0)
                {
                    throw new InvalidOperationException("The server rejected the nickname or session entry.");
                }
                return;
            }

            if (_client.Phase == DemoClientPhase.Lobby)
            {
                _pendingNickname = null;
                if (_pendingHostRoomName is not null)
                {
                    if (!_hostRoomCreateSent)
                    {
                        _client.CreateRoom(_pendingHostRoomName, 2);
                        _hostRoomCreateSent = true;
                        ShellStage = ShellConnectionStage.CreatingRoom;
                    }
                    else if (_client.Snapshot.LastRejection.Length > 0)
                    {
                        throw new InvalidOperationException("The server rejected room creation.");
                    }
                }
                else
                {
                    ShellStage = ShellConnectionStage.Ready;
                }
                return;
            }

            if (_client.Phase == DemoClientPhase.Room)
            {
                _pendingNickname = null;
                _pendingHostRoomName = null;
                _sessionEntrySent = false;
                _hostRoomCreateSent = false;
                ShellStage = ShellConnectionStage.Ready;
            }
        }

        private void SynchronizeScene()
        {
            if (_client is null) return;
            string active = SceneManager.GetActiveScene().name;
            bool battleSceneRequired = _client.Phase == DemoClientPhase.PreparingBattle ||
                _client.Phase == DemoClientPhase.InBattle ||
                _client.Phase == DemoClientPhase.SettlementPendingAuthority ||
                _client.Phase == DemoClientPhase.Settlement;
            if (battleSceneRequired && active != BattleSceneName)
            {
                _sceneReadySent = false;
                SceneManager.LoadScene(BattleSceneName);
                return;
            }
            bool lobbyRequired = _client.Phase == DemoClientPhase.Lobby || _client.Phase == DemoClientPhase.Room;
            if (lobbyRequired && active != LobbySceneName)
            {
                _presenter?.Clear();
                _presenter = null;
                if (_hud is not null) Destroy(_hud.gameObject);
                _hud = null;
                SceneManager.LoadScene(LobbySceneName);
                return;
            }
            if (_client.Phase == DemoClientPhase.PreparingBattle && active == BattleSceneName && !_sceneReadySent)
            {
                _client.MarkBattleSceneReady();
                _sceneReadySent = true;
            }
        }

        private LocalInputSample ReadGameplayInput()
        {
            Keyboard? keyboard = Keyboard.current;
            sbyte moveX = (sbyte)(((keyboard?.dKey.isPressed ?? false) ? 1 : 0) -
                ((keyboard?.aKey.isPressed ?? false) ? 1 : 0));
            sbyte moveZ = (sbyte)(((keyboard?.wKey.isPressed ?? false) ? 1 : 0) -
                ((keyboard?.sKey.isPressed ?? false) ? 1 : 0));
            bool fire = Mouse.current?.leftButton.isPressed ?? false;
            return new LocalInputSample(moveX, moveZ, GetCurrentAim(), fire);
        }

        private bool ConsumePredictionTick(double elapsedSeconds)
        {
            _predictionSeconds += elapsedSeconds;
            double secondsPerTick = 1d / SimulationConfig.TickRate;
            if (_predictionSeconds < secondsPerTick) return false;
            _predictionSeconds -= secondsPerTick;
            return true;
        }

        private ushort GetCurrentAim()
        {
            BattleState? state = _client?.PredictedBattleState;
            PlayerSlot? localSlot = _client?.LocalPlayerSlot;
            if (state is null || !localSlot.HasValue)
            {
                _aimDiagnostic = $"Aim unavailable: state={state is not null}, slot={localSlot}";
                return 0;
            }
            PlayerState player = state.GetPlayerState(localSlot.Value);
            Camera? camera = EnsurePresenter().GameplayCamera;
            if (camera == null)
            {
                _aimDiagnostic = "Aim unavailable: gameplay camera missing";
                return player.Aim;
            }
            Vector2 screenPosition = Mouse.current?.position.ReadValue() ??
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance))
            {
                _aimDiagnostic = $"Aim projection failed: mouse={screenPosition}, ray={ray}";
                return player.Aim;
            }
            Vector3 point = ray.GetPoint(distance);
            ushort aim = BattleSimulation.GetAimToward(
                new ArenaPoint(player.PositionX, player.PositionZ),
                new ArenaPoint(
                    Mathf.RoundToInt(point.x * BattlePresenter.WorldUnitsPerSimulationUnit),
                    Mathf.RoundToInt(point.z * BattlePresenter.WorldUnitsPerSimulationUnit)));
            _aimDiagnostic = $"Aim slot={localSlot.Value.Value} mouse={screenPosition} world={point:F2} " +
                $"input={aim} predicted={player.Aim} focused={Application.isFocused}";
            return aim;
        }

        private BattlePresenter EnsurePresenter()
        {
            if (_presenter == null)
            {
                _presenter = GetComponent<BattlePresenter>();
                if (_presenter == null) _presenter = gameObject.AddComponent<BattlePresenter>();
            }
            _presenter.Configure(_definition);
            return _presenter;
        }

        private BattleHudView EnsureHud()
        {
            if (_hud is not null) return _hud;
            var hudObject = new GameObject("v3-B Formal Battle HUD");
            hudObject.transform.SetParent(transform, false);
            _hud = hudObject.AddComponent<BattleHudView>();
            _hud.Configure(BattlePresentationCatalog.LoadRequired());
            return _hud;
        }

        private void OnGUI()
        {
            if (_instance != this) return;
            if (!_debugVisible) return;
            GUILayout.BeginArea(new Rect(16, Mathf.Max(140, Screen.height - 440), Mathf.Min(780, Screen.width - 32), 420), GUI.skin.box);
            if (_debugVisible && _client is not null)
                GUILayout.Label(LockstepDiagnosticsFormatter.Format(_client.Snapshot, _replayPlayer, _client.InputDelayTicks));
            if (_debugVisible) GUILayout.Label(_aimDiagnostic);
            if (_debugVisible && _lastError.Length > 0) GUILayout.Label("Error: " + _lastError);
            GUILayout.EndArea();
        }

        private void DrawBattleHud()
        {
            if (_client is null) return;
            BattleState? state = _client.PredictedBattleState;
            if (state is not null && state.IsGameplayEnabled)
            {
                PlayerState first = state.GetPlayerState(new PlayerSlot(0));
                PlayerState second = state.GetPlayerState(new PlayerSlot(1));
                int round = first.RoundWins + second.RoundWins + 1;
                GUILayout.Label($"P1 HP {first.HitPoints}     P2 HP {second.HitPoints}");
                GUILayout.Label($"Round {round}     Score {first.RoundWins} - {second.RoundWins}     Time {(state.RoundTicksRemaining + 29U) / 30U}s");
                if (state.Phase == BattlePhase.RoundCountdown)
                    GUILayout.Label($"{Math.Max(1U, (state.PhaseTicksRemaining + 29U) / 30U)}");
                else if (state.Phase == BattlePhase.Playing) GUILayout.Label("FIGHT");
                else if (state.Phase == BattlePhase.RoundEnded)
                    GUILayout.Label(state.RoundResult.HasWinner ? $"Round winner: P{state.RoundResult.WinnerSlot.Value + 1}" : "DRAW");
            }
            if (_client.Phase == DemoClientPhase.PreparingBattle) GUILayout.Label("Loading arena / waiting for all players...");
            if (_client.Phase == DemoClientPhase.SettlementPendingAuthority) GUILayout.Label("Verifying final authority...");
            if (_client.Phase == DemoClientPhase.Settlement)
            {
                DemoClientSnapshot snapshot = _client.Snapshot;
                string winner = snapshot.WinnerPlayerId == snapshot.SessionId ? "YOU WIN" :
                    snapshot.WinnerPlayerId == 0 ? "DRAW" : "YOU LOSE";
                GUILayout.Label($"{winner}   Final score {snapshot.Slot0RoundWins} - {snapshot.Slot1RoundWins}");
                GUILayout.Label(snapshot.SettlementReason.ToString());
                Button("Return To Lobby", true, () => _client.ReturnToLobby());
            }
            GUILayout.Label("WASD move | Mouse aim | Hold left mouse to fire | F1 debug");
        }

        public static string FormatDiagnostics(DemoClientSnapshot snapshot)
        {
            return LockstepDiagnosticsFormatter.Format(snapshot);
        }

        public async Task BeginHostLanAsync(string nickname, string roomName)
        {
            string validNickname = RequireText(nickname, nameof(nickname));
            string validRoomName = RequireText(roomName, nameof(roomName));
            DisconnectToMainMenu();
            int generation = ++_shellFlowGeneration;
            try
            {
                ShellStage = ShellConnectionStage.StartingServer;
                _lanServerLauncher.StartOwned(LanServerPathResolver.Resolve(), controlPort, battlePort);
                ShellStage = ShellConnectionStage.WaitingForServer;
                await _lanServerLauncher.WaitUntilReadyAsync(
                    "127.0.0.1",
                    controlPort,
                    TimeSpan.FromSeconds(5));
                if (generation != _shellFlowGeneration) return;

                HostedLanAddress = LanAddressUtility.FindPreferredPrivateIpv4() ?? "Unavailable";
                _pendingNickname = validNickname;
                _pendingHostRoomName = validRoomName;
                CreateAndConnectClient("127.0.0.1");
            }
            catch (Exception exception)
            {
                if (generation == _shellFlowGeneration) FailShell(exception);
            }
        }

        public void BeginJoinLan(string nickname, string serverAddress)
        {
            string validNickname = RequireText(nickname, nameof(nickname));
            if (!IPAddress.TryParse(serverAddress, out IPAddress? address) ||
                address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Server address must be a numeric IPv4 address.", nameof(serverAddress));
            }

            DisconnectToMainMenu();
            ++_shellFlowGeneration;
            try
            {
                _pendingNickname = validNickname;
                CreateAndConnectClient(address.ToString());
            }
            catch (Exception exception)
            {
                FailShell(exception);
            }
        }

        public void RefreshRooms()
        {
            RequireClient().RequestRoomList();
        }

        public void CreateRoom(string roomName)
        {
            RequireClient().CreateRoom(RequireText(roomName, nameof(roomName)), 2);
        }

        public void JoinRoom(ulong roomId)
        {
            RequireClient().JoinRoom(roomId);
        }

        public void LeaveRoom()
        {
            RequireClient().LeaveRoom();
        }

        public void SetReady(bool value)
        {
            RequireClient().SetReady(value);
        }

        public void StartBattle()
        {
            RequireClient().StartBattle();
        }

        public void ReturnToLobby()
        {
            RequireClient().ReturnToLobby();
            ExitReplay();
        }

        public bool TryConsumeCommandRejection(out string message)
        {
            if (_client is null || !_client.TryConsumeRejection(
                    out ControlRejectReasonMessage reason,
                    out string detail))
            {
                message = string.Empty;
                return false;
            }

            message = HumanizeCommandRejection(reason, detail);
            return true;
        }

        public void DisconnectToMainMenu()
        {
            if (_client is not null && IsBattlePhase(_client.Phase))
            {
                throw new InvalidOperationException("Disconnect to main menu is not available during battle.");
            }

            ++_shellFlowGeneration;
            _replayPlayer = null;
            _client?.Dispose();
            _client = null;
            _lanServerLauncher.StopOwned();
            _pendingNickname = null;
            _pendingHostRoomName = null;
            _sessionEntrySent = false;
            _hostRoomCreateSent = false;
            _sceneReadySent = false;
            _predictionSeconds = 0d;
            _presenter?.Clear();
            _presenter = null;
            if (_hud is not null) Destroy(_hud.gameObject);
            _hud = null;
            HostedLanAddress = string.Empty;
            UserFacingError = string.Empty;
            _lastError = string.Empty;
            ShellStage = ShellConnectionStage.Idle;
        }

        private void CreateAndConnectClient(string address)
        {
            _client = new TcpDemoClient(new DemoClientOptions(
                controlPort, 4096, 32768, 1024, 7, 257, 8, 512,
                ClientPredictionCapacity, 16, 64, 4096, 4096, 1024, 11, 251,
                address, _definition));
            _client.BeginConnect();
            _sessionEntrySent = false;
            _hostRoomCreateSent = false;
            _lastError = string.Empty;
            UserFacingError = string.Empty;
            _sceneReadySent = false;
            ShellStage = ShellConnectionStage.Connecting;
        }

        private TcpDemoClient RequireClient()
        {
            return _client ?? throw new InvalidOperationException("No server connection is active.");
        }

        private void FailShell(Exception exception)
        {
            ExitReplay();
            _lastError = exception.ToString();
            UserFacingError = HumanizeShellError(exception);
            ShellStage = ShellConnectionStage.Failed;
            _client?.Dispose();
            _client = null;
            _lanServerLauncher.StopOwned();
            _pendingNickname = null;
            _pendingHostRoomName = null;
            _sessionEntrySent = false;
            _hostRoomCreateSent = false;
        }

        private static string HumanizeShellError(Exception exception)
        {
            if (exception is FileNotFoundException)
                return "找不到本地服务器程序，请先完成 Windows 构建。";
            if (exception is TimeoutException)
                return "服务器启动或连接超时，请检查端口与防火墙后重试。";
            if (exception is SocketException socketException && socketException.SocketErrorCode == SocketError.AddressAlreadyInUse)
                return "服务器端口已被占用，请关闭旧服务器后重试。";
            if (exception is SocketException)
                return "无法连接服务器，请检查局域网地址、防火墙和服务器状态。";
            if (exception is ArgumentException)
                return "输入无效，请检查昵称、房间名和 IPv4 地址。";
            if (exception.Message.IndexOf("nickname", StringComparison.OrdinalIgnoreCase) >= 0)
                return "昵称不可用，请更换昵称后重试。";
            if (exception.Message.IndexOf("room", StringComparison.OrdinalIgnoreCase) >= 0)
                return "创建房间失败，请修改房间名后重试。";
            return "连接流程失败，请重试或返回主菜单。";
        }

        private static string HumanizeCommandRejection(
            ControlRejectReasonMessage reason,
            string detail)
        {
            return reason switch
            {
                ControlRejectReasonMessage.ControlRejectReasonRoomNotFound =>
                    "房间不存在或已关闭，请刷新房间列表。",
                ControlRejectReasonMessage.ControlRejectReasonRoomFull =>
                    "房间已满，请选择其他房间。",
                ControlRejectReasonMessage.ControlRejectReasonNotHost =>
                    "只有房主可以开始比赛。",
                ControlRejectReasonMessage.ControlRejectReasonNotReady =>
                    "所有玩家准备后才能开始比赛。",
                ControlRejectReasonMessage.ControlRejectReasonNicknameInUse =>
                    "昵称已被使用，请更换昵称。",
                ControlRejectReasonMessage.ControlRejectReasonResourceLimit =>
                    "服务器资源已满，请稍后重试。",
                ControlRejectReasonMessage.ControlRejectReasonInvalidValue =>
                    "服务器拒绝了该输入，请检查后重试。",
                ControlRejectReasonMessage.ControlRejectReasonInvalidPhase =>
                    "当前状态无法执行该操作，请刷新后重试。",
                _ => "服务器拒绝了该操作，请重试。",
            };
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A non-empty value is required.", parameterName);
            return value.Trim();
        }

        private static bool IsBattlePhase(DemoClientPhase phase)
        {
            return phase == DemoClientPhase.PreparingBattle ||
                   phase == DemoClientPhase.InBattle ||
                   phase == DemoClientPhase.SettlementPendingAuthority ||
                   phase == DemoClientPhase.Settlement;
        }

        private void Button(string label, bool enabled, Action action)
        {
            bool previous = GUI.enabled;
            GUI.enabled = enabled;
            if (GUILayout.Button(label))
            {
                try
                {
                    action();
                    _lastError = string.Empty;
                }
                catch (Exception exception)
                {
                    _lastError = exception.ToString();
                }
            }
            GUI.enabled = previous;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _client?.Dispose();
            _lanServerLauncher.Dispose();
            _instance = null;
        }
    }
}
