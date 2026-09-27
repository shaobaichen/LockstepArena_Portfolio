#nullable enable

using System;
using LockstepArena.Client.Demo;
using LockstepArena.Protocol.Wire;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LockstepArena.Demo
{
    public sealed class RoomListItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text roomNameText = null!;
        [SerializeField] private TMP_Text countText = null!;
        [SerializeField] private TMP_Text statusText = null!;
        [SerializeField] private Image background = null!;
        [SerializeField] private Button selectButton = null!;

        public void Configure(DemoRoomSummarySnapshot room, bool selected, Action<ulong> onSelected)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (onSelected == null) throw new ArgumentNullException(nameof(onSelected));

            TMP_FontAsset font = GameShellUiController.GetRuntimeFont();
            roomNameText.font = font;
            countText.font = font;
            statusText.font = font;
            roomNameText.text = room.RoomName;
            countText.text = $"{room.ParticipantCount}/{room.Capacity}";
            statusText.text = GetStatus(room);
            background.color = selected
                ? new Color(0.08f, 0.55f, 0.72f, 0.95f)
                : new Color(0.06f, 0.12f, 0.20f, 0.92f);
            selectButton.interactable = true;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected(room.RoomId));
        }

        private static string GetStatus(DemoRoomSummarySnapshot room)
        {
            if (room.Lifecycle != RoomLifecycleMessage.RoomLifecycleOpen) return "比赛中";
            return room.ParticipantCount >= room.Capacity ? "已满" : "等待中";
        }
    }
}
