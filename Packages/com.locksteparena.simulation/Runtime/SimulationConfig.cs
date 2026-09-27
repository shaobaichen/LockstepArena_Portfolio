namespace LockstepArena.Simulation
{
    public static class SimulationConfig
    {
        public const int TickRate = 30;
        public const int PositionUnitsPerMeter = 1_000;
        public const int MoveUnitsPerTick = 100;

        public const int ArenaMinX = -5_000;
        public const int ArenaMaxX = 5_000;
        public const int ArenaMinZ = -3_000;
        public const int ArenaMaxZ = 3_000;
    }
}
