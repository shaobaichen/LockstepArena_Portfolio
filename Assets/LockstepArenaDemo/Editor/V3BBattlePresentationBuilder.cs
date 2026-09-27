#nullable enable

using System;
using System.IO;
using System.Linq;
using LockstepArena.Simulation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LockstepArena.Demo.Editor
{
    public static class V3BBattlePresentationBuilder
    {
        private const string Root = "Assets/LockstepArenaDemo/Resources/BattlePresentation";
        private const string CatalogPath = Root + "/V3BBattlePresentationCatalog.asset";
        private const string PrefabRoot = "Assets/LockstepArenaDemo/Prefabs/BattlePresentation";
        private const string RobotPrefabPath = PrefabRoot + "/StylooRobotView.prefab";
        private const string BattleScenePath = "Assets/LockstepArenaDemo/Scenes/BattleScene.unity";
        private const string RobotPath = "Assets/Art/Characters/StylooRobot/robot.fbx";
        private const string WeaponPath = "Assets/Art/Weapons/SciFiGun/LongPistol_small.fbx";

        [MenuItem("Tools/Lockstep Arena/Build v3-B Battle Presentation")]
        public static void BuildAssets()
        {
            EnsureFolder("Assets/LockstepArenaDemo/Resources", "BattlePresentation");
            EnsureFolder("Assets/LockstepArenaDemo/Prefabs", "BattlePresentation");
            ConfigureNormalTexture("Assets/Art/Characters/StylooRobot/robot_normal.png");

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader is unavailable.");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("URP Unlit shader is unavailable.");
            Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? unlit;

            Material robot = MaterialAt("RobotGunmetal", lit, Color.white);
            Texture2D robotColor = Load<Texture2D>("Assets/Art/Characters/StylooRobot/robot_color.png");
            Texture2D robotNormal = Load<Texture2D>("Assets/Art/Characters/StylooRobot/robot_normal.png");
            Texture2D robotMetallic = Load<Texture2D>("Assets/Art/Characters/StylooRobot/robot_metallic.png");
            Texture2D robotEmission = Load<Texture2D>("Assets/Art/Characters/StylooRobot/robot_emission.png");
            robot.SetTexture("_BaseMap", robotColor);
            robot.SetTexture("_MainTex", robotColor);
            robot.SetTexture("_BumpMap", robotNormal);
            robot.SetTexture("_MetallicGlossMap", robotMetallic);
            robot.SetTexture("_EmissionMap", robotEmission);
            robot.SetColor("_EmissionColor", Color.white * 0.65f);
            robot.SetFloat("_Metallic", 1f);
            robot.SetFloat("_Smoothness", 0.42f);
            robot.SetFloat("_Cull", 2f);
            robot.EnableKeyword("_NORMALMAP");
            robot.EnableKeyword("_METALLICSPECGLOSSMAP");
            robot.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(robot);

            Material weapon = MaterialAt("WeaponGunmetal", lit, new Color(0.14f, 0.18f, 0.24f));
            weapon.SetFloat("_Cull", 0f);
            weapon.SetFloat("_Metallic", 0.68f);
            weapon.SetFloat("_Smoothness", 0.52f);
            Material floor = MaterialAt("ArenaFloor", lit, new Color(0.055f, 0.075f, 0.085f));
            floor.SetFloat("_Metallic", 0.48f);
            floor.SetFloat("_Smoothness", 0.32f);
            Material wall = MaterialAt("ArenaWall", lit, new Color(0.1f, 0.13f, 0.17f));
            Material cover = MaterialAt("ArenaCover", lit, new Color(0.18f, 0.22f, 0.27f));
            cover.SetFloat("_Metallic", 0.65f);
            Material cyan = EmissiveAt("TeamCyan", lit, new Color(0.04f, 0.52f, 0.82f), new Color(0.08f, 0.72f, 1f) * 2.4f);
            Material red = EmissiveAt("TeamRed", lit, new Color(0.72f, 0.07f, 0.045f), new Color(1f, 0.18f, 0.12f) * 2.1f);
            Material projectile = EmissiveAt("EnergyBolt", unlit, Color.white, new Color(0.2f, 0.86f, 1f) * 3f);
            Material muzzle = MaterialAt("MuzzleFlash", particle, Color.white);
            muzzle.SetTexture("_BaseMap", Load<Texture2D>("Assets/Art/VFX/Kenney/muzzle_03.png"));
            Material particles = MaterialAt("ParticleSpark", particle, Color.white);
            particles.SetTexture("_BaseMap", Load<Texture2D>("Assets/Art/VFX/Kenney/spark_05.png"));

            ConfigureModel(RobotPath, true, robot);
            ConfigureModel(WeaponPath, false, weapon);
            ConfigureModel("Assets/Art/Environment/SciFiArena/Platform_Simple.fbx", false, floor);
            ConfigureModel("Assets/Art/Environment/SciFiArena/Door_Frame_Square.fbx", false, wall);
            ConfigureModel("Assets/Art/Environment/SciFiArena/Column_Simple.fbx", false, wall);
            ConfigureModel("Assets/Art/Environment/SciFiArena/Prop_Computer.fbx", false, cover);
            ConfigureModel("Assets/Art/Props/Prop_Crate.fbx", false, cover);

            BattlePresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<BattlePresentationCatalog>(CatalogPath);
            if (catalog is null)
            {
                catalog = ScriptableObject.CreateInstance<BattlePresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.RobotPrefab = EnsureRobotPrefab(robot, weapon, cyan);
            catalog.WeaponPrefab = Load<GameObject>(WeaponPath);
            catalog.PlatformPrefab = Load<GameObject>("Assets/Art/Environment/SciFiArena/Platform_Simple.fbx");
            catalog.DoorFramePrefab = Load<GameObject>("Assets/Art/Environment/SciFiArena/Door_Frame_Square.fbx");
            catalog.ColumnPrefab = Load<GameObject>("Assets/Art/Environment/SciFiArena/Column_Simple.fbx");
            catalog.ComputerPrefab = Load<GameObject>("Assets/Art/Environment/SciFiArena/Prop_Computer.fbx");
            catalog.CratePrefab = Load<GameObject>("Assets/Art/Props/Prop_Crate.fbx");
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(RobotPath).OfType<AnimationClip>()
                .Where(value => !value.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            catalog.IdleClip = FindClip(clips, "iddle", "idle");
            catalog.RunClip = FindClip(clips, "walking", "walk");
            catalog.ShootClip = FindClip(clips, "attackminiguns", "attack");
            catalog.RobotMaterial = robot;
            catalog.WeaponMaterial = weapon;
            catalog.FloorMaterial = floor;
            catalog.WallMaterial = wall;
            catalog.CoverMaterial = cover;
            catalog.CyanMaterial = cyan;
            catalog.RedMaterial = red;
            catalog.ProjectileMaterial = projectile;
            catalog.MuzzleMaterial = muzzle;
            catalog.ParticleMaterial = particles;
            catalog.PanelSprite = Load<Sprite>("Assets/Art/UI/SciFi/panel_dark_frame.png");
            catalog.ButtonSprite = Load<Sprite>("Assets/Art/UI/SciFi/button_primary.png");
            catalog.Font = TMP_Settings.defaultFontAsset;
            catalog.ShootSound = Load<AudioClip>("Assets/Art/Audio/Kenney/laserSmall_003.ogg");
            catalog.PlayerHitSound = Load<AudioClip>("Assets/Art/Audio/Kenney/impactMetal_001.ogg");
            catalog.EnvironmentHitSound = Load<AudioClip>("Assets/Art/Audio/Kenney/impactMetal_light_001.ogg");
            catalog.DeathSound = Load<AudioClip>("Assets/Art/Audio/Kenney/lowFrequency_explosion_000.ogg");
            catalog.CountdownSound = Load<AudioClip>("Assets/Art/Audio/Kenney/tone1.ogg");
            catalog.FightSound = Load<AudioClip>("Assets/Art/Audio/Kenney/twoTone1.ogg");
            catalog.VictorySound = Load<AudioClip>("Assets/Art/Audio/Kenney/powerUp3.ogg");
            catalog.ClickSound = Load<AudioClip>("Assets/Art/Audio/Kenney/click3.ogg");
            catalog.AmbientSound = Load<AudioClip>("Assets/Art/Audio/Kenney/spaceEngineLow_000.ogg");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built v3-B battle presentation catalog with clips: {string.Join(", ", clips.Select(value => value.name))}");
        }

        [MenuItem("Tools/Lockstep Arena/Capture v3-B Battle Preview")]
        public static void CapturePreview()
        {
            BuildAssets();
            EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
            var root = new GameObject("v3-B Preview");
            BattlePresenter presenter = root.AddComponent<BattlePresenter>();
            presenter.Configure(BattleDefinition.CreateDefault());
            ActiveRoster roster = new ActiveRoster(new[] { new PlayerId(11), new PlayerId(22) });
            var players = new[]
            {
                new PlayerState(-2800, -650, 5200, 75, 1, 0),
                new PlayerState(2700, 650, 38000, 50, 0, 0),
            };
            var projectiles = new[]
            {
                new ProjectileState(21, new PlayerId(11), -900, -280, 1000, 120, 20),
                new ProjectileState(22, new PlayerId(22), 1350, 420, -1000, -80, 20),
            };
            BattleState state = new BattleState(570, roster, players, BattleDefinition.CreateDefault(),
                BattlePhase.Playing, 0, 1230, RoundResult.None, null, projectiles, 23);
            presenter.Present(state, new PlayerSlot(0));
            foreach (PlayerPresentation player in root.GetComponentsInChildren<PlayerPresentation>())
            {
                player.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                BakePreviewSkins(player);
            }

            var hudRoot = new GameObject("v3-B Preview HUD");
            BattleHudView hud = hudRoot.AddComponent<BattleHudView>();
            hud.Configure(catalog);
            hud.PresentPreview(state);
            Camera camera = presenter.GameplayCamera!;
            Canvas canvas = hudRoot.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;

            const int width = 1280;
            const int height = 720;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            string directory = Path.GetFullPath("Docs/Evidence/V3B");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "v3b-battle-preview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(hudRoot);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("Captured v3-B preview: " + path);
        }

        private static void BakePreviewSkins(PlayerPresentation player)
        {
            foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh { name = skin.name + " Preview Mesh" };
                skin.BakeMesh(mesh, true);
                var baked = new GameObject(skin.name + " Preview Geometry", typeof(MeshFilter), typeof(MeshRenderer));
                baked.transform.SetParent(skin.transform, false);
                baked.GetComponent<MeshFilter>().sharedMesh = mesh;
                baked.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
            }
        }

        private static GameObject EnsureRobotPrefab(Material robotMaterial, Material weaponMaterial, Material teamMaterial)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            if (existing is not null)
            {
                bool hasModelAnchor = FindNamed(existing.transform, "Robot Model Anchor") is not null;
                bool hasMuzzle = FindNamed(existing.transform, "Visual Muzzle") is not null;
                bool hasRing = FindNamed(existing.transform, "Team Ground Ring") is not null;
                if (hasModelAnchor && hasMuzzle && hasRing) return existing;
                if (hasModelAnchor || hasMuzzle || hasRing)
                    throw new InvalidOperationException("The robot view prefab is partially authored; edit it manually instead of rebuilding it.");
            }

            GameObject source = Load<GameObject>(RobotPath);
            var root = new GameObject("Styloo Robot View");
            try
            {
                var anchor = new GameObject("Robot Model Anchor");
                anchor.transform.SetParent(root.transform, false);
                var scale = new GameObject("Robot Scale");
                scale.transform.SetParent(anchor.transform, false);
                scale.transform.localPosition = new Vector3(-0.06525f, 0.03f, 0.197f);
                scale.transform.localScale = Vector3.one * 0.174f;
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, scale.transform);
                model.name = "Styloo Robot Model";
                RemovePhysics(model);
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    Array.Fill(materials, robotMaterial);
                    renderer.sharedMaterials = materials;
                }
                Transform hand = FindBone(model.transform, "handdown") ??
                    FindBone(model.transform, "minigun") ?? model.transform;
                var weaponAnchor = new GameObject("Weapon Anchor");
                weaponAnchor.transform.SetParent(hand, false);
                weaponAnchor.transform.localPosition = new Vector3(0.0003f, 0f, 0.0008f);
                weaponAnchor.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                GameObject blaster = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(WeaponPath), weaponAnchor.transform);
                blaster.name = "View Blaster";
                RemovePhysics(blaster);
                blaster.transform.localScale = Vector3.one * 0.512f;
                foreach (Renderer renderer in blaster.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = weaponMaterial;
                var muzzle = new GameObject("Visual Muzzle");
                muzzle.transform.SetParent(blaster.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 0f, 0.05f);

                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Team Ground Ring";
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
                ring.transform.localScale = new Vector3(0.38f, 0.008f, 0.38f);
                RemovePhysics(ring);
                ring.GetComponent<Renderer>().sharedMaterial = teamMaterial;
                return PrefabUtility.SaveAsPrefabAsset(root, RobotPrefabPath) ??
                    throw new InvalidOperationException("Unable to save the editor-ready robot prefab.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform? FindNamed(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);

        private static Transform? FindBone(Transform root, string fragment) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child =>
                child.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);

        private static void BuildBattleScene(BattlePresentationCatalog catalog)
        {
            Scene scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Battle Presentation") UnityEngine.Object.DestroyImmediate(root);

            var presentationRoot = new GameObject("Battle Presentation");
            BattleScenePresentation presentation = presentationRoot.AddComponent<BattleScenePresentation>();
            var arena = new GameObject("Arena Environment");
            arena.transform.SetParent(presentationRoot.transform, false);
            presentation.ArenaRoot = arena;

            BattleDefinition definition = BattleDefinition.CreateDefault();
            ArenaRectangle bounds = definition.Arena.Bounds;
            float width = (bounds.MaxX - bounds.MinX) / BattlePresenter.WorldUnitsPerSimulationUnit;
            float depth = (bounds.MaxZ - bounds.MinZ) / BattlePresenter.WorldUnitsPerSimulationUnit;
            CreateSceneBox(arena.transform, "Combat Floor", new Vector3(0f, -0.12f, 0f),
                new Vector3(width, 0.2f, depth), catalog.FloorMaterial!);
            for (int index = 0; index < definition.Arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = definition.Arena.GetObstacle(index);
                Vector3 position = BattlePresenter.ToWorld((obstacle.MinX + obstacle.MaxX) / 2,
                    index == 0 ? 0.62f : 0.45f, (obstacle.MinZ + obstacle.MaxZ) / 2);
                Vector3 scale = new Vector3(
                    (obstacle.MaxX - obstacle.MinX) / BattlePresenter.WorldUnitsPerSimulationUnit,
                    index == 0 ? 1.24f : 0.9f,
                    (obstacle.MaxZ - obstacle.MinZ) / BattlePresenter.WorldUnitsPerSimulationUnit);
                CreateSceneBox(arena.transform,
                    index == 0 ? "Deterministic Central Reactor" : $"Deterministic Energy Relay {index}",
                    position, scale, catalog.CoverMaterial!);
                CreateSceneBox(arena.transform, $"Neutral Energy Strip {index}",
                    position + Vector3.up * (scale.y * 0.5f + 0.015f),
                    new Vector3(scale.x * 0.66f, 0.03f, scale.z * 0.22f), catalog.CyanMaterial!);
            }

            float backZ = bounds.MaxZ / BattlePresenter.WorldUnitsPerSimulationUnit + 0.14f;
            float frontZ = bounds.MinZ / BattlePresenter.WorldUnitsPerSimulationUnit - 0.14f;
            float leftX = bounds.MinX / BattlePresenter.WorldUnitsPerSimulationUnit - 0.14f;
            float rightX = bounds.MaxX / BattlePresenter.WorldUnitsPerSimulationUnit + 0.14f;
            CreateSceneBox(arena.transform, "Back Wall", new Vector3(0f, 0.9f, backZ),
                new Vector3(width + 0.5f, 1.8f, 0.28f), catalog.WallMaterial!);
            CreateSceneBox(arena.transform, "Left Wall", new Vector3(leftX, 0.65f, 0f),
                new Vector3(0.28f, 1.3f, depth), catalog.WallMaterial!);
            CreateSceneBox(arena.transform, "Right Wall", new Vector3(rightX, 0.65f, 0f),
                new Vector3(0.28f, 1.3f, depth), catalog.WallMaterial!);
            CreateSceneBox(arena.transform, "Low Front Edge", new Vector3(0f, 0.14f, frontZ),
                new Vector3(width + 0.5f, 0.28f, 0.24f), catalog.WallMaterial!);
            CreateSceneSpawnMarker(arena.transform, definition.Arena.GetSpawn(0), catalog.CyanMaterial!, "P1 Spawn Marker");
            CreateSceneSpawnMarker(arena.transform, definition.Arena.GetSpawn(1), catalog.RedMaterial!, "P2 Spawn Marker");
            CreateSceneDecoration(arena.transform, catalog.DoorFramePrefab, "Back Door Frame",
                new Vector3(0f, 0f, backZ - 0.12f), Quaternion.Euler(0f, 180f, 0f), 1.5f, catalog.WallMaterial!);
            CreateSceneDecoration(arena.transform, catalog.ColumnPrefab, "Left Boundary Column",
                new Vector3(leftX - 0.2f, 0f, backZ), Quaternion.identity, 1.6f, catalog.WallMaterial!);
            CreateSceneDecoration(arena.transform, catalog.ColumnPrefab, "Right Boundary Column",
                new Vector3(rightX + 0.2f, 0f, backZ), Quaternion.identity, 1.6f, catalog.WallMaterial!);
            CreateSceneDecoration(arena.transform, catalog.ComputerPrefab, "Wall Computer",
                new Vector3(-3.6f, 0f, backZ - 0.2f), Quaternion.Euler(0f, 180f, 0f), 0.8f, catalog.CoverMaterial!);
            CreateSceneDecoration(arena.transform, catalog.CratePrefab, "Outside Crate",
                new Vector3(4.4f, 0f, backZ + 0.45f), Quaternion.identity, 0.65f, catalog.CoverMaterial!);

            var cameraObject = new GameObject("Gameplay Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(presentationRoot.transform, false);
            presentation.GameplayCamera = cameraObject.GetComponent<Camera>();
            BattlePresenter.ApplyGameplayCamera(presentation.GameplayCamera);

            var lightObject = new GameObject("Cool Arena Key Light", typeof(Light));
            lightObject.transform.SetParent(presentationRoot.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.72f, 0.84f, 1f);
            light.intensity = 1.15f;

            var ambientObject = new GameObject("Arena Ambient Audio", typeof(AudioSource));
            ambientObject.transform.SetParent(presentationRoot.transform, false);
            presentation.AmbientSource = ambientObject.GetComponent<AudioSource>();
            presentation.AmbientSource.clip = catalog.AmbientSound;
            presentation.AmbientSource.loop = true;
            presentation.AmbientSource.playOnAwake = true;
            presentation.AmbientSource.volume = 0.13f;

            var uiAudioObject = new GameObject("Battle UI Audio", typeof(AudioSource));
            uiAudioObject.transform.SetParent(presentationRoot.transform, false);
            presentation.UiSource = uiAudioObject.GetComponent<AudioSource>();
            presentation.UiSource.playOnAwake = false;
            presentation.UiSource.spatialBlend = 0f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.22f, 0.28f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        private static void CreateSceneBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.position = position;
            value.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>());
            value.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateSceneSpawnMarker(Transform parent, ArenaPoint spawn, Material material, string name)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.position = BattlePresenter.ToWorld(spawn.X, 0.012f, spawn.Z);
            marker.transform.localScale = new Vector3(0.48f, 0.006f, 0.48f);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateSceneDecoration(Transform parent, GameObject? prefab, string name,
            Vector3 position, Quaternion rotation, float targetHeight, Material material)
        {
            if (prefab is null) return;
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.position = position;
            holder.transform.rotation = rotation;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
            instance.name = name + " Model";
            RemovePhysics(instance);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                Array.Fill(materials, material);
                renderer.sharedMaterials = materials;
            }
            NormalizeVisual(instance.transform, targetHeight);
        }

        private static void NormalizeVisual(Transform root, float targetHeight)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            if (bounds.size.y > 0.001f) root.localScale *= targetHeight / bounds.size.y;
            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            root.position += root.parent!.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private static void RemovePhysics(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
                UnityEngine.Object.DestroyImmediate(body);
        }

        private static void ConfigureModel(string path, bool animations, Material material)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) throw new InvalidOperationException("Missing model importer: " + path);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importAnimation = animations;
            if (animations)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    string lower = clip.name.ToLowerInvariant();
                    clip.loopTime = lower.Contains("iddle") || lower.Contains("idle") || lower.Contains("walking");
                    clip.loopPose = clip.loopTime;
                }
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
            foreach (Material sourceMaterial in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceMaterial.name), material);
            importer.SaveAndReimport();
        }

        private static void ConfigureNormalTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        private static Material MaterialAt(string name, Shader shader, Color color)
        {
            string path = $"{Root}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material is null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EmissiveAt(string name, Shader shader, Color baseColor, Color emission)
        {
            Material material = MaterialAt(name, shader, baseColor);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            return material;
        }

        private static AnimationClip FindClip(AnimationClip[] clips, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                AnimationClip? clip = clips.FirstOrDefault(value => value.name.ToLowerInvariant().Contains(candidate));
                if (clip is not null) return clip;
            }
            throw new InvalidOperationException("Required robot animation clip was not found: " + string.Join("/", candidates));
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
