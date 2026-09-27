using System;
using System.Collections.Generic;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal sealed class CoordinatorRunResult
    {
        public CoordinatorRunResult(
            int[] publicationBatchSizes,
            FrameData[] publishedFrames,
            FrameData[] history,
            BattleState finalState,
            ulong[] digestsAfterEachFrame)
        {
            PublicationBatchSizes = publicationBatchSizes;
            PublishedFrames = publishedFrames;
            History = history;
            FinalState = finalState;
            DigestsAfterEachFrame = digestsAfterEachFrame;
        }

        public int[] PublicationBatchSizes { get; }

        public FrameData[] PublishedFrames { get; }

        public FrameData[] History { get; }

        public BattleState FinalState { get; }

        public ulong[] DigestsAfterEachFrame { get; }
    }

    internal static class Gate4MultiTickGoldenVector
    {
        private static readonly int[] CoordinatorATickOffsets = { 3, 2, 1, 0 };
        private static readonly int[] CoordinatorASlotOrder = { 2, 0, 3, 1 };
        private static readonly int[] CoordinatorBSlotPasses = { 1, 3, 0, 2 };
        private static readonly int[] CoordinatorBTickOffsets = { 2, 0, 3, 1 };

        internal static CoordinatorRunResult RunCoordinatorA()
        {
            return Run(true);
        }

        internal static CoordinatorRunResult RunCoordinatorB()
        {
            return Run(false);
        }

        internal static (
            AuthoritativeFrameCoordinator Coordinator,
            BattleSimulation Simulation,
            FrameData[] Publication) CreateCoordinatorAFirstBlock()
        {
            ActiveRoster coordinatorRoster = CreateRoster();
            AuthoritativeFrameCoordinator coordinator = new AuthoritativeFrameCoordinator(
                coordinatorRoster,
                0U,
                3U,
                5);
            BattleSimulation simulation = CreateSimulation();
            FrameData[] publication = SubmitCoordinatorABlock(
                coordinator,
                coordinatorRoster,
                0U,
                IgnorePublication);
            return (coordinator, simulation, publication);
        }

        private static CoordinatorRunResult Run(bool useCoordinatorA)
        {
            ActiveRoster coordinatorRoster = CreateRoster();
            AuthoritativeFrameCoordinator coordinator = new AuthoritativeFrameCoordinator(
                coordinatorRoster,
                0U,
                3U,
                5);
            BattleSimulation simulation = CreateSimulation();
            List<int> batchSizes = new List<int>();
            List<FrameData> publishedFrames = new List<FrameData>();
            List<ulong> digests = new List<ulong>();

            for (uint blockStart = 0U; blockStart < 12U; blockStart += 4U)
            {
                void Record(FrameData[] publication)
                {
                    RecordPublication(publication, simulation, batchSizes, publishedFrames, digests);
                }

                if (useCoordinatorA)
                {
                    SubmitCoordinatorABlock(coordinator, coordinatorRoster, blockStart, Record);
                }
                else
                {
                    SubmitCoordinatorBBlock(coordinator, coordinatorRoster, blockStart, Record);
                }
            }

            return new CoordinatorRunResult(
                batchSizes.ToArray(),
                publishedFrames.ToArray(),
                coordinator.GetAuthoritativeHistorySnapshot(),
                simulation.State,
                digests.ToArray());
        }

        private static FrameData[] SubmitCoordinatorABlock(
            AuthoritativeFrameCoordinator coordinator,
            ActiveRoster roster,
            uint blockStart,
            Action<FrameData[]> onPublication)
        {
            FrameData[] publication = Array.Empty<FrameData>();
            for (int offsetIndex = 0; offsetIndex < CoordinatorATickOffsets.Length; offsetIndex++)
            {
                uint tick = blockStart + (uint)CoordinatorATickOffsets[offsetIndex];
                InputFrame[] inputs = CreateInputs(tick);
                for (int orderIndex = 0; orderIndex < CoordinatorASlotOrder.Length; orderIndex++)
                {
                    int slotValue = CoordinatorASlotOrder[orderIndex];
                    PlayerSlot slot = new PlayerSlot(slotValue);
                    FrameData[] candidate = coordinator.Submit(roster.GetPlayerId(slot), inputs[slotValue]);
                    if (candidate.Length > 0)
                    {
                        publication = candidate;
                        onPublication(candidate);
                    }
                }
            }

            return publication;
        }

        private static void SubmitCoordinatorBBlock(
            AuthoritativeFrameCoordinator coordinator,
            ActiveRoster roster,
            uint blockStart,
            Action<FrameData[]> onPublication)
        {
            for (int passIndex = 0; passIndex < CoordinatorBSlotPasses.Length; passIndex++)
            {
                int slotValue = CoordinatorBSlotPasses[passIndex];
                PlayerSlot slot = new PlayerSlot(slotValue);
                for (int offsetIndex = 0; offsetIndex < CoordinatorBTickOffsets.Length; offsetIndex++)
                {
                    uint tick = blockStart + (uint)CoordinatorBTickOffsets[offsetIndex];
                    InputFrame input = CreateInputs(tick)[slotValue];
                    FrameData[] batch = coordinator.Submit(roster.GetPlayerId(slot), input);
                    if (batch.Length > 0)
                    {
                        onPublication(batch);
                    }
                }
            }
        }

        private static void IgnorePublication(FrameData[] publication)
        {
        }

        private static void RecordPublication(
            FrameData[] publication,
            BattleSimulation simulation,
            List<int> batchSizes,
            List<FrameData> publishedFrames,
            List<ulong> digests)
        {
            if (publication.Length == 0)
            {
                return;
            }

            batchSizes.Add(publication.Length);
            for (int index = 0; index < publication.Length; index++)
            {
                FrameData frame = publication[index];
                publishedFrames.Add(frame);
                simulation.Step(frame);
                digests.Add(StateDigest.Compute(simulation.State));
            }
        }

        private static BattleSimulation CreateSimulation()
        {
            ActiveRoster roster = CreateRoster();
            return new BattleSimulation(BattleState.CreateInitial(roster, new[]
            {
                new PlayerState(-1_000, 0, 0),
                new PlayerState(1_000, 0, 0),
                new PlayerState(0, -1_000, 0),
                new PlayerState(0, 1_000, 0),
            }));
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
            return new[]
            {
                new InputFrame(tick, new PlayerSlot(0), 1, 0, unchecked((ushort)((tick * 1_000U) + 1U))),
                new InputFrame(tick, new PlayerSlot(1), -1, 0, unchecked((ushort)((tick * 2_000U) + 2U))),
                new InputFrame(tick, new PlayerSlot(2), 0, 1, unchecked((ushort)((tick * 3_000U) + 3U))),
                new InputFrame(tick, new PlayerSlot(3), 0, -1, unchecked((ushort)((tick * 4_000U) + 4U))),
            };
        }
    }
}
