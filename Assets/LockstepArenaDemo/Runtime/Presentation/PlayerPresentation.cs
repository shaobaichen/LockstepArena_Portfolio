#nullable enable

using System.Collections.Generic;
using LockstepArena.Simulation;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class PlayerPresentation : MonoBehaviour
    {
        private readonly List<Renderer> _robotRenderers = new List<Renderer>();
        private Animation? _animation;
        private Transform? _visualRoot;
        private Transform? _modelRoot;
        private Transform? _muzzle;
        private Vector3 _modelLocalPosition;
        private Quaternion _modelLocalRotation;
        private Vector3 _modelLocalScale;
        private Vector3 _lastPosition;
        private bool _hasPosition;
        private bool _dead;
        private float _flashUntil;

        public PlayerSlot Slot { get; private set; }
        public Transform Muzzle => _muzzle ?? transform;

        public void Configure(PlayerSlot slot, BattlePresentationCatalog catalog)
        {
            Slot = slot;
            gameObject.name = $"P{slot.Value + 1} Robot View";

            var visual = new GameObject("Robot Visual");
            visual.transform.SetParent(transform, false);
            _visualRoot = visual.transform;
            if (catalog.RobotPrefab is null)
                throw new System.InvalidOperationException("The battle robot view prefab is missing.");
            GameObject view = Instantiate(catalog.RobotPrefab, visual.transform, false);
            view.name = "Styloo Robot View";
            RemovePhysics(view);
            _modelRoot = FindNamed(view.transform, "Styloo Robot Model") ??
                throw new System.InvalidOperationException("The robot view prefab needs a Styloo Robot Model child.");
            _muzzle = FindNamed(view.transform, "Visual Muzzle") ??
                throw new System.InvalidOperationException("The robot view prefab needs a Visual Muzzle anchor.");
            Transform ring = FindNamed(view.transform, "Team Ground Ring") ??
                throw new System.InvalidOperationException("The robot view prefab needs a Team Ground Ring child.");
            ring.GetComponent<Renderer>().sharedMaterial = slot.Value == 0 ? catalog.CyanMaterial : catalog.RedMaterial;
            _animation = _modelRoot.GetComponentInChildren<Animation>();
            if (_animation is null) _animation = _modelRoot.gameObject.AddComponent<Animation>();
            AddClip(catalog.IdleClip, "Idle");
            AddClip(catalog.RunClip, "Run");
            AddClip(catalog.ShootClip, "Shoot");
            if (_animation.GetClip("Idle") is not null)
            {
                _animation.clip = _animation.GetClip("Idle");
                _animation.Play("Idle");
            }
            _modelLocalPosition = _modelRoot.localPosition;
            _modelLocalRotation = _modelRoot.localRotation;
            _modelLocalScale = _modelRoot.localScale;
            foreach (SkinnedMeshRenderer skin in _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.updateWhenOffscreen = true;
                _robotRenderers.Add(skin);
            }
        }

        public void Present(PlayerState state, ushort? renderAim = null)
        {
            Vector3 position = BattlePresenter.ToWorld(state.PositionX, 0f, state.PositionZ);
            bool moving = _hasPosition && (position - _lastPosition).sqrMagnitude > 0.000001f;
            transform.position = position;
            BattleSimulation.GetAimDirection(renderAim ?? state.Aim, out int directionX, out int directionZ);
            transform.rotation = Quaternion.LookRotation(new Vector3(directionX, 0f, directionZ));

            bool isDead = state.HitPoints <= 0;
            if (_visualRoot is not null)
            {
                _visualRoot.localRotation = isDead ? Quaternion.Euler(0f, 0f, Slot.Value == 0 ? 72f : -72f) : Quaternion.identity;
                _visualRoot.localPosition = isDead ? new Vector3(0f, 0.22f, 0f) : Vector3.zero;
            }

            if (isDead)
            {
                if (!_dead) _animation?.Stop();
            }
            else if (_animation is not null && !_animation.IsPlaying("Shoot"))
            {
                string clip = moving ? "Run" : "Idle";
                if (_animation.GetClip(clip) is not null && !_animation.IsPlaying(clip)) _animation.CrossFade(clip, 0.08f);
            }

            _dead = isDead;
            _lastPosition = position;
            _hasPosition = true;
        }

        public void TriggerShoot()
        {
            if (_dead || _animation is null || _animation.GetClip("Shoot") is null) return;
            _animation.CrossFade("Shoot", 0.03f);
        }

        public void TriggerHitFlash()
        {
            _flashUntil = Time.unscaledTime + 0.1f;
        }

        private void Update()
        {
            if (_robotRenderers.Count == 0) return;
            bool flash = Time.unscaledTime < _flashUntil;
            var properties = new MaterialPropertyBlock();
            if (flash) properties.SetColor("_BaseColor", Color.white);
            foreach (Renderer renderer in _robotRenderers) renderer.SetPropertyBlock(properties);
        }

        private void LateUpdate()
        {
            if (_modelRoot is null) return;
            _modelRoot.localPosition = _modelLocalPosition;
            _modelRoot.localRotation = _modelLocalRotation;
            _modelRoot.localScale = _modelLocalScale;
        }

        private void AddClip(AnimationClip? clip, string name)
        {
            if (_animation is null || clip is null) return;
            _animation.AddClip(clip, name);
            if (name == "Idle" || name == "Run") clip.wrapMode = WrapMode.Loop;
        }

        private static void RemovePhysics(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) DestroyView(collider);
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true)) DestroyView(body);
        }

        private static Transform? FindNamed(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private static void DestroyView(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
