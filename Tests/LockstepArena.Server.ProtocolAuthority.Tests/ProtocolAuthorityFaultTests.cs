using System;
using System.Linq;
using System.Reflection;
using LockstepArena.Simulation;

namespace LockstepArena.Server.ProtocolAuthority.Tests
{
    internal static class ProtocolAuthorityFaultTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(PostPublicationStepFailureRethrowsAndAdvancesAuthority), PostPublicationStepFailureRethrowsAndAdvancesAuthority),
            new TestCase(nameof(FaultedProcessorRejectsBeforePayloadValidation), FaultedProcessorRejectsBeforePayloadValidation),
        };

        private static void PostPublicationStepFailureRethrowsAndAdvancesAuthority()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateMismatchedSubject();

            TestAssert.Throws<ArgumentException>(
                () => ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U));

            TestAssert.Equal(101U, processor.NextPublishTick);
            TestAssert.Equal(101U, processor.ServerState.Tick);
        }

        private static void FaultedProcessorRejectsBeforePayloadValidation()
        {
            (ProtocolAuthorityProcessor processor, ActiveRoster roster) = CreateMismatchedSubject();
            TestAssert.Throws<ArgumentException>(
                () => ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U));

            TestAssert.Throws<InvalidOperationException>(
                () => processor.SubmitPlayerInputPayload(null!));
        }

        private static (ProtocolAuthorityProcessor Processor, ActiveRoster Roster) CreateMismatchedSubject()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(2);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);
            BattleSimulation simulation = GetServerSimulation(processor);
            simulation.Step(FrameData.Create(roster, 100U, new[]
            {
                ProtocolAuthorityTestData.CreateInput(100U, 0),
                ProtocolAuthorityTestData.CreateInput(100U, 1),
            }));
            return (processor, roster);
        }

        private static BattleSimulation GetServerSimulation(ProtocolAuthorityProcessor processor)
        {
            FieldInfo[] fields = typeof(ProtocolAuthorityProcessor)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(BattleSimulation))
                .ToArray();

            TestAssert.Equal(1, fields.Length);
            return (BattleSimulation)(fields[0].GetValue(processor)
                ?? throw new InvalidOperationException("Server simulation field was null."));
        }
    }
}
