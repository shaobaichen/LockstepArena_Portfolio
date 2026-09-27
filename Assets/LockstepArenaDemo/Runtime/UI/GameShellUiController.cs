#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LockstepArena.Client.Demo;
using LockstepArena.Protocol.Wire;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LockstepArena.Demo
{
    public sealed class GameShellUiController : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private GameObject mainMenuScreen = null!;
        [SerializeField] private GameObject modeSelectScreen = null!;
        [SerializeField] private GameObject createLanScreen = null!;
        [SerializeField] private GameObject joinLanScreen = null!;
        [SerializeField] private GameObject connectingOverlay = null!;
        [SerializeField] private GameObject lobbyScreen = null!;
        [SerializeField] private GameObject roomScreen = null!;
        [SerializeField] private GameObject errorOverlay = null!;
        [SerializeField] private GameObject settingsOverlay = null!;
        [SerializeField] private GameObject createRoomModal = null!;

        [Header("Forms")]
        [SerializeField] private TMP_InputField hostNicknameInput = null!;
        [SerializeField] private TMP_InputField hostRoomNameInput = null!;
        [SerializeField] private TMP_InputField joinNicknameInput = null!;
        [SerializeField] private TMP_InputField joinAddressInput = null!;
        [SerializeField] private TMP_InputField createRoomNameInput = null!;

        [Header("Progress and errors")]
        [SerializeField] private TMP_Text connectingText = null!;
        [SerializeField] private TMP_Text errorText = null!;

        [Header("Lobby")]
        [SerializeField] private Transform roomListContent = null!;
        [SerializeField] private RoomListItemView roomListItemPrefab = null!;
        [SerializeField] private TMP_Text selectedRoomNameText = null!;
        [SerializeField] private TMP_Text selectedRoomOwnerText = null!;
        [SerializeField] private TMP_Text selectedRoomStatusText = null!;
        [SerializeField] private Button joinSelectedRoomButton = null!;

        [Header("Room")]
        [SerializeField] private TMP_Text currentRoomNameText = null!;
        [SerializeField] private TMP_Text inviteAddressText = null!;
        [SerializeField] private GameObject inviteAddressGroup = null!;
        [SerializeField] private GameObject copyAddressButton = null!;
        [SerializeField] private PlayerCardView leftPlayerCard = null!;
        [SerializeField] private PlayerCardView rightPlayerCard = null!;
        [SerializeField] private Button readyButton = null!;
        [SerializeField] private TMP_Text readyButtonText = null!;
        [SerializeField] private Button startButton = null!;
        [SerializeField] private TMP_Text startButtonText = null!;

        private static TMP_FontAsset? runtimeFont;
        private LockstepArenaDemoController controller = null!;
        private ShellScreen screen = ShellScreen.MainMenu;
        private ulong selectedRoomId;
        private string roomsFingerprint = string.Empty;
        private string localError = string.Empty;
        private Func<Task>? retryAction;
        private bool preserveConnectionOnError;

        private enum ShellScreen
        {
            MainMenu,
            ModeSelect,
            CreateLan,
            JoinLan,
            Lobby,
            Room,
        }

        private void Awake()
        {
            controller = FindFirstObjectByType<LockstepArenaDemoController>()
                ?? throw new InvalidOperationException("LockstepArenaDemoController is missing from LobbyScene.");
            ApplyRuntimeFont();
            ShowScreen(ShellScreen.MainMenu);
            WireButtons();
        }

        private void Update()
        {
            DemoClientSnapshot? snapshot = controller.ClientSnapshot;
            if (snapshot?.Phase == DemoClientPhase.Room)
            {
                ShowScreen(ShellScreen.Room);
                RefreshRoom(snapshot);
            }
            else if (snapshot?.Phase == DemoClientPhase.Lobby)
            {
                ShowScreen(ShellScreen.Lobby);
                RefreshLobby(snapshot);
            }

            if (controller.TryConsumeCommandRejection(out string rejection))
            {
                localError = rejection;
                retryAction = null;
                preserveConnectionOnError = true;
            }

            bool connecting = controller.ShellStage != ShellConnectionStage.Idle &&
                              controller.ShellStage != ShellConnectionStage.Ready &&
                              controller.ShellStage != ShellConnectionStage.Failed;
            connectingOverlay.SetActive(connecting);
            connectingText.text = StageText(controller.ShellStage);

            bool failed = controller.ShellStage == ShellConnectionStage.Failed || localError.Length > 0;
            errorOverlay.SetActive(failed);
            errorText.text = localError.Length > 0 ? localError : controller.UserFacingError;
        }

        public void ShowModeSelect() => ShowScreen(ShellScreen.ModeSelect);
        public void ShowCreateLan() => ShowScreen(ShellScreen.CreateLan);
        public void ShowJoinLan() => ShowScreen(ShellScreen.JoinLan);
        public void ShowMainMenu()
        {
            if (controller.ClientSnapshot == null || !IsBattlePhase(controller.ClientSnapshot.Phase))
                controller.DisconnectToMainMenu();
            localError = string.Empty;
            retryAction = null;
            preserveConnectionOnError = false;
            ShowScreen(ShellScreen.MainMenu);
        }

        public void ToggleSettings(bool visible) => settingsOverlay.SetActive(visible);
        public void ToggleFullscreen(bool value) => Screen.fullScreen = value;
        public void QuitGame() => Application.Quit();

        public async void HostLan()
        {
            string nickname = hostNicknameInput.text;
            string roomName = hostRoomNameInput.text;
            retryAction = () => HostLanAsync(nickname, roomName);
            await retryAction();
        }

        public async void JoinLan()
        {
            string nickname = joinNicknameInput.text;
            string address = joinAddressInput.text;
            retryAction = () => JoinLanAsync(nickname, address);
            await retryAction();
        }

        public async void Retry()
        {
            localError = string.Empty;
            preserveConnectionOnError = false;
            if (retryAction != null) await retryAction();
        }

        public void DismissError()
        {
            if (!preserveConnectionOnError)
            {
                ShowMainMenu();
                return;
            }

            localError = string.Empty;
            retryAction = null;
            preserveConnectionOnError = false;
        }

        public void RefreshRooms() => Execute(controller.RefreshRooms);
        public void OpenCreateRoomModal() => createRoomModal.SetActive(true);
        public void CloseCreateRoomModal() => createRoomModal.SetActive(false);
        public void CreateRoom()
        {
            Execute(() => controller.CreateRoom(createRoomNameInput.text));
            if (localError.Length == 0) createRoomModal.SetActive(false);
        }

        public void JoinSelectedRoom()
        {
            Execute(() => controller.JoinRoom(selectedRoomId));
        }

        public void LeaveRoom() => Execute(controller.LeaveRoom);
        public void StartBattle() => Execute(controller.StartBattle);

        public void ToggleReady()
        {
            DemoClientSnapshot? snapshot = controller.ClientSnapshot;
            DemoRoomParticipantSnapshot? local = snapshot?.Participants
                .FirstOrDefault(participant => participant.SessionId == snapshot.SessionId);
            Execute(() => controller.SetReady(!(local?.IsReady ?? false)));
        }

        public void CopyInviteAddress()
        {
            if (controller.HostedLanAddress.Length > 0)
                GUIUtility.systemCopyBuffer = controller.HostedLanAddress;
        }

        private async Task HostLanAsync(string nickname, string roomName)
        {
            localError = string.Empty;
            preserveConnectionOnError = false;
            try
            {
                await controller.BeginHostLanAsync(nickname, roomName);
            }
            catch (Exception exception)
            {
                localError = HumanizeInputError(exception);
            }
        }

        private Task JoinLanAsync(string nickname, string address)
        {
            localError = string.Empty;
            preserveConnectionOnError = false;
            try
            {
                if (!IPAddress.TryParse(address, out IPAddress? parsed) ||
                    parsed.AddressFamily != AddressFamily.InterNetwork)
                    throw new ArgumentException("IPv4 address is invalid.");
                controller.BeginJoinLan(nickname, parsed.ToString());
            }
            catch (Exception exception)
            {
                localError = HumanizeInputError(exception);
            }
            return Task.CompletedTask;
        }

        private void Execute(Action action)
        {
            localError = string.Empty;
            preserveConnectionOnError = false;
            try { action(); }
            catch (Exception exception) { localError = HumanizeInputError(exception); }
        }

        private void RefreshLobby(DemoClientSnapshot snapshot)
        {
            string fingerprint = string.Join(";", snapshot.Rooms.Select(room =>
                $"{room.RoomId}:{room.ParticipantCount}:{room.Capacity}:{(int)room.Lifecycle}"));
            if (fingerprint != roomsFingerprint)
            {
                roomsFingerprint = fingerprint;
                foreach (Transform child in roomListContent) Destroy(child.gameObject);
                if (snapshot.Rooms.Count > 0 && snapshot.Rooms.All(room => room.RoomId != selectedRoomId))
                    selectedRoomId = snapshot.Rooms[0].RoomId;
                foreach (DemoRoomSummarySnapshot room in snapshot.Rooms)
                {
                    RoomListItemView item = Instantiate(roomListItemPrefab, roomListContent);
                    item.Configure(room, room.RoomId == selectedRoomId, SelectRoom);
                }
            }

            DemoRoomSummarySnapshot? selected = snapshot.Rooms.FirstOrDefault(room => room.RoomId == selectedRoomId);
            selectedRoomNameText.text = selected?.RoomName ?? "选择一个房间";
            selectedRoomOwnerText.text = selected == null ? "" : $"房主  {selected.HostNickname}";
            selectedRoomStatusText.text = selected == null
                ? "暂无可加入房间"
                : $"人数  {selected.ParticipantCount}/{selected.Capacity}";
            joinSelectedRoomButton.interactable = selected != null &&
                selected.Lifecycle == RoomLifecycleMessage.RoomLifecycleOpen &&
                selected.ParticipantCount < selected.Capacity;
        }

        private void SelectRoom(ulong roomId)
        {
            selectedRoomId = roomId;
            roomsFingerprint = string.Empty;
        }

        private void RefreshRoom(DemoClientSnapshot snapshot)
        {
            currentRoomNameText.text = snapshot.RoomName;
            DemoRoomParticipantSnapshot[] participants = snapshot.Participants
                .OrderBy(participant => participant.JoinOrdinal).ToArray();
            leftPlayerCard.Show(participants.ElementAtOrDefault(0));
            rightPlayerCard.Show(participants.ElementAtOrDefault(1));

            DemoRoomParticipantSnapshot? local = participants
                .FirstOrDefault(participant => participant.SessionId == snapshot.SessionId);
            bool isHost = snapshot.RoomHostSessionId == snapshot.SessionId;
            bool full = snapshot.RoomCapacity > 0 && participants.Length == snapshot.RoomCapacity;
            bool everyoneReady = full && participants.All(participant => participant.IsReady);

            readyButton.interactable = local != null;
            readyButtonText.text = local?.IsReady == true ? "取消准备" : "准备";
            startButton.gameObject.SetActive(isHost);
            startButton.interactable = isHost && everyoneReady;
            startButtonText.text = everyoneReady ? "开始比赛" : "等待玩家准备...";

            bool showInvite = controller.OwnsLocalServer && controller.HostedLanAddress.Length > 0;
            inviteAddressGroup.SetActive(showInvite);
            copyAddressButton.SetActive(showInvite);
            inviteAddressText.text = showInvite ? controller.HostedLanAddress : string.Empty;
        }

        private void ShowScreen(ShellScreen target)
        {
            screen = target;
            mainMenuScreen.SetActive(screen == ShellScreen.MainMenu);
            modeSelectScreen.SetActive(screen == ShellScreen.ModeSelect);
            createLanScreen.SetActive(screen == ShellScreen.CreateLan);
            joinLanScreen.SetActive(screen == ShellScreen.JoinLan);
            lobbyScreen.SetActive(screen == ShellScreen.Lobby);
            roomScreen.SetActive(screen == ShellScreen.Room);
        }

        private void WireButtons()
        {
            Bind("MainMenuScreen/StartButton", ShowModeSelect);
            Bind("MainMenuScreen/SettingsButton", () => ToggleSettings(true));
            Bind("MainMenuScreen/QuitButton", QuitGame);
            Bind("ModeSelectScreen/CreateCard", ShowCreateLan);
            Bind("ModeSelectScreen/JoinCard", ShowJoinLan);
            Bind("ModeSelectScreen/BackButton", () => ShowScreen(ShellScreen.MainMenu));
            Bind("CreateLanScreen/CreateButton", HostLan);
            Bind("CreateLanScreen/BackButton", () => ShowScreen(ShellScreen.ModeSelect));
            Bind("JoinLanScreen/JoinButton", JoinLan);
            Bind("JoinLanScreen/BackButton", () => ShowScreen(ShellScreen.ModeSelect));
            Bind("LobbyScreen/RefreshButton", RefreshRooms);
            Bind("LobbyScreen/CreateRoomButton", OpenCreateRoomModal);
            Bind("LobbyScreen/RoomDetails/JoinSelectedRoomButton", JoinSelectedRoom);
            Bind("LobbyScreen/BackButton", ShowMainMenu);
            Bind("RoomScreen/LeaveButton", LeaveRoom);
            Bind("RoomScreen/CopyAddressButton", CopyInviteAddress);
            Bind("RoomScreen/ReadyButton", ToggleReady);
            Bind("RoomScreen/StartButton", StartBattle);
            Bind("ErrorOverlay/RetryButton", Retry);
            Bind("ErrorOverlay/BackButton", DismissError);
            Bind("SettingsOverlay/BackButton", () => ToggleSettings(false));
            Bind("CreateRoomModal/CreateButton", CreateRoom);
            Bind("CreateRoomModal/CancelButton", CloseCreateRoomModal);
            Toggle? fullscreenToggle = settingsOverlay.GetComponentInChildren<Toggle>(true);
            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener(ToggleFullscreen);
            }
        }

        private void Bind(string path, UnityEngine.Events.UnityAction action)
        {
            Transform target = transform.Find(path)
                ?? throw new InvalidOperationException($"GameShell control is missing: {path}");
            Button button = target.GetComponent<Button>()
                ?? throw new InvalidOperationException($"GameShell Button is missing: {path}");
            button.onClick.AddListener(action);
        }

        internal static TMP_FontAsset GetRuntimeFont()
        {
            if (runtimeFont == null)
            {
                foreach (string family in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" })
                {
                    runtimeFont = TMP_FontAsset.CreateFontAsset(family, "Regular", 48);
                    if (runtimeFont != null) break;
                }

                if (runtimeFont == null)
                    throw new InvalidOperationException("No supported Windows UI font is installed.");
            }
            return runtimeFont;
        }

        private void ApplyRuntimeFont()
        {
            TMP_FontAsset font = GetRuntimeFont();
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true)) text.font = font;
            foreach (TMP_InputField input in GetComponentsInChildren<TMP_InputField>(true))
            {
                input.textComponent.font = font;
                if (input.placeholder is TMP_Text placeholder) placeholder.font = font;
            }
        }

        private static string StageText(ShellConnectionStage stage)
        {
            return stage switch
            {
                ShellConnectionStage.StartingServer => "正在启动本地服务器...",
                ShellConnectionStage.WaitingForServer => "正在等待服务器就绪...",
                ShellConnectionStage.Connecting => "正在连接服务器...",
                ShellConnectionStage.EnteringSession => "正在进入大厅...",
                ShellConnectionStage.CreatingRoom => "正在创建房间...",
                _ => "请稍候...",
            };
        }

        private static string HumanizeInputError(Exception exception)
        {
            return exception is ArgumentException
                ? "输入无效，请检查昵称、房间名和 IPv4 地址。"
                : "操作失败，请重试。";
        }

        private static bool IsBattlePhase(DemoClientPhase phase)
        {
            return phase == DemoClientPhase.PreparingBattle ||
                   phase == DemoClientPhase.InBattle ||
                   phase == DemoClientPhase.SettlementPendingAuthority ||
                   phase == DemoClientPhase.Settlement;
        }
    }
}
