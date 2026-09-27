using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.FrameSync;
using LockstepArena.Server.ProtocolAuthority;
using LockstepArena.Simulation;

namespace LockstepArena.LiveTcp.Tests
{
    internal static class ScheduledProtocolAuthorityTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(LegacyModeRetainsImmediateContinuousPublication), LegacyModeRetainsImmediateContinuousPublication),
            new TestCase(nameof(ScheduledModeStartsWithoutLogicalAdvance), ScheduledModeStartsWithoutLogicalAdvance),
            new TestCase(nameof(ScheduledModeUsesOnePublisherForSubmitAndPoll), ScheduledModeUsesOnePublisherForSubmitAndPoll),
            new TestCase(nameof(CompleteFrameBeforeMaturityReturnsEmpty), CompleteFrameBeforeMaturityReturnsEmpty),
            new TestCase(nameof(ScheduledPollPublishesAtExactMaturity), ScheduledPollPublishesAtExactMaturity),
            new TestCase(nameof(MatureGapCompletionPublishesImmediatelyOnSubmit), MatureGapCompletionPublishesImmediatelyOnSubmit),
            new TestCase(nameof(PollSerializesZeroFramesAsEmpty), PollSerializesZeroFramesAsEmpty),
            new TestCase(nameof(PollSerializesOneFrameAsOnePayload), PollSerializesOneFrameAsOnePayload),
            new TestCase(nameof(PollSerializesMultipleFramesAsIndependentOrderedPayloads), PollSerializesMultipleFramesAsIndependentOrderedPayloads),
            new TestCase(nameof(ParseFailureBeforePublicationDoesNotFault), ParseFailureBeforePublicationDoesNotFault),
            new TestCase(nameof(MappingFailureBeforePublicationDoesNotFault), MappingFailureBeforePublicationDoesNotFault),
            new TestCase(nameof(SubmitRejectionBeforePublicationDoesNotFault), SubmitRejectionBeforePublicationDoesNotFault),
            new TestCase(nameof(PollFailureFaultsAndRethrowsOriginal), PollFailureFaultsAndRethrowsOriginal),
            new TestCase(nameof(SimulationFailureAfterPublicationReturnsNoPartialPayloadAndFaults), SimulationFailureAfterPublicationReturnsNoPartialPayloadAndFaults),
            new TestCase(nameof(FaultedProcessorRejectsSubmitAndPollFirst), FaultedProcessorRejectsSubmitAndPollFirst),
            new TestCase(nameof(LegacyAndScheduledProcessorsHaveIndependentAuthorityTimelines), LegacyAndScheduledProcessorsHaveIndependentAuthorityTimelines),
        };

        private static void LegacyModeRetainsImmediateContinuousPublication()
        {
            ActiveRoster roster = CreateRoster(2);
            var processor = new ProtocolAuthorityProcessor(CreateInitialState(roster), 4U, 4);
            Submit(processor, roster, 100U, 0);
            byte[][] output = Submit(processor, roster, 100U, 1);
            TestAssert.Equal(1, output.Length);
            TestAssert.Equal(100U, ParseFrame(output[0], roster).Tick);
        }

        private static void ScheduledModeStartsWithoutLogicalAdvance()
        {
            ActiveRoster roster = CreateRoster(2);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster);
            TickDrivenFramePublisher publisher = GetScheduledPublisher(processor);
            TestAssert.Equal(100U, processor.ServerState.Tick);
            TestAssert.Equal(100U, processor.NextPublishTick);
            TestAssert.Equal(100UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(null, publisher.EligibilityCeiling);
        }

        private static void ScheduledModeUsesOnePublisherForSubmitAndPoll()
        {
            ProtocolAuthorityProcessor processor = CreateScheduled(CreateRoster(1));
            TickDrivenFramePublisher direct = GetScheduledPublisher(processor);
            StopwatchTickDriver driver = GetScheduledDriver(processor);
            ElapsedTickPacer pacer = (ElapsedTickPacer)(GetUniquePrivateField(
                typeof(StopwatchTickDriver), typeof(ElapsedTickPacer)).GetValue(driver)
                ?? throw new InvalidOperationException("Driver pacer was null."));
            TickDrivenFramePublisher throughDriver = (TickDrivenFramePublisher)(GetUniquePrivateField(
                typeof(ElapsedTickPacer), typeof(TickDrivenFramePublisher)).GetValue(pacer)
                ?? throw new InvalidOperationException("Pacer publisher was null."));
            TestAssert.Same(direct, throughDriver);
        }

        private static void CompleteFrameBeforeMaturityReturnsEmpty()
        {
            ActiveRoster roster = CreateRoster(2);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster);
            TestAssert.Equal(0, CompleteTick(processor, roster, 100U).Length);
            TestAssert.Equal(100U, processor.ServerState.Tick);
        }

        private static void ScheduledPollPublishesAtExactMaturity()
        {
            ActiveRoster roster = CreateRoster(2);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster);
            CompleteTick(processor, roster, 100U);
            ForceNextPollAdvances(processor, 2U);
            byte[][] output = processor.PollAuthority();
            TestAssert.Equal(1, output.Length);
            TestAssert.Equal(100U, ParseFrame(output[0], roster).Tick);
            TestAssert.Equal(101U, processor.ServerState.Tick);
        }

        private static void MatureGapCompletionPublishesImmediatelyOnSubmit()
        {
            ActiveRoster roster = CreateRoster(2);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster);
            ForceNextPollAdvances(processor, 2U);
            TestAssert.Equal(0, processor.PollAuthority().Length);
            Submit(processor, roster, 100U, 0);
            byte[][] output = Submit(processor, roster, 100U, 1);
            TestAssert.Equal(1, output.Length);
            TestAssert.Equal(100U, ParseFrame(output[0], roster).Tick);
        }

        private static void PollSerializesZeroFramesAsEmpty()
        {
            ProtocolAuthorityProcessor processor = CreateScheduled(CreateRoster(1));
            ForceNextPollAdvances(processor, 1U);
            byte[][] first = processor.PollAuthority();
            byte[][] second = processor.PollAuthority();
            TestAssert.Equal(0, first.Length);
            TestAssert.Equal(0, second.Length);
        }

        private static void PollSerializesOneFrameAsOnePayload()
        {
            ActiveRoster roster = CreateRoster(1);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster);
            CompleteTick(processor, roster, 100U);
            ForceNextPollAdvances(processor, 2U);
            byte[][] output = processor.PollAuthority();
            TestAssert.Equal(1, output.Length);
            TestAssert.Equal(100U, ParseFrame(output[0], roster).Tick);
        }

        private static void PollSerializesMultipleFramesAsIndependentOrderedPayloads()
        {
            ActiveRoster roster = CreateRoster(1);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster);
            CompleteTick(processor, roster, 100U);
            CompleteTick(processor, roster, 101U);
            CompleteTick(processor, roster, 102U);
            ForceNextPollAdvances(processor, 4U);
            byte[][] output = processor.PollAuthority();
            TestAssert.Equal(3, output.Length);
            TestAssert.NotSame(output[0], output[1]);
            TestAssert.NotSame(output[1], output[2]);
            for (int index = 0; index < output.Length; index++)
            {
                TestAssert.Equal(checked((uint)(100 + index)), ParseFrame(output[index], roster).Tick);
            }
        }

        private static void ParseFailureBeforePublicationDoesNotFault()
        {
            ActiveRoster roster = CreateRoster(1);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster, 0U);
            TestAssert.Throws<InvalidProtocolBufferException>(
                () => processor.SubmitPlayerInputPayload(new byte[] { 0x12, 0x05, 0x01 }));
            TestAssert.Equal(1, CompleteTick(processor, roster, 100U).Length);
        }

        private static void MappingFailureBeforePublicationDoesNotFault()
        {
            ActiveRoster roster = CreateRoster(1);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster, 0U);
            var wire = new PlayerInputSubmissionMessage
            {
                SubmittedPlayerId = roster.GetPlayerId(new PlayerSlot(0)).Value,
                Input = new InputFrameMessage { Tick = 100U, PlayerSlot = 0U, MoveX = 2 },
            };
            TestAssert.Throws<ProtocolMappingException>(
                () => processor.SubmitPlayerInputPayload(wire.ToByteArray()));
            TestAssert.Equal(1, CompleteTick(processor, roster, 100U).Length);
        }

        private static void SubmitRejectionBeforePublicationDoesNotFault()
        {
            ActiveRoster roster = CreateRoster(2);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster, 0U);
            Submit(processor, roster, 100U, 0);
            TestAssert.Throws<InvalidOperationException>(() => Submit(processor, roster, 100U, 0));
            TestAssert.Equal(1, Submit(processor, roster, 100U, 1).Length);
        }

        private static void PollFailureFaultsAndRethrowsOriginal()
        {
            (ProtocolAuthorityProcessor processor, TickDrivenFramePublisher publisher) = CreatePollFaultFixture();
            ForceNextPollAdvances(processor, 2U);
            InvalidOperationException exception = TestAssert.ThrowsAndReturn<InvalidOperationException>(
                () => processor.PollAuthority());
            TestAssert.Equal("A planned publication Tick was absent from pending storage.", exception.Message);
            TestAssert.Equal(102U, publisher.NextPublishTick);
            TestAssert.Throws<InvalidOperationException>(() => processor.PollAuthority());
        }

        private static void SimulationFailureAfterPublicationReturnsNoPartialPayloadAndFaults()
        {
            ActiveRoster roster = CreateRoster(1);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster, 0U);
            SetProcessorSimulationState(processor, CreateState(roster, 101U));
            byte[][] sentinel = { new byte[] { 0xA5 } };
            byte[][] result = sentinel;
            TestAssert.Throws<ArgumentException>(
                () => result = CompleteTick(processor, roster, 100U));
            TestAssert.Same(sentinel, result);
            TestAssert.Equal(101U, processor.NextPublishTick);
            TestAssert.Throws<InvalidOperationException>(() => processor.PollAuthority());
        }

        private static void FaultedProcessorRejectsSubmitAndPollFirst()
        {
            ActiveRoster roster = CreateRoster(1);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster, 0U);
            SetProcessorSimulationState(processor, CreateState(roster, 101U));
            TestAssert.Throws<ArgumentException>(() => CompleteTick(processor, roster, 100U));
            TestAssert.Throws<InvalidOperationException>(() => processor.SubmitPlayerInputPayload(null!));
            TestAssert.Throws<InvalidOperationException>(() => processor.PollAuthority());
        }

        private static void LegacyAndScheduledProcessorsHaveIndependentAuthorityTimelines()
        {
            ActiveRoster roster = CreateRoster(1);
            var legacy = new ProtocolAuthorityProcessor(CreateInitialState(roster), 4U, 4);
            ProtocolAuthorityProcessor scheduled = CreateScheduled(roster);
            TestAssert.Equal(1, CompleteTick(legacy, roster, 100U).Length);
            TestAssert.Equal(0, CompleteTick(scheduled, roster, 100U).Length);
            TestAssert.Equal(101U, legacy.NextPublishTick);
            TestAssert.Equal(100U, scheduled.NextPublishTick);
            ForceNextPollAdvances(scheduled, 2U);
            TestAssert.Equal(1, scheduled.PollAuthority().Length);
        }

        internal static ActiveRoster CreateRoster(int count)
        {
            var ids = new PlayerId[count];
            for (int index = 0; index < count; index++)
            {
                ids[index] = new PlayerId(10_000UL - checked((ulong)(index * 17)));
            }

            return new ActiveRoster(ids);
        }

        internal static BattleState CreateInitialState(ActiveRoster roster)
        {
            return CreateState(roster, 100U);
        }

        private static BattleState CreateState(ActiveRoster roster, uint tick)
        {
            var states = new PlayerState[roster.Count];
            for (int index = 0; index < states.Length; index++)
            {
                states[index] = new PlayerState(index * 100, -(index * 100), checked((ushort)(1_000 + index)));
            }

            return new BattleState(tick, roster, states);
        }

        internal static ProtocolAuthorityProcessor CreateScheduled(
            ActiveRoster roster,
            uint inputDelayTicks = 2U)
        {
            return new ProtocolAuthorityProcessor(CreateInitialState(roster), inputDelayTicks, 8U, 8);
        }

        internal static byte[][] CompleteTick(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster,
            uint tick)
        {
            byte[][] output = Array.Empty<byte[]>();
            for (int slotValue = 0; slotValue < roster.Count; slotValue++)
            {
                output = Submit(processor, roster, tick, slotValue);
            }

            return output;
        }

        internal static byte[][] Submit(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster,
            uint tick,
            int slotValue)
        {
            PlayerSlot slot = new PlayerSlot(slotValue);
            InputFrame input = CreateInput(tick, slotValue);
            return processor.SubmitPlayerInputPayload(
                ProtocolMapper.ToWire(roster.GetPlayerId(slot), input).ToByteArray());
        }

        internal static InputFrame CreateInput(uint tick, int slotValue)
        {
            return new InputFrame(tick, new PlayerSlot(slotValue), 0, 0,
                checked((ushort)(10_000 + slotValue)));
        }

        internal static FrameData ParseFrame(byte[] payload, ActiveRoster roster)
        {
            return ProtocolMapper.ToDomain(AuthoritativeFrameMessage.Parser.ParseFrom(payload), roster);
        }

        internal static TickDrivenFramePublisher GetScheduledPublisher(
            ProtocolAuthorityProcessor processor)
        {
            return (TickDrivenFramePublisher)(GetUniquePrivateField(
                typeof(ProtocolAuthorityProcessor), typeof(TickDrivenFramePublisher)).GetValue(processor)
                ?? throw new InvalidOperationException("Processor publisher was null."));
        }

        internal static StopwatchTickDriver GetScheduledDriver(
            ProtocolAuthorityProcessor processor)
        {
            return (StopwatchTickDriver)(GetUniquePrivateField(
                typeof(ProtocolAuthorityProcessor), typeof(StopwatchTickDriver)).GetValue(processor)
                ?? throw new InvalidOperationException("Processor driver was null."));
        }

        internal static void ForceNextPollAdvances(
            ProtocolAuthorityProcessor processor,
            uint dueAdvances)
        {
            StopwatchTickDriver driver = GetScheduledDriver(processor);
            Stopwatch stopwatch = (Stopwatch)(GetUniquePrivateField(
                typeof(StopwatchTickDriver), typeof(Stopwatch)).GetValue(driver)
                ?? throw new InvalidOperationException("Driver Stopwatch was null."));
            FieldInfo baselineField = GetUniquePrivateField(typeof(StopwatchTickDriver), typeof(long));
            stopwatch.Stop();
            long stableElapsed = stopwatch.ElapsedTicks;
            UInt128 numerator = (UInt128)(ulong)Stopwatch.Frequency * dueAdvances
                + (uint)SimulationConfig.TickRate - 1U;
            UInt128 requiredElapsedWide = numerator / (uint)SimulationConfig.TickRate;
            long requiredElapsed = checked((long)requiredElapsedWide);
            baselineField.SetValue(driver, checked(stableElapsed - requiredElapsed));
        }

        internal static FieldInfo GetUniquePrivateField(Type owner, Type fieldType)
        {
            FieldInfo? match = null;
            FieldInfo[] fields = owner.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType != fieldType)
                {
                    continue;
                }

                if (match is not null)
                {
                    throw new InvalidOperationException($"Multiple private {fieldType.Name} fields found on {owner.Name}.");
                }

                match = fields[index];
            }

            return match ?? throw new InvalidOperationException(
                $"No private {fieldType.Name} field found on {owner.Name}.");
        }

        private static (ProtocolAuthorityProcessor Processor, TickDrivenFramePublisher Publisher)
            CreatePollFaultFixture()
        {
            ActiveRoster roster = CreateRoster(1);
            ProtocolAuthorityProcessor processor = CreateScheduled(roster, 0U);
            TickDrivenFramePublisher publisher = GetScheduledPublisher(processor);
            CompleteTick(processor, roster, 100U);
            CompleteTick(processor, roster, 101U);
            CompleteTick(processor, roster, 102U);

            AuthoritativeFrameCoordinator coordinator = (AuthoritativeFrameCoordinator)(
                GetUniquePrivateField(typeof(TickDrivenFramePublisher), typeof(AuthoritativeFrameCoordinator))
                    .GetValue(publisher) ?? throw new InvalidOperationException("Publisher coordinator was null."));
            Type pendingType = typeof(Dictionary<uint, StrictFrameCollector>);
            var pending = (Dictionary<uint, StrictFrameCollector>)(
                GetUniquePrivateField(typeof(AuthoritativeFrameCoordinator), pendingType).GetValue(coordinator)
                ?? throw new InvalidOperationException("Coordinator pending storage was null."));
            StrictFrameCollector collector = pending[102U];
            GetUniquePrivateField(typeof(StrictFrameCollector), typeof(FrameData))
                .SetValue(collector, CreateStandaloneFrame(roster, 101U));
            return (processor, publisher);
        }

        private static FrameData CreateStandaloneFrame(ActiveRoster roster, uint tick)
        {
            var inputs = new InputFrame[roster.Count];
            for (int slotValue = 0; slotValue < inputs.Length; slotValue++)
            {
                inputs[slotValue] = CreateInput(tick, slotValue);
            }

            return FrameData.Create(roster, tick, inputs);
        }

        private static void SetProcessorSimulationState(
            ProtocolAuthorityProcessor processor,
            BattleState state)
        {
            BattleSimulation simulation = (BattleSimulation)(GetUniquePrivateField(
                typeof(ProtocolAuthorityProcessor), typeof(BattleSimulation)).GetValue(processor)
                ?? throw new InvalidOperationException("Processor simulation was null."));
            GetUniquePrivateField(typeof(BattleSimulation), typeof(BattleState)).SetValue(simulation, state);
        }
    }
}
