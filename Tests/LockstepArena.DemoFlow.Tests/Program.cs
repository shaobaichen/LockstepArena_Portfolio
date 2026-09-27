using System;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = Combine(ControlProtocolTests.All, SessionRoomTests.All, BattlePreparationTests.All, SettlementLifecycleTests.All, Gate14DemoGoldenTests.All, ReplayPlaybackTests.All, DiagnosticsTests.All);
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
                    Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
                }
            }

            Console.WriteLine($"RESULT {tests.Length - failures}/{tests.Length} passed");
            return failures == 0 ? 0 : 1;
        }

        private static TestCase[] Combine(params TestCase[][] groups)
        {
            int count = 0;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                count += groups[groupIndex].Length;
            }

            var result = new TestCase[count];
            int offset = 0;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                groups[groupIndex].CopyTo(result, offset);
                offset += groups[groupIndex].Length;
            }

            return result;
        }
    }
}
