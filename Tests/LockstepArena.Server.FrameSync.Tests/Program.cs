using System;
using System.Collections.Generic;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
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

    internal static class TestAssert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"Expected <{expected}> but found <{actual}>.");
            }
        }

        public static void Same(object expected, object actual)
        {
            if (!ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException("Expected both values to reference the same object.");
            }
        }

        public static void Throws<TException>(Action body)
            where TException : Exception
        {
            try
            {
                body();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Expected {typeof(TException).Name} but found {exception.GetType().Name}.",
                    exception);
            }

            throw new InvalidOperationException($"Expected {typeof(TException).Name} but no exception was thrown.");
        }
    }

    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = Combine(
                CoordinatorContractTests.All,
                CoordinatorRosterTests.All,
                CoordinatorWindowTests.All,
                CoordinatorRejectTests.All,
                CoordinatorPublicationTests.All,
                CoordinatorHistoryTests.All,
                CoordinatorTickLimitTests.All,
                CoordinatorDeterminismTests.All);
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
            int count = 0;
            foreach (TestCase[] group in groups)
            {
                count += group.Length;
            }

            TestCase[] combined = new TestCase[count];
            int offset = 0;
            foreach (TestCase[] group in groups)
            {
                Array.Copy(group, 0, combined, offset, group.Length);
                offset += group.Length;
            }

            return combined;
        }
    }

    internal static class CoordinatorTestData
    {
        private static readonly ulong[] PlayerIdValues = { 91UL, 17UL, 73UL, 44UL };

        public static ActiveRoster CreateRoster(int playerCount)
        {
            PlayerId[] playerIds = new PlayerId[playerCount];
            for (int index = 0; index < playerCount; index++)
            {
                playerIds[index] = new PlayerId(PlayerIdValues[index]);
            }

            return new ActiveRoster(playerIds);
        }

        public static InputFrame CreateInput(uint tick, int slot)
        {
            return new InputFrame(
                tick,
                new PlayerSlot(slot),
                (sbyte)((slot % 3) - 1),
                (sbyte)(((slot + 1) % 3) - 1),
                checked((ushort)(1000 + slot)));
        }
    }
}
