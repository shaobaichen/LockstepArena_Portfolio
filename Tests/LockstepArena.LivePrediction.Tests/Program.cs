using System;

namespace LockstepArena.LivePrediction.Tests
{
    internal sealed class TestCase
    {
        public TestCase(string name, Action body)
        {
            Name = name;
            Body = body;
        }

        public string Name { get; }

        public Action Body { get; }
    }

    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = Combine(
                TcpSharedBattleSessionTests.ConstructionTests,
                TcpSharedBattleSessionTests.PumpTests,
                PredictedTcpClientBattleRuntimeTests.TransportTests,
                PredictedTcpClientBattleRuntimeTests.CoreTests,
                PredictedTcpClientBattleRuntimeTests.BoundedAuthorityTests,
                Gate13WeakNetworkGoldenTests.All);
            int failures = 0;
            for (int index = 0; index < tests.Length; index++)
            {
                TestCase test = tests[index];
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
            int length = 0;
            for (int index = 0; index < groups.Length; index++)
            {
                length += groups[index].Length;
            }

            var result = new TestCase[length];
            int offset = 0;
            for (int index = 0; index < groups.Length; index++)
            {
                Array.Copy(groups[index], 0, result, offset, groups[index].Length);
                offset += groups[index].Length;
            }

            return result;
        }
    }
}
