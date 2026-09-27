#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LockstepArena.Client.Demo;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class V3CReplayPresentationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SuccessfulBattlePresentationClearsRecoveredDiagnosticError(bool replay)
        {
            using var fixture = new ReplayFixture();
            if (replay) fixture.Controller.WatchReplay();
            fixture.SilencePresenter();
            Set(fixture.Controller, "_lastError", "The authored BattleScene presentation is missing.");
            InvokeRequired(fixture.Controller, "AdvanceBattlePresentation", 0d);
            string error = (string)typeof(LockstepArenaDemoController).GetField("_lastError",
                BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(fixture.Controller)!;
            Assert.That(error, Is.Empty,
                "A recovered scene binding error must not remain as a current error after successful live/replay presentation.");
        }

        [Test]
        public void WatchReplayRequiresVerifiedSettlementAndSnapshot()
        {
            using var fixture = new ReplayFixture();
            PropertyInfo? canWatch = typeof(LockstepArenaDemoController).GetProperty("CanWatchReplay");
            Assert.That(canWatch, Is.Not.Null);
            Set(fixture.Client, "_settlementVerified", false);
            Assert.That(canWatch!.GetValue(fixture.Controller), Is.False);
            Set(fixture.Client, "_settlementVerified", true);
            Set(fixture.Client, "_retainedReplay", null);
            Assert.That(canWatch.GetValue(fixture.Controller), Is.False);
            Set(fixture.Client, "_retainedReplay", fixture.Snapshot);
            Set(fixture.Client, "_phase", DemoClientPhase.InBattle);
            Assert.That(canWatch.GetValue(fixture.Controller), Is.False);
            Set(fixture.Client, "_phase", DemoClientPhase.Settlement);
            Assert.That(canWatch.GetValue(fixture.Controller), Is.True);
        }

        [Test]
        public void ReplayUsesOfflineStateSuppressesGameplayAndReturnsToSameResult()
        {
            using var fixture = new ReplayFixture();
            InvokeRequired(fixture.Controller, "WatchReplay");
            fixture.SilencePresenter();
            var player = (BattleReplayPlayer)ReadRequired(fixture.Controller, "ReplayPlayer")!;
            Assert.That(player.CurrentState.Tick, Is.Zero);
            Assert.That(ReadRequired(fixture.Controller, "IsReplayMode"), Is.True);
            Set(fixture.Client, "_phase", DemoClientPhase.InBattle);
            Assert.That(InvokeRequired(fixture.Controller, "CollectGameplayInput", 1d), Is.Null,
                "Even an unexpected live-phase observation cannot sample gameplay while replay is active.");
            Set(fixture.Client, "_phase", DemoClientPhase.Settlement);
            InvokeRequired(fixture.Controller, "AdvanceBattlePresentation", 1d / SimulationConfig.TickRate);
            Assert.That(player.CurrentFrame, Is.EqualTo(1));
            TMP_Text hp = fixture.Root.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == "HP");
            Assert.That(hp.text, Does.Contain("100 / 100"), "HUD must read replay, not retained live HP 25.");
            for (int index = 0; index < fixture.Snapshot.AuthoritativeFrames.Count + 2; index++)
                InvokeRequired(fixture.Controller, "AdvanceBattlePresentation", 1d / SimulationConfig.TickRate);
            Assert.That(player.IsComplete, Is.True);
            Assert.That(ReadRequired(fixture.Controller, "IsReplayMode"), Is.True);
            Assert.That(fixture.Client.Phase, Is.EqualTo(DemoClientPhase.Settlement));
            InvokeRequired(fixture.Controller, "ExitReplay");
            Assert.That(ReadRequired(fixture.Controller, "IsReplayMode"), Is.False);
            Assert.That(fixture.Client.Phase, Is.EqualTo(DemoClientPhase.Settlement));
            Assert.That(fixture.Client.RetainedReplay, Is.SameAs(fixture.Snapshot));
            InvokeRequired(fixture.Controller, "WatchReplay");
            Assert.That(((BattleReplayPlayer)ReadRequired(fixture.Controller, "ReplayPlayer")!).CurrentFrame, Is.Zero);
            fixture.Controller.ReturnToLobby();
            Assert.That(ReadRequired(fixture.Controller, "IsReplayMode"), Is.False);
            Assert.That(fixture.Client.RetainedReplay, Is.Null);
        }

        [Test]
        public void SettlementReplayButtonAndReplayCompleteKeepSeparateActions()
        {
            using var fixture = new ReplayFixture();
            BattleHudView hud = fixture.Root.AddComponent<BattleHudView>();
            hud.Configure(fixture.Catalog);
            Button? watch = fixture.Root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Watch Replay Button");
            Assert.That(watch, Is.Not.Null);
            MethodInfo present = typeof(BattleHudView).GetMethod("Present")!;
            int watched = 0;
            int returned = 0;
            Action watchAction = () => watched++;
            Action returnAction = () => returned++;
            Set(fixture.Client, "_settlementVerified", false);
            present.Invoke(hud, new object[] { fixture.Snapshot.InitialState, DemoClientPhase.Settlement,
                fixture.Client.Snapshot, returnAction, true, watchAction });
            Assert.That(watch!.interactable, Is.False);
            Set(fixture.Client, "_settlementVerified", true);
            present.Invoke(hud, new object[] { fixture.Snapshot.InitialState, DemoClientPhase.Settlement,
                fixture.Client.Snapshot, returnAction, true, watchAction });
            Assert.That(watch.interactable, Is.True);
            watch.onClick.Invoke();
            Assert.That(watched, Is.EqualTo(1));
            Assert.That(returned, Is.Zero);
            var player = new BattleReplayPlayer(fixture.Snapshot);
            while (player.Tick()) { }
            int back = 0;
            InvokeRequired(hud, "PresentReplay", player, (Action)(() => back++));
            Assert.That(fixture.Root.GetComponentsInChildren<TMP_Text>(true)
                .Any(text => text.gameObject.activeInHierarchy && text.text.Contains("REPLAY COMPLETE")), Is.True);
            Assert.That(fixture.Root.transform.Find("Battle HUD Canvas/Settlement")!.gameObject.activeSelf, Is.False);
            Button backButton = fixture.Root.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Back To Result Button");
            backButton.onClick.Invoke();
            Assert.That(back, Is.EqualTo(1));
            Assert.That(returned, Is.Zero, "Replay completion must not send ReturnToLobby.");
        }

        [Test]
        public void FullPresentationResetAllowsSameProjectileAndDamageAgain()
        {
            var tracker = new BattlePresentationTracker();
            BattleState fired = FiredState();
            BattleState hit = new BattleState(1, fired.Roster,
                new[] { new PlayerState(-100, 0, 0, 100, 0, 0), new PlayerState(100, 0, 32768, 75, 0, 0) },
                fired.Definition!, BattlePhase.Playing, 0, 100, RoundResult.None, null,
                Array.Empty<ProjectileState>(), 2);
            Assert.That(tracker.Observe(fired).SpawnedProjectileIds, Is.EqualTo(new ulong[] { 1 }));
            Assert.That(tracker.Observe(hit).DamagedSlots, Is.EqualTo(new[] { 1 }));
            InvokeRequired(tracker, "Reset");
            Assert.That(tracker.Observe(fired).SpawnedProjectileIds, Is.EqualTo(new ulong[] { 1 }));
            Assert.That(tracker.Observe(hit).DamagedSlots, Is.EqualTo(new[] { 1 }));
            Assert.That(tracker.Observe(hit).HasAny, Is.False);
        }

        [Test]
        public void PresenterClearRemovesOwnedTransientsAndReplaysSameMuzzle()
        {
            using var fixture = new ReplayFixture();
            fixture.SilencePresenter();
            fixture.Presenter.Present(FiredState(), new PlayerSlot(0));
            Assert.That(fixture.Root.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(1),
                "Transient effects must be owned by the presenter, not leak between live and replay.");
            fixture.Presenter.Clear();
            Assert.That(fixture.Root.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            fixture.SilencePresenter();
            fixture.Presenter.Present(FiredState(), new PlayerSlot(0));
            Assert.That(fixture.Root.GetComponentsInChildren<ParticleSystem>(true), Has.Length.EqualTo(1),
                "The same projectile identity must emit a muzzle burst on the second replay.");
        }

        private static BattleState FiredState()
        {
            var roster = new ActiveRoster(new[] { new PlayerId(11), new PlayerId(22) });
            return new BattleState(0, roster,
                new[] { new PlayerState(-100, 0, 0, 100, 0, 0), new PlayerState(100, 0, 32768, 100, 0, 0) },
                BattleDefinition.CreateDefault(), BattlePhase.Playing, 0, 100, RoundResult.None, null,
                new[] { new ProjectileState(1, new PlayerId(11), -50, 0, 1000, 0, 20) }, 2);
        }

        private static object? ReadRequired(object target, string property)
        {
            PropertyInfo? value = target.GetType().GetProperty(property);
            Assert.That(value, Is.Not.Null, property);
            return value!.GetValue(target);
        }

        private static object? InvokeRequired(object target, string method, params object[] arguments)
        {
            MethodInfo? value = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(value, Is.Not.Null, method);
            return value!.Invoke(target, arguments);
        }

        private static void Set(object target, string field, object? value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

        private sealed class ReplayFixture : IDisposable
        {
            private readonly HashSet<ParticleSystem> _originalParticles;
            private readonly GameObject _scene;
            public readonly GameObject Root = new GameObject("Replay Presentation Test");
            public readonly BattlePresentationCatalog Catalog;
            public readonly LockstepArenaDemoController Controller;
            public readonly BattlePresenter Presenter;
            public readonly TcpDemoClient Client;
            public readonly BattleReplaySnapshot Snapshot;

            public ReplayFixture()
            {
                _originalParticles = new HashSet<ParticleSystem>(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None));
                Catalog = UnityEngine.Object.Instantiate(BattlePresentationCatalog.LoadRequired());
                foreach (FieldInfo field in typeof(BattlePresentationCatalog).GetFields())
                    if (field.FieldType == typeof(AudioClip)) field.SetValue(Catalog, null);
                _scene = new GameObject("Replay Scene Bindings");
                BattleScenePresentation scene = _scene.AddComponent<BattleScenePresentation>();
                scene.ArenaRoot = _scene;
                scene.GameplayCamera = _scene.AddComponent<Camera>();
                scene.AmbientSource = _scene.AddComponent<AudioSource>();
                scene.UiSource = _scene.AddComponent<AudioSource>();
                Controller = Root.AddComponent<LockstepArenaDemoController>();
                Controller.enabled = false;
                Presenter = Root.AddComponent<BattlePresenter>();
                Client = new TcpDemoClient(new DemoClientOptions(46000, 4096, 32768, 1024, 7, 257,
                    8, 512, 8, 16, 64, 4096, 4096, 1024, 11, 251));
                BattleState initial = BattleState.CreateGameplayInitial(
                    new ActiveRoster(new[] { new PlayerId(11), new PlayerId(22) }), BattleDefinition.CreateDefault());
                FrameData[] frames = Enumerable.Range(0, 3).Select(index => FrameData.Create(initial.Roster, (uint)index,
                    new[] { new InputFrame((uint)index, new PlayerSlot(0), 1, 0, 0, true),
                        new InputFrame((uint)index, new PlayerSlot(1), 0, 0, 32768) })).ToArray();
                Snapshot = new BattleReplaySnapshot(initial, frames);
                BattleState live = new BattleState(20, initial.Roster,
                    new[] { new PlayerState(-100, 0, 0, 25, 0, 2), new PlayerState(100, 0, 32768, 0, 0, 0) },
                    initial.Definition!, BattlePhase.MatchEnded, 0, 0, RoundResult.PlayerWin(new PlayerSlot(0)), new PlayerSlot(0),
                    Array.Empty<ProjectileState>(), 5);
                Set(Client, "_phase", DemoClientPhase.Settlement);
                Set(Client, "_settlementVerified", true);
                Set(Client, "_retainedReplay", Snapshot);
                Set(Client, "_lastBattleState", live);
                Set(Controller, "_client", Client);
                Set(Controller, "_presenter", Presenter);
            }

            public void SilencePresenter()
            {
                Presenter.Configure(BattleDefinition.CreateDefault());
                Set(Presenter, "_catalog", Catalog);
            }

            public void Dispose()
            {
                foreach (ParticleSystem particle in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                    if (!_originalParticles.Contains(particle)) UnityEngine.Object.DestroyImmediate(particle.gameObject);
                Client.Dispose();
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(_scene);
                UnityEngine.Object.DestroyImmediate(Catalog);
            }
        }
    }
}
