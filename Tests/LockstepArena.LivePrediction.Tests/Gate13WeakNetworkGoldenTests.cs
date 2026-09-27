using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Google.Protobuf;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol;
using LockstepArena.Simulation;

namespace LockstepArena.LivePrediction.Tests
{
    internal static class Gate13WeakNetworkGoldenTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(CorrectLivePredictionGoldenConvergesCleanly), CorrectLivePredictionGoldenConvergesCleanly),
            new TestCase(nameof(WrongRemotePredictionGoldenRollsBackAndConverges), WrongRemotePredictionGoldenRollsBackAndConverges),
            new TestCase(nameof(TerminalPredictionAndFinalAuthorityDoNotWrap), TerminalPredictionAndFinalAuthorityDoNotWrap),
            new TestCase(nameof(TwoWeakNetworkSchedulesProduceIdenticalAuthorityReplayAndFinalDigest), TwoWeakNetworkSchedulesProduceIdenticalAuthorityReplayAndFinalDigest),
        };

        private static void CorrectLivePredictionGoldenConvergesCleanly()
        {
            Gate13LivePredictionGoldenResult result =
                Gate13LivePredictionGoldenVector.RunCorrect(3, 5, 7);

            TestAssert.Equal(1, result.AuthoritativeFrames.Length);
            TestAssert.Equal(1, result.DirtySequence.Length);
            TestAssert.True(!result.DirtySequence[0]);
            TestAssert.Equal(101U, result.ServerState.Tick);
            TestAssert.Equal(101U, result.PredictedAuthoritativeState.Tick);
            TestAssert.Equal(101U, result.PredictedState.Tick);
            TestAssert.Equal(1, result.ReplayFrameCount);
            AssertPlayer(result.PredictedState, 0, -200, 0, 10100);
            AssertPlayer(result.PredictedState, 1, 300, 0, 2000);
            AssertAllFinalStates(result);
            TestAssert.Equal(0x5DB198E1CB8F8ED4UL, StateDigest.Compute(result.ServerState));
        }

        private static void WrongRemotePredictionGoldenRollsBackAndConverges()
        {
            Gate13LivePredictionGoldenResult result =
                Gate13LivePredictionGoldenVector.RunWrong(3, 5, 7);

            TestAssert.Equal(4, result.AuthoritativeFrames.Length);
            AssertTickSequence(result.AuthoritativeFrames, 100U);
            AssertDirty(result.DirtySequence, true, true, true, false);
            AssertDigests(
                result.AuthoritativeDigests,
                0xA64F91C7685696ACUL,
                0x8763814FEEAFD898UL,
                0x3C3A2EFAE2A06B79UL,
                0xC3CAAFCE96D7F832UL);
            AssertDigests(
                result.PredictionCheckpointDigests,
                0x8B8B6B468244E4C7UL,
                0xEDA7579F3B1F2A20UL,
                0x40EADCE076E81EFDUL,
                0xFC6C81B3C49F4B1EUL,
                0xC3CAAFCE96D7F832UL);
            TestAssert.Equal(104U, result.ServerState.Tick);
            TestAssert.Equal(104U, result.PredictedAuthoritativeState.Tick);
            TestAssert.Equal(104U, result.PredictedState.Tick);
            TestAssert.Equal(0, result.PendingPredictionCount);
            TestAssert.Equal(0, result.PendingAuthorityCount);
            TestAssert.Equal(4, result.ReplayFrameCount);
            AssertPlayer(result.PredictedState, 0, -300, 0, 10103);
            AssertPlayer(result.PredictedState, 1, 200, -100, 20100);
            AssertAllFinalStates(result);
            TestAssert.Equal(0xC3CAAFCE96D7F832UL, StateDigest.Compute(result.ServerState));
        }

        private static void TerminalPredictionAndFinalAuthorityDoNotWrap()
        {
            var roster = new ActiveRoster(new[] { new PlayerId(7UL) });
            var initial = new BattleState(
                uint.MaxValue - 1U,
                roster,
                new[] { new PlayerState(0, 0, 100) });
            using var pair = new LoopbackPair();
            using var runtime = new PredictedTcpClientBattleRuntime(
                pair.Client,
                initial,
                new PlayerId(7UL),
                new PlayerSlot(0),
                1,
                1,
                1,
                1,
                4096,
                256,
                3,
                253);

            PredictedClientUpdateResult predicted = runtime.Update(
                new LocalInputSample(1, 0, 101));
            TestAssert.True(predicted.LocalPredictionSent);
            FrameData finalAuthority = FrameData.Create(
                roster,
                uint.MaxValue - 1U,
                new[]
                {
                    new InputFrame(uint.MaxValue - 1U, new PlayerSlot(0), 1, 0, 101),
                });
            byte[] payload = ProtocolMapper.ToWire(finalAuthority).ToByteArray();
            byte[] framed = TcpSharedBattleSessionTests.Frame(payload);
            pair.Accepted.GetStream().Write(framed, 0, framed.Length);
            TcpSharedBattleSessionTests.WaitForReadable(pair.Client.Client);

            PredictedClientUpdateResult reconciled = runtime.Update(null);
            TestAssert.Equal(1, reconciled.ReconciledAuthoritativeFrameCount);
            TestAssert.Equal(uint.MaxValue, runtime.AuthoritativeState.Tick);
            TestAssert.Equal(uint.MaxValue, runtime.PredictedState.Tick);
            PredictedClientUpdateResult terminal = runtime.Update(
                new LocalInputSample(1, 0, 102));
            TestAssert.True(!terminal.LocalPredictionSent);
            TestAssert.Equal(uint.MaxValue, runtime.PredictedState.Tick);
        }

        private static void TwoWeakNetworkSchedulesProduceIdenticalAuthorityReplayAndFinalDigest()
        {
            Gate13LivePredictionGoldenResult first =
                Gate13LivePredictionGoldenVector.RunWrong(3, 5, 7);
            Gate13LivePredictionGoldenResult second =
                Gate13LivePredictionGoldenVector.RunWrong(1, 2, 3);

            TestAssert.Equal(first.AuthoritativeFrames.Length, second.AuthoritativeFrames.Length);
            for (int index = 0; index < first.AuthoritativeFrames.Length; index++)
            {
                TcpSharedBattleSessionTests.AssertFramesEqual(
                    first.AuthoritativeFrames[index],
                    second.AuthoritativeFrames[index]);
            }

            AssertDirty(first.DirtySequence, second.DirtySequence);
            PredictedTcpClientBattleRuntimeTests.AssertStatesEqual(
                first.ServerState,
                second.ServerState);
            PredictedTcpClientBattleRuntimeTests.AssertStatesEqual(
                first.ReconstructedState,
                second.ReconstructedState);
            TestAssert.Equal(
                StateDigest.Compute(first.ServerState),
                StateDigest.Compute(second.ServerState));
            TestAssert.Equal(0xC3CAAFCE96D7F832UL, StateDigest.Compute(second.ServerState));
        }

        private static void AssertAllFinalStates(Gate13LivePredictionGoldenResult result)
        {
            PredictedTcpClientBattleRuntimeTests.AssertStatesEqual(
                result.ServerState,
                result.PredictedAuthoritativeState);
            PredictedTcpClientBattleRuntimeTests.AssertStatesEqual(
                result.ServerState,
                result.PredictedState);
            PredictedTcpClientBattleRuntimeTests.AssertStatesEqual(
                result.ServerState,
                result.SecondClientState);
            PredictedTcpClientBattleRuntimeTests.AssertStatesEqual(
                result.ServerState,
                result.ReconstructedState);
        }

        private static void AssertPlayer(
            BattleState state,
            int slotValue,
            int x,
            int z,
            ushort aim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slotValue));
            TestAssert.Equal(x, player.PositionX);
            TestAssert.Equal(z, player.PositionZ);
            TestAssert.Equal(aim, player.Aim);
        }

        private static void AssertTickSequence(FrameData[] frames, uint firstTick)
        {
            for (int index = 0; index < frames.Length; index++)
            {
                TestAssert.Equal(firstTick + (uint)index, frames[index].Tick);
            }
        }

        private static void AssertDirty(bool[] actual, params bool[] expected)
        {
            TestAssert.Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                TestAssert.Equal(expected[index], actual[index]);
            }
        }

        private static void AssertDigests(ulong[] actual, params ulong[] expected)
        {
            TestAssert.Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                TestAssert.Equal(expected[index], actual[index]);
            }
        }
    }
}
