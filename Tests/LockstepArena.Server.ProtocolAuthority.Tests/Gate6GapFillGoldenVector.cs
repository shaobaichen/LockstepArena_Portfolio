using System;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.ProtocolAuthority.Tests
{
    internal sealed class Gate6GapFillGoldenResult
    {
        public Gate6GapFillGoldenResult(
            int[] preGapOutputLengths,
            byte[][] authoritativePayloads,
            FrameData[] authoritativeFrames,
            BattleState[] clientStates,
            ulong[] clientDigests,
            BattleState serverState,
            BattleState clientState,
            uint nextPublishTick)
        {
            PreGapOutputLengths = preGapOutputLengths;
            AuthoritativePayloads = authoritativePayloads;
            AuthoritativeFrames = authoritativeFrames;
            ClientStates = clientStates;
            ClientDigests = clientDigests;
            ServerState = serverState;
            ClientState = clientState;
            NextPublishTick = nextPublishTick;
        }

        public int[] PreGapOutputLengths { get; }

        public byte[][] AuthoritativePayloads { get; }

        public FrameData[] AuthoritativeFrames { get; }

        public BattleState[] ClientStates { get; }

        public ulong[] ClientDigests { get; }

        public BattleState ServerState { get; }

        public BattleState ClientState { get; }

        public uint NextPublishTick { get; }
    }

    internal static class Gate6GapFillGoldenVector
    {
        public static Gate6GapFillGoldenResult RunApprovedArrivalOrder()
        {
            return Run(new (uint Tick, int Slot)[]
            {
                (100U, 0), (100U, 2), (100U, 1),
                (101U, 3), (101U, 1), (101U, 0), (101U, 2),
                (102U, 2), (102U, 0), (102U, 3), (102U, 1),
                (100U, 3),
            });
        }

        public static Gate6GapFillGoldenResult RunAlternateArrivalOrder()
        {
            return Run(new (uint Tick, int Slot)[]
            {
                (100U, 3), (100U, 1), (100U, 2),
                (102U, 1), (102U, 3), (102U, 0), (102U, 2),
                (101U, 2), (101U, 0), (101U, 3), (101U, 1),
                (100U, 0),
            });
        }

        private static Gate6GapFillGoldenResult Run((uint Tick, int Slot)[] arrivalOrder)
        {
            ActiveRoster serverRoster = CreateRoster();
            ActiveRoster clientRoster = CreateRoster();
            BattleState serverInitialState = CreateInitialState(serverRoster);
            BattleState clientInitialState = CreateInitialState(clientRoster);
            var processor = new ProtocolAuthorityProcessor(serverInitialState, 2U, 5);
            var clientSimulation = new BattleSimulation(clientInitialState);
            var preGapOutputLengths = new int[arrivalOrder.Length - 1];
            byte[][] authoritativePayloads = Array.Empty<byte[]>();

            for (int index = 0; index < arrivalOrder.Length; index++)
            {
                (uint tick, int slotValue) = arrivalOrder[index];
                PlayerSlot slot = new PlayerSlot(slotValue);
                PlayerInputSubmissionMessage wire = ProtocolMapper.ToWire(
                    serverRoster.GetPlayerId(slot),
                    CreateInput(tick, slotValue));
                byte[][] output = processor.SubmitPlayerInputPayload(wire.ToByteArray());

                if (index < preGapOutputLengths.Length)
                {
                    preGapOutputLengths[index] = output.Length;
                }
                else
                {
                    authoritativePayloads = output;
                }
            }

            var frames = new FrameData[authoritativePayloads.Length];
            var clientStates = new BattleState[authoritativePayloads.Length];
            var clientDigests = new ulong[authoritativePayloads.Length];
            for (int index = 0; index < authoritativePayloads.Length; index++)
            {
                AuthoritativeFrameMessage wire =
                    AuthoritativeFrameMessage.Parser.ParseFrom(authoritativePayloads[index]);
                FrameData frame = ProtocolMapper.ToDomain(wire, clientRoster);
                clientSimulation.Step(frame);
                frames[index] = frame;
                clientStates[index] = clientSimulation.State;
                clientDigests[index] = StateDigest.Compute(clientSimulation.State);
            }

            return new Gate6GapFillGoldenResult(
                preGapOutputLengths,
                authoritativePayloads,
                frames,
                clientStates,
                clientDigests,
                processor.ServerState,
                clientSimulation.State,
                processor.NextPublishTick);
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

        private static BattleState CreateInitialState(ActiveRoster roster)
        {
            return new BattleState(100U, roster, new[]
            {
                new PlayerState(-300, 0, 1_000),
                new PlayerState(300, 0, 2_000),
                new PlayerState(0, -300, 3_000),
                new PlayerState(0, 300, 4_000),
            });
        }

        private static InputFrame CreateInput(uint tick, int slotValue)
        {
            (int moveX, int moveZ, int aim) = (tick, slotValue) switch
            {
                (100U, 0) => (1, 0, 10_100),
                (100U, 1) => (-1, 0, 20_100),
                (100U, 2) => (0, 1, 30_100),
                (100U, 3) => (0, -1, 40_100),
                (101U, 0) => (0, 1, 10_101),
                (101U, 1) => (0, -1, 20_101),
                (101U, 2) => (1, 0, 30_101),
                (101U, 3) => (-1, 0, 40_101),
                (102U, 0) => (-1, 0, 10_102),
                (102U, 1) => (1, 0, 20_102),
                (102U, 2) => (0, -1, 30_102),
                (102U, 3) => (0, 1, 40_102),
                _ => throw new ArgumentOutOfRangeException(nameof(tick)),
            };

            return new InputFrame(
                tick,
                new PlayerSlot(slotValue),
                checked((sbyte)moveX),
                checked((sbyte)moveZ),
                checked((ushort)aim));
        }
    }
}
