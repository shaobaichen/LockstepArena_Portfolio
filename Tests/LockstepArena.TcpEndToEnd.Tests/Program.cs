using System;
using System.Collections.Generic;

namespace LockstepArena.TcpEndToEnd.Tests
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

        public static void True(bool value)
        {
            if (!value)
            {
                throw new InvalidOperationException("Expected true but found false.");
            }
        }

        public static void NotSame(object first, object second)
        {
            if (ReferenceEquals(first, second))
            {
                throw new InvalidOperationException("Expected distinct object references.");
            }
        }

        public static void SequenceEqual(byte[] expected, byte[] actual)
        {
            Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                Equal(expected[index], actual[index]);
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

            throw new InvalidOperationException(
                $"Expected {typeof(TException).Name} but no exception was thrown.");
        }
    }

    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = LoopbackTcpEndToEndTests.All;
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
    }
}
