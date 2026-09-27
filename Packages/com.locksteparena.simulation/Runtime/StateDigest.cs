namespace LockstepArena.Simulation
{
    public static class StateDigest
    {
        private const ulong OffsetBasis = 14_695_981_039_346_656_037UL;
        private const ulong Prime = 1_099_511_628_211UL;

        public static ulong Compute(BattleState state)
        {
            ulong hash = OffsetBasis;
            AddUInt32(ref hash, state.Tick);
            AddUInt32(ref hash, checked((uint)state.PlayerCount));

            for (int index = 0; index < state.PlayerCount; index++)
            {
                PlayerSlot slot = new PlayerSlot(index);
                AddUInt64(ref hash, state.Roster.GetPlayerId(slot).Value);
                PlayerState player = state.GetPlayerState(slot);
                AddInt32(ref hash, player.PositionX);
                AddInt32(ref hash, player.PositionZ);
                AddUInt16(ref hash, player.Aim);
                if (state.IsGameplayEnabled)
                {
                    AddInt32(ref hash, player.HitPoints);
                    AddInt32(ref hash, player.RoundWins);
                    AddUInt32(ref hash, player.FireCooldownTicks);
                }
            }

            if (state.IsGameplayEnabled) AddGameplay(ref hash, state);

            return hash;
        }

        private static void AddGameplay(ref ulong hash, BattleState state)
        {
            AddByte(ref hash, 1);
            AddInt32(ref hash, (int)state.Phase);
            AddUInt32(ref hash, state.PhaseTicksRemaining);
            AddUInt32(ref hash, state.RoundTicksRemaining);
            AddInt32(ref hash, (int)state.RoundResult.Kind);
            AddByte(ref hash, state.RoundResult.HasWinner ? (byte)1 : (byte)0);
            if (state.RoundResult.HasWinner) AddInt32(ref hash, state.RoundResult.WinnerSlot.Value);
            AddByte(ref hash, state.MatchWinnerSlot.HasValue ? (byte)1 : (byte)0);
            if (state.MatchWinnerSlot.HasValue) AddInt32(ref hash, state.MatchWinnerSlot.Value.Value);
            AddUInt64(ref hash, state.NextProjectileId);
            AddUInt32(ref hash, checked((uint)state.ProjectileCount));
            for (int index = 0; index < state.ProjectileCount; index++)
            {
                ProjectileState projectile = state.GetProjectile(index);
                AddUInt64(ref hash, projectile.ProjectileId);
                AddUInt64(ref hash, projectile.OwnerPlayerId.Value);
                AddInt32(ref hash, projectile.PositionX);
                AddInt32(ref hash, projectile.PositionZ);
                AddInt32(ref hash, projectile.DirectionX);
                AddInt32(ref hash, projectile.DirectionZ);
                AddUInt32(ref hash, projectile.LifetimeTicksRemaining);
            }

            BattleDefinition definition = state.Definition!;
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
            AddString(ref hash, definition.Arena.ArenaId);
            AddRectangle(ref hash, definition.Arena.Bounds);
            AddUInt32(ref hash, checked((uint)definition.Arena.SpawnCount));
            for (int index = 0; index < definition.Arena.SpawnCount; index++)
            {
                ArenaPoint spawn = definition.Arena.GetSpawn(index);
                AddInt32(ref hash, spawn.X);
                AddInt32(ref hash, spawn.Z);
            }
            AddUInt32(ref hash, checked((uint)definition.Arena.ObstacleCount));
            for (int index = 0; index < definition.Arena.ObstacleCount; index++)
                AddRectangle(ref hash, definition.Arena.GetObstacle(index));
        }

        private static void AddRectangle(ref ulong hash, ArenaRectangle rectangle)
        {
            AddInt32(ref hash, rectangle.MinX);
            AddInt32(ref hash, rectangle.MaxX);
            AddInt32(ref hash, rectangle.MinZ);
            AddInt32(ref hash, rectangle.MaxZ);
        }

        private static void AddString(ref ulong hash, string value)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
            AddUInt32(ref hash, checked((uint)bytes.Length));
            for (int index = 0; index < bytes.Length; index++) AddByte(ref hash, bytes[index]);
        }

        private static void AddUInt64(ref ulong hash, ulong value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
            AddByte(ref hash, (byte)(value >> 16));
            AddByte(ref hash, (byte)(value >> 24));
            AddByte(ref hash, (byte)(value >> 32));
            AddByte(ref hash, (byte)(value >> 40));
            AddByte(ref hash, (byte)(value >> 48));
            AddByte(ref hash, (byte)(value >> 56));
        }

        private static void AddInt32(ref ulong hash, int value)
        {
            AddUInt32(ref hash, unchecked((uint)value));
        }

        private static void AddUInt32(ref ulong hash, uint value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
            AddByte(ref hash, (byte)(value >> 16));
            AddByte(ref hash, (byte)(value >> 24));
        }

        private static void AddUInt16(ref ulong hash, ushort value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash = unchecked((hash ^ value) * Prime);
        }
    }
}
