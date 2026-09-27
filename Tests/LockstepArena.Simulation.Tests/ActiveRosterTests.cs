using System;

namespace LockstepArena.Simulation.Tests
{
    internal static class ActiveRosterTests
    {
        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(ConstructorCopiesPlayerIds), ConstructorCopiesPlayerIds),
            new TestCase(nameof(ConstructorRejectsEmptyRoster), ConstructorRejectsEmptyRoster),
            new TestCase(nameof(ConstructorRejectsDuplicatePlayerIds), ConstructorRejectsDuplicatePlayerIds),
            new TestCase(nameof(GetAndTryGetUseStableSlotMapping), GetAndTryGetUseStableSlotMapping),
            new TestCase(nameof(TryGetSlotReportsMissingExplicitly), TryGetSlotReportsMissingExplicitly),
            new TestCase(nameof(StructuralComparisonUsesOrderedPlayerIds), StructuralComparisonUsesOrderedPlayerIds),
        };

        private static void ConstructorCopiesPlayerIds()
        {
            PlayerId[] source =
            {
                new PlayerId(9_003),
                new PlayerId(42),
                new PlayerId(7_000_001),
            };
            ActiveRoster roster = new ActiveRoster(source);

            source[0] = new PlayerId(1);

            TestAssert.Equal(9_003UL, roster.GetPlayerId(new PlayerSlot(0)).Value);
        }

        private static void ConstructorRejectsEmptyRoster()
        {
            TestAssert.Throws<ArgumentException>(() => new ActiveRoster(Array.Empty<PlayerId>()));
        }

        private static void ConstructorRejectsDuplicatePlayerIds()
        {
            PlayerId duplicate = new PlayerId(42);

            TestAssert.Throws<ArgumentException>(() => new ActiveRoster(new[]
            {
                duplicate,
                new PlayerId(7_000_001),
                duplicate,
            }));
        }

        private static void GetAndTryGetUseStableSlotMapping()
        {
            ActiveRoster roster = CreateRoster();

            TestAssert.Equal(3, roster.Count);
            TestAssert.Equal(9_003UL, roster.GetPlayerId(new PlayerSlot(0)).Value);
            TestAssert.Equal(42UL, roster.GetPlayerId(new PlayerSlot(1)).Value);
            TestAssert.Equal(7_000_001UL, roster.GetPlayerId(new PlayerSlot(2)).Value);
            TestAssert.Equal(true, roster.TryGetSlot(new PlayerId(42), out PlayerSlot slot));
            TestAssert.Equal(new PlayerSlot(1), slot);
            TestAssert.Throws<ArgumentOutOfRangeException>(() => roster.GetPlayerId(new PlayerSlot(roster.Count)));
        }

        private static void TryGetSlotReportsMissingExplicitly()
        {
            ActiveRoster roster = CreateRoster();

            TestAssert.Equal(false, roster.TryGetSlot(new PlayerId(999), out _));
        }

        private static void StructuralComparisonUsesOrderedPlayerIds()
        {
            ActiveRoster first = CreateRoster();
            ActiveRoster same = CreateRoster();
            ActiveRoster reordered = new ActiveRoster(new[]
            {
                new PlayerId(42),
                new PlayerId(9_003),
                new PlayerId(7_000_001),
            });
            ActiveRoster shorter = new ActiveRoster(new[]
            {
                new PlayerId(9_003),
                new PlayerId(42),
            });

            TestAssert.Equal(true, first.HasSameStructure(same));
            TestAssert.Equal(false, first.HasSameStructure(reordered));
            TestAssert.Equal(false, first.HasSameStructure(shorter));
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
    }
}
