using System;
using System.Collections.Generic;
using LockstepArena.Server.FrameSync;
using LockstepArena.Simulation;

namespace LockstepArena.Server.TickAuthority.Tests
{
    internal sealed class Gate9GoldenResult
    {
        public Gate9GoldenResult(
            FrameData[][] publicationBatches,
            FrameData[] authoritativeFrames,
            BattleState[] simulationStates,
            ulong[] digests,
            FrameData[] history,
            BattleState finalState,
            ulong collectionTick,
            uint? eligibilityCeiling,
            uint nextPublishTick)
        {
            PublicationBatches = publicationBatches;
            AuthoritativeFrames = authoritativeFrames;
            SimulationStates = simulationStates;
            Digests = digests;
            History = history;
            FinalState = finalState;
            CollectionTick = collectionTick;
            EligibilityCeiling = eligibilityCeiling;
            NextPublishTick = nextPublishTick;
        }

        public FrameData[][] PublicationBatches { get; }

        public FrameData[] AuthoritativeFrames { get; }

        public BattleState[] SimulationStates { get; }

        public ulong[] Digests { get; }

        public FrameData[] History { get; }

        public BattleState FinalState { get; }

        public ulong CollectionTick { get; }

        public uint? EligibilityCeiling { get; }

        public uint NextPublishTick { get; }
    }

    internal static class Gate9TickAuthorityGoldenVector
    {
        internal static Gate9GoldenResult RunTwoPlayer()
        {
            ActiveRoster roster = new ActiveRoster(new[]
            {
                new PlayerId(900UL),
                new PlayerId(7UL),
            });
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 10U, 2U, 4U, 2);
            BattleSimulation simulation = new BattleSimulation(new BattleState(10U, roster, new[]
            {
                new PlayerState(0, 0, 100),
                new PlayerState(0, 0, 200),
            }));
            List<FrameData[]> batches = new List<FrameData[]>();
            List<FrameData> frames = new List<FrameData>();
            List<BattleState> states = new List<BattleState>();
            List<ulong> digests = new List<ulong>();

            Record(
                publisher.Submit(
                    roster.GetPlayerId(new PlayerSlot(1)),
                    new InputFrame(10U, new PlayerSlot(1), -1, 0, 201)),
                simulation,
                batches,
                frames,
                states,
                digests);
            Record(
                publisher.Submit(
                    roster.GetPlayerId(new PlayerSlot(0)),
                    new InputFrame(10U, new PlayerSlot(0), 1, 0, 101)),
                simulation,
                batches,
                frames,
                states,
                digests);
            Record(publisher.AdvanceOneTick(), simulation, batches, frames, states, digests);
            Record(publisher.AdvanceOneTick(), simulation, batches, frames, states, digests);

            return CreateResult(publisher, simulation, batches, frames, states, digests);
        }

        internal static Gate9GoldenResult RunThreePlayer()
        {
            ActiveRoster roster = new ActiveRoster(new[]
            {
                new PlayerId(500UL),
                new PlayerId(1UL),
                new PlayerId(300UL),
            });
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 20U, 1U, 4U, 2);
            BattleSimulation simulation = new BattleSimulation(new BattleState(20U, roster, new[]
            {
                new PlayerState(0, 0, 1000),
                new PlayerState(0, 0, 2000),
                new PlayerState(0, 0, 3000),
            }));
            List<FrameData[]> batches = new List<FrameData[]>();
            List<FrameData> frames = new List<FrameData>();
            List<BattleState> states = new List<BattleState>();
            List<ulong> digests = new List<ulong>();

            Record(
                publisher.Submit(
                    roster.GetPlayerId(new PlayerSlot(2)),
                    new InputFrame(20U, new PlayerSlot(2), -1, 0, 3001)),
                simulation,
                batches,
                frames,
                states,
                digests);
            Record(
                publisher.Submit(
                    roster.GetPlayerId(new PlayerSlot(0)),
                    new InputFrame(20U, new PlayerSlot(0), 1, 0, 1001)),
                simulation,
                batches,
                frames,
                states,
                digests);
            Record(publisher.AdvanceOneTick(), simulation, batches, frames, states, digests);
            Record(
                publisher.Submit(
                    roster.GetPlayerId(new PlayerSlot(1)),
                    new InputFrame(20U, new PlayerSlot(1), 0, 1, 2001)),
                simulation,
                batches,
                frames,
                states,
                digests);

            return CreateResult(publisher, simulation, batches, frames, states, digests);
        }

        internal static Gate9GoldenResult RunFourPlayerPrimary()
        {
            return RunFourPlayer(
                new[] { 3, 1, 0, 2 },
                new[] { 2, 0, 3, 1 },
                new[] { 1, 3, 0, 2 },
                new[] { 0, 2, 1 });
        }

        internal static Gate9GoldenResult RunFourPlayerAlternative()
        {
            return RunFourPlayer(
                new[] { 0, 2, 1, 3 },
                new[] { 1, 3, 0, 2 },
                new[] { 2, 0, 3, 1 },
                new[] { 1, 0, 2 });
        }

        private static Gate9GoldenResult RunFourPlayer(
            int[] tick101Order,
            int[] tick102Order,
            int[] tick103Order,
            int[] tick100PartialOrder)
        {
            ActiveRoster roster = CreateFourPlayerRoster();
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 100U, 2U, 8U, 3);
            BattleSimulation simulation = new BattleSimulation(new BattleState(100U, roster, new[]
            {
                new PlayerState(-300, 0, 1000),
                new PlayerState(300, 0, 2000),
                new PlayerState(0, -300, 3000),
                new PlayerState(0, 300, 4000),
            }));
            List<FrameData[]> batches = new List<FrameData[]>();
            List<FrameData> frames = new List<FrameData>();
            List<BattleState> states = new List<BattleState>();
            List<ulong> digests = new List<ulong>();

            SubmitTick(publisher, roster, CreateFourPlayerInputs(101U), tick101Order, simulation, batches, frames, states, digests);
            SubmitTick(publisher, roster, CreateFourPlayerInputs(102U), tick102Order, simulation, batches, frames, states, digests);
            SubmitTick(publisher, roster, CreateFourPlayerInputs(103U), tick103Order, simulation, batches, frames, states, digests);
            SubmitTick(publisher, roster, CreateFourPlayerInputs(100U), tick100PartialOrder, simulation, batches, frames, states, digests);
            Record(publisher.AdvanceOneTick(), simulation, batches, frames, states, digests);
            Record(publisher.AdvanceOneTick(), simulation, batches, frames, states, digests);
            Record(publisher.AdvanceOneTick(), simulation, batches, frames, states, digests);
            InputFrame finalGapInput = CreateFourPlayerInputs(100U)[3];
            Record(
                publisher.Submit(roster.GetPlayerId(finalGapInput.PlayerSlot), finalGapInput),
                simulation,
                batches,
                frames,
                states,
                digests);
            Record(publisher.AdvanceOneTick(), simulation, batches, frames, states, digests);
            Record(publisher.AdvanceOneTick(), simulation, batches, frames, states, digests);

            return CreateResult(publisher, simulation, batches, frames, states, digests);
        }

        private static ActiveRoster CreateFourPlayerRoster()
        {
            return new ActiveRoster(new[]
            {
                new PlayerId(0x0102030405060708UL),
                new PlayerId(0x000000000000002AUL),
                new PlayerId(0xFFEEDDCCBBAA0099UL),
                new PlayerId(0x00000000000F4243UL),
            });
        }

        private static InputFrame[] CreateFourPlayerInputs(uint tick)
        {
            if (tick == 100U)
            {
                return new[]
                {
                    new InputFrame(tick, new PlayerSlot(0), 1, 0, 10100),
                    new InputFrame(tick, new PlayerSlot(1), -1, 0, 20100),
                    new InputFrame(tick, new PlayerSlot(2), 0, 1, 30100),
                    new InputFrame(tick, new PlayerSlot(3), 0, -1, 40100),
                };
            }

            if (tick == 101U)
            {
                return new[]
                {
                    new InputFrame(tick, new PlayerSlot(0), 0, 1, 10101),
                    new InputFrame(tick, new PlayerSlot(1), 0, -1, 20101),
                    new InputFrame(tick, new PlayerSlot(2), 1, 0, 30101),
                    new InputFrame(tick, new PlayerSlot(3), -1, 0, 40101),
                };
            }

            if (tick == 102U)
            {
                return new[]
                {
                    new InputFrame(tick, new PlayerSlot(0), -1, 0, 10102),
                    new InputFrame(tick, new PlayerSlot(1), 1, 0, 20102),
                    new InputFrame(tick, new PlayerSlot(2), 0, -1, 30102),
                    new InputFrame(tick, new PlayerSlot(3), 0, 1, 40102),
                };
            }

            return new[]
            {
                new InputFrame(tick, new PlayerSlot(0), 0, -1, 10103),
                new InputFrame(tick, new PlayerSlot(1), 0, 1, 20103),
                new InputFrame(tick, new PlayerSlot(2), -1, 0, 30103),
                new InputFrame(tick, new PlayerSlot(3), 1, 0, 40103),
            };
        }

        private static void SubmitTick(
            TickDrivenFramePublisher publisher,
            ActiveRoster roster,
            InputFrame[] inputs,
            int[] slotOrder,
            BattleSimulation simulation,
            List<FrameData[]> batches,
            List<FrameData> frames,
            List<BattleState> states,
            List<ulong> digests)
        {
            for (int index = 0; index < slotOrder.Length; index++)
            {
                InputFrame input = inputs[slotOrder[index]];
                Record(
                    publisher.Submit(roster.GetPlayerId(input.PlayerSlot), input),
                    simulation,
                    batches,
                    frames,
                    states,
                    digests);
            }
        }

        private static void Record(
            FrameData[] publication,
            BattleSimulation simulation,
            List<FrameData[]> batches,
            List<FrameData> frames,
            List<BattleState> states,
            List<ulong> digests)
        {
            if (publication.Length == 0)
            {
                return;
            }

            batches.Add(publication);
            for (int index = 0; index < publication.Length; index++)
            {
                FrameData frame = publication[index];
                frames.Add(frame);
                simulation.Step(frame);
                states.Add(simulation.State);
                digests.Add(StateDigest.Compute(simulation.State));
            }
        }

        private static Gate9GoldenResult CreateResult(
            TickDrivenFramePublisher publisher,
            BattleSimulation simulation,
            List<FrameData[]> batches,
            List<FrameData> frames,
            List<BattleState> states,
            List<ulong> digests)
        {
            return new Gate9GoldenResult(
                batches.ToArray(),
                frames.ToArray(),
                states.ToArray(),
                digests.ToArray(),
                publisher.GetAuthoritativeHistorySnapshot(),
                simulation.State,
                publisher.CollectionTick,
                publisher.EligibilityCeiling,
                publisher.NextPublishTick);
        }
    }
}
