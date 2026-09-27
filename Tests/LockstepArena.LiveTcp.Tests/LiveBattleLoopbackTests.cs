using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Server.FrameSync;
using LockstepArena.Server.LiveTcp;
using LockstepArena.Server.ProtocolAuthority;
using LockstepArena.Simulation;

namespace LockstepArena.LiveTcp.Tests
{
    internal static class LiveBattleLoopbackTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(LiveLoopbackGoldenReachesTick103AndApprovedDigest), LiveLoopbackGoldenReachesTick103AndApprovedDigest),
            new TestCase(nameof(MatureIncompleteTickSendsNothingUntilLateGapCompletion), MatureIncompleteTickSendsNothingUntilLateGapCompletion),
            new TestCase(nameof(TwoLiveRunsProduceSameFlattenedAuthoritySequenceAndState), TwoLiveRunsProduceSameFlattenedAuthoritySequenceAndState),
        };

        private static void LiveLoopbackGoldenReachesTick103AndApprovedDigest()
        {
            Gate11LiveBattleGoldenResult result = Gate11LiveBattleGoldenVector.Run();
            TestAssert.Equal(3, result.AuthoritativeFrames.Length);
            TestAssert.SequenceEqual(new[] { 100U, 101U, 102U }, new[]
            {
                result.AuthoritativeFrames[0].Tick,
                result.AuthoritativeFrames[1].Tick,
                result.AuthoritativeFrames[2].Tick,
            });
            TestAssert.Equal(3, result.AuthoritativeMessageWriteCount);
            TestAssert.Equal(103U, result.ServerNextPublishTick);
            AssertApprovedFinalState(result.ServerState);
            AssertApprovedFinalState(result.ClientState);
            AssertStateEqual(result.ServerState, result.ClientState);
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, StateDigest.Compute(result.ServerState));
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, StateDigest.Compute(result.ClientState));
        }

        private static void MatureIncompleteTickSendsNothingUntilLateGapCompletion()
        {
            ActiveRoster serverRoster = Gate11LiveBattleGoldenVector.CreateRoster();
            ActiveRoster clientRoster = Gate11LiveBattleGoldenVector.CreateRoster();
            (PlayerId PlayerId, InputFrame Input)[] submissions =
                Gate11LiveBattleGoldenVector.CreateSubmissions(serverRoster);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var clientSocket = new TcpClient(AddressFamily.InterNetwork);
                clientSocket.Connect(IPAddress.Loopback, endpoint.Port);
                TcpClient serverSocket = listener.AcceptTcpClient();
                using var server = new TcpServerBattlePump(
                    serverSocket,
                    Gate11LiveBattleGoldenVector.CreateInitialState(serverRoster),
                    2U, 8U, 5, 1_048_576, 16, 3, 3);
                using var client = new TcpClientBattlePump(
                    clientSocket,
                    Gate11LiveBattleGoldenVector.CreateInitialState(clientRoster),
                    1_048_576, 16, 5, 5);

                for (int index = 0; index < 11; index++)
                {
                    client.SendInput(submissions[index].PlayerId, submissions[index].Input);
                }

                ProtocolAuthorityProcessor processor = TcpServerBattlePumpTests.GetProcessor(server);
                Stopwatch ingestDeadline = Stopwatch.StartNew();
                while (!HasApprovedPreGapPending(processor) && ingestDeadline.ElapsedMilliseconds < 10_000)
                {
                    TestAssert.Equal(0, server.PumpOnce());
                    TestAssert.Equal(0, client.PumpReceiveOnce().Length);
                }

                TestAssert.True(HasApprovedPreGapPending(processor));
                ScheduledProtocolAuthorityTests.ForceNextPollAdvances(processor, 4U);
                TestAssert.Equal(0, server.PumpOnce());
                TestAssert.Equal(0, client.PumpReceiveOnce().Length);
                TestAssert.Equal(100U, server.ServerState.Tick);
                TestAssert.Equal(100U, client.ClientState.Tick);

                client.SendInput(submissions[11].PlayerId, submissions[11].Input);
                var frames = new List<FrameData>();
                int writes = 0;
                Stopwatch completionDeadline = Stopwatch.StartNew();
                while (frames.Count < 3 && completionDeadline.ElapsedMilliseconds < 10_000)
                {
                    writes += server.PumpOnce();
                    frames.AddRange(client.PumpReceiveOnce());
                }

                TestAssert.Equal(3, writes);
                TestAssert.Equal(3, frames.Count);
                TestAssert.SequenceEqual(new[] { 100U, 101U, 102U },
                    new[] { frames[0].Tick, frames[1].Tick, frames[2].Tick });
                TestAssert.Equal(103U, server.ServerState.Tick);
                TestAssert.Equal(103U, client.ClientState.Tick);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void TwoLiveRunsProduceSameFlattenedAuthoritySequenceAndState()
        {
            Gate11LiveBattleGoldenResult first = Gate11LiveBattleGoldenVector.Run();
            Gate11LiveBattleGoldenResult second = Gate11LiveBattleGoldenVector.Run();
            TestAssert.Equal(first.AuthoritativeFrames.Length, second.AuthoritativeFrames.Length);
            for (int index = 0; index < first.AuthoritativeFrames.Length; index++)
            {
                AssertFrameEqual(first.AuthoritativeFrames[index], second.AuthoritativeFrames[index]);
            }

            AssertStateEqual(first.ServerState, second.ServerState);
            AssertStateEqual(first.ClientState, second.ClientState);
            TestAssert.Equal(first.ServerNextPublishTick, second.ServerNextPublishTick);
        }

        private static bool HasApprovedPreGapPending(ProtocolAuthorityProcessor processor)
        {
            TickDrivenFramePublisher publisher = ScheduledProtocolAuthorityTests.GetScheduledPublisher(processor);
            AuthoritativeFrameCoordinator coordinator = (AuthoritativeFrameCoordinator)(
                ScheduledProtocolAuthorityTests.GetUniquePrivateField(
                    typeof(TickDrivenFramePublisher), typeof(AuthoritativeFrameCoordinator)).GetValue(publisher)
                ?? throw new InvalidOperationException("Publisher coordinator was null."));
            Type dictionaryType = typeof(Dictionary<uint, StrictFrameCollector>);
            var pending = (Dictionary<uint, StrictFrameCollector>)(
                ScheduledProtocolAuthorityTests.GetUniquePrivateField(
                    typeof(AuthoritativeFrameCoordinator), dictionaryType).GetValue(coordinator)
                ?? throw new InvalidOperationException("Coordinator pending storage was null."));
            return pending.TryGetValue(100U, out StrictFrameCollector? tick100) && !tick100.IsComplete
                && pending.TryGetValue(101U, out StrictFrameCollector? tick101) && tick101.IsComplete
                && pending.TryGetValue(102U, out StrictFrameCollector? tick102) && tick102.IsComplete;
        }

        private static void AssertApprovedFinalState(BattleState state)
        {
            TestAssert.Equal(103U, state.Tick);
            AssertPlayer(state, 0, -300, 100, 10_102);
            AssertPlayer(state, 1, 300, -100, 20_102);
            AssertPlayer(state, 2, 100, -300, 30_102);
            AssertPlayer(state, 3, -100, 300, 40_102);
        }

        private static void AssertPlayer(
            BattleState state,
            int slot,
            int x,
            int z,
            int aim)
        {
            PlayerState actual = state.GetPlayerState(new PlayerSlot(slot));
            TestAssert.Equal(x, actual.PositionX);
            TestAssert.Equal(z, actual.PositionZ);
            TestAssert.Equal(checked((ushort)aim), actual.Aim);
        }

        private static void AssertFrameEqual(FrameData left, FrameData right)
        {
            TestAssert.Equal(left.Tick, right.Tick);
            TestAssert.True(left.Roster.HasSameStructure(right.Roster));
            TestAssert.Equal(left.InputCount, right.InputCount);
            for (int slotValue = 0; slotValue < left.InputCount; slotValue++)
            {
                PlayerSlot slot = new PlayerSlot(slotValue);
                InputFrame leftInput = left.GetInput(slot);
                InputFrame rightInput = right.GetInput(slot);
                TestAssert.Equal(leftInput.Tick, rightInput.Tick);
                TestAssert.Equal(leftInput.PlayerSlot, rightInput.PlayerSlot);
                TestAssert.Equal(leftInput.MoveX, rightInput.MoveX);
                TestAssert.Equal(leftInput.MoveZ, rightInput.MoveZ);
                TestAssert.Equal(leftInput.Aim, rightInput.Aim);
            }
        }

        private static void AssertStateEqual(BattleState left, BattleState right)
        {
            TestAssert.Equal(left.Tick, right.Tick);
            TestAssert.True(left.Roster.HasSameStructure(right.Roster));
            TestAssert.Equal(left.PlayerCount, right.PlayerCount);
            for (int slotValue = 0; slotValue < left.PlayerCount; slotValue++)
            {
                PlayerState leftPlayer = left.GetPlayerState(new PlayerSlot(slotValue));
                PlayerState rightPlayer = right.GetPlayerState(new PlayerSlot(slotValue));
                TestAssert.Equal(leftPlayer.PositionX, rightPlayer.PositionX);
                TestAssert.Equal(leftPlayer.PositionZ, rightPlayer.PositionZ);
                TestAssert.Equal(leftPlayer.Aim, rightPlayer.Aim);
            }
        }
    }
}
