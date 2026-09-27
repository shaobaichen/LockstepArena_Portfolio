using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class UnityDemoSceneTests
    {
        [Test]
        public void LobbyAndBattleScenesAreBuildEnabledAndUseOnePersistentRoot()
        {
            const string lobbyPath = "Assets/LockstepArenaDemo/Scenes/LobbyScene.unity";
            const string battlePath = "Assets/LockstepArenaDemo/Scenes/BattleScene.unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(lobbyPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(battlePath), Is.Not.Null);
            string[] enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            Assert.That(enabledScenes, Is.EqualTo(new[] { lobbyPath, battlePath }));

            var scene = EditorSceneManager.OpenScene(lobbyPath, OpenSceneMode.Single);
            MonoBehaviour[] controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component.GetType().FullName == "LockstepArena.Demo.LockstepArenaDemoController")
                .ToArray();
            Assert.That(controllers, Has.Length.EqualTo(1));
        }

        [Test]
        public void LobbySceneContainsTheV3GameShellCanvasAndInputSystemEventSystem()
        {
            const string lobbyPath = "Assets/LockstepArenaDemo/Scenes/LobbyScene.unity";
            var scene = EditorSceneManager.OpenScene(lobbyPath, OpenSceneMode.Single);
            GameObject[] objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();

            Assert.That(objects.Count(gameObject =>
                gameObject.GetComponent<LockstepArenaDemoController>() != null), Is.EqualTo(1));
            Assert.That(objects.Count(gameObject =>
                gameObject.name == "GameShellCanvas" && gameObject.GetComponent<Canvas>() != null), Is.EqualTo(1));
            Assert.That(objects.Count(gameObject => gameObject.GetComponents<MonoBehaviour>().Any(component =>
                component.GetType().FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule")), Is.EqualTo(1));
            Assert.That(objects.Count(gameObject => gameObject.GetComponents<MonoBehaviour>().Any(component =>
                component.GetType().FullName == "LockstepArena.Demo.GameShellUiController")), Is.EqualTo(1));
        }

        [Test]
        public void BattleSceneContainsAnInputSystemEventSystemForSettlementActions()
        {
            const string battlePath = "Assets/LockstepArenaDemo/Scenes/BattleScene.unity";
            var scene = EditorSceneManager.OpenScene(battlePath, OpenSceneMode.Single);
            GameObject[] objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();

            Assert.That(objects.Count(gameObject => gameObject.GetComponents<MonoBehaviour>().Any(component =>
                component.GetType().FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule")), Is.EqualTo(1));
        }

        [Test]
        public void BattleSceneContainsEditableArenaCameraLightingAndNoGameplayPhysics()
        {
            const string battlePath = "Assets/LockstepArenaDemo/Scenes/BattleScene.unity";
            var scene = EditorSceneManager.OpenScene(battlePath, OpenSceneMode.Single);
            GameObject[] objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();

            Assert.That(objects.Count(gameObject => gameObject.name == "Battle Presentation" &&
                gameObject.GetComponents<MonoBehaviour>().Any(component =>
                    component.GetType().FullName == "LockstepArena.Demo.BattleScenePresentation")), Is.EqualTo(1));
            Assert.That(objects.Count(gameObject => gameObject.name == "Arena Environment"), Is.EqualTo(1));
            Assert.That(objects.Count(gameObject => gameObject.name == "Gameplay Camera" &&
                gameObject.GetComponent<Camera>() != null), Is.EqualTo(1));
            Assert.That(objects.Count(gameObject => gameObject.name == "Cool Arena Key Light" &&
                gameObject.GetComponent<Light>() != null), Is.EqualTo(1));
            Assert.That(objects.Where(gameObject => gameObject.name == "Arena Environment")
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<Collider>(true)), Is.Empty);
            Assert.That(objects.Where(gameObject => gameObject.name == "Arena Environment")
                .SelectMany(gameObject => gameObject.GetComponentsInChildren<Rigidbody>(true)), Is.Empty);
        }

        [Test]
        public void GameShellCanvasPrefabContainsEveryApprovedScreenRoot()
        {
            const string prefabPath = "Assets/LockstepArenaDemo/Prefabs/UI/GameShellCanvas.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            string[] names = prefab.GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.name)
                .ToArray();
            Assert.That(names, Does.Contain("MainMenuScreen"));
            Assert.That(names, Does.Contain("ModeSelectScreen"));
            Assert.That(names, Does.Contain("CreateLanScreen"));
            Assert.That(names, Does.Contain("JoinLanScreen"));
            Assert.That(names, Does.Contain("ConnectingOverlay"));
            Assert.That(names, Does.Contain("LobbyScreen"));
            Assert.That(names, Does.Contain("RoomScreen"));
            Assert.That(names, Does.Contain("ErrorOverlay"));
            Assert.That(names, Does.Contain("SettingsOverlay"));
        }
    }
}
