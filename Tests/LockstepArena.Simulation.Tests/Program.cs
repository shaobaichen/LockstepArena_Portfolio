using System;

namespace LockstepArena.Simulation.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = Combine(
                ActiveRosterTests.All,
                ContractTests.All,
                GameplayContractTests.All,
                FrameCollectionTests.All,
                BattleSimulationTests.All,
                GameplaySimulationTests.All,
                DeterminismTests.All);
            int failures = 0;

            foreach (TestCase test in tests)
            {
                try
                {
                    test.Body();
                    Console.WriteLine($"PASS {test.Name}");
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
                }
            }

            Console.WriteLine($"RESULT {tests.Length - failures}/{tests.Length} passed");
            return failures == 0 ? 0 : 1;
        }

        private static TestCase[] Combine(params TestCase[][] groups)
        {
            int length = 0;
            foreach (TestCase[] group in groups)
            {
                length += group.Length;
            }

            TestCase[] combined = new TestCase[length];
            int offset = 0;
            foreach (TestCase[] group in groups)
            {
                Array.Copy(group, 0, combined, offset, group.Length);
                offset += group.Length;
            }

            return combined;
        }
    }
}
