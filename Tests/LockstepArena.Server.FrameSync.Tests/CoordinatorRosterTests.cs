using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal static class CoordinatorRosterTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("TwoPlayerSameTickArrivalOrderIsCanonical", TwoPlayerSameTickArrivalOrderIsCanonical),
            new TestCase("ThreePlayerSameTickArrivalOrderIsCanonical", ThreePlayerSameTickArrivalOrderIsCanonical),
            new TestCase("FourPlayerSameTickArrivalOrderIsCanonical", FourPlayerSameTickArrivalOrderIsCanonical),
        };

        private static void TwoPlayerSameTickArrivalOrderIsCanonical()
        {
            AssertReverseArrivalIsCanonical(2);
        }

        private static void ThreePlayerSameTickArrivalOrderIsCanonical()
        {
            AssertReverseArrivalIsCanonical(3);
        }

        private static void FourPlayerSameTickArrivalOrderIsCanonical()
        {
            AssertReverseArrivalIsCanonical(4);
        }

        private static void AssertReverseArrivalIsCanonical(int playerCount)
        {
            const uint tick = 41U;
            ActiveRoster roster = CoordinatorTestData.CreateRoster(playerCount);
            AuthoritativeFrameCoordinator coordinator =
                new AuthoritativeFrameCoordinator(roster, tick, 2U, 4);

            FrameData[] publication = new FrameData[0];
            for (int slot = playerCount - 1; slot >= 0; slot--)
            {
                publication = coordinator.Submit(
                    roster.GetPlayerId(new PlayerSlot(slot)),
                    CoordinatorTestData.CreateInput(tick, slot));
                TestAssert.Equal(slot == 0 ? 1 : 0, publication.Length);
            }

            FrameData frame = publication[0];
            TestAssert.Equal(tick, frame.Tick);
            for (int slot = 0; slot < playerCount; slot++)
            {
                InputFrame expected = CoordinatorTestData.CreateInput(tick, slot);
                InputFrame actual = frame.GetInput(new PlayerSlot(slot));
                TestAssert.Equal(expected.Tick, actual.Tick);
                TestAssert.Equal(expected.PlayerSlot, actual.PlayerSlot);
                TestAssert.Equal(expected.MoveX, actual.MoveX);
                TestAssert.Equal(expected.MoveZ, actual.MoveZ);
                TestAssert.Equal(expected.Aim, actual.Aim);
            }
        }
    }
}
