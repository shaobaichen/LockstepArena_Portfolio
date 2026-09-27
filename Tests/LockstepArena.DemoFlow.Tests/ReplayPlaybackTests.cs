using System;
using System.Reflection;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Simulation;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class ReplayPlaybackTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(OfflineReplayUsesTickRateAndHoldsExactAuthorityAtEnd), OfflineReplayUsesTickRateAndHoldsExactAuthorityAtEnd),
            new TestCase(nameof(PauseResumeAndRestartNeverCollectOrInventInputs), PauseResumeAndRestartNeverCollectOrInventInputs),
        };

        private static object CreatePlayer(BattleReplaySnapshot snapshot)
        {
            Type? type = typeof(ReplayPlaybackTests).Assembly.GetType("LockstepArena.Demo.BattleReplayPlayer");
            TestAssert.True(type is not null);
            return Activator.CreateInstance(type!, snapshot)!;
        }

        private static T Read<T>(object player, string property) =>
            (T)player.GetType().GetProperty(property)!.GetValue(player)!;

        private static void Call(object player, string method, params object[] arguments) =>
            player.GetType().GetMethod(method)!.Invoke(player, arguments);

        internal static BattleReplaySnapshot CreateSnapshot()
        {
            BattleState initial = BattleState.CreateGameplayInitial(
                new ActiveRoster(new[] { new PlayerId(11), new PlayerId(22) }), BattleDefinition.CreateDefault());
            var first = FrameData.Create(initial.Roster, 0, new[]
            {
                new InputFrame(0, new PlayerSlot(0), 1, 0, 0, true),
                new InputFrame(0, new PlayerSlot(1), 0, -1, 32768, false),
            });
            var second = FrameData.Create(initial.Roster, 1, new[]
            {
                new InputFrame(1, new PlayerSlot(0), 0, 1, 16384, false),
                new InputFrame(1, new PlayerSlot(1), -1, 0, 32768, true),
            });
            return new BattleReplaySnapshot(initial, new[] { first, second });
        }

        private static void OfflineReplayUsesTickRateAndHoldsExactAuthorityAtEnd()
        {
            BattleReplaySnapshot snapshot = CreateSnapshot();
            object player = CreatePlayer(snapshot);
            TestAssert.True(BattleStateValueComparer.HaveSameValue(snapshot.InitialState, Read<BattleState>(player, "CurrentState")));
            TestAssert.Equal(0, Read<int>(player, "CurrentFrame"));
            TestAssert.Equal(2, Read<int>(player, "TotalFrames"));
            Call(player, "AdvanceTime", 0.5d / SimulationConfig.TickRate);
            TestAssert.Equal(0, Read<int>(player, "CurrentFrame"));
            Call(player, "AdvanceTime", 0.5d / SimulationConfig.TickRate);
            TestAssert.Equal(1, Read<int>(player, "CurrentFrame"));
            TestAssert.Equal(1U, Read<BattleState>(player, "CurrentState").Tick);
            Call(player, "Tick");
            TestAssert.Equal(2, Read<int>(player, "CurrentFrame"));
            TestAssert.True(Read<bool>(player, "IsComplete"));
            var expected = new BattleSimulation(snapshot.InitialState);
            foreach (FrameData frame in snapshot.AuthoritativeFrames) expected.Step(frame);
            TestAssert.True(BattleStateValueComparer.HaveSameValue(expected.State, Read<BattleState>(player, "CurrentState")));
            TestAssert.Equal(StateDigest.Compute(expected.State), StateDigest.Compute(Read<BattleState>(player, "CurrentState")));
            Call(player, "Tick");
            Call(player, "AdvanceTime", 10d);
            TestAssert.Equal(2, Read<int>(player, "CurrentFrame"));
            TestAssert.Equal(StateDigest.Compute(expected.State), StateDigest.Compute(Read<BattleState>(player, "CurrentState")));
        }

        private static void PauseResumeAndRestartNeverCollectOrInventInputs()
        {
            BattleReplaySnapshot snapshot = CreateSnapshot();
            object player = CreatePlayer(snapshot);
            Call(player, "Tick");
            Call(player, "Pause");
            TestAssert.True(Read<bool>(player, "IsPaused"));
            Call(player, "AdvanceTime", 10d);
            Call(player, "Tick");
            TestAssert.Equal(1, Read<int>(player, "CurrentFrame"));
            Call(player, "Resume");
            Call(player, "AdvanceTime", 0d);
            TestAssert.Equal(1, Read<int>(player, "CurrentFrame"));
            Call(player, "AdvanceTime", 1d / SimulationConfig.TickRate);
            ulong finalDigest = StateDigest.Compute(Read<BattleState>(player, "CurrentState"));
            TestAssert.True(Read<bool>(player, "IsComplete"));
            Call(player, "Restart");
            TestAssert.Equal(0, Read<int>(player, "CurrentFrame"));
            TestAssert.True(!Read<bool>(player, "IsPaused") && !Read<bool>(player, "IsComplete"));
            TestAssert.Equal(StateDigest.Compute(snapshot.InitialState), StateDigest.Compute(Read<BattleState>(player, "CurrentState")));
            Call(player, "TogglePause");
            Call(player, "Tick");
            TestAssert.Equal(0, Read<int>(player, "CurrentFrame"));
            Call(player, "TogglePause");
            Call(player, "Tick");
            Call(player, "Tick");
            TestAssert.Equal(finalDigest, StateDigest.Compute(Read<BattleState>(player, "CurrentState")));
        }
    }
}
