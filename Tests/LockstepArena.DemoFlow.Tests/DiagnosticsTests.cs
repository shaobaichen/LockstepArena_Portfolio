using System;
using System.Reflection;
using LockstepArena.Client.Demo;
using LockstepArena.Demo;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class DiagnosticsTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(LiveDiagnosticsGroupsRealValuesAndComparesOnlyAlignedDigests), LiveDiagnosticsGroupsRealValuesAndComparesOnlyAlignedDigests),
            new TestCase(nameof(ReplayDiagnosticsAreAuthoritativeOfflineAndDoNotAdvanceState), ReplayDiagnosticsAreAuthoritativeOfflineAndDoNotAdvanceState),
            new TestCase(nameof(InputDelayDiagnosticsReadBootstrapWithoutChangingClient), InputDelayDiagnosticsReadBootstrapWithoutChangingClient),
        };

        private static string Format(DemoClientSnapshot snapshot, BattleReplayPlayer? replay = null, uint? delay = null)
        {
            Type? formatter = typeof(DiagnosticsTests).Assembly.GetType("LockstepArena.Demo.LockstepDiagnosticsFormatter");
            TestAssert.True(formatter is not null);
            return (string)formatter!.GetMethod("Format")!.Invoke(null, new object?[] { snapshot, replay, delay })!;
        }

        private static TcpDemoClient CreateClient()
        {
            var client = new TcpDemoClient(new DemoClientOptions(46000, 4096, 32768, 1024, 7, 257,
                8, 512, 8, 16, 64, 4096, 4096, 1024, 11, 251));
            Set(client, "_phase", DemoClientPhase.Settlement);
            Set(client, "_serverStateTick", 12U);
            Set(client, "_authoritativeTick", 10U);
            Set(client, "_predictedTick", 10U);
            Set(client, "_pendingPredictionCount", 2);
            Set(client, "_pendingAuthoritativeFrameCount", 1);
            Set(client, "_latestDirty", true);
            Set(client, "_cumulativeDirtyFrameCount", 4);
            Set(client, "_replayFrameCount", 10);
            Set(client, "_authoritativeDigest", 0xD8E54FF828A4C670UL);
            Set(client, "_predictedDigest", 0xD8E54FF828A4C670UL);
            Set(client, "_settlementVerified", true);
            return client;
        }

        private static void LiveDiagnosticsGroupsRealValuesAndComparesOnlyAlignedDigests()
        {
            using var client = CreateClient();
            string actual = Format(client.Snapshot, null, 7U);
            foreach (string value in new[] { "LOCKSTEP DIAGNOSTICS", "NETWORK", "PREDICTION", "DETERMINISM", "REPLAY", "MATCH",
                "Server Tick: 12", "Authoritative Tick: 10", "Predicted Tick: 10", "Input Delay: 7 ticks",
                "Pending Prediction: 2", "Pending Authority: 1", "Latest Dirty: True", "Rollback Count (dirty frames): 4",
                "Authority Digest: D8E54FF828A4C670", "Predicted Digest: D8E54FF828A4C670", "Digest Match: True (same tick)",
                "Authoritative Frames: 10", "Replay Verified: True", "Phase: Settlement", "Settlement Verified: True" })
                TestAssert.True(actual.Contains(value, StringComparison.Ordinal));

            Set(client, "_predictedDigest", 0xD8E54FF828A4C671UL);
            TestAssert.True(Format(client.Snapshot).Contains("Digest Match: False (same tick)", StringComparison.Ordinal));
            Set(client, "_predictedTick", 11U);
            string unaligned = Format(client.Snapshot);
            TestAssert.True(unaligned.Contains("Digest Match: N/A (different ticks)", StringComparison.Ordinal));
            TestAssert.True(unaligned.Contains("Input Delay: N/A", StringComparison.Ordinal));
        }

        private static void ReplayDiagnosticsAreAuthoritativeOfflineAndDoNotAdvanceState()
        {
            using var client = CreateClient();
            var player = new BattleReplayPlayer(ReplayPlaybackTests.CreateSnapshot());
            player.Tick();
            BattleState before = player.CurrentState;
            ulong digest = StateDigest.Compute(before);
            int frame = player.CurrentFrame;
            string actual = Format(client.Snapshot, player);
            foreach (string value in new[] { "REPLAY MODE", "Frame: 1 / 2", "Source: AUTHORITATIVE", "Network: OFFLINE",
                "Digest: " + digest.ToString("X16") })
                TestAssert.True(actual.Contains(value, StringComparison.Ordinal));
            TestAssert.True(!actual.Contains("PREDICTION", StringComparison.Ordinal));
            TestAssert.True(ReferenceEquals(before, player.CurrentState));
            TestAssert.Equal(digest, StateDigest.Compute(player.CurrentState));
            TestAssert.Equal(frame, player.CurrentFrame);
            TestAssert.Equal(DemoClientPhase.Settlement, client.Phase);
        }

        private static void InputDelayDiagnosticsReadBootstrapWithoutChangingClient()
        {
            using var client = CreateClient();
            PropertyInfo? delay = typeof(TcpDemoClient).GetProperty("InputDelayTicks");
            TestAssert.True(delay is not null);
            TestAssert.True(delay!.SetMethod is null);
            TestAssert.True(delay.GetValue(client) is null);
            Set(client, "_preparing", new BattlePreparingEventMessage
            {
                Bootstrap = new BattleBootstrapMessage { InputDelayTicks = 7U },
            });
            TestAssert.Equal(7U, (uint)delay.GetValue(client)!);
            TestAssert.Equal(DemoClientPhase.Settlement, client.Phase);
        }

        private static void Set(TcpDemoClient client, string name, object value) =>
            typeof(TcpDemoClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client, value);
    }
}
