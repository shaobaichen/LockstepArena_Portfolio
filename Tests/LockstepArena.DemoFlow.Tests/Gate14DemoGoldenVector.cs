using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using LockstepArena.Client.Demo;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Server.DemoHost;
using LockstepArena.Simulation;

namespace LockstepArena.DemoFlow.Tests
{
    internal sealed class Gate14DemoGoldenResult
    {
        internal Gate14DemoGoldenResult(
            ulong bravoSessionId,
            ulong alphaSessionId,
            ulong roomId,
            ulong battleId,
            BattleState initialState,
            BattleState[] serverStates,
            bool[] alphaDirtyFrames,
            bool[] bravoDirtyFrames,
            uint alphaAuthoritativeTick,
            uint alphaPredictedTick,
            uint bravoAuthoritativeTick,
            uint bravoPredictedTick,
            ulong alphaAuthoritativeDigest,
            ulong alphaPredictedDigest,
            ulong bravoAuthoritativeDigest,
            ulong bravoPredictedDigest,
            int alphaReplayFrameCount,
            int bravoReplayFrameCount,
            bool alphaSettlementVerified,
            bool bravoSettlementVerified,
            DemoClientPhase alphaFinalPhase,
            DemoClientPhase bravoFinalPhase,
            int retainedSessionCount,
            int retainedRoomCount)
        {
            BravoSessionId = bravoSessionId;
            AlphaSessionId = alphaSessionId;
            RoomId = roomId;
            BattleId = battleId;
            InitialState = initialState;
            ServerStates = (BattleState[])serverStates.Clone();
            AlphaDirtyFrames = (bool[])alphaDirtyFrames.Clone();
            BravoDirtyFrames = (bool[])bravoDirtyFrames.Clone();
            AlphaAuthoritativeTick = alphaAuthoritativeTick;
            AlphaPredictedTick = alphaPredictedTick;
            BravoAuthoritativeTick = bravoAuthoritativeTick;
            BravoPredictedTick = bravoPredictedTick;
            AlphaAuthoritativeDigest = alphaAuthoritativeDigest;
            AlphaPredictedDigest = alphaPredictedDigest;
            BravoAuthoritativeDigest = bravoAuthoritativeDigest;
            BravoPredictedDigest = bravoPredictedDigest;
            AlphaReplayFrameCount = alphaReplayFrameCount;
            BravoReplayFrameCount = bravoReplayFrameCount;
            AlphaSettlementVerified = alphaSettlementVerified;
            BravoSettlementVerified = bravoSettlementVerified;
            AlphaFinalPhase = alphaFinalPhase;
            BravoFinalPhase = bravoFinalPhase;
            RetainedSessionCount = retainedSessionCount;
            RetainedRoomCount = retainedRoomCount;
        }

        internal ulong BravoSessionId { get; }
        internal ulong AlphaSessionId { get; }
        internal ulong RoomId { get; }
        internal ulong BattleId { get; }
        internal BattleState InitialState { get; }
        internal BattleState[] ServerStates { get; }
        internal bool[] AlphaDirtyFrames { get; }
        internal bool[] BravoDirtyFrames { get; }
        internal uint AlphaAuthoritativeTick { get; }
        internal uint AlphaPredictedTick { get; }
        internal uint BravoAuthoritativeTick { get; }
        internal uint BravoPredictedTick { get; }
        internal ulong AlphaAuthoritativeDigest { get; }
        internal ulong AlphaPredictedDigest { get; }
        internal ulong BravoAuthoritativeDigest { get; }
        internal ulong BravoPredictedDigest { get; }
        internal int AlphaReplayFrameCount { get; }
        internal int BravoReplayFrameCount { get; }
        internal bool AlphaSettlementVerified { get; }
        internal bool BravoSettlementVerified { get; }
        internal DemoClientPhase AlphaFinalPhase { get; }
        internal DemoClientPhase BravoFinalPhase { get; }
        internal int RetainedSessionCount { get; }
        internal int RetainedRoomCount { get; }
    }

    internal static class Gate14DemoGoldenVector
    {
        internal static Gate14DemoGoldenResult Run(bool alternateSegmentation)
        {
            PlayerState[] spawns =
            {
                new PlayerState(-300, 0, 1000),
                new PlayerState(300, 0, 2000),
            };
            using var server = new TcpDemoServer(new DemoServerOptions(
                0, 0, 4, 4, 2, spawns, 2U, 8U, 8, 4U,
                4096, 32768, 64, alternateSegmentation ? 7 : 3,
                alternateSegmentation ? 2 : 13, 8,
                alternateSegmentation ? 1 : 17,
                4096, 64, alternateSegmentation ? 9 : 5,
                alternateSegmentation ? 2 : 13));
            using var bravo = new TcpDemoClient(CreateClientOptions(server.ControlPort, alternateSegmentation));
            using var alpha = new TcpDemoClient(CreateClientOptions(server.ControlPort, alternateSegmentation));

            EnterLobby(bravo, "Bravo", server);
            EnterLobby(alpha, "Alpha", server);
            ulong bravoSessionId = bravo.Snapshot.SessionId;
            ulong alphaSessionId = alpha.Snapshot.SessionId;

            alpha.CreateRoom("Golden Room", 2);
            PumpUntil(() => alpha.Phase == DemoClientPhase.Room, server, alpha, bravo);
            ulong roomId = alpha.Snapshot.RoomId;
            bravo.JoinRoom(roomId);
            PumpUntil(() => bravo.Phase == DemoClientPhase.Room, server, alpha, bravo);
            alpha.SetReady(true);
            bravo.SetReady(true);
            Pump(server, alpha, bravo, 300);
            alpha.StartBattle();
            PumpUntil(
                () => alpha.Phase == DemoClientPhase.InBattle && bravo.Phase == DemoClientPhase.InBattle,
                server,
                alpha,
                bravo);

            DemoRoom room = server.GetRoom(roomId);
            BattlePreparation preparation = room.Preparation ?? throw new InvalidOperationException("Golden battle preparation is missing.");
            StopBattleClock(preparation);
            BattleState initialState = preparation.InitialState;
            ulong battleId = preparation.BattleId;

            LocalInputSample[] alphaInputs =
            {
                new LocalInputSample(-1, 0, 10100),
                new LocalInputSample(0, 1, 10101),
                new LocalInputSample(1, 0, 10102),
                new LocalInputSample(0, -1, 10103),
            };
            LocalInputSample[] bravoInputs =
            {
                new LocalInputSample(1, 0, 20100),
                new LocalInputSample(0, -1, 20101),
                new LocalInputSample(-1, 0, 20102),
                new LocalInputSample(0, 1, 20103),
            };
            for (int index = 0; index < alphaInputs.Length; index++)
            {
                if (!alpha.PumpOnce(alphaInputs[index]).PredictionSent ||
                    !bravo.PumpOnce(bravoInputs[index]).PredictionSent)
                {
                    throw new InvalidOperationException("Golden input was not predicted and sent.");
                }
            }

            PumpServerUntilInputsAreBuffered(server, preparation);
            var states = new List<BattleState>();
            var alphaDirty = new List<bool>();
            var bravoDirty = new List<bool>();
            uint observedTick = initialState.Tick;
            for (int advance = 0; advance < 5; advance++)
            {
                ForceNextPollAdvances(preparation, 1U);
                server.PumpOnce();
                BattleState serverState = preparation.SharedSession?.ServerState
                    ?? throw new InvalidOperationException("Golden battle session disappeared before settlement.");
                if (serverState.Tick != observedTick)
                {
                    states.Add(serverState);
                    observedTick = serverState.Tick;
                    DrainAuthorityTo(alpha, observedTick, alphaDirty);
                    DrainAuthorityTo(bravo, observedTick, bravoDirty);
                }
            }

            PumpUntil(
                () => alpha.Phase == DemoClientPhase.Settlement && bravo.Phase == DemoClientPhase.Settlement,
                server,
                alpha,
                bravo);
            DemoClientSnapshot alphaSettlement = alpha.Snapshot;
            DemoClientSnapshot bravoSettlement = bravo.Snapshot;
            alpha.ReturnToLobby();
            bravo.ReturnToLobby();
            PumpUntil(
                () => alpha.Phase == DemoClientPhase.Lobby &&
                    bravo.Phase == DemoClientPhase.Lobby &&
                    server.RoomCount == 0,
                server,
                alpha,
                bravo);

            DemoClientPhase alphaReturnedPhase = alpha.Phase;
            DemoClientPhase bravoReturnedPhase = bravo.Phase;
            int retainedSessionCount = server.SessionCount;
            int retainedRoomCount = server.RoomCount;
            alpha.Exit();
            bravo.Exit();
            PumpUntil(
                () => alpha.Phase == DemoClientPhase.Disconnected &&
                    bravo.Phase == DemoClientPhase.Disconnected &&
                    server.SessionCount == 0,
                server,
                alpha,
                bravo);

            return new Gate14DemoGoldenResult(
                bravoSessionId,
                alphaSessionId,
                roomId,
                battleId,
                initialState,
                states.ToArray(),
                alphaDirty.ToArray(),
                bravoDirty.ToArray(),
                alphaSettlement.AuthoritativeTick,
                alphaSettlement.PredictedTick,
                bravoSettlement.AuthoritativeTick,
                bravoSettlement.PredictedTick,
                alphaSettlement.AuthoritativeDigest,
                alphaSettlement.PredictedDigest,
                bravoSettlement.AuthoritativeDigest,
                bravoSettlement.PredictedDigest,
                alphaSettlement.ReplayFrameCount,
                bravoSettlement.ReplayFrameCount,
                alphaSettlement.SettlementVerified,
                bravoSettlement.SettlementVerified,
                alphaReturnedPhase,
                bravoReturnedPhase,
                retainedSessionCount,
                retainedRoomCount);
        }

        private static DemoClientOptions CreateClientOptions(int controlPort, bool alternate)
        {
            return new DemoClientOptions(
                controlPort, 4096, 32768, 64,
                alternate ? 7 : 3,
                alternate ? 2 : 13,
                8,
                alternate ? 1 : 17,
                4, 1, 16, 16,
                4096, 64,
                alternate ? 9 : 5,
                alternate ? 2 : 13);
        }

        private static void EnterLobby(TcpDemoClient client, string nickname, TcpDemoServer server)
        {
            client.BeginConnect();
            PumpUntil(() => client.Phase == DemoClientPhase.AwaitingSessionEntry, server, client);
            client.EnterSession(nickname);
            PumpUntil(() => client.Phase == DemoClientPhase.Lobby, server, client);
        }

        private static void PumpServerUntilInputsAreBuffered(TcpDemoServer server, BattlePreparation preparation)
        {
            for (int index = 0; index < 512 && preparation.SharedSession!.NextPublishTick == 0U; index++)
            {
                server.PumpOnce();
            }
        }

        private static void DrainAuthorityTo(TcpDemoClient client, uint targetTick, List<bool> dirty)
        {
            for (int index = 0; index < 5000; index++)
            {
                if (client.Snapshot.AuthoritativeTick == targetTick) return;
                DemoClientPumpResult result = client.PumpOnce(null);
                if (result.AuthoritativeFramesProcessed > 0) dirty.Add(client.Snapshot.LatestDirty);
            }
            throw new InvalidOperationException("Golden client did not consume authority within the bounded pump count.");
        }

        private static void PumpUntil(Func<bool> condition, TcpDemoServer server, params TcpDemoClient[] clients)
        {
            for (int index = 0; index < 50000; index++)
            {
                if (condition()) return;
                Pump(server, clients, 1);
            }
            throw new InvalidOperationException("Golden flow did not reach the expected phase within the bounded pump count.");
        }

        private static void Pump(TcpDemoServer server, TcpDemoClient first, TcpDemoClient second, int count)
        {
            Pump(server, new[] { first, second }, count);
        }

        private static void Pump(TcpDemoServer server, TcpDemoClient client, int count)
        {
            Pump(server, new[] { client }, count);
        }

        private static void Pump(TcpDemoServer server, TcpDemoClient[] clients, int count)
        {
            for (int iteration = 0; iteration < count; iteration++)
            {
                for (int index = 0; index < clients.Length; index++) clients[index].PumpOnce(null);
                server.PumpOnce();
                for (int index = 0; index < clients.Length; index++) clients[index].PumpOnce(null);
            }
        }

        private static void StopBattleClock(BattlePreparation preparation)
        {
            object driver = GetBattleDriver(preparation);
            ((Stopwatch)GetFieldByTypeName(driver, nameof(Stopwatch))).Stop();
        }

        private static void ForceNextPollAdvances(BattlePreparation preparation, uint dueAdvances)
        {
            object driver = GetBattleDriver(preparation);
            var stopwatch = (Stopwatch)GetFieldByTypeName(driver, nameof(Stopwatch));
            FieldInfo baseline = GetUniquePrivateField(driver.GetType(), typeof(long));
            long stableElapsed = stopwatch.ElapsedTicks;
            UInt128 numerator = ((UInt128)(ulong)Stopwatch.Frequency * dueAdvances) +
                (uint)SimulationConfig.TickRate - 1U;
            long requiredElapsed = checked((long)(numerator / (uint)SimulationConfig.TickRate));
            baseline.SetValue(driver, checked(stableElapsed - requiredElapsed));
        }

        private static object GetBattleDriver(BattlePreparation preparation)
        {
            object shared = preparation.SharedSession ?? throw new InvalidOperationException("Missing shared session.");
            object processor = GetFieldByTypeName(shared, "ProtocolAuthorityProcessor");
            return GetFieldByTypeName(processor, "StopwatchTickDriver");
        }

        private static object GetFieldByTypeName(object owner, string typeName)
        {
            FieldInfo[] fields = owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            object? result = null;
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType.Name != typeName) continue;
                if (result is not null) throw new InvalidOperationException($"Multiple private {typeName} fields found.");
                result = fields[index].GetValue(owner);
            }
            return result ?? throw new InvalidOperationException($"Missing private {typeName} field.");
        }

        private static FieldInfo GetUniquePrivateField(Type owner, Type fieldType)
        {
            FieldInfo? result = null;
            FieldInfo[] fields = owner.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType != fieldType) continue;
                if (result is not null) throw new InvalidOperationException($"Multiple private {fieldType.Name} fields found.");
                result = fields[index];
            }
            return result ?? throw new InvalidOperationException($"Missing private {fieldType.Name} field.");
        }
    }
}
