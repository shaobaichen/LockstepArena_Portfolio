#nullable enable

using LockstepArena.Client.Demo;
using TMPro;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class PlayerCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nicknameText = null!;
        [SerializeField] private TMP_Text readyText = null!;
        [SerializeField] private GameObject hostBadge = null!;
        [SerializeField] private GameObject emptyState = null!;

        public void Show(DemoRoomParticipantSnapshot? participant)
        {
            bool occupied = participant != null;
            nicknameText.gameObject.SetActive(occupied);
            readyText.gameObject.SetActive(occupied);
            hostBadge.SetActive(occupied && participant!.IsHost);
            emptyState.SetActive(!occupied);
            if (!occupied) return;

            nicknameText.text = participant!.Nickname;
            readyText.text = participant.IsReady ? "已准备" : "等待中";
            readyText.color = participant.IsReady
                ? new Color(0.25f, 0.95f, 0.82f)
                : new Color(0.66f, 0.72f, 0.80f);
        }
    }
}
