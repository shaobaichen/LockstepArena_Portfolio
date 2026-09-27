namespace LockstepArena.Simulation
{
    public readonly struct PlayerState
    {
        public PlayerState(int positionX, int positionZ, ushort aim)
            : this(positionX, positionZ, aim, 0, 0, 0)
        {
        }

        public PlayerState(int positionX, int positionZ, ushort aim, int hitPoints, int roundWins, uint fireCooldownTicks)
        {
            if (hitPoints < 0) throw new System.ArgumentOutOfRangeException(nameof(hitPoints));
            if (roundWins < 0) throw new System.ArgumentOutOfRangeException(nameof(roundWins));
            PositionX = positionX;
            PositionZ = positionZ;
            Aim = aim;
            HitPoints = hitPoints;
            RoundWins = roundWins;
            FireCooldownTicks = fireCooldownTicks;
        }

        public int PositionX { get; }

        public int PositionZ { get; }

        public ushort Aim { get; }

        public int HitPoints { get; }

        public int RoundWins { get; }

        public uint FireCooldownTicks { get; }
    }
}
