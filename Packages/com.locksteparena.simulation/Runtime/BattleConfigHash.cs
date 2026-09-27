using System;
using System.Text;

namespace LockstepArena.Simulation
{
    public static class BattleConfigHash
    {
        private const ulong OffsetBasis = 14_695_981_039_346_656_037UL;
        private const ulong Prime = 1_099_511_628_211UL;

        public static ulong Compute(BattleDefinition definition)
        {
            if (definition is null) throw new ArgumentNullException(nameof(definition));

            ulong hash = OffsetBasis;
            GameplayConfig gameplay = definition.Gameplay;
            AddInt32(ref hash, gameplay.MaxHitPoints);
            AddInt32(ref hash, gameplay.ProjectileDamage);
            AddInt32(ref hash, gameplay.MoveUnitsPerTick);
            AddUInt32(ref hash, gameplay.FireIntervalTicks);
            AddInt32(ref hash, gameplay.ProjectileUnitsPerTick);
            AddUInt32(ref hash, gameplay.ProjectileLifetimeTicks);
            AddInt32(ref hash, gameplay.PlayerRadiusUnits);
            AddInt32(ref hash, gameplay.ProjectileRadiusUnits);
            AddInt32(ref hash, gameplay.MuzzleOffsetUnits);
            AddInt32(ref hash, gameplay.MuzzleSafetyMarginUnits);
            AddUInt32(ref hash, gameplay.RoundDurationTicks);
            AddUInt32(ref hash, gameplay.RoundCountdownTicks);
            AddUInt32(ref hash, gameplay.RoundEndDelayTicks);
            AddInt32(ref hash, gameplay.RoundsToWin);

            ArenaConfig arena = definition.Arena;
            AddString(ref hash, arena.ArenaId);
            AddRectangle(ref hash, arena.Bounds);
            AddUInt32(ref hash, checked((uint)arena.SpawnCount));
            for (int index = 0; index < arena.SpawnCount; index++)
            {
                ArenaPoint spawn = arena.GetSpawn(index);
                AddInt32(ref hash, spawn.X);
                AddInt32(ref hash, spawn.Z);
            }

            AddUInt32(ref hash, checked((uint)arena.ObstacleCount));
            for (int index = 0; index < arena.ObstacleCount; index++)
                AddRectangle(ref hash, arena.GetObstacle(index));
            return hash;
        }

        private static void AddRectangle(ref ulong hash, ArenaRectangle value)
        {
            AddInt32(ref hash, value.MinX);
            AddInt32(ref hash, value.MaxX);
            AddInt32(ref hash, value.MinZ);
            AddInt32(ref hash, value.MaxZ);
        }

        private static void AddString(ref ulong hash, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            AddUInt32(ref hash, checked((uint)bytes.Length));
            for (int index = 0; index < bytes.Length; index++) AddByte(ref hash, bytes[index]);
        }

        private static void AddInt32(ref ulong hash, int value) => AddUInt32(ref hash, unchecked((uint)value));

        private static void AddUInt32(ref ulong hash, uint value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
            AddByte(ref hash, (byte)(value >> 16));
            AddByte(ref hash, (byte)(value >> 24));
        }

        private static void AddByte(ref ulong hash, byte value) => hash = unchecked((hash ^ value) * Prime);
    }
}
