using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation.Tests
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

        public static void NotEqual<T>(T unexpected, T actual)
        {
            if (EqualityComparer<T>.Default.Equals(unexpected, actual))
            {
                throw new InvalidOperationException($"Did not expect <{unexpected}>.");
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
}
