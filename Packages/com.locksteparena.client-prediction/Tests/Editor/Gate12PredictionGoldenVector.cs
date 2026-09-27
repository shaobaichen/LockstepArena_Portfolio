using LockstepArena.Simulation;

namespace LockstepArena.Client.Prediction.Verification
{
    public sealed class Gate12PredictionGoldenResult
    {
        public Gate12PredictionGoldenResult(
            BattleState[] predictedStatesAfterPredict,
            bool[] dirtyResults,
            BattleState[] authoritativeStatesAfterReconcile,
            BattleState[] predictedStatesAfterReconcile,
            int[] pendingCountsAfterReconcile)
        {
            PredictedStatesAfterPredict = predictedStatesAfterPredict;
            DirtyResults = dirtyResults;
            AuthoritativeStatesAfterReconcile = authoritativeStatesAfterReconcile;
            PredictedStatesAfterReconcile = predictedStatesAfterReconcile;
            PendingCountsAfterReconcile = pendingCountsAfterReconcile;
        }

        public BattleState[] PredictedStatesAfterPredict { get; }

        public bool[] DirtyResults { get; }

        public BattleState[] AuthoritativeStatesAfterReconcile { get; }

        public BattleState[] PredictedStatesAfterReconcile { get; }

        public int[] PendingCountsAfterReconcile { get; }
    }

    public static class Gate12PredictionGoldenVector
    {
        public static Gate12PredictionGoldenResult RunCorrect()
        {
            return Run(useWrongTick101Prediction: false);
        }

        public static Gate12PredictionGoldenResult RunWrong()
        {
            return Run(useWrongTick101Prediction: true);
        }

        private static Gate12PredictionGoldenResult Run(bool useWrongTick101Prediction)
        {
            ActiveRoster roster = CreateRoster();
            var timeline = new ClientPredictionTimeline(CreateInitialState(roster), 3);
            FrameData[] authoritativeFrames = CreateAuthoritativeFrames(roster);
            FrameData[] predictedFrames = CreatePredictedFrames(
                roster,
                authoritativeFrames,
                useWrongTick101Prediction);
            var predictedStatesAfterPredict = new BattleState[3];
            var dirtyResults = new bool[3];
            var authoritativeStatesAfterReconcile = new BattleState[3];
            var predictedStatesAfterReconcile = new BattleState[3];
            var pendingCountsAfterReconcile = new int[3];

            for (int index = 0; index < predictedFrames.Length; index++)
            {
                timeline.Predict(predictedFrames[index]);
                predictedStatesAfterPredict[index] = timeline.PredictedState;
            }

            for (int index = 0; index < authoritativeFrames.Length; index++)
            {
                dirtyResults[index] = timeline.ReconcileAuthoritative(authoritativeFrames[index]);
                authoritativeStatesAfterReconcile[index] = timeline.AuthoritativeState;
                predictedStatesAfterReconcile[index] = timeline.PredictedState;
                pendingCountsAfterReconcile[index] = timeline.PendingPredictionCount;
            }

            return new Gate12PredictionGoldenResult(
                predictedStatesAfterPredict,
                dirtyResults,
                authoritativeStatesAfterReconcile,
                predictedStatesAfterReconcile,
                pendingCountsAfterReconcile);
        }

        private static ActiveRoster CreateRoster()
        {
            return new ActiveRoster(
                new[]
                {
                    new PlayerId(0x0102030405060708UL),
                    new PlayerId(0x000000000000002AUL),
                    new PlayerId(0xFFEEDDCCBBAA0099UL),
                    new PlayerId(0x00000000000F4243UL),
                });
        }

        private static BattleState CreateInitialState(ActiveRoster roster)
        {
            return new BattleState(
                100U,
                roster,
                new[]
                {
                    new PlayerState(-300, 0, 1000),
                    new PlayerState(300, 0, 2000),
                    new PlayerState(0, -300, 3000),
                    new PlayerState(0, 300, 4000),
                });
        }

        private static FrameData[] CreateAuthoritativeFrames(ActiveRoster roster)
        {
            return new[]
            {
                CreateFrame(roster, 100U, new sbyte[] { 1, -1, 0, 0 }, new sbyte[] { 0, 0, 1, -1 }, new ushort[] { 10100, 20100, 30100, 40100 }),
                CreateFrame(roster, 101U, new sbyte[] { 0, 0, 1, -1 }, new sbyte[] { 1, -1, 0, 0 }, new ushort[] { 10101, 20101, 30101, 40101 }),
                CreateFrame(roster, 102U, new sbyte[] { -1, 1, 0, 0 }, new sbyte[] { 0, 0, -1, 1 }, new ushort[] { 10102, 20102, 30102, 40102 }),
            };
        }

        private static FrameData[] CreatePredictedFrames(
            ActiveRoster roster,
            FrameData[] authoritativeFrames,
            bool useWrongTick101Prediction)
        {
            if (!useWrongTick101Prediction)
            {
                return new[]
                {
                    authoritativeFrames[0],
                    authoritativeFrames[1],
                    authoritativeFrames[2],
                };
            }

            return new[]
            {
                authoritativeFrames[0],
                CreateFrame(roster, 101U, new sbyte[] { 0, 0, -1, -1 }, new sbyte[] { 1, -1, 0, 0 }, new ushort[] { 10101, 20101, 30101, 40101 }),
                authoritativeFrames[2],
            };
        }

        private static FrameData CreateFrame(
            ActiveRoster roster,
            uint tick,
            sbyte[] moveX,
            sbyte[] moveZ,
            ushort[] aim)
        {
            var inputs = new InputFrame[roster.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                inputs[index] = new InputFrame(
                    tick,
                    new PlayerSlot(index),
                    moveX[index],
                    moveZ[index],
                    aim[index]);
            }

            return FrameData.Create(roster, tick, inputs);
        }
    }
}
