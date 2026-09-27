#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using LockstepArena.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class V3BBattlePresentationTests
    {
        [Test]
        public void TeamColorsAreCanonicalAndIndependentOfLocalSlot()
        {
            Assert.That(BattlePresentationReadModel.GetTeamColor(new PlayerSlot(0)),
                Is.EqualTo(new Color(0.08f, 0.72f, 1f, 1f)));
            Assert.That(BattlePresentationReadModel.GetTeamColor(new PlayerSlot(1)),
                Is.EqualTo(new Color(1f, 0.18f, 0.12f, 1f)));
        }

        [Test]
        public void HudReadModelUsesDeterministicHpRoundTimerAndWins()
        {
            BattleState state = CreateState(
                42, BattlePhase.Playing, 0, 1781, RoundResult.None, null,
                new PlayerState(-3000, 0, 0, 75, 1, 0),
                new PlayerState(3000, 0, 32768, 50, 0, 0));

            BattlePresentationSnapshot snapshot = BattlePresentationReadModel.Create(state);

            Assert.That(snapshot.Player1HitPoints, Is.EqualTo(75));
            Assert.That(snapshot.Player2HitPoints, Is.EqualTo(50));
            Assert.That(snapshot.Player1RoundWins, Is.EqualTo(1));
            Assert.That(snapshot.Player2RoundWins, Is.Zero);
            Assert.That(snapshot.RoundNumber, Is.EqualTo(2));
            Assert.That(snapshot.SecondsRemaining, Is.EqualTo(60));
            Assert.That(snapshot.Announcement, Is.EqualTo(BattleAnnouncementKind.Fight));
        }

        [Test]
        public void HudHealthBarsShrinkFromCanonicalHitPoints()
        {
            var root = new GameObject("HUD Health Test");
            try
            {
                BattleHudView hud = root.AddComponent<BattleHudView>();
                hud.Configure(BattlePresentationCatalog.LoadRequired());
                hud.PresentPreview(CreateState(
                    42, BattlePhase.Playing, 0, 1781, RoundResult.None, null,
                    new PlayerState(-3000, 0, 0, 75, 1, 0),
                    new PlayerState(3000, 0, 32768, 50, 0, 0)));

                RectTransform first = root.transform.Find(
                    "Battle HUD Canvas/P1 Panel/HP Bar Background/HP Fill").GetComponent<RectTransform>();
                RectTransform second = root.transform.Find(
                    "Battle HUD Canvas/P2 Panel/HP Bar Background/HP Fill").GetComponent<RectTransform>();

                Assert.That(first.anchorMax.x, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(second.anchorMin.x, Is.EqualTo(0.5f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(90U, BattleAnnouncementKind.Countdown3)]
        [TestCase(60U, BattleAnnouncementKind.Countdown2)]
        [TestCase(30U, BattleAnnouncementKind.Countdown1)]
        public void CountdownMapsToExactlyOneAnnouncement(uint ticks, BattleAnnouncementKind expected)
        {
            BattleState state = CreateState(1, BattlePhase.RoundCountdown, ticks, 1800,
                RoundResult.None, null, DefaultPlayer(0), DefaultPlayer(1));

            BattlePresentationSnapshot snapshot = BattlePresentationReadModel.Create(state);

            Assert.That(snapshot.Announcement, Is.EqualTo(expected));
            Assert.That(snapshot.AnnouncementText, Is.Not.Empty);
        }

        [Test]
        public void FinalVictorySupersedesRoundWin()
        {
            BattleState state = CreateState(99, BattlePhase.MatchEnded, 0, 1200,
                RoundResult.PlayerWin(new PlayerSlot(0)), new PlayerSlot(0),
                new PlayerState(-3000, 0, 0, 0, 2, 0), DefaultPlayer(1));

            BattlePresentationSnapshot snapshot = BattlePresentationReadModel.Create(state);

            Assert.That(snapshot.Announcement, Is.EqualTo(BattleAnnouncementKind.MatchVictory));
            Assert.That(snapshot.WinnerSlot, Is.EqualTo(0));
            Assert.That(snapshot.AnnouncementText, Is.EqualTo("MATCH\nVICTORY\nP1"));
        }

        [Test]
        public void DrawAndRoundWinAreMutuallyExclusive()
        {
            BattlePresentationSnapshot draw = BattlePresentationReadModel.Create(CreateState(
                80, BattlePhase.RoundEnded, 20, 0, RoundResult.Draw, null,
                DefaultPlayer(0), DefaultPlayer(1)));
            BattlePresentationSnapshot win = BattlePresentationReadModel.Create(CreateState(
                81, BattlePhase.RoundEnded, 20, 0, RoundResult.PlayerWin(new PlayerSlot(1)), null,
                DefaultPlayer(0), DefaultPlayer(1)));

            Assert.That(draw.Announcement, Is.EqualTo(BattleAnnouncementKind.Draw));
            Assert.That(draw.WinnerSlot, Is.EqualTo(-1));
            Assert.That(win.Announcement, Is.EqualTo(BattleAnnouncementKind.RoundWin));
            Assert.That(win.WinnerSlot, Is.EqualTo(1));
        }

        [Test]
        public void ResetPresentationDataReturnsPlayersToAliveCountdownState()
        {
            BattleState initial = BattleState.CreateGameplayInitial(CreateRoster(), BattleDefinition.CreateDefault());

            BattlePresentationSnapshot snapshot = BattlePresentationReadModel.Create(initial);

            Assert.That(snapshot.Player1HitPoints, Is.EqualTo(100));
            Assert.That(snapshot.Player2HitPoints, Is.EqualTo(100));
            Assert.That(snapshot.RoundNumber, Is.EqualTo(1));
            Assert.That(snapshot.Announcement, Is.EqualTo(BattleAnnouncementKind.Countdown3));
        }

        [Test]
        public void ProjectileLifecycleAndDamageAreObservedOnceAcrossRepeatedState()
        {
            var tracker = new BattlePresentationTracker();
            ProjectileState projectile = new ProjectileState(7, new PlayerId(11), -1000, 0, 1000, 0, 20);
            BattleState fired = CreateState(10, BattlePhase.Playing, 0, 1700, RoundResult.None, null,
                DefaultPlayer(0), DefaultPlayer(1), projectile);
            BattleState hit = CreateState(11, BattlePhase.Playing, 0, 1699, RoundResult.None, null,
                DefaultPlayer(0), new PlayerState(3000, 0, 32768, 75, 0, 0));

            BattlePresentationEvents first = tracker.Observe(fired);
            BattlePresentationEvents repeated = tracker.Observe(fired);
            BattlePresentationEvents impact = tracker.Observe(hit);
            BattlePresentationEvents rollbackRepeated = tracker.Observe(fired);
            BattlePresentationEvents replayedImpact = tracker.Observe(hit);

            Assert.That(first.SpawnedProjectileIds, Is.EqualTo(new ulong[] { 7 }));
            Assert.That(repeated.HasAny, Is.False);
            Assert.That(impact.RemovedProjectileIds, Is.EqualTo(new ulong[] { 7 }));
            Assert.That(impact.DamagedSlots, Is.EqualTo(new int[] { 1 }));
            Assert.That(rollbackRepeated.HasAny, Is.False);
            Assert.That(replayedImpact.HasAny, Is.False);
        }

        [Test]
        public void SimultaneousHitAndExpiryDoNotTurnBothRemovalsIntoPlayerImpacts()
        {
            var root = new GameObject("Mixed Projectile Removal Test");
            var sceneRoot = new GameObject("Mixed Removal Scene Bindings");
            BattlePresentationCatalog catalog = UnityEngine.Object.Instantiate(BattlePresentationCatalog.LoadRequired());
            var originalParticles = new HashSet<ParticleSystem>(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None));
            try
            {
                BattleScenePresentation scene = sceneRoot.AddComponent<BattleScenePresentation>();
                scene.ArenaRoot = sceneRoot;
                scene.GameplayCamera = sceneRoot.AddComponent<Camera>();
                scene.AmbientSource = sceneRoot.AddComponent<AudioSource>();
                scene.UiSource = sceneRoot.AddComponent<AudioSource>();
                BattlePresenter presenter = root.AddComponent<BattlePresenter>();
                presenter.Configure(BattleDefinition.CreateDefault());
                // Silence only audio on a disposable catalog copy; test the real visual feedback.
                catalog.ShootSound = catalog.PlayerHitSound = catalog.EnvironmentHitSound = catalog.DeathSound = null;
                typeof(BattlePresenter).GetField("_catalog", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!.SetValue(presenter, catalog);

                BattleState before = CreateState(10, BattlePhase.Playing, 0, 1700, RoundResult.None, null,
                    new PlayerState(-3500, 0, 0, 100, 0, 0), new PlayerState(3500, 0, 32768, 100, 0, 0),
                    new ProjectileState(7, new PlayerId(11), 3200, 0, 1000, 0, 20),
                    new ProjectileState(8, new PlayerId(11), -3500, 1000, 1000, 0, 1));
                presenter.Present(before, new PlayerSlot(0));
                var previousParticles = new HashSet<ParticleSystem>(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None));

                var simulation = new BattleSimulation(before);
                simulation.Step(FrameData.Create(before.Roster, before.Tick, new[]
                {
                    new InputFrame(before.Tick, new PlayerSlot(0), 0, 0, 0),
                    new InputFrame(before.Tick, new PlayerSlot(1), 0, 0, 32768),
                }));
                Assert.That(simulation.State.ProjectileCount, Is.Zero);
                Assert.That(simulation.State.GetPlayerState(new PlayerSlot(1)).HitPoints, Is.EqualTo(75));
                presenter.Present(simulation.State, new PlayerSlot(0));

                ParticleSystem[] impacts = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                    .Where(particle => !previousParticles.Contains(particle)).ToArray();
                Assert.That(impacts, Has.Length.EqualTo(3), "One HP hit feedback plus two unattributed removals.");
                Color neutral = new Color(1f, 0.72f, 0.25f);
                Assert.That(impacts.Count(particle => particle.main.startColor.color == neutral), Is.EqualTo(2),
                    "Neither removed projectile has authoritative per-ID attribution in BattleState.");
                Assert.That(impacts.Count(particle => particle.main.startColor.color == Color.white), Is.Zero,
                    "A player HP decrease must not classify every simultaneous removal as a player hit.");
            }
            finally
            {
                foreach (ParticleSystem particle in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                    if (!originalParticles.Contains(particle)) UnityEngine.Object.DestroyImmediate(particle.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sceneRoot);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void FormalBattleHudHasNoPersistentControlsRow()
        {
            var root = new GameObject("Formal HUD Controls Test");
            try
            {
                root.AddComponent<BattleHudView>().Configure(BattlePresentationCatalog.LoadRequired());
                Assert.That(root.transform.Find("Battle HUD Canvas/Controls"), Is.Null);
                Assert.That(root.transform.Find("Battle HUD Canvas/Round"), Is.Not.Null);
                Assert.That(root.transform.Find("Battle HUD Canvas/Timer"), Is.Not.Null);
                Assert.That(root.transform.Find("Battle HUD Canvas/Announcement"), Is.Not.Null);
                Assert.That(root.transform.Find("Battle HUD Canvas/Settlement"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CatalogContainsTheApprovedMinimalAssetSet()
        {
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();

            Assert.That(AssetDatabase.GetAssetPath(catalog.RobotPrefab), Is.EqualTo(
                "Assets/LockstepArenaDemo/Prefabs/BattlePresentation/StylooRobotView.prefab"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.WeaponPrefab), Is.EqualTo(
                "Assets/Art/Weapons/SciFiGun/LongPistol_small.fbx"));
            Assert.That(catalog.IdleClip?.name, Does.Contain("iddle"));
            Assert.That(catalog.RunClip?.name, Does.Contain("walking"));
            Assert.That(catalog.ShootClip?.name, Does.Contain("attackminiguns"));
            Assert.That(catalog.ProjectileMaterial, Is.Not.Null);
            Assert.That(catalog.ShootSound, Is.Not.Null);
            Assert.That(catalog.PlayerHitSound, Is.Not.Null);
            Assert.That(catalog.EnvironmentHitSound, Is.Not.Null);
            Assert.That(catalog.AmbientSound, Is.Not.Null);
        }

        [Test]
        public void ImportedPlayerAndWeaponContainNoGameplayPhysicsAuthority()
        {
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
            Assert.That(catalog.RobotPrefab!.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(catalog.RobotPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(catalog.WeaponPrefab!.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(catalog.WeaponPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void PlayerPresentationUsesAReadableRobotBodyMaterial()
        {
            var root = new GameObject("Robot Readability Test");
            try
            {
                PlayerPresentation player = root.AddComponent<PlayerPresentation>();
                player.Configure(new PlayerSlot(0), BattlePresentationCatalog.LoadRequired());

                SkinnedMeshRenderer[] bodies = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(bodies, Is.Not.Empty);
                Assert.That(bodies, Has.All.Matches<SkinnedMeshRenderer>(renderer =>
                    renderer.enabled && renderer.gameObject.activeInHierarchy));
                Assert.That(bodies, Has.All.Matches<SkinnedMeshRenderer>(renderer =>
                    renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null));
                Assert.That(bodies, Has.All.Matches<SkinnedMeshRenderer>(renderer =>
                    renderer.sharedMaterial.color == Color.white));
                Assert.That(bodies, Has.All.Matches<SkinnedMeshRenderer>(renderer =>
                    Array.TrueForAll(renderer.sharedMaterials,
                        material => material == BattlePresentationCatalog.LoadRequired().RobotMaterial)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RobotMaterialUsesTheImportedColorTexture()
        {
            Material material = BattlePresentationCatalog.LoadRequired().RobotMaterial!;

            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Is.EqualTo(
                "Assets/Art/Characters/StylooRobot/robot_color.png"));
        }

        [Test]
        public void RobotViewPrefabUsesOneEditorReadyUrpMaterialWithAllSourceMaps()
        {
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
            Material material = catalog.RobotMaterial!;

            Assert.That(AssetDatabase.GetAssetPath(catalog.RobotPrefab), Is.EqualTo(
                "Assets/LockstepArenaDemo/Prefabs/BattlePresentation/StylooRobotView.prefab"));
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Is.EqualTo(
                "Assets/Art/Characters/StylooRobot/robot_color.png"));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")), Is.EqualTo(
                "Assets/Art/Characters/StylooRobot/robot_normal.png"));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")), Is.EqualTo(
                "Assets/Art/Characters/StylooRobot/robot_metallic.png"));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_EmissionMap")), Is.EqualTo(
                "Assets/Art/Characters/StylooRobot/robot_emission.png"));
            Assert.That(catalog.RobotPrefab!.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                Has.All.Matches<SkinnedMeshRenderer>(renderer => Array.TrueForAll(renderer.sharedMaterials,
                    rendererMaterial => rendererMaterial == material)));
        }

        [Test]
        public void BattleSourceModelsUseTheirEditorReadyUrpMaterials()
        {
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();

            AssertModelMaterial("Assets/Art/Characters/StylooRobot/robot.fbx", catalog.RobotMaterial!);
            AssertModelMaterial("Assets/Art/Weapons/SciFiGun/LongPistol_small.fbx", catalog.WeaponMaterial!);
            AssertModelMaterial("Assets/Art/Environment/SciFiArena/Door_Frame_Square.fbx", catalog.WallMaterial!);
            AssertModelMaterial("Assets/Art/Environment/SciFiArena/Column_Simple.fbx", catalog.WallMaterial!);
            AssertModelMaterial("Assets/Art/Environment/SciFiArena/Prop_Computer.fbx", catalog.CoverMaterial!);
            AssertModelMaterial("Assets/Art/Props/Prop_Crate.fbx", catalog.CoverMaterial!);
        }

        [Test]
        public void PlayerPresentationUsesGroundRingWithoutFloatingTeamCore()
        {
            var root = new GameObject("Team Marker Test");
            try
            {
                PlayerPresentation player = root.AddComponent<PlayerPresentation>();
                player.Configure(new PlayerSlot(0), BattlePresentationCatalog.LoadRequired());

                Assert.That(FindTransform(root.transform, "Team Ground Ring"), Is.Not.Null);
                Assert.That(root.transform.Find("Team Core"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RobotViewPrefabExposesEditablePresentationAnchors()
        {
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
            GameObject prefab = catalog.RobotPrefab!;

            Assert.That(FindTransform(prefab.transform, "Robot Model Anchor"), Is.Not.Null);
            Assert.That(FindTransform(prefab.transform, "View Blaster"), Is.Not.Null);
            Assert.That(FindTransform(prefab.transform, "Visual Muzzle"), Is.Not.Null);
            Assert.That(FindTransform(prefab.transform, "Team Ground Ring"), Is.Not.Null);
        }

        [Test]
        public void PlayerPresentationUsesThePrefabAuthoredMuzzleAndTeamRing()
        {
            var root = new GameObject("Robot Prefab Authoring Test");
            try
            {
                PlayerPresentation player = root.AddComponent<PlayerPresentation>();
                player.Configure(new PlayerSlot(0), BattlePresentationCatalog.LoadRequired());

                Transform visual = root.transform.Find("Robot Visual/Styloo Robot View");
                Transform ring = FindTransform(visual, "Team Ground Ring")!;
                Assert.That(player.Muzzle.IsChildOf(visual), Is.True);
                Assert.That(ring.IsChildOf(visual), Is.True);
                Assert.That(root.transform.Find("Team Ground Ring"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlayerPresentationCanUseCurrentLocalAimWithoutChangingSimulationState()
        {
            var root = new GameObject("Local Aim Presentation Test");
            try
            {
                PlayerPresentation player = root.AddComponent<PlayerPresentation>();
                player.Configure(new PlayerSlot(0), BattlePresentationCatalog.LoadRequired());
                var state = new PlayerState(-3500, 0, 0, 100, 0, 0);
                var method = typeof(PlayerPresentation).GetMethod(
                    "Present",
                    new[] { typeof(PlayerState), typeof(ushort?) });

                Assert.That(method, Is.Not.Null,
                    "The presentation needs a render-only aim override for immediate local mouse facing.");
                method!.Invoke(player, new object?[] { state, (ushort?)16384 });

                Assert.That(Vector3.Angle(root.transform.forward, Vector3.forward), Is.LessThan(0.1f));
                Assert.That(state.Aim, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ControllerRetriesCameraBindingAfterScenePresentationBecomesAvailable()
        {
            BattleScenePresentation[] existing = UnityEngine.Object.FindObjectsByType<BattleScenePresentation>(
                FindObjectsSortMode.None);
            var root = new GameObject("Camera Binding Retry Test");
            root.SetActive(false);
            var sceneRoot = new GameObject("Late Battle Scene Presentation");
            sceneRoot.SetActive(false);
            try
            {
                foreach (BattleScenePresentation scene in existing) scene.gameObject.SetActive(false);
                LockstepArenaDemoController controller = root.AddComponent<LockstepArenaDemoController>();
                var ensure = typeof(LockstepArenaDemoController).GetMethod("EnsurePresenter",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    ensure.Invoke(controller, null));
                Assert.That(failure!.InnerException, Is.TypeOf<InvalidOperationException>());

                BattleScenePresentation presentation = sceneRoot.AddComponent<BattleScenePresentation>();
                presentation.ArenaRoot = sceneRoot;
                presentation.GameplayCamera = sceneRoot.AddComponent<Camera>();
                presentation.AmbientSource = sceneRoot.AddComponent<AudioSource>();
                presentation.UiSource = sceneRoot.AddComponent<AudioSource>();
                sceneRoot.SetActive(true);

                BattlePresenter presenter = (BattlePresenter)ensure.Invoke(controller, null)!;
                Assert.That(presenter.GameplayCamera, Is.SameAs(presentation.GameplayCamera),
                    "An early scene-load failure must not permanently cache an unconfigured presenter.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sceneRoot);
                foreach (BattleScenePresentation scene in existing)
                    if (scene != null) scene.gameObject.SetActive(true);
            }
        }

        [Test]
        public void AnimatedRobotSkinsStayUpdatedOutsideImportedBounds()
        {
            var root = new GameObject("Robot Skin Culling Test");
            try
            {
                PlayerPresentation player = root.AddComponent<PlayerPresentation>();
                player.Configure(new PlayerSlot(0), BattlePresentationCatalog.LoadRequired());

                SkinnedMeshRenderer[] bodies = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(bodies, Is.Not.Empty);
                Assert.That(bodies, Has.All.Matches<SkinnedMeshRenderer>(renderer =>
                    renderer.updateWhenOffscreen));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AnimatedRobotAndWeaponGeometryStaysNearPlayerAcrossClips()
        {
            var root = new GameObject("Animated Weapon Geometry Test");
            try
            {
                BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
                PlayerPresentation player = root.AddComponent<PlayerPresentation>();
                player.Configure(new PlayerSlot(0), catalog);
                Transform model = FindTransform(root.transform, "Styloo Robot Model")!;
                Transform blaster = FindTransform(root.transform, "View Blaster")!;
                Transform muzzle = FindTransform(root.transform, "Visual Muzzle")!;
                Animation animation = model.GetComponentInChildren<Animation>();
                SkinnedMeshRenderer[] bodies = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Renderer[] pieces = blaster.GetComponentsInChildren<Renderer>(true);
                Assert.That(animation, Is.Not.Null);
                Assert.That(bodies, Is.Not.Empty);
                Assert.That(pieces, Is.Not.Empty);

                foreach (AnimationClip? clip in new[] { catalog.IdleClip, catalog.RunClip, catalog.ShootClip })
                {
                    Assert.That(clip, Is.Not.Null);
                    clip!.SampleAnimation(animation.gameObject, clip.length * 0.5f);
                    Bounds robotBounds = BakedWorldBounds(bodies);
                    Bounds weaponBounds = pieces[0].bounds;
                    for (int index = 1; index < pieces.Length; index++) weaponBounds.Encapsulate(pieces[index].bounds);
                    string context = $"{clip.name}: robot={robotBounds}, weapon={weaponBounds}, scale={blaster.localScale}, " +
                        $"worldScale={blaster.lossyScale}, position={blaster.position}, muzzle={muzzle.position}";
                    Assert.That(robotBounds.size.magnitude, Is.LessThan(3f), context);
                    Assert.That((robotBounds.center - root.transform.position).magnitude, Is.LessThan(2f), context);
                    Assert.That(weaponBounds.size.magnitude, Is.LessThan(2f), context);
                    Assert.That((weaponBounds.center - root.transform.position).magnitude, Is.LessThan(2f), context);
                    Assert.That((muzzle.position - weaponBounds.center).magnitude, Is.LessThan(1f), context);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdleAnimationKeepsRobotSkinAtReadableScaleAndPosition()
        {
            var root = new GameObject("Robot Animation Geometry Test");
            try
            {
                BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
                PlayerPresentation player = root.AddComponent<PlayerPresentation>();
                player.Configure(new PlayerSlot(0), catalog);
                Transform scaleRoot = root.transform.Find("Robot Visual/Styloo Robot View/Robot Model Anchor/Robot Scale");
                Transform model = scaleRoot.Find("Styloo Robot Model");
                Animation animation = model.GetComponentInChildren<Animation>();
                Assert.That(animation, Is.Not.Null);
                Vector3 modelBeforeSample = model.localPosition;
                catalog.IdleClip!.SampleAnimation(animation.gameObject, catalog.IdleClip.length * 0.5f);
                Vector3 modelAfterSample = model.localPosition;
                typeof(PlayerPresentation).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(player, null);

                SkinnedMeshRenderer[] bodies = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Bounds bakedBounds = BakedWorldBounds(bodies);
                string context = $"baked={bakedBounds} scale={scaleRoot.localScale} " +
                    $"pos={scaleRoot.localPosition} modelBefore={modelBeforeSample} " +
                    $"modelSampled={modelAfterSample} modelAfter={model.localPosition}";
                Assert.That(bakedBounds.size.y, Is.GreaterThan(1.2f), context);
                Assert.That(bakedBounds.min.y, Is.GreaterThanOrEqualTo(-0.05f), context);
                Assert.That(bakedBounds.max.y, Is.LessThan(1.8f), context);
                Assert.That(new Vector2(bakedBounds.center.x, bakedBounds.center.z).magnitude,
                    Is.LessThan(0.75f), context);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FixedCameraProjectsRightSideMouseAimOntoPositiveWorldX()
        {
            var root = new GameObject("Camera Projection Test");
            try
            {
                Camera camera = root.AddComponent<Camera>();
                BattlePresenter.ApplyGameplayCamera(camera);
                Vector3 screen = camera.WorldToScreenPoint(new Vector3(3f, 0f, 0f));
                Ray ray = camera.ScreenPointToRay(screen);
                var plane = new Plane(Vector3.up, Vector3.zero);

                Assert.That(plane.Raycast(ray, out float distance), Is.True);
                Assert.That(ray.GetPoint(distance).x, Is.GreaterThan(2.9f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static BattleState CreateState(
            uint tick,
            BattlePhase phase,
            uint phaseTicks,
            uint roundTicks,
            RoundResult result,
            PlayerSlot? matchWinner,
            PlayerState first,
            PlayerState second,
            params ProjectileState[] projectiles)
        {
            return new BattleState(tick, CreateRoster(), new[] { first, second },
                BattleDefinition.CreateDefault(), phase, phaseTicks, roundTicks,
                result, matchWinner, projectiles, 100);
        }

        private static ActiveRoster CreateRoster() =>
            new ActiveRoster(new[] { new PlayerId(11), new PlayerId(22) });

        private static void AssertModelMaterial(string path, Material expected)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, path);
            Assert.That(renderers, Has.All.Matches<Renderer>(renderer =>
                Array.TrueForAll(renderer.sharedMaterials, material => material == expected)), path);
        }

        private static Transform? FindTransform(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => value.name == name);
        }

        private static Bounds BakedWorldBounds(SkinnedMeshRenderer[] bodies)
        {
            bool hasVertex = false;
            Bounds bounds = default;
            foreach (SkinnedMeshRenderer skin in bodies)
            {
                var mesh = new Mesh();
                try
                {
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 world = skin.transform.TransformPoint(vertex);
                        if (hasVertex) bounds.Encapsulate(world);
                        else { bounds = new Bounds(world, Vector3.zero); hasVertex = true; }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            Assert.That(hasVertex, Is.True);
            return bounds;
        }

        private static PlayerState DefaultPlayer(int slot) => slot == 0
            ? new PlayerState(-3500, 0, 0, 100, 0, 0)
            : new PlayerState(3500, 0, 32768, 100, 0, 0);
    }
}
