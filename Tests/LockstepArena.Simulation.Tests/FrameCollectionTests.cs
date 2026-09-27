using System;

namespace LockstepArena.Simulation.Tests
{
    internal static class FrameCollectionTests
    {
        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(SubmitReturnsFalseUntilLastAcceptedInput), SubmitReturnsFalseUntilLastAcceptedInput),
            new TestCase(nameof(UnknownPlayerIdIsRejectedWithoutPollution), UnknownPlayerIdIsRejectedWithoutPollution),
            new TestCase(nameof(PlayerIdSlotMismatchIsRejectedWithoutPollution), PlayerIdSlotMismatchIsRejectedWithoutPollution),
            new TestCase(nameof(WrongTickIsRejectedWithoutPollution), WrongTickIsRejectedWithoutPollution),
            new TestCase(nameof(UnknownSlotIsRejectedWithoutPollution), UnknownSlotIsRejectedWithoutPollution),
            new TestCase(nameof(DuplicateSlotIsRejectedWithoutPollution), DuplicateSlotIsRejectedWithoutPollution),
            new TestCase(nameof(IncompleteCollectorCannotReturnFrame), IncompleteCollectorCannotReturnFrame),
            new TestCase(nameof(CompleteCollectorRejectsFurtherSubmit), CompleteCollectorRejectsFurtherSubmit),
        };

        private static void SubmitReturnsFalseUntilLastAcceptedInput()
        {
            ActiveRoster roster = CreateRoster();
            StrictFrameCollector collector = new StrictFrameCollector(roster, 7);

            TestAssert.Equal(false, collector.Submit(new PlayerId(7_000_001), Input(7, 2, aim: 300)));
            TestAssert.Equal(false, collector.Submit(new PlayerId(9_003), Input(7, 0, aim: 100)));
            TestAssert.Equal(true, collector.Submit(new PlayerId(42), Input(7, 1, aim: 200)));

            TestAssert.Equal(true, collector.IsComplete);
            FrameData frame = collector.GetCompletedFrame();
            TestAssert.Equal((ushort)100, frame.GetInput(new PlayerSlot(0)).Aim);
            TestAssert.Equal((ushort)200, frame.GetInput(new PlayerSlot(1)).Aim);
            TestAssert.Equal((ushort)300, frame.GetInput(new PlayerSlot(2)).Aim);
        }

        private static void UnknownPlayerIdIsRejectedWithoutPollution()
        {
            ActiveRoster roster = CreateRoster();
            StrictFrameCollector collector = new StrictFrameCollector(roster, 5);
            TestAssert.Equal(false, collector.Submit(new PlayerId(9_003), Input(5, 0)));

            TestAssert.Throws<ArgumentException>(() => collector.Submit(new PlayerId(999), Input(5, 1)));

            CompleteRemaining(collector, 5);
        }

        private static void PlayerIdSlotMismatchIsRejectedWithoutPollution()
        {
            ActiveRoster roster = CreateRoster();
            StrictFrameCollector collector = new StrictFrameCollector(roster, 5);
            TestAssert.Equal(false, collector.Submit(new PlayerId(9_003), Input(5, 0)));

            TestAssert.Throws<ArgumentException>(() => collector.Submit(new PlayerId(42), Input(5, 2)));

            CompleteRemaining(collector, 5);
        }

        private static void WrongTickIsRejectedWithoutPollution()
        {
            ActiveRoster roster = CreateRoster();
            StrictFrameCollector collector = new StrictFrameCollector(roster, 5);
            TestAssert.Equal(false, collector.Submit(new PlayerId(9_003), Input(5, 0)));

            TestAssert.Throws<ArgumentException>(() => collector.Submit(new PlayerId(42), Input(6, 1)));

            CompleteRemaining(collector, 5);
        }

        private static void UnknownSlotIsRejectedWithoutPollution()
        {
            ActiveRoster roster = CreateRoster();
            StrictFrameCollector collector = new StrictFrameCollector(roster, 5);
            TestAssert.Equal(false, collector.Submit(new PlayerId(9_003), Input(5, 0)));

            TestAssert.Throws<ArgumentOutOfRangeException>(() => collector.Submit(
                new PlayerId(42),
                Input(5, 3)));

            CompleteRemaining(collector, 5);
        }

        private static void DuplicateSlotIsRejectedWithoutPollution()
        {
            ActiveRoster roster = CreateRoster();
            StrictFrameCollector collector = new StrictFrameCollector(roster, 5);
            TestAssert.Equal(false, collector.Submit(new PlayerId(9_003), Input(5, 0, aim: 100)));

            TestAssert.Throws<InvalidOperationException>(() => collector.Submit(
                new PlayerId(9_003),
                Input(5, 0, aim: 999)));

            CompleteRemaining(collector, 5);
            TestAssert.Equal((ushort)100, collector.GetCompletedFrame().GetInput(new PlayerSlot(0)).Aim);
        }

        private static void IncompleteCollectorCannotReturnFrame()
        {
            ActiveRoster roster = CreateRoster();
            StrictFrameCollector collector = new StrictFrameCollector(roster, 5);
            TestAssert.Equal(false, collector.Submit(new PlayerId(9_003), Input(5, 0)));

            TestAssert.Equal(false, collector.IsComplete);
            TestAssert.Throws<InvalidOperationException>(() => collector.GetCompletedFrame());
        }

        private static void CompleteCollectorRejectsFurtherSubmit()
        {
            ActiveRoster roster = CreateRoster();
            StrictFrameCollector collector = new StrictFrameCollector(roster, 5);
            TestAssert.Equal(false, collector.Submit(new PlayerId(9_003), Input(5, 0)));
            CompleteRemaining(collector, 5);
            FrameData completed = collector.GetCompletedFrame();

            TestAssert.Throws<InvalidOperationException>(() => collector.Submit(
                new PlayerId(42),
                Input(5, 1)));
            TestAssert.Equal(true, ReferenceEquals(completed, collector.GetCompletedFrame()));
        }

        private static void CompleteRemaining(StrictFrameCollector collector, uint tick)
        {
            TestAssert.Equal(false, collector.Submit(new PlayerId(42), Input(tick, 1)));
            TestAssert.Equal(true, collector.Submit(new PlayerId(7_000_001), Input(tick, 2)));
            TestAssert.Equal(true, collector.IsComplete);
        }

        private static ActiveRoster CreateRoster()
        {
            return new ActiveRoster(new[]
            {
                new PlayerId(9_003),
                new PlayerId(42),
                new PlayerId(7_000_001),
            });
        }

        private static InputFrame Input(
            uint tick,
            int slot,
            sbyte moveX = 0,
            sbyte moveZ = 0,
            ushort aim = 0)
        {
            return new InputFrame(tick, new PlayerSlot(slot), moveX, moveZ, aim);
        }
    }
}
