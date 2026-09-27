using System;

namespace LockstepArena.Simulation
{
    public static class BattleStateValueComparer
    {
        public static bool HaveSameValue(BattleState left, BattleState right)
        {
            if (left is null) throw new ArgumentNullException(nameof(left));
            if (right is null) throw new ArgumentNullException(nameof(right));
            if (left.Tick != right.Tick || left.PlayerCount != right.PlayerCount ||
                left.IsGameplayEnabled != right.IsGameplayEnabled ||
                !left.Roster.HasSameStructure(right.Roster)) return false;

            for (int index = 0; index < left.PlayerCount; index++)
            {
                var slot = new PlayerSlot(index);
                PlayerState a = left.GetPlayerState(slot);
                PlayerState b = right.GetPlayerState(slot);
                if (a.PositionX != b.PositionX || a.PositionZ != b.PositionZ || a.Aim != b.Aim) return false;
                if (left.IsGameplayEnabled && (a.HitPoints != b.HitPoints || a.RoundWins != b.RoundWins ||
                    a.FireCooldownTicks != b.FireCooldownTicks)) return false;
            }

            if (!left.IsGameplayEnabled) return true;
            if (!DefinitionsEqual(left.Definition!, right.Definition!) || left.Phase != right.Phase ||
                left.PhaseTicksRemaining != right.PhaseTicksRemaining ||
                left.RoundTicksRemaining != right.RoundTicksRemaining ||
                left.RoundResult != right.RoundResult ||
                left.MatchWinnerSlot != right.MatchWinnerSlot ||
                left.NextProjectileId != right.NextProjectileId ||
                left.ProjectileCount != right.ProjectileCount) return false;

            for (int index = 0; index < left.ProjectileCount; index++)
                if (left.GetProjectile(index) != right.GetProjectile(index)) return false;
            return true;
        }

        private static bool DefinitionsEqual(BattleDefinition left, BattleDefinition right)
        {
            GameplayConfig a = left.Gameplay;
            GameplayConfig b = right.Gameplay;
            if (a.MaxHitPoints != b.MaxHitPoints || a.ProjectileDamage != b.ProjectileDamage ||
                a.MoveUnitsPerTick != b.MoveUnitsPerTick || a.FireIntervalTicks != b.FireIntervalTicks ||
                a.ProjectileUnitsPerTick != b.ProjectileUnitsPerTick || a.ProjectileLifetimeTicks != b.ProjectileLifetimeTicks ||
                a.PlayerRadiusUnits != b.PlayerRadiusUnits || a.ProjectileRadiusUnits != b.ProjectileRadiusUnits ||
                a.MuzzleOffsetUnits != b.MuzzleOffsetUnits || a.MuzzleSafetyMarginUnits != b.MuzzleSafetyMarginUnits ||
                a.RoundDurationTicks != b.RoundDurationTicks || a.RoundCountdownTicks != b.RoundCountdownTicks ||
                a.RoundEndDelayTicks != b.RoundEndDelayTicks || a.RoundsToWin != b.RoundsToWin) return false;

            ArenaConfig x = left.Arena;
            ArenaConfig y = right.Arena;
            if (!string.Equals(x.ArenaId, y.ArenaId, StringComparison.Ordinal) || x.Bounds != y.Bounds ||
                x.SpawnCount != y.SpawnCount || x.ObstacleCount != y.ObstacleCount) return false;
            for (int index = 0; index < x.SpawnCount; index++) if (x.GetSpawn(index) != y.GetSpawn(index)) return false;
            for (int index = 0; index < x.ObstacleCount; index++) if (x.GetObstacle(index) != y.GetObstacle(index)) return false;
            return true;
        }
    }
}
