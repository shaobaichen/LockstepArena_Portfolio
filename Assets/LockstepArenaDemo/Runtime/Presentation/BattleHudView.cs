#nullable enable

using System;
using LockstepArena.Client.Demo;
using LockstepArena.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LockstepArena.Demo
{
    public sealed class BattleHudView : MonoBehaviour
    {
        private TMP_Text? _p1Hp;
        private TMP_Text? _p2Hp;
        private TMP_Text? _p1Wins;
        private TMP_Text? _p2Wins;
        private TMP_Text? _round;
        private TMP_Text? _timer;
        private TMP_Text? _announcement;
        private RectTransform? _p1Bar;
        private RectTransform? _p2Bar;
        private GameObject? _settlement;
        private TMP_Text? _settlementText;
        private Button? _returnButton;
        private Action? _returnAction;
        private Button? _watchButton;
        private Action? _watchAction;
        private GameObject? _replay;
        private TMP_Text? _replayText;
        private Button? _backButton;
        private Action? _backAction;
        private bool _built;

        public BattleAnnouncementKind VisibleAnnouncement { get; private set; }

        public void Configure(BattlePresentationCatalog catalog)
        {
            if (_built) return;
            _built = true;
            Build(catalog);
        }

        public void Present(
            BattleState state,
            DemoClientPhase clientPhase,
            DemoClientSnapshot snapshot,
            Action returnToLobby,
            bool canWatchReplay = false,
            Action? watchReplay = null)
        {
            ApplyBattleState(state);
            _replay!.SetActive(false);
            bool settled = clientPhase == DemoClientPhase.Settlement;
            _settlement!.SetActive(settled);
            if (settled)
            {
                string localResult = snapshot.WinnerPlayerId == 0 ? "DRAW" :
                    snapshot.WinnerPlayerId == snapshot.SessionId ? "YOU WIN" : "YOU LOSE";
                _settlementText!.text = $"{localResult}\nFINAL {snapshot.Slot0RoundWins} - {snapshot.Slot1RoundWins}";
                _returnAction = returnToLobby;
                _watchAction = watchReplay;
                _watchButton!.interactable = snapshot.SettlementVerified && canWatchReplay;
            }
        }

        public void PresentReplay(BattleReplayPlayer player, Action backToResult)
        {
            ApplyBattleState(player.CurrentState);
            _settlement!.SetActive(false);
            _replay!.SetActive(true);
            string status = player.IsComplete ? "REPLAY COMPLETE" : player.IsPaused ? "REPLAY PAUSED" : "REPLAY";
            _replayText!.text = $"{status}   {player.CurrentFrame} / {player.TotalFrames}\nSPACE PAUSE / RESUME   |   ESC BACK TO RESULT";
            _backButton!.gameObject.SetActive(player.IsComplete);
            _backAction = backToResult;
        }

        public void PresentPreview(BattleState state)
        {
            ApplyBattleState(state);
            _settlement!.SetActive(false);
            _replay!.SetActive(false);
        }

        private void ApplyBattleState(BattleState state)
        {
            BattlePresentationSnapshot model = BattlePresentationReadModel.Create(state);
            _p1Hp!.text = $"P1   {model.Player1HitPoints} / {state.Definition!.Gameplay.MaxHitPoints}";
            _p2Hp!.text = $"P2   {model.Player2HitPoints} / {state.Definition.Gameplay.MaxHitPoints}";
            float firstHealth = model.Player1HitPoints / (float)state.Definition.Gameplay.MaxHitPoints;
            float secondHealth = model.Player2HitPoints / (float)state.Definition.Gameplay.MaxHitPoints;
            _p1Bar!.anchorMax = new Vector2(firstHealth, 1f);
            _p2Bar!.anchorMin = new Vector2(1f - secondHealth, 0f);
            _p1Wins!.text = Marks(model.Player1RoundWins);
            _p2Wins!.text = Marks(model.Player2RoundWins);
            _round!.text = $"ROUND {model.RoundNumber}";
            _timer!.text = $"{model.SecondsRemaining / 60:00}:{model.SecondsRemaining % 60:00}";

            VisibleAnnouncement = model.Announcement;
            _announcement!.gameObject.SetActive(model.Announcement != BattleAnnouncementKind.None);
            _announcement.text = model.AnnouncementText;
            _announcement.color = model.Announcement == BattleAnnouncementKind.MatchVictory
                ? new Color(1f, 0.76f, 0.2f)
                : model.WinnerSlot == 0 ? BattlePresentationReadModel.GetTeamColor(new PlayerSlot(0))
                : model.WinnerSlot == 1 ? BattlePresentationReadModel.GetTeamColor(new PlayerSlot(1))
                : Color.white;

        }

        private void Build(BattlePresentationCatalog catalog)
        {
            if (FindFirstObjectByType<EventSystem>() is null)
            {
                var events = new GameObject("Battle EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }

            var canvasObject = new GameObject("Battle HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            _p1Bar = CreatePlayerPanel(root, catalog, true, out _p1Hp, out _p1Wins);
            _p2Bar = CreatePlayerPanel(root, catalog, false, out _p2Hp, out _p2Wins);

            _round = CreateText(root, "Round", "ROUND 1", 24, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(240, 36), catalog.Font);
            _timer = CreateText(root, "Timer", "01:00", 34, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -66), new Vector2(240, 44), catalog.Font);
            _announcement = CreateText(root, "Announcement", string.Empty, 52, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(540, 190), catalog.Font);
            _announcement.fontStyle = FontStyles.Bold;

            _settlement = CreatePanel(root, "Settlement", catalog.PanelSprite, new Color(0.025f, 0.055f, 0.09f, 0.94f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 300));
            _settlementText = CreateText(_settlement.GetComponent<RectTransform>(), "Result", "YOU WIN", 40,
                TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -58),
                new Vector2(400, 100), catalog.Font);
            _settlementText.color = new Color(1f, 0.76f, 0.2f);
            _returnButton = CreateButton(_settlement.GetComponent<RectTransform>(), catalog, "RETURN TO LOBBY",
                new Vector2(0, 42));
            _returnButton.onClick.AddListener(() => _returnAction?.Invoke());
            _watchButton = CreateButton(_settlement.GetComponent<RectTransform>(), catalog, "WATCH REPLAY",
                new Vector2(0, 100));
            _watchButton.name = "Watch Replay Button";
            _watchButton.interactable = false;
            _watchButton.onClick.AddListener(() => _watchAction?.Invoke());
            _settlement.SetActive(false);

            _replay = CreatePanel(root, "Replay Status", catalog.PanelSprite, new Color(0.025f, 0.055f, 0.09f, 0.94f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 76), new Vector2(620, 132));
            _replayText = CreateText(_replay.GetComponent<RectTransform>(), "Replay Progress", "REPLAY", 18,
                TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -34),
                new Vector2(600, 60), catalog.Font);
            _backButton = CreateButton(_replay.GetComponent<RectTransform>(), catalog, "BACK TO RESULT",
                new Vector2(0, 30));
            _backButton.name = "Back To Result Button";
            _backButton.onClick.AddListener(() => _backAction?.Invoke());
            _replay.SetActive(false);
        }

        private static RectTransform CreatePlayerPanel(
            RectTransform root,
            BattlePresentationCatalog catalog,
            bool left,
            out TMP_Text hp,
            out TMP_Text wins)
        {
            Vector2 anchor = new Vector2(left ? 0f : 1f, 1f);
            var panel = CreatePanel(root, left ? "P1 Panel" : "P2 Panel", catalog.PanelSprite,
                new Color(0.025f, 0.055f, 0.09f, 0.9f), anchor, anchor,
                new Vector2(left ? 210 : -210, -72), new Vector2(380, 104));
            hp = CreateText(panel.GetComponent<RectTransform>(), "HP", left ? "P1 100 / 100" : "P2 100 / 100", 24,
                left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(330, 34), catalog.Font);
            wins = CreateText(panel.GetComponent<RectTransform>(), "Wins", "WINS 0 / 2", 18,
                left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(330, 30), catalog.Font);

            GameObject barBackground = new GameObject("HP Bar Background", typeof(RectTransform), typeof(Image));
            barBackground.transform.SetParent(panel.transform, false);
            RectTransform barRect = barBackground.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.sizeDelta = new Vector2(330, 14);
            barRect.anchoredPosition = new Vector2(0, 0);
            barBackground.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 1f);
            GameObject fill = new GameObject("HP Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barBackground.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image image = fill.GetComponent<Image>();
            image.color = left ? BattlePresentationReadModel.GetTeamColor(new PlayerSlot(0)) :
                BattlePresentationReadModel.GetTeamColor(new PlayerSlot(1));
            return fillRect;
        }

        private static GameObject CreatePanel(RectTransform parent, string name, Sprite? sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            Image image = panel.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite is null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = color;
            return panel;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string value, float size,
            TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position,
            Vector2 dimensions, TMP_FontAsset? font)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            if (font is not null) text.font = font;
            return text;
        }

        private static Button CreateButton(RectTransform parent, BattlePresentationCatalog catalog, string label, Vector2 position)
        {
            var buttonObject = new GameObject("Return Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280, 48);
            rect.anchoredPosition = position;
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = catalog.ButtonSprite;
            image.type = catalog.ButtonSprite is null ? Image.Type.Simple : Image.Type.Sliced;
            image.color = new Color(0.05f, 0.65f, 0.9f, 1f);
            CreateText(rect, "Label", label, 20, TextAlignmentOptions.Center, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, catalog.Font).rectTransform.offsetMin = Vector2.zero;
            TMP_Text text = buttonObject.GetComponentInChildren<TMP_Text>();
            text.rectTransform.offsetMax = Vector2.zero;
            return buttonObject.GetComponent<Button>();
        }

        private static string Marks(int wins) => $"WINS {Mathf.Clamp(wins, 0, 2)} / 2";
    }
}
