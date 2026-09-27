#nullable enable

using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LockstepArena.Demo.Editor
{
    public static class V3AGameShellBuilder
    {
        private const string UiRoot = "Assets/LockstepArenaDemo/Prefabs/UI";
        private const string LobbyScene = "Assets/LockstepArenaDemo/Scenes/LobbyScene.unity";
        private static readonly Color Background = new Color(0.015f, 0.035f, 0.065f, 0.96f);
        private static readonly Color Panel = new Color(0.035f, 0.09f, 0.15f, 0.96f);
        private static readonly Color Cyan = new Color(0.12f, 0.78f, 0.95f, 1f);
        private static readonly Color White = new Color(0.90f, 0.95f, 1f, 1f);
        private static readonly Color Muted = new Color(0.55f, 0.65f, 0.74f, 1f);

        [MenuItem("Lockstep Arena/Rebuild v3-A Shell Assets")]
        public static void BuildAssets()
        {
            EnsureFolders();
            RoomListItemView roomListItem = BuildRoomListItem();
            PlayerCardView playerCard = BuildPlayerCard();
            GameObject canvasPrefab = BuildCanvas(roomListItem, playerCard);
            BuildLobbyScene(canvasPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("v3-A game shell prefabs and LobbyScene rebuilt.");
        }

        private static RoomListItemView BuildRoomListItem()
        {
            GameObject root = UiObject("RoomListItem", null);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(600, 86);
            Image background = root.AddComponent<Image>();
            background.sprite = Sprite("field_dark");
            background.type = Image.Type.Sliced;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            root.AddComponent<LayoutElement>().preferredHeight = 86;

            TextMeshProUGUI roomName = Text(root.transform, "RoomNameText", "ROOM", 28, TextAlignmentOptions.MidlineLeft,
                new Vector2(-120, 13), new Vector2(300, 32));
            TextMeshProUGUI count = Text(root.transform, "CountText", "0/2", 23, TextAlignmentOptions.MidlineRight,
                new Vector2(210, 13), new Vector2(100, 32));
            TextMeshProUGUI status = Text(root.transform, "StatusText", "等待中", 19, TextAlignmentOptions.MidlineLeft,
                new Vector2(-120, -22), new Vector2(300, 26));
            status.color = Muted;

            RoomListItemView view = root.AddComponent<RoomListItemView>();
            Set(view, "roomNameText", roomName);
            Set(view, "countText", count);
            Set(view, "statusText", status);
            Set(view, "background", background);
            Set(view, "selectButton", button);

            string path = $"{UiRoot}/RoomListItem.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<RoomListItemView>();
        }

        private static PlayerCardView BuildPlayerCard()
        {
            GameObject root = UiObject("PlayerCard", null);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(390, 360);
            Image background = root.AddComponent<Image>();
            background.sprite = Sprite("panel_dark_frame");
            background.type = Image.Type.Sliced;
            background.color = new Color(0.12f, 0.20f, 0.30f, 0.98f);

            TextMeshProUGUI nickname = Text(root.transform, "NicknameText", "PLAYER", 34,
                TextAlignmentOptions.Center, new Vector2(0, 40), new Vector2(330, 60));
            TextMeshProUGUI ready = Text(root.transform, "ReadyText", "等待中", 24,
                TextAlignmentOptions.Center, new Vector2(0, -38), new Vector2(300, 44));
            GameObject hostBadge = PanelObject(root.transform, "HostBadge", new Vector2(-120, 135), new Vector2(110, 34), Cyan);
            Text(hostBadge.transform, "Label", "HOST", 17, TextAlignmentOptions.Center, Vector2.zero, new Vector2(100, 28));
            GameObject empty = UiObject("EmptyState", root.transform);
            Stretch(empty.GetComponent<RectTransform>());
            Text(empty.transform, "Label", "等待玩家加入", 24, TextAlignmentOptions.Center, Vector2.zero, new Vector2(260, 50)).color = Muted;
            empty.SetActive(false);

            PlayerCardView view = root.AddComponent<PlayerCardView>();
            Set(view, "nicknameText", nickname);
            Set(view, "readyText", ready);
            Set(view, "hostBadge", hostBadge);
            Set(view, "emptyState", empty);

            string path = $"{UiRoot}/PlayerCard.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<PlayerCardView>();
        }

        private static GameObject BuildCanvas(RoomListItemView roomItemPrefab, PlayerCardView playerCardPrefab)
        {
            GameObject root = UiObject("GameShellCanvas", null);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            GameShellUiController shell = root.AddComponent<GameShellUiController>();

            GameObject backdrop = PanelObject(root.transform, "Backdrop", Vector2.zero, Vector2.zero, Background);
            Stretch(backdrop.GetComponent<RectTransform>());
            backdrop.transform.SetAsFirstSibling();
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.raycastTarget = false;

            GameObject main = Screen(root.transform, "MainMenuScreen");
            Text(main.transform, "Title", "LOCKSTEP ARENA", 76, TextAlignmentOptions.Center,
                new Vector2(0, 260), new Vector2(1100, 100)).color = Cyan;
            Text(main.transform, "Subtitle", "FRAME BATTLE", 28, TextAlignmentOptions.Center,
                new Vector2(0, 190), new Vector2(500, 50)).color = Muted;
            Button(main.transform, "StartButton", "开始游戏", new Vector2(0, 20), new Vector2(430, 86), "button_primary", Cyan);
            Button(main.transform, "SettingsButton", "设置", new Vector2(0, -90), new Vector2(360, 68), "button_secondary", White);
            Button(main.transform, "QuitButton", "退出", new Vector2(0, -180), new Vector2(360, 68), "button_secondary", White);
            Text(main.transform, "Version", "v3-A  ·  LAN MULTIPLAYER", 17, TextAlignmentOptions.BottomRight,
                new Vector2(780, -500), new Vector2(300, 30)).color = Muted;

            GameObject mode = Screen(root.transform, "ModeSelectScreen");
            Heading(mode.transform, "选择游戏方式", "同一局域网内，创建或加入一场比赛");
            Button(mode.transform, "CreateCard", "创建局域网游戏\n在本机自动启动服务器", new Vector2(-300, 0),
                new Vector2(500, 300), "panel_dark_frame", Cyan);
            Button(mode.transform, "JoinCard", "加入局域网游戏\n输入好友的 IPv4 地址", new Vector2(300, 0),
                new Vector2(500, 300), "panel_dark_frame", White);
            Button(mode.transform, "BackButton", "返回", new Vector2(0, -310), new Vector2(260, 62), "button_secondary", White);

            GameObject create = Screen(root.transform, "CreateLanScreen");
            Heading(create.transform, "创建局域网游戏", "服务器会在这台电脑上自动启动");
            TMP_InputField hostNickname = Input(create.transform, "HostNicknameInput", "昵称", new Vector2(0, 70));
            TMP_InputField hostRoom = Input(create.transform, "HostRoomNameInput", "房间名称", new Vector2(0, -20));
            hostNickname.text = "Alpha";
            hostRoom.text = "Golden Room";
            Button(create.transform, "CreateButton", "创建游戏", new Vector2(0, -150), new Vector2(420, 74), "button_primary", Cyan);
            Button(create.transform, "BackButton", "返回", new Vector2(0, -245), new Vector2(260, 60), "button_secondary", White);

            GameObject join = Screen(root.transform, "JoinLanScreen");
            Heading(join.transform, "加入局域网游戏", "输入主机显示的 IPv4 地址");
            TMP_InputField joinNickname = Input(join.transform, "JoinNicknameInput", "昵称", new Vector2(0, 70));
            TMP_InputField joinAddress = Input(join.transform, "JoinAddressInput", "例如 192.168.1.23", new Vector2(0, -20));
            joinNickname.text = "Bravo";
            joinAddress.text = "127.0.0.1";
            Button(join.transform, "JoinButton", "加入游戏", new Vector2(0, -150), new Vector2(420, 74), "button_primary", Cyan);
            Button(join.transform, "BackButton", "返回", new Vector2(0, -245), new Vector2(260, 60), "button_secondary", White);

            GameObject connecting = Overlay(root.transform, "ConnectingOverlay");
            TextMeshProUGUI connectingText = Text(connecting.transform, "ProgressText", "请稍候...", 34,
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(760, 80));

            GameObject lobby = Screen(root.transform, "LobbyScreen");
            Heading(lobby.transform, "局域网大厅", "选择房间，或创建一场新的比赛");
            GameObject leftPanel = PanelObject(lobby.transform, "RoomBrowser", new Vector2(-380, -30), new Vector2(760, 650), Panel);
            Transform roomContent = ScrollContent(leftPanel.transform);
            GameObject rightPanel = PanelObject(lobby.transform, "RoomDetails", new Vector2(390, -30), new Vector2(650, 650), Panel);
            TextMeshProUGUI selectedName = Text(rightPanel.transform, "SelectedRoomName", "选择一个房间", 38,
                TextAlignmentOptions.Center, new Vector2(0, 170), new Vector2(560, 70));
            TextMeshProUGUI selectedOwner = Text(rightPanel.transform, "SelectedRoomOwner", "", 24,
                TextAlignmentOptions.Center, new Vector2(0, 75), new Vector2(500, 50));
            TextMeshProUGUI selectedStatus = Text(rightPanel.transform, "SelectedRoomStatus", "暂无可加入房间", 24,
                TextAlignmentOptions.Center, new Vector2(0, 10), new Vector2(500, 50));
            Button joinSelected = Button(rightPanel.transform, "JoinSelectedRoomButton", "加入房间", new Vector2(0, -160),
                new Vector2(410, 74), "button_primary", Cyan);
            Button(lobby.transform, "RefreshButton", "刷新", new Vector2(-590, -435), new Vector2(220, 62), "button_secondary", White);
            Button(lobby.transform, "CreateRoomButton", "+ 创建房间", new Vector2(-330, -435), new Vector2(260, 62), "button_primary", Cyan);
            Button(lobby.transform, "BackButton", "返回主菜单", new Vector2(550, -435), new Vector2(260, 62), "button_secondary", White);

            GameObject room = Screen(root.transform, "RoomScreen");
            TextMeshProUGUI currentRoomName = Text(room.transform, "CurrentRoomName", "ROOM", 48,
                TextAlignmentOptions.Center, new Vector2(0, 420), new Vector2(800, 70));
            Button(room.transform, "LeaveButton", "离开房间", new Vector2(-730, 430), new Vector2(240, 58), "button_secondary", White);
            GameObject invite = PanelObject(room.transform, "InviteAddressGroup", new Vector2(650, 430), new Vector2(430, 70), Panel);
            TextMeshProUGUI inviteAddress = Text(invite.transform, "InviteAddressText", "192.168.1.23", 22,
                TextAlignmentOptions.MidlineLeft, new Vector2(-70, 0), new Vector2(240, 40));
            Button(room.transform, "CopyAddressButton", "复制地址", new Vector2(790, 430), new Vector2(160, 48), "button_secondary", White);

            PlayerCardView leftCard = (PlayerCardView)PrefabUtility.InstantiatePrefab(playerCardPrefab, room.transform);
            leftCard.name = "LeftPlayerCard";
            SetRect(leftCard.GetComponent<RectTransform>(), new Vector2(-360, 50), new Vector2(390, 360));
            PlayerCardView rightCard = (PlayerCardView)PrefabUtility.InstantiatePrefab(playerCardPrefab, room.transform);
            rightCard.name = "RightPlayerCard";
            SetRect(rightCard.GetComponent<RectTransform>(), new Vector2(360, 50), new Vector2(390, 360));
            Text(room.transform, "Versus", "VS", 72, TextAlignmentOptions.Center, new Vector2(0, 60), new Vector2(180, 100)).color = Cyan;
            Text(room.transform, "Rule", "BEST OF THREE", 25, TextAlignmentOptions.Center, new Vector2(0, -205), new Vector2(400, 44)).color = Muted;
            Button ready = Button(room.transform, "ReadyButton", "准备", new Vector2(-230, -320), new Vector2(360, 76), "button_ready", White);
            Button start = Button(room.transform, "StartButton", "等待玩家准备...", new Vector2(230, -320), new Vector2(420, 76), "button_primary", Cyan);

            GameObject error = Overlay(root.transform, "ErrorOverlay");
            Text(error.transform, "Title", "连接失败", 42, TextAlignmentOptions.Center, new Vector2(0, 120), new Vector2(600, 70)).color = new Color(1f, 0.35f, 0.30f);
            TextMeshProUGUI errorText = Text(error.transform, "ErrorText", "操作失败，请重试。", 25,
                TextAlignmentOptions.Center, new Vector2(0, 35), new Vector2(720, 90));
            Button(error.transform, "RetryButton", "重试", new Vector2(-140, -95), new Vector2(240, 62), "button_primary", Cyan);
            Button(error.transform, "BackButton", "返回", new Vector2(140, -95), new Vector2(240, 62), "button_secondary", White);

            GameObject settings = Overlay(root.transform, "SettingsOverlay");
            Text(settings.transform, "Title", "设置", 44, TextAlignmentOptions.Center, new Vector2(0, 130), new Vector2(500, 70));
            Toggle(settings.transform, "FullscreenToggle", "全屏", new Vector2(0, 25));
            Button(settings.transform, "BackButton", "返回", new Vector2(0, -110), new Vector2(260, 62), "button_secondary", White);

            GameObject modal = Overlay(root.transform, "CreateRoomModal", new Vector2(720, 390));
            Text(modal.transform, "Title", "创建房间", 38, TextAlignmentOptions.Center, new Vector2(0, 105), new Vector2(500, 60));
            TMP_InputField modalRoom = Input(modal.transform, "RoomNameInput", "房间名称", new Vector2(0, 20));
            modalRoom.text = "Arena Room";
            Button(modal.transform, "CreateButton", "创建", new Vector2(-135, -100), new Vector2(220, 58), "button_primary", Cyan);
            Button(modal.transform, "CancelButton", "取消", new Vector2(135, -100), new Vector2(220, 58), "button_secondary", White);

            Set(shell, "mainMenuScreen", main);
            Set(shell, "modeSelectScreen", mode);
            Set(shell, "createLanScreen", create);
            Set(shell, "joinLanScreen", join);
            Set(shell, "connectingOverlay", connecting);
            Set(shell, "lobbyScreen", lobby);
            Set(shell, "roomScreen", room);
            Set(shell, "errorOverlay", error);
            Set(shell, "settingsOverlay", settings);
            Set(shell, "createRoomModal", modal);
            Set(shell, "hostNicknameInput", hostNickname);
            Set(shell, "hostRoomNameInput", hostRoom);
            Set(shell, "joinNicknameInput", joinNickname);
            Set(shell, "joinAddressInput", joinAddress);
            Set(shell, "createRoomNameInput", modalRoom);
            Set(shell, "connectingText", connectingText);
            Set(shell, "errorText", errorText);
            Set(shell, "roomListContent", roomContent);
            Set(shell, "roomListItemPrefab", roomItemPrefab);
            Set(shell, "selectedRoomNameText", selectedName);
            Set(shell, "selectedRoomOwnerText", selectedOwner);
            Set(shell, "selectedRoomStatusText", selectedStatus);
            Set(shell, "joinSelectedRoomButton", joinSelected);
            Set(shell, "currentRoomNameText", currentRoomName);
            Set(shell, "inviteAddressText", inviteAddress);
            Set(shell, "inviteAddressGroup", invite);
            Set(shell, "copyAddressButton", room.transform.Find("CopyAddressButton").gameObject);
            Set(shell, "leftPlayerCard", leftCard);
            Set(shell, "rightPlayerCard", rightCard);
            Set(shell, "readyButton", ready);
            Set(shell, "readyButtonText", ready.GetComponentInChildren<TMP_Text>());
            Set(shell, "startButton", start);
            Set(shell, "startButtonText", start.GetComponentInChildren<TMP_Text>());

            mode.SetActive(false);
            create.SetActive(false);
            join.SetActive(false);
            connecting.SetActive(false);
            lobby.SetActive(false);
            room.SetActive(false);
            error.SetActive(false);
            settings.SetActive(false);
            modal.SetActive(false);

            string path = $"{UiRoot}/GameShellCanvas.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildLobbyScene(GameObject canvasPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LobbyScene";

            GameObject controller = new GameObject("LockstepArenaDemoController");
            controller.AddComponent<LockstepArenaDemoController>();
            PrefabUtility.InstantiatePrefab(canvasPrefab, scene);

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            Camera camera = new GameObject("ShellCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.05f, 0.09f);
            camera.transform.position = new Vector3(0, 5.5f, -13f);
            camera.transform.rotation = Quaternion.Euler(15f, 0, 0);
            camera.fieldOfView = 48;
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);

            Light light = new GameObject("ShellKeyLight", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.45f, 0.80f, 1f);
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(35, -30, 0);
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);

            GameObject backdrop = new GameObject("ShellBackdrop");
            AddModel(backdrop.transform, "Assets/Art/Environment/SciFiArena/Platform_Simple.fbx", new Vector3(0, -2.2f, 6), new Vector3(5, 1, 3));
            AddModel(backdrop.transform, "Assets/Art/Environment/SciFiArena/Door_Frame_Square.fbx", new Vector3(0, -0.8f, 8), new Vector3(2.2f, 2.2f, 2.2f));
            AddModel(backdrop.transform, "Assets/Art/Environment/SciFiArena/Column_Simple.fbx", new Vector3(-6, -1.2f, 6), new Vector3(1.5f, 2.5f, 1.5f));
            AddModel(backdrop.transform, "Assets/Art/Environment/SciFiArena/Column_Simple.fbx", new Vector3(6, -1.2f, 6), new Vector3(1.5f, 2.5f, 1.5f));
            AddModel(backdrop.transform, "Assets/Art/Props/Prop_Crate.fbx", new Vector3(-4.5f, -1.5f, 4), Vector3.one * 1.3f);
            SceneManager.MoveGameObjectToScene(backdrop, scene);

            EditorSceneManager.SaveScene(scene, LobbyScene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScene, true),
                new EditorBuildSettingsScene("Assets/LockstepArenaDemo/Scenes/BattleScene.unity", true),
            };
        }

        private static void AddModel(Transform parent, string path, Vector3 position, Vector3 scale)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException($"Missing decorative model: {path}");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = Path.GetFileNameWithoutExtension(path);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = scale;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            Material material = EnsureBackdropMaterial();
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterial = material;
        }

        private static Material EnsureBackdropMaterial()
        {
            const string folder = UiRoot + "/Materials";
            const string path = folder + "/ShellBackdrop.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            EnsureFolder(folder);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP Lit shader is unavailable.");
            material = new Material(shader) { color = new Color(0.08f, 0.28f, 0.34f) };
            material.SetColor("_BaseColor", new Color(0.08f, 0.28f, 0.34f));
            material.SetColor("_EmissionColor", new Color(0.01f, 0.10f, 0.14f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform ScrollContent(Transform parent)
        {
            GameObject scroll = UiObject("RoomScroll", parent);
            Stretch(scroll.GetComponent<RectTransform>(), 28, 28, 28, 28);
            Image background = scroll.AddComponent<Image>();
            background.color = new Color(0.01f, 0.03f, 0.06f, 0.8f);
            ScrollRect scrollRect = scroll.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            GameObject viewport = UiObject("Viewport", scroll.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            GameObject content = UiObject("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            return content.transform;
        }

        private static GameObject Screen(Transform parent, string name)
        {
            GameObject screen = UiObject(name, parent);
            Stretch(screen.GetComponent<RectTransform>());
            return screen;
        }

        private static GameObject Overlay(Transform parent, string name, Vector2? size = null)
        {
            GameObject overlay = PanelObject(parent, name, Vector2.zero, size ?? new Vector2(900, 430), Panel);
            if (!size.HasValue)
            {
                Image blocker = overlay.GetComponent<Image>();
                blocker.color = new Color(0.02f, 0.05f, 0.09f, 0.98f);
            }
            return overlay;
        }

        private static void Heading(Transform parent, string title, string subtitle)
        {
            Text(parent, "Heading", title, 52, TextAlignmentOptions.Center, new Vector2(0, 400), new Vector2(1100, 76)).color = White;
            Text(parent, "Subheading", subtitle, 23, TextAlignmentOptions.Center, new Vector2(0, 340), new Vector2(900, 44)).color = Muted;
        }

        private static TMP_InputField Input(Transform parent, string name, string placeholderText, Vector2 position)
        {
            GameObject root = PanelObject(parent, name, position, new Vector2(560, 68), new Color(0.05f, 0.10f, 0.16f, 1));
            Image image = root.GetComponent<Image>();
            image.sprite = Sprite("field_dark");
            image.type = Image.Type.Sliced;
            TMP_InputField input = root.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            GameObject area = UiObject("Text Area", root.transform);
            Stretch(area.GetComponent<RectTransform>(), 20, 20, 8, 8);
            area.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = Text(area.transform, "Placeholder", placeholderText, 23,
                TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            placeholder.color = new Color(0.50f, 0.58f, 0.66f, 1);
            Stretch(placeholder.rectTransform);
            TextMeshProUGUI text = Text(area.transform, "Text", "", 23,
                TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.zero);
            Stretch(text.rectTransform);
            input.textViewport = area.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private static Button Button(Transform parent, string name, string label, Vector2 position, Vector2 size, string spriteName, Color textColor)
        {
            GameObject root = PanelObject(parent, name, position, size, Color.white);
            Image image = root.GetComponent<Image>();
            image.sprite = Sprite(spriteName);
            image.type = Image.Type.Sliced;
            Button button = root.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.80f, 0.96f, 1f);
            colors.pressedColor = new Color(0.55f, 0.76f, 0.85f);
            colors.disabledColor = new Color(0.28f, 0.32f, 0.36f, 0.75f);
            colors.colorMultiplier = 1;
            button.colors = colors;
            TextMeshProUGUI text = Text(root.transform, "Label", label, size.y >= 150 ? 29 : 25,
                TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(24, 12));
            text.color = textColor;
            return button;
        }

        private static void Toggle(Transform parent, string name, string label, Vector2 position)
        {
            GameObject root = UiObject(name, parent);
            SetRect(root.GetComponent<RectTransform>(), position, new Vector2(320, 60));
            Toggle toggle = root.AddComponent<Toggle>();
            GameObject background = PanelObject(root.transform, "Background", new Vector2(-120, 0), new Vector2(46, 46), new Color(0.08f, 0.15f, 0.22f));
            GameObject check = PanelObject(background.transform, "Checkmark", Vector2.zero, new Vector2(30, 30), Cyan);
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            Text(root.transform, "Label", label, 25, TextAlignmentOptions.MidlineLeft, new Vector2(35, 0), new Vector2(220, 50));
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string value, float size,
            TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions)
        {
            GameObject root = UiObject(name, parent);
            SetRect(root.GetComponent<RectTransform>(), position, dimensions);
            TextMeshProUGUI text = root.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = White;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject PanelObject(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject root = UiObject(name, parent);
            SetRect(root.GetComponent<RectTransform>(), position, size);
            root.AddComponent<Image>().color = color;
            return root;
        }

        private static GameObject UiObject(string name, Transform? parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            if (parent != null) root.transform.SetParent(parent, false);
            return root;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left = 0, float right = 0, float bottom = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Sprite Sprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/SciFi/{name}.png")
                ?? throw new InvalidOperationException($"Missing UI sprite: {name}");
        }

        private static void Set(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException($"Missing serialized property {propertyName} on {target.GetType().Name}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/LockstepArenaDemo/Prefabs");
            EnsureFolder(UiRoot);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
