#nullable enable

using System;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class BattleScenePresentation : MonoBehaviour
    {
        public GameObject? ArenaRoot;
        public Camera? GameplayCamera;
        public AudioSource? AmbientSource;
        public AudioSource? UiSource;

        public void RequireConfigured()
        {
            if (ArenaRoot is null || GameplayCamera is null || AmbientSource is null || UiSource is null)
                throw new InvalidOperationException("BattleScene presentation references are incomplete. Rebuild the v3-B presentation scene.");
        }
    }
}
