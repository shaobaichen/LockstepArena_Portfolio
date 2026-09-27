#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class UnityDemoPresentationTests
    {
        [Test]
        public void UnityFormatsFrozenGoldenSettlementAndDiagnostics()
        {
            Type snapshotType = Type.GetType("LockstepArena.Client.Demo.DemoClientSnapshot, LockstepArena.Client.Demo")!;
            Type roomType = Type.GetType("LockstepArena.Client.Demo.DemoRoomSummarySnapshot, LockstepArena.Client.Demo")!;
            Type participantType = Type.GetType("LockstepArena.Client.Demo.DemoRoomParticipantSnapshot, LockstepArena.Client.Demo")!;
            Type lifecycleType = Type.GetType("LockstepArena.Protocol.Wire.RoomLifecycleMessage, LockstepArena.Protocol")!;
            ConstructorInfo constructor = snapshotType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
            object snapshot = constructor.Invoke(new object[]
            {
                Enum.Parse(Type.GetType("LockstepArena.Client.Demo.DemoClientPhase, LockstepArena.Client.Demo")!, "Settlement"),
                2UL, "Alpha", 1UL, "Golden Room",
                "Room1 Golden Room host=Alpha 2/2 RoomLifecycleOpen",
                "0:Alpha PlayerId2 host ready | 1:Bravo PlayerId1 ready",
                1UL, "Slot0/PlayerId2 | Slot1/PlayerId1", "NOT_HOST",
                4U, 4U, 4U, 4U, 0, 0, 4, true, 4,
                0xD8E54FF828A4C670UL, 0xD8E54FF828A4C670UL, true,
                Enum.Parse(Type.GetType("LockstepArena.Protocol.Wire.BattleSettlementReasonMessage, LockstepArena.Protocol")!, "BattleSettlementReasonMatchCompleted"),
                2UL, 2U, 1U, "Match completed.",
                Array.CreateInstance(roomType, 0), Array.CreateInstance(participantType, 0),
                0UL, 0U, Enum.Parse(lifecycleType, "RoomLifecycleUnspecified"),
            });
            Type controllerType = Type.GetType("LockstepArena.Demo.LockstepArenaDemoController, LockstepArena.Demo")!;
            MethodInfo formatter = controllerType.GetMethod("FormatDiagnostics", BindingFlags.Public | BindingFlags.Static)!;
            string actual = (string)formatter.Invoke(null, new[] { snapshot })!;
            foreach (string value in new[] { "NETWORK", "PREDICTION", "DETERMINISM", "REPLAY", "MATCH",
                "Authoritative Tick: 4", "Predicted Tick: 4", "Latest Dirty: True", "Rollback Count (dirty frames): 4",
                "Digest Match: True (same tick)", "Authority Digest: D8E54FF828A4C670", "Authoritative Frames: 4",
                "Settlement Verified: True", "Phase: Settlement", "Score: 2-1" })
                Assert.That(actual, Does.Contain(value));
        }

        [Test]
        public void UnityLoadsGameplayRootAndPresenterTypes()
        {
            Assert.That(Type.GetType("LockstepArena.Demo.LockstepArenaDemoController, LockstepArena.Demo"), Is.Not.Null);
            Assert.That(Type.GetType("LockstepArena.Demo.BattlePresenter, LockstepArena.Demo"), Is.Not.Null);
        }

        [Test]
        public void UnityGameplayPredictionCapacityMatchesServerFutureWindow()
        {
            Type controllerType = Type.GetType("LockstepArena.Demo.LockstepArenaDemoController, LockstepArena.Demo")!;
            FieldInfo capacity = controllerType.GetField(
                "ClientPredictionCapacity",
                BindingFlags.NonPublic | BindingFlags.Static)!;

            Assert.That(capacity, Is.Not.Null);
            Assert.That(capacity.GetRawConstantValue(), Is.EqualTo(8));
        }

        [Test]
        public void UnityLoadsSupportedDemoUnlitShader()
        {
            Shader shader = Resources.Load<Shader>("LockstepArenaDemoUnlit");

            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
        }

        [Test]
        public void UnityDemoContinuesPumpingWhenWindowLosesFocus()
        {
            bool previous = Application.runInBackground;
            var root = new GameObject("Background Pump Test");
            try
            {
                LockstepArenaDemoController controller = root.AddComponent<LockstepArenaDemoController>();
                Application.runInBackground = false;
                MethodInfo awake = typeof(LockstepArenaDemoController).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                try
                {
                    awake.Invoke(controller, null);
                }
                catch (TargetInvocationException exception) when (
                    exception.InnerException is InvalidOperationException invalidOperation &&
                    invalidOperation.Message.Contains("DontDestroyOnLoad"))
                {
                    // EditMode cannot execute the final player-only persistence call.
                }

                Assert.That(Application.runInBackground, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Application.runInBackground = previous;
            }
        }

        [Test]
        public void UnityPredictionInputUsesSimulationTickCadence()
        {
            MethodInfo pacer = typeof(LockstepArenaDemoController).GetMethod(
                "ConsumePredictionTick",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            Assert.That(pacer, Is.Not.Null);
            var root = new GameObject("Prediction Pacer Test");
            try
            {
                LockstepArenaDemoController controller = root.AddComponent<LockstepArenaDemoController>();
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0.01d })!, Is.False);
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0.01d })!, Is.False);
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0.01d })!, Is.False);
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0.004d })!, Is.True);
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0d })!, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnitySelectsPreferredPrivateLanAddress()
        {
            string? address = LanAddressUtility.SelectPreferredPrivateIpv4(new[]
            {
                IPAddress.Parse("127.0.0.1"),
                IPAddress.Parse("169.254.1.4"),
                IPAddress.Parse("172.20.1.4"),
                IPAddress.Parse("10.1.2.3"),
                IPAddress.Parse("192.168.1.23"),
            });

            Assert.That(address, Is.EqualTo("192.168.1.23"));
        }

        [Test]
        public void UnityResolvesEditorAndPlayerDemoHostLayouts()
        {
            string editor = LanServerPathResolver.ResolveFromApplicationDataPath(
                Path.Combine("E:\\", "repo", "Assets"), true);
            string player = LanServerPathResolver.ResolveFromApplicationDataPath(
                Path.Combine("C:\\", "Games", "LockstepArena_Data"), false);

            Assert.That(editor, Does.EndWith(Path.Combine(
                "Server", "LockstepArena.Server.DemoHost", "bin", "Release", "net8.0",
                "LockstepArena.Server.DemoHost.exe")));
            Assert.That(player, Is.EqualTo(Path.Combine(
                "C:\\", "Games", "Server", "LockstepArena.Server.DemoHost.exe")));
        }

        [Test]
        public void UnityDefinesRecoverableShellStagesAndSafeEmptyLauncher()
        {
            Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "StartingServer"), Is.True);
            Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "CreatingRoom"), Is.True);
            Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "Failed"), Is.True);

            using var launcher = new LanServerLauncher();
            launcher.StopOwned();
            Assert.That(launcher.OwnsServer, Is.False);
        }

        [Test]
        public void UnityHostLauncherRejectsOccupiedControlPortWithoutClosingExistingListener()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            try
            {
                listener.Start();
                int controlPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                int battlePort = GetUnusedTcpPort(controlPort);
                using var launcher = new LanServerLauncher();

                SocketException exception = Assert.Throws<SocketException>(() => launcher.StartOwned(
                    Path.Combine(Environment.SystemDirectory, "where.exe"),
                    controlPort,
                    battlePort))!;

                Assert.That(exception.SocketErrorCode, Is.EqualTo(SocketError.AddressAlreadyInUse));
                Assert.That(listener.Server.IsBound, Is.True);
                Assert.That(launcher.OwnsServer, Is.False);

                MethodInfo humanize = typeof(LockstepArenaDemoController).GetMethod(
                    "HumanizeShellError",
                    BindingFlags.NonPublic | BindingFlags.Static)!;
                Assert.That(
                    humanize.Invoke(null, new object[] { exception }),
                    Is.EqualTo("服务器端口已被占用，请关闭旧服务器后重试。"));
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public async Task UnityReadinessRejectsExitedOwnedProcessEvenWhenPortLaterAccepts()
        {
            int controlPort = GetUnusedTcpPort();
            int battlePort = GetUnusedTcpPort(controlPort);
            using var launcher = new LanServerLauncher();
            launcher.StartOwned(
                Path.Combine(Environment.SystemDirectory, "hostname.exe"),
                controlPort,
                battlePort);
            for (int attempt = 0; attempt < 100 && launcher.OwnsServer; attempt++)
            {
                await Task.Delay(20);
            }
            Assert.That(launcher.OwnsServer, Is.False, "The test process must exit before readiness is probed.");

            var listener = new TcpListener(IPAddress.Any, controlPort);
            try
            {
                listener.Start();

                try
                {
                    await launcher.WaitUntilReadyAsync("127.0.0.1", controlPort, TimeSpan.FromSeconds(1));
                    Assert.Fail("An exited owned process must not accept a different listener as ready.");
                }
                catch (InvalidOperationException)
                {
                }
                Assert.That(listener.Server.IsBound, Is.True);
                Assert.That(launcher.OwnsServer, Is.False);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void UnityExposesThePlayerFacingGameShellApi()
        {
            Type controller = typeof(LockstepArenaDemoController);

            Assert.That(controller.GetProperty("ClientSnapshot")?.GetMethod?.IsPublic, Is.True);
            Assert.That(controller.GetProperty("ShellStage")?.GetMethod?.IsPublic, Is.True);
            Assert.That(controller.GetProperty("UserFacingError")?.GetMethod?.IsPublic, Is.True);
            Assert.That(controller.GetProperty("HostedLanAddress")?.GetMethod?.IsPublic, Is.True);
            Assert.That(controller.GetProperty("OwnsLocalServer")?.GetMethod?.IsPublic, Is.True);

            AssertPublicMethod(controller, "BeginHostLanAsync", typeof(Task), typeof(string), typeof(string));
            AssertPublicMethod(controller, "BeginJoinLan", typeof(void), typeof(string), typeof(string));
            AssertPublicMethod(controller, "RefreshRooms", typeof(void));
            AssertPublicMethod(controller, "CreateRoom", typeof(void), typeof(string));
            AssertPublicMethod(controller, "JoinRoom", typeof(void), typeof(ulong));
            AssertPublicMethod(controller, "LeaveRoom", typeof(void));
            AssertPublicMethod(controller, "SetReady", typeof(void), typeof(bool));
            AssertPublicMethod(controller, "StartBattle", typeof(void));
            AssertPublicMethod(controller, "DisconnectToMainMenu", typeof(void));
        }

        [Test]
        public void UnityDisconnectToMainMenuIsSafeWithoutAClient()
        {
            var root = new GameObject("Safe Disconnect Test");
            try
            {
                LockstepArenaDemoController controller = root.AddComponent<LockstepArenaDemoController>();

                Assert.DoesNotThrow(controller.DisconnectToMainMenu);
                Assert.That(controller.ShellStage, Is.EqualTo(ShellConnectionStage.Idle));
                Assert.That(controller.UserFacingError, Is.Empty);
                Assert.That(controller.OwnsLocalServer, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnityExposesTheOneClickV3AWindowsBuild()
        {
            Type? buildType = Type.GetType(
                "LockstepArena.Demo.Editor.V3AWindowsBuild, LockstepArena.Demo.Editor");
            Assert.That(buildType, Is.Not.Null);
            MethodInfo? build = buildType!.GetMethod(
                "BuildWindowsPlayer",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(build, Is.Not.Null);
            Assert.That(build!.ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test]
        public void UnityPackagesTmpEssentialResourcesForPlayerStartup()
        {
            Type settingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro")!;
            UnityEngine.Object? settings = Resources.Load("TMP Settings", settingsType);

            Assert.That(settings, Is.Not.Null);
            PropertyInfo? defaultFont = settingsType.GetProperty("defaultFontAsset");
            Assert.That(defaultFont, Is.Not.Null);
            Assert.That(defaultFont!.GetValue(settings), Is.Not.Null);
        }

        [Test]
        public void UnityCreatesTheShellFontFromAWindowsSystemFontReference()
        {
            MethodInfo? method = typeof(GameShellUiController).GetMethod(
                "GetRuntimeFont",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            object? font = method!.Invoke(null, null);
            Assert.That(font, Is.Not.Null);
            PropertyInfo? populationMode = font!.GetType().GetProperty("atlasPopulationMode");
            Assert.That(populationMode, Is.Not.Null);
            Assert.That(populationMode!.GetValue(font)?.ToString(), Is.EqualTo("DynamicOS"));
        }

        [Test]
        public void UnityLobbyJoinButtonInvokesTheShellJoinCommand()
        {
            GameObject shellRoot = PrefabUtility.LoadPrefabContents(
                "Assets/LockstepArenaDemo/Prefabs/UI/GameShellCanvas.prefab");
            var controllerRoot = new GameObject("Join Command Test Controller");
            try
            {
                GameShellUiController shell = shellRoot.GetComponent<GameShellUiController>();
                LockstepArenaDemoController controller = controllerRoot.AddComponent<LockstepArenaDemoController>();
                typeof(GameShellUiController).GetField(
                    "controller",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(shell, controller);
                typeof(GameShellUiController).GetMethod(
                    "WireButtons",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(shell, null);

                Button join = shellRoot.transform.Find(
                    "LobbyScreen/RoomDetails/JoinSelectedRoomButton")!.GetComponent<Button>();
                join.onClick.Invoke();

                string error = (string)typeof(GameShellUiController).GetField(
                    "localError",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shell)!;
                Assert.That(error, Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerRoot);
                PrefabUtility.UnloadPrefabContents(shellRoot);
            }
        }

        [Test]
        public void UnityHumanizesEveryApprovedAsyncRejection()
        {
            MethodInfo? humanize = typeof(LockstepArenaDemoController).GetMethod(
                "HumanizeCommandRejection",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(humanize, Is.Not.Null);

            Type reasonType = Type.GetType(
                "LockstepArena.Protocol.Wire.ControlRejectReasonMessage, LockstepArena.Protocol")!;
            (string Reason, string Expected)[] cases =
            {
                ("ControlRejectReasonRoomNotFound", "房间不存在或已关闭，请刷新房间列表。"),
                ("ControlRejectReasonRoomFull", "房间已满，请选择其他房间。"),
                ("ControlRejectReasonNotHost", "只有房主可以开始比赛。"),
                ("ControlRejectReasonNotReady", "所有玩家准备后才能开始比赛。"),
                ("ControlRejectReasonUnspecified", "服务器拒绝了该操作，请重试。"),
            };

            foreach ((string reason, string expected) in cases)
            {
                object value = Enum.Parse(reasonType, reason);
                string actual = (string)humanize!.Invoke(null, new[] { value, "raw detail" })!;
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(actual, Does.Not.Contain(reason));
                Assert.That(actual, Does.Not.Contain("raw detail"));
            }
        }

        [Test]
        public void UnityAsyncRejectionOverlayDismissesWithoutDisconnectingOrRepeating()
        {
            GameObject shellRoot = PrefabUtility.LoadPrefabContents(
                "Assets/LockstepArenaDemo/Prefabs/UI/GameShellCanvas.prefab");
            var controllerRoot = new GameObject("Async Rejection Test Controller");
            object client = CreateDetachedDemoClient();
            try
            {
                GameShellUiController shell = shellRoot.GetComponent<GameShellUiController>();
                LockstepArenaDemoController controller = controllerRoot.AddComponent<LockstepArenaDemoController>();
                typeof(LockstepArenaDemoController).GetField(
                    "_client",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller, client);
                typeof(GameShellUiController).GetField(
                    "controller",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(shell, controller);
                InjectCommandRejection(client, "ControlRejectReasonNotHost", "Only the host may start.");

                InvokePrivate(shell, "Update");

                GameObject errorOverlay = (GameObject)typeof(GameShellUiController).GetField(
                    "errorOverlay",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shell)!;
                string localError = (string)typeof(GameShellUiController).GetField(
                    "localError",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shell)!;
                Assert.That(errorOverlay.activeSelf, Is.True);
                Assert.That(localError, Is.EqualTo("只有房主可以开始比赛。"));

                MethodInfo? dismiss = typeof(GameShellUiController).GetMethod(
                    "DismissError",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(dismiss, Is.Not.Null);
                dismiss!.Invoke(shell, null);
                Assert.That(typeof(LockstepArenaDemoController).GetField(
                    "_client",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller), Is.SameAs(client));

                InvokePrivate(shell, "Update");
                localError = (string)typeof(GameShellUiController).GetField(
                    "localError",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shell)!;
                Assert.That(errorOverlay.activeSelf, Is.False);
                Assert.That(localError, Is.Empty);
            }
            finally
            {
                ((IDisposable)client).Dispose();
                UnityEngine.Object.DestroyImmediate(controllerRoot);
                PrefabUtility.UnloadPrefabContents(shellRoot);
            }
        }

        private static void AssertPublicMethod(
            Type type,
            string name,
            Type returnType,
            params Type[] parameterTypes)
        {
            MethodInfo? method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing public method {name}.");
            Assert.That(method!.ReturnType, Is.EqualTo(returnType), $"Unexpected return type for {name}.");
        }

        private static int GetUnusedTcpPort(int excludedPort = 0)
        {
            while (true)
            {
                var listener = new TcpListener(IPAddress.Any, 0);
                try
                {
                    listener.Start();
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    if (port != excludedPort) return port;
                }
                finally
                {
                    listener.Stop();
                }
            }
        }

        private static object CreateDetachedDemoClient()
        {
            Type optionsType = Type.GetType(
                "LockstepArena.Client.Demo.DemoClientOptions, LockstepArena.Client.Demo")!;
            object options = Activator.CreateInstance(optionsType, new object?[]
            {
                46000, 1024, 2048, 32, 3, 8, 4, 64,
                4, 4, 8, 16, 1024, 32, 3, 8,
                "127.0.0.1", null,
            })!;
            Type clientType = Type.GetType(
                "LockstepArena.Client.Demo.TcpDemoClient, LockstepArena.Client.Demo")!;
            return Activator.CreateInstance(clientType, options)!;
        }

        private static void InjectCommandRejection(object client, string reasonName, string detail)
        {
            Type reasonType = Type.GetType(
                "LockstepArena.Protocol.Wire.ControlRejectReasonMessage, LockstepArena.Protocol")!;
            Type rejectionType = Type.GetType(
                "LockstepArena.Protocol.Wire.CommandRejectedEventMessage, LockstepArena.Protocol")!;
            object rejection = Activator.CreateInstance(rejectionType)!;
            rejectionType.GetProperty("Reason")!.SetValue(rejection, Enum.Parse(reasonType, reasonName));
            rejectionType.GetProperty("Detail")!.SetValue(rejection, detail);

            Type eventType = Type.GetType(
                "LockstepArena.Protocol.Wire.ServerControlEventMessage, LockstepArena.Protocol")!;
            object message = Activator.CreateInstance(eventType)!;
            eventType.GetProperty("CommandRejected")!.SetValue(message, rejection);
            client.GetType().GetMethod(
                "ProcessEvent",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(client, new[] { message });
        }

        private static void InvokePrivate(object target, string methodName)
        {
            target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);
        }
    }
}
