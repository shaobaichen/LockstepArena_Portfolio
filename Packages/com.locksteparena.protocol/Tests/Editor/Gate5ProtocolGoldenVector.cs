using Google.Protobuf;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Protocol.Verification
{
    public sealed class Gate5ProtocolGoldenResult
    {
        public Gate5ProtocolGoldenResult(
            FrameData[] mappedFramesA,
            FrameData[] mappedFramesB,
            byte[][] serializedFramesA,
            byte[][] serializedFramesB,
            ulong[] digestsA,
            ulong[] digestsB,
            BattleState finalStateA,
            BattleState finalStateB)
        {
            MappedFramesA = mappedFramesA;
            MappedFramesB = mappedFramesB;
            SerializedFramesA = serializedFramesA;
            SerializedFramesB = serializedFramesB;
            DigestsA = digestsA;
            DigestsB = digestsB;
            FinalStateA = finalStateA;
            FinalStateB = finalStateB;
        }

        public FrameData[] MappedFramesA { get; }

        public FrameData[] MappedFramesB { get; }

        public byte[][] SerializedFramesA { get; }

        public byte[][] SerializedFramesB { get; }

        public ulong[] DigestsA { get; }

        public ulong[] DigestsB { get; }

        public BattleState FinalStateA { get; }

        public BattleState FinalStateB { get; }
    }

    public static class Gate5ProtocolGoldenVector
    {
        private static readonly int[] WireOrderA = { 2, 0, 3, 1 };
        private static readonly int[] WireOrderB = { 1, 3, 0, 2 };

        public static Gate5ProtocolGoldenResult Run()
        {
            RunResult runA = RunRoundTrip(WireOrderA);
            RunResult runB = RunRoundTrip(WireOrderB);
            return new Gate5ProtocolGoldenResult(
                runA.MappedFrames,
                runB.MappedFrames,
                runA.SerializedFrames,
                runB.SerializedFrames,
                runA.Digests,
                runB.Digests,
                runA.FinalState,
                runB.FinalState);
        }

        private static RunResult RunRoundTrip(int[] wireOrder)
        {
            ActiveRoster roster = CreateRoster();
            BattleSimulation simulation = CreateSimulation(roster);
            var frames = new FrameData[12];
            var serialized = new byte[12][];
            var digests = new ulong[12];

            for (uint tick = 0U; tick < 12U; tick++)
            {
                FrameData domainFrame = CreateFrame(roster, tick);
                AuthoritativeFrameMessage wire = ReorderInputs(
                    ProtocolMapper.ToWire(domainFrame),
                    wireOrder);
                byte[] bytes = wire.ToByteArray();
                AuthoritativeFrameMessage parsed = AuthoritativeFrameMessage.Parser.ParseFrom(bytes);
                FrameData mapped = ProtocolMapper.ToDomain(parsed, roster);

                simulation.Step(mapped);
                int index = checked((int)tick);
                frames[index] = mapped;
                serialized[index] = bytes;
                digests[index] = StateDigest.Compute(simulation.State);
            }

            return new RunResult(frames, serialized, digests, simulation.State);
        }

        private static AuthoritativeFrameMessage ReorderInputs(
            AuthoritativeFrameMessage canonical,
            int[] wireOrder)
        {
            var reordered = new AuthoritativeFrameMessage
            {
                Tick = canonical.Tick,
                Roster = canonical.Roster,
            };
            for (int index = 0; index < wireOrder.Length; index++)
            {
                reordered.Inputs.Add(canonical.Inputs[wireOrder[index]]);
            }

            return reordered;
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

        private static BattleSimulation CreateSimulation(ActiveRoster roster)
        {
            return new BattleSimulation(BattleState.CreateInitial(roster, new[]
            {
                new PlayerState(-1_000, 0, 0),
                new PlayerState(1_000, 0, 0),
                new PlayerState(0, -1_000, 0),
                new PlayerState(0, 1_000, 0),
            }));
        }

        private static FrameData CreateFrame(ActiveRoster roster, uint tick)
        {
            return FrameData.Create(roster, tick, new[]
            {
                new InputFrame(tick, new PlayerSlot(0), 1, 0, unchecked((ushort)((tick * 1_000U) + 1U))),
                new InputFrame(tick, new PlayerSlot(1), -1, 0, unchecked((ushort)((tick * 2_000U) + 2U))),
                new InputFrame(tick, new PlayerSlot(2), 0, 1, unchecked((ushort)((tick * 3_000U) + 3U))),
                new InputFrame(tick, new PlayerSlot(3), 0, -1, unchecked((ushort)((tick * 4_000U) + 4U))),
            });
        }

        private sealed class RunResult
        {
            public RunResult(
                FrameData[] mappedFrames,
                byte[][] serializedFrames,
                ulong[] digests,
                BattleState finalState)
            {
                MappedFrames = mappedFrames;
                SerializedFrames = serializedFrames;
                Digests = digests;
                FinalState = finalState;
            }

            public FrameData[] MappedFrames { get; }

            public byte[][] SerializedFrames { get; }

            public ulong[] Digests { get; }

            public BattleState FinalState { get; }
        }
    }
}
