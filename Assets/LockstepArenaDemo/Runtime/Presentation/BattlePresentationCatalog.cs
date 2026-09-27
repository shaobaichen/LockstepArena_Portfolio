#nullable enable

using TMPro;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class BattlePresentationCatalog : ScriptableObject
    {
        public GameObject? RobotPrefab;
        public GameObject? WeaponPrefab;
        public GameObject? PlatformPrefab;
        public GameObject? DoorFramePrefab;
        public GameObject? ColumnPrefab;
        public GameObject? ComputerPrefab;
        public GameObject? CratePrefab;
        public AnimationClip? IdleClip;
        public AnimationClip? RunClip;
        public AnimationClip? ShootClip;
        public Material? RobotMaterial;
        public Material? WeaponMaterial;
        public Material? FloorMaterial;
        public Material? WallMaterial;
        public Material? CoverMaterial;
        public Material? CyanMaterial;
        public Material? RedMaterial;
        public Material? ProjectileMaterial;
        public Material? MuzzleMaterial;
        public Material? ParticleMaterial;
        public Sprite? PanelSprite;
        public Sprite? ButtonSprite;
        public TMP_FontAsset? Font;
        public AudioClip? ShootSound;
        public AudioClip? PlayerHitSound;
        public AudioClip? EnvironmentHitSound;
        public AudioClip? DeathSound;
        public AudioClip? CountdownSound;
        public AudioClip? FightSound;
        public AudioClip? VictorySound;
        public AudioClip? ClickSound;
        public AudioClip? AmbientSound;

        public static BattlePresentationCatalog LoadRequired() =>
            Resources.Load<BattlePresentationCatalog>("BattlePresentation/V3BBattlePresentationCatalog") ??
            throw new System.InvalidOperationException(
                "The v3-B battle presentation catalog is missing. Run Tools/Lockstep Arena/Build v3-B Battle Presentation.");
    }
}
