#nullable enable

using System;
using System.Collections.Generic;
using LockstepArena.Simulation;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class BattlePresenter : MonoBehaviour
    {
        public const float WorldUnitsPerSimulationUnit = 1000f;
        private const float SimulationToWorld = 1f / WorldUnitsPerSimulationUnit;
        private readonly Dictionary<int, PlayerPresentation> _players = new Dictionary<int, PlayerPresentation>();
        private readonly Dictionary<ulong, GameObject> _projectiles = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<ulong, Vector3> _lastProjectilePositions = new Dictionary<ulong, Vector3>();
        private readonly List<ulong> _staleProjectileIds = new List<ulong>();
        private readonly BattlePresentationTracker _tracker = new BattlePresentationTracker();
        private BattleDefinition? _definition;
        private BattlePresentationCatalog? _catalog;
        private AudioSource? _ambientSource;
        private AudioSource? _uiSource;
        private BattleAnnouncementKind _lastAnnouncement;
        private GameObject? _transients;

        public Camera? GameplayCamera { get; private set; }

        public void Configure(BattleDefinition definition)
        {
            if (_definition is not null) return;
            if (definition is null) throw new ArgumentNullException(nameof(definition));
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
            BattleScenePresentation scene = FindFirstObjectByType<BattleScenePresentation>() ??
                throw new InvalidOperationException(
                    "The authored BattleScene presentation is missing. Run Tools/Lockstep Arena/Build v3-B Battle Presentation.");
            scene.RequireConfigured();
            GameplayCamera = scene.GameplayCamera;
            _ambientSource = scene.AmbientSource;
            _uiSource = scene.UiSource;
            _catalog = catalog;
            _definition = definition;
        }

        public void Present(BattleState state, PlayerSlot? localSlot, ushort? localVisualAim = null)
        {
            if (!state.IsGameplayEnabled) return;
            Configure(state.Definition!);
            BattlePresentationEvents events = _tracker.Observe(state);
            for (int index = 0; index < state.PlayerCount; index++)
            {
                if (!_players.TryGetValue(index, out PlayerPresentation player))
                {
                    var playerObject = new GameObject($"P{index + 1} Presentation");
                    playerObject.transform.SetParent(transform, false);
                    player = playerObject.AddComponent<PlayerPresentation>();
                    player.Configure(new PlayerSlot(index), _catalog!);
                    _players.Add(index, player);
                }
                ushort? renderAim = localSlot.HasValue && localSlot.Value.Value == index ? localVisualAim : null;
                player.Present(state.GetPlayerState(new PlayerSlot(index)), renderAim);
            }

            foreach (ulong id in events.SpawnedProjectileIds)
            {
                ProjectileState projectile = FindProjectile(state, id);
                if (state.Roster.TryGetSlot(projectile.OwnerPlayerId, out PlayerSlot owner) &&
                    _players.TryGetValue(owner.Value, out PlayerPresentation player))
                {
                    player.TriggerShoot();
                    SpawnBurst(player.Muzzle.position, BattlePresentationReadModel.GetTeamColor(owner), 7, 0.75f, _catalog!.MuzzleMaterial);
                    PlayWorld(_catalog!.ShootSound, player.Muzzle.position, 0.42f);
                }
            }

            foreach (int slot in events.DamagedSlots)
            {
                if (!_players.TryGetValue(slot, out PlayerPresentation player)) continue;
                player.TriggerHitFlash();
                SpawnBurst(player.transform.position + Vector3.up * 0.65f,
                    BattlePresentationReadModel.GetTeamColor(new PlayerSlot(slot)), 12, 1.05f);
                PlayWorld(_catalog!.PlayerHitSound, player.transform.position, 0.55f);
                if (state.GetPlayerState(new PlayerSlot(slot)).HitPoints == 0)
                    PlayWorld(_catalog.DeathSound, player.transform.position, 0.55f);
            }

            foreach (ulong id in events.RemovedProjectileIds)
            {
                if (!_lastProjectilePositions.TryGetValue(id, out Vector3 position)) continue;
                // BattleState has no per-projectile removal attribution. HP-based hit feedback is handled above.
                SpawnBurst(position, new Color(1f, 0.72f, 0.25f), 7, 0.8f);
                PlayWorld(_catalog!.EnvironmentHitSound, position, 0.35f);
            }

            SynchronizeProjectiles(state);
            PresentAnnouncementSound(state);
        }

        public void Clear()
        {
            if (_transients != null)
            {
                _transients.SetActive(false);
                DestroyView(_transients);
                _transients = null;
            }
            _uiSource?.Stop();
            _tracker.Reset();
            _lastAnnouncement = BattleAnnouncementKind.None;
            foreach (PlayerPresentation player in _players.Values) if (player is not null) DestroyView(player.gameObject);
            foreach (GameObject projectile in _projectiles.Values) if (projectile is not null) DestroyView(projectile);
            _players.Clear();
            _projectiles.Clear();
            _lastProjectilePositions.Clear();
            _staleProjectileIds.Clear();
            GameplayCamera = null;
            _ambientSource = null;
            _uiSource = null;
            _definition = null;
            _catalog = null;
        }

        public static Vector3 ToWorld(int x, float y, int z) => new Vector3(x * SimulationToWorld, y, z * SimulationToWorld);

        private void SynchronizeProjectiles(BattleState state)
        {
            _staleProjectileIds.Clear();
            foreach (ulong id in _projectiles.Keys) _staleProjectileIds.Add(id);
            for (int index = 0; index < state.ProjectileCount; index++)
            {
                ProjectileState value = state.GetProjectile(index);
                if (!_projectiles.TryGetValue(value.ProjectileId, out GameObject projectile))
                {
                    projectile = CreateProjectile(value.ProjectileId);
                    _projectiles.Add(value.ProjectileId, projectile);
                }
                Vector3 position = ToWorld(value.PositionX, 0.42f, value.PositionZ);
                projectile.transform.position = position;
                projectile.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(value.DirectionX, 0f, value.DirectionZ));
                _lastProjectilePositions[value.ProjectileId] = position;
                _staleProjectileIds.Remove(value.ProjectileId);
            }
            foreach (ulong id in _staleProjectileIds)
            {
                DestroyView(_projectiles[id]);
                _projectiles.Remove(id);
            }
        }

        private GameObject CreateProjectile(ulong id)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            projectile.name = "Energy Bolt " + id;
            projectile.transform.SetParent(transform, false);
            projectile.transform.localScale = new Vector3(0.055f, 0.12f, 0.055f);
            DestroyView(projectile.GetComponent<Collider>());
            projectile.GetComponent<Renderer>().sharedMaterial = _catalog!.ProjectileMaterial;
            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.13f;
            trail.startWidth = 0.075f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.025f;
            trail.sharedMaterial = _catalog.ProjectileMaterial;
            trail.startColor = Color.white;
            trail.endColor = new Color(0.08f, 0.72f, 1f, 0f);
            return projectile;
        }

        public static void ApplyGameplayCamera(Camera camera)
        {
            if (camera is null) throw new ArgumentNullException(nameof(camera));
            camera.transform.position = new Vector3(0f, 9.2f, -8.2f);
            camera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            camera.fieldOfView = 44f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.055f, 0.085f);
        }

        private void PresentAnnouncementSound(BattleState state)
        {
            BattlePresentationSnapshot model = BattlePresentationReadModel.Create(state);
            if (model.Announcement == BattleAnnouncementKind.None || model.Announcement == _lastAnnouncement) return;
            AudioClip? clip = model.Announcement switch
            {
                BattleAnnouncementKind.Countdown1 or BattleAnnouncementKind.Countdown2 or BattleAnnouncementKind.Countdown3 => _catalog!.CountdownSound,
                BattleAnnouncementKind.Fight => _catalog!.FightSound,
                BattleAnnouncementKind.RoundWin or BattleAnnouncementKind.Draw or BattleAnnouncementKind.MatchVictory => _catalog!.VictorySound,
                _ => null,
            };
            if (clip is not null) _uiSource?.PlayOneShot(clip, 0.45f);
            _lastAnnouncement = model.Announcement;
        }

        private void SpawnBurst(Vector3 position, Color color, short count, float speed, Material? material = null)
        {
            var effect = new GameObject("Presentation Burst");
            effect.transform.SetParent(TransientRoot(), false);
            effect.transform.position = position;
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.12f;
            main.startLifetime = 0.2f;
            main.startSpeed = speed;
            main.startSize = 0.13f;
            main.startColor = color;
            main.maxParticles = 24;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = material ?? _catalog!.ParticleMaterial;
            particles.Play();
        }

        private void PlayWorld(AudioClip? clip, Vector3 position, float volume)
        {
            if (clip is null) return;
            var audio = new GameObject("Transient Battle Audio");
            audio.transform.SetParent(TransientRoot(), false);
            audio.transform.position = position;
            AudioSource source = audio.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 0.8f;
            source.minDistance = 2f;
            source.maxDistance = 18f;
            source.pitch = 0.97f + UnityEngine.Random.value * 0.06f;
            source.Play();
            Destroy(audio, clip.length + 0.1f);
        }

        private Transform TransientRoot()
        {
            if (_transients == null)
            {
                _transients = new GameObject("Battle Transients");
                _transients.transform.SetParent(transform, false);
            }
            return _transients.transform;
        }

        private static ProjectileState FindProjectile(BattleState state, ulong id)
        {
            for (int index = 0; index < state.ProjectileCount; index++)
            {
                ProjectileState projectile = state.GetProjectile(index);
                if (projectile.ProjectileId == id) return projectile;
            }
            throw new InvalidOperationException("A spawned projectile was not present in the observed state.");
        }

        private static void DestroyView(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
