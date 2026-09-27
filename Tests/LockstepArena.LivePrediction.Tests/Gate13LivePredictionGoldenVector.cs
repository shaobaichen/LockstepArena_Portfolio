using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Server.LiveTcp;
using LockstepArena.Simulation;

namespace LockstepArena.LivePrediction.Tests
{
    internal sealed class Gate13LivePredictionGoldenResult
    {
        public Gate13LivePredictionGoldenResult(
            BattleState serverState,
            BattleState predictedAuthoritativeState,
            BattleState predictedState,
            BattleState secondClientState,
            BattleState reconstructedState,
            FrameData[] authoritativeFrames,
            bool[] dirtySequence,
            ulong[] authoritativeDigests,
            ulong[] predictionCheckpointDigests,
            int pendingPredictionCount,
            int pendingAuthorityCount,
            int replayFrameCount)
        {
            ServerState = serverState;
            PredictedAuthoritativeState = predictedAuthoritativeState;
            PredictedState = predictedState;
            SecondClientState = secondClientState;
            ReconstructedState = reconstructedState;
            AuthoritativeFrames = authoritativeFrames;
            DirtySequence = dirtySequence;
            AuthoritativeDigests = authoritativeDigests;
            PredictionCheckpointDigests = predictionCheckpointDigests;
            PendingPredictionCount = pendingPredictionCount;
            PendingAuthorityCount = pendingAuthorityCount;
            ReplayFrameCount = replayFrameCount;
        }

        public BattleState ServerState { get; }

        public BattleState PredictedAuthoritativeState { get; }

        public BattleState PredictedState { get; }

        public BattleState SecondClientState { get; }

        public BattleState ReconstructedState { get; }

        public FrameData[] AuthoritativeFrames { get; }

        public bool[] DirtySequence { get; }

        public ulong[] AuthoritativeDigests { get; }

        public ulong[] PredictionCheckpointDigests { get; }

        public int PendingPredictionCount { get; }

        public int PendingAuthorityCount { get; }

        public int ReplayFrameCount { get; }
    }

    internal static class Gate13LivePredictionGoldenVector
    {
        private const int MaxPayloadLength = 4096;

        public static Gate13LivePredictionGoldenResult RunCorrect(
            int serverReadCapacity,
            int predictedClientReadCapacity,
            int secondClientReadCapacity)
        {
            using LiveFixture fixture = CreateFixture(
                serverReadCapacity,
                predictedClientReadCapacity,
                secondClientReadCapacity);

            fixture.Predicted.Update(new LocalInputSample(1, 0, 10100));
            fixture.Second.SendInput(
                fixture.Roster.GetPlayerId(new PlayerSlot(1)),
                new InputFrame(100U, new PlayerSlot(1), 0, 0, 2000));
            TcpSharedBattleSessionTests.ForceNextPollAdvances(fixture.Session, 2U);
            PumpServerUntil(fixture.Session, 1);

            var dirty = new List<bool>();
            var authoritativeDigests = new List<ulong>();
            ReconcilePredictedUntil(
                fixture.Predicted,
                1,
                dirty,
                authoritativeDigests);
            FrameData[] authority = PumpSecondUntil(fixture.Second, 1);

            return CreateResult(
                fixture,
                authority,
                dirty.ToArray(),
                authoritativeDigests.ToArray(),
                Array.Empty<ulong>());
        }

        public static Gate13LivePredictionGoldenResult RunWrong(
            int serverReadCapacity,
            int predictedClientReadCapacity,
            int secondClientReadCapacity)
        {
            using LiveFixture fixture = CreateFixture(
                serverReadCapacity,
                predictedClientReadCapacity,
                secondClientReadCapacity);

            fixture.Predicted.Update(new LocalInputSample(1, 0, 10100));
            fixture.Predicted.Update(new LocalInputSample(0, 1, 10101));
            fixture.Predicted.Update(new LocalInputSample(-1, 0, 10102));
            ulong preAuthorityPrediction = StateDigest.Compute(fixture.Predicted.PredictedState);

            fixture.Second.SendInput(
                fixture.Roster.GetPlayerId(new PlayerSlot(1)),
                new InputFrame(100U, new PlayerSlot(1), -1, 0, 20100));
            TcpSharedBattleSessionTests.ForceNextPollAdvances(fixture.Session, 2U);
            PumpServerUntil(fixture.Session, 1);

            var dirty = new List<bool>();
            var authoritativeDigests = new List<ulong>();
            ReconcilePredictedUntil(
                fixture.Predicted,
                1,
                dirty,
                authoritativeDigests);
            ulong afterDirty100 = StateDigest.Compute(fixture.Predicted.PredictedState);
            var authority = new List<FrameData>(PumpSecondUntil(fixture.Second, 1));

            fixture.Predicted.Update(new LocalInputSample(0, -1, 10103));
            ulong afterPrediction103 = StateDigest.Compute(fixture.Predicted.PredictedState);
            fixture.Second.SendInput(
                fixture.Roster.GetPlayerId(new PlayerSlot(1)),
                new InputFrame(101U, new PlayerSlot(1), 0, -1, 20101));
            fixture.Second.SendInput(
                fixture.Roster.GetPlayerId(new PlayerSlot(1)),
                new InputFrame(102U, new PlayerSlot(1), 1, 0, 20102));
            fixture.Second.SendInput(
                fixture.Roster.GetPlayerId(new PlayerSlot(1)),
                new InputFrame(103U, new PlayerSlot(1), -1, 0, 20100));
            TcpSharedBattleSessionTests.ForceNextPollAdvances(fixture.Session, 3U);
            PumpServerUntil(fixture.Session, 3);

            ReconcilePredictedUntil(
                fixture.Predicted,
                1,
                dirty,
                authoritativeDigests);
            ulong afterDirty101 = StateDigest.Compute(fixture.Predicted.PredictedState);
            ReconcilePredictedUntil(
                fixture.Predicted,
                1,
                dirty,
                authoritativeDigests);
            ulong afterDirty102 = StateDigest.Compute(fixture.Predicted.PredictedState);
            ReconcilePredictedUntil(
                fixture.Predicted,
                1,
                dirty,
                authoritativeDigests);
            authority.AddRange(PumpSecondUntil(fixture.Second, 3));

            return CreateResult(
                fixture,
                authority.ToArray(),
                dirty.ToArray(),
                authoritativeDigests.ToArray(),
                new[]
                {
                    preAuthorityPrediction,
                    afterDirty100,
                    afterPrediction103,
                    afterDirty101,
                    afterDirty102,
                });
        }

        private static LiveFixture CreateFixture(
            int serverReadCapacity,
            int predictedClientReadCapacity,
            int secondClientReadCapacity)
        {
            var roster = new ActiveRoster(new[]
            {
                new PlayerId(0x0102030405060708UL),
                new PlayerId(0x2AUL),
            });
            var initial = new BattleState(
                100U,
                roster,
                new[]
                {
                    new PlayerState(-300, 0, 1000),
                    new PlayerState(300, 0, 2000),
                });
            return new LiveFixture(
                initial,
                serverReadCapacity,
                predictedClientReadCapacity,
                secondClientReadCapacity);
        }

        private static Gate13LivePredictionGoldenResult CreateResult(
            LiveFixture fixture,
            FrameData[] authority,
            bool[] dirty,
            ulong[] authoritativeDigests,
            ulong[] predictionDigests)
        {
            return new Gate13LivePredictionGoldenResult(
                fixture.Session.ServerState,
                fixture.Predicted.AuthoritativeState,
                fixture.Predicted.PredictedState,
                fixture.Second.ClientState,
                fixture.Predicted.ReconstructAuthoritativeState(),
                authority,
                dirty,
                authoritativeDigests,
                predictionDigests,
                fixture.Predicted.PendingPredictionCount,
                fixture.Predicted.PendingAuthoritativeFrameCount,
                fixture.Predicted.ReplayFrameCount);
        }

        private static void PumpServerUntil(TcpSharedBattleSession session, int expectedPublished)
        {
            int published = 0;
            for (int attempt = 0; attempt < 20_000 && published < expectedPublished; attempt++)
            {
                published += session.PumpOnce();
            }

            if (published != expectedPublished)
            {
                throw new InvalidOperationException("The shared session did not publish the expected frames.");
            }
        }

        private static void ReconcilePredictedUntil(
            PredictedTcpClientBattleRuntime runtime,
            int expectedCount,
            List<bool> dirty,
            List<ulong> authoritativeDigests)
        {
            int reconciled = 0;
            for (int attempt = 0; attempt < 20_000 && reconciled < expectedCount; attempt++)
            {
                PredictedClientUpdateResult update = runtime.Update(null);
                for (int index = 0; index < update.ReconciledAuthoritativeFrameCount; index++)
                {
                    dirty.Add(update.DirtyFrameCount > index);
                    authoritativeDigests.Add(StateDigest.Compute(runtime.AuthoritativeState));
                }

                reconciled += update.ReconciledAuthoritativeFrameCount;
            }

            if (reconciled != expectedCount)
            {
                throw new InvalidOperationException("The predicted client did not reconcile the expected frames.");
            }
        }

        private static FrameData[] PumpSecondUntil(
            TcpClientBattlePump client,
            int expectedCount)
        {
            var frames = new List<FrameData>();
            for (int attempt = 0; attempt < 20_000 && frames.Count < expectedCount; attempt++)
            {
                frames.AddRange(client.PumpReceiveOnce());
            }

            if (frames.Count != expectedCount)
            {
                throw new InvalidOperationException("The second client did not receive the expected frames.");
            }

            return frames.ToArray();
        }

        private sealed class LiveFixture : IDisposable
        {
            private readonly TcpListener _listener;

            public LiveFixture(
                BattleState initialState,
                int serverReadCapacity,
                int predictedClientReadCapacity,
                int secondClientReadCapacity)
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start(2);
                int port = ((IPEndPoint)_listener.LocalEndpoint).Port;

                var predictedClient = new TcpClient(AddressFamily.InterNetwork);
                predictedClient.Connect(IPAddress.Loopback, port);
                TcpClient predictedAccepted = _listener.AcceptTcpClient();
                var secondClient = new TcpClient(AddressFamily.InterNetwork);
                secondClient.Connect(IPAddress.Loopback, port);
                TcpClient secondAccepted = _listener.AcceptTcpClient();

                Roster = initialState.Roster;
                Predicted = new PredictedTcpClientBattleRuntime(
                    predictedClient,
                    initialState,
                    Roster.GetPlayerId(new PlayerSlot(0)),
                    new PlayerSlot(0),
                    4,
                    1,
                    8,
                    8,
                    MaxPayloadLength,
                    16,
                    5,
                    predictedClientReadCapacity);
                Second = new TcpClientBattlePump(
                    secondClient,
                    initialState,
                    MaxPayloadLength,
                    16,
                    5,
                    secondClientReadCapacity);
                Session = new TcpSharedBattleSession(
                    initialState,
                    new[]
                    {
                        new TcpBattleParticipantBinding(
                            predictedAccepted,
                            Roster.GetPlayerId(new PlayerSlot(0)),
                            new PlayerSlot(0)),
                        new TcpBattleParticipantBinding(
                            secondAccepted,
                            Roster.GetPlayerId(new PlayerSlot(1)),
                            new PlayerSlot(1)),
                    },
                    2U,
                    8U,
                    8,
                    MaxPayloadLength,
                    16,
                    3,
                    serverReadCapacity);
            }

            public ActiveRoster Roster { get; }

            public PredictedTcpClientBattleRuntime Predicted { get; }

            public TcpClientBattlePump Second { get; }

            public TcpSharedBattleSession Session { get; }

            public void Dispose()
            {
                Session.Dispose();
                Predicted.Dispose();
                Second.Dispose();
                _listener.Stop();
            }
        }
    }
}
