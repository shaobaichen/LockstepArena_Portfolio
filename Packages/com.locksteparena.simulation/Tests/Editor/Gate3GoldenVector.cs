using System;

namespace LockstepArena.Simulation.Verification
{
    public readonly struct Gate3GoldenVectorResult
    {
        public Gate3GoldenVectorResult(BattleState state, ulong digest)
        {
            State = state;
            Digest = digest;
        }

        public BattleState State { get; }

        public ulong Digest { get; }
    }

    public static class Gate3GoldenVector
    {
        public const uint TickCount = 1_000;

        private static readonly int[][] SubmissionOrders =
        {
            new[] { 2, 0, 3, 1 },
            new[] { 1, 3, 0, 2 },
            new[] { 3, 2, 1, 0 },
            new[] { 0, 2, 1, 3 },
        };

        public static Gate3GoldenVectorResult Run()
        {
            ActiveRoster stateRoster = CreateRoster();
            ActiveRoster frameRoster = CreateRoster();
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial(stateRoster, new[]
            {
                new PlayerState(-1_000, 0, 0),
                new PlayerState(1_000, 0, 0),
                new PlayerState(0, -1_000, 0),
                new PlayerState(0, 1_000, 0),
            }));

            for (uint tick = 0; tick < TickCount; tick++)
            {
                InputFrame[] inputs = CreateInputs(tick);
                StrictFrameCollector collector = new StrictFrameCollector(frameRoster, tick);
                int[] order = SubmissionOrders[tick % (uint)SubmissionOrders.Length];
                for (int index = 0; index < order.Length; index++)
                {
                    int slotValue = order[index];
                    PlayerSlot slot = new PlayerSlot(slotValue);
                    bool completed = collector.Submit(frameRoster.GetPlayerId(slot), inputs[slotValue]);
                    if (completed != (index == order.Length - 1))
                    {
                        throw new InvalidOperationException("Collector completion did not match the final accepted input.");
                    }
                }

                simulation.Step(collector.GetCompletedFrame());
            }

            BattleState state = simulation.State;
            return new Gate3GoldenVectorResult(state, StateDigest.Compute(state));
        }

        private static ActiveRoster CreateRoster()
        {
            return new ActiveRoster(new[]
            {
                new PlayerId(0x0102030405060708UL),
                new PlayerId(0x000000000000002AUL),
                new PlayerId(0xFFEEDDCCBBAA0099UL),
                new PlayerId(0x00000000000F4243UL),
            });
        }

        private static InputFrame[] CreateInputs(uint tick)
        {
            sbyte[] moveX = new sbyte[4];
            sbyte[] moveZ = new sbyte[4];
            int phase = (int)(tick % 400U);
            if (phase < 100)
            {
                moveX[0] = 1;
                moveX[1] = -1;
                moveZ[2] = 1;
                moveZ[3] = -1;
            }
            else if (phase >= 150 && phase < 250)
            {
                moveX[0] = -1;
                moveX[1] = 1;
                moveZ[2] = -1;
                moveZ[3] = 1;
            }
            else if (phase >= 250 && phase < 325)
            {
                moveZ[0] = 1;
                moveZ[1] = -1;
                moveX[2] = 1;
                moveX[3] = -1;
            }
            else if (phase >= 325)
            {
                moveZ[0] = -1;
                moveZ[1] = 1;
                moveX[2] = -1;
                moveX[3] = 1;
            }

            return new[]
            {
                new InputFrame(
                    tick,
                    new PlayerSlot(0),
                    moveX[0],
                    moveZ[0],
                    unchecked((ushort)((tick * 997U) + 123U))),
                new InputFrame(
                    tick,
                    new PlayerSlot(1),
                    moveX[1],
                    moveZ[1],
                    unchecked((ushort)((tick * 619U) + 45_678U))),
                new InputFrame(
                    tick,
                    new PlayerSlot(2),
                    moveX[2],
                    moveZ[2],
                    unchecked((ushort)((tick * 313U) + 777U))),
                new InputFrame(
                    tick,
                    new PlayerSlot(3),
                    moveX[3],
                    moveZ[3],
                    unchecked((ushort)((tick * 1_597U) + 40_000U))),
            };
        }
    }
}
