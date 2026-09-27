using System;

namespace LockstepArena.Simulation
{
    public sealed class GameplayConfig
    {
        public GameplayConfig(
            int maxHitPoints,
            int projectileDamage,
            int moveUnitsPerTick,
            uint fireIntervalTicks,
            int projectileUnitsPerTick,
            uint projectileLifetimeTicks,
            int playerRadiusUnits,
            int projectileRadiusUnits,
            int muzzleOffsetUnits,
            int muzzleSafetyMarginUnits,
            uint roundDurationTicks,
            uint roundCountdownTicks,
            uint roundEndDelayTicks,
            int roundsToWin)
        {
            if (maxHitPoints < 1) throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            if (projectileDamage < 1) throw new ArgumentOutOfRangeException(nameof(projectileDamage));
            if (moveUnitsPerTick < 1) throw new ArgumentOutOfRangeException(nameof(moveUnitsPerTick));
            if (fireIntervalTicks < 1) throw new ArgumentOutOfRangeException(nameof(fireIntervalTicks));
            if (projectileUnitsPerTick < 1) throw new ArgumentOutOfRangeException(nameof(projectileUnitsPerTick));
            if (projectileLifetimeTicks < 1) throw new ArgumentOutOfRangeException(nameof(projectileLifetimeTicks));
            if (playerRadiusUnits < 1) throw new ArgumentOutOfRangeException(nameof(playerRadiusUnits));
            if (projectileRadiusUnits < 1) throw new ArgumentOutOfRangeException(nameof(projectileRadiusUnits));
            if (muzzleOffsetUnits < 0) throw new ArgumentOutOfRangeException(nameof(muzzleOffsetUnits));
            if (muzzleSafetyMarginUnits < 0) throw new ArgumentOutOfRangeException(nameof(muzzleSafetyMarginUnits));
            if (roundDurationTicks < 1) throw new ArgumentOutOfRangeException(nameof(roundDurationTicks));
            if (roundCountdownTicks < 1) throw new ArgumentOutOfRangeException(nameof(roundCountdownTicks));
            if (roundEndDelayTicks < 1) throw new ArgumentOutOfRangeException(nameof(roundEndDelayTicks));
            if (roundsToWin < 1) throw new ArgumentOutOfRangeException(nameof(roundsToWin));
            if (muzzleOffsetUnits > playerRadiusUnits - projectileRadiusUnits - muzzleSafetyMarginUnits)
                throw new ArgumentException("Muzzle offset must keep the projectile inside the player collision body.", nameof(muzzleOffsetUnits));

            MaxHitPoints = maxHitPoints;
            ProjectileDamage = projectileDamage;
            MoveUnitsPerTick = moveUnitsPerTick;
            FireIntervalTicks = fireIntervalTicks;
            ProjectileUnitsPerTick = projectileUnitsPerTick;
            ProjectileLifetimeTicks = projectileLifetimeTicks;
            PlayerRadiusUnits = playerRadiusUnits;
            ProjectileRadiusUnits = projectileRadiusUnits;
            MuzzleOffsetUnits = muzzleOffsetUnits;
            MuzzleSafetyMarginUnits = muzzleSafetyMarginUnits;
            RoundDurationTicks = roundDurationTicks;
            RoundCountdownTicks = roundCountdownTicks;
            RoundEndDelayTicks = roundEndDelayTicks;
            RoundsToWin = roundsToWin;
        }

        public int MaxHitPoints { get; }
        public int ProjectileDamage { get; }
        public int MoveUnitsPerTick { get; }
        public uint FireIntervalTicks { get; }
        public int ProjectileUnitsPerTick { get; }
        public uint ProjectileLifetimeTicks { get; }
        public int PlayerRadiusUnits { get; }
        public int ProjectileRadiusUnits { get; }
        public int MuzzleOffsetUnits { get; }
        public int MuzzleSafetyMarginUnits { get; }
        public uint RoundDurationTicks { get; }
        public uint RoundCountdownTicks { get; }
        public uint RoundEndDelayTicks { get; }
        public int RoundsToWin { get; }

        public static GameplayConfig CreateDefault()
        {
            return new GameplayConfig(100, 25, 100, 6, 300, 45, 100, 20, 70, 5, 1_800, 90, 30, 2);
        }
    }
}
