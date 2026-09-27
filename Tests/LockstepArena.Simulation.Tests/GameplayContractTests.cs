using System;
using LockstepArena.Simulation;

namespace LockstepArena.Simulation.Tests
{
    internal static class GameplayContractTests
    {
        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(GameplayConfigRejectsInvalidGeometryAndDurations), GameplayConfigRejectsInvalidGeometryAndDurations),
            new TestCase(nameof(ArenaConfigCopiesCallerOwnedArrays), ArenaConfigCopiesCallerOwnedArrays),
            new TestCase(nameof(GameplayInitialStateUsesSpawnsAndCompleteDefaults), GameplayInitialStateUsesSpawnsAndCompleteDefaults),
            new TestCase(nameof(LegacyInputFrameDefaultsFireToFalse), LegacyInputFrameDefaultsFireToFalse),
            new TestCase(nameof(GameplayInitialStateUsesRosterSlotsWithoutPlayerBranches), GameplayInitialStateUsesRosterSlotsWithoutPlayerBranches),
            new TestCase(nameof(GameplayInitialStateFacesVerticalSpawnsTowardEachOther), GameplayInitialStateFacesVerticalSpawnsTowardEachOther),
            new TestCase(nameof(BattleConfigHashMatchesEqualDefinitionsAndChangesWithGameplayOrArena), BattleConfigHashMatchesEqualDefinitionsAndChangesWithGameplayOrArena),
        };

        private static void GameplayConfigRejectsInvalidGeometryAndDurations()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(() => Config(moveUnitsPerTick: 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => Config(projectileLifetimeTicks: 0));
            TestAssert.Throws<ArgumentException>(() => Config(
                playerRadiusUnits: 100,
                projectileRadiusUnits: 20,
                muzzleOffsetUnits: 76,
                muzzleSafetyMarginUnits: 5));
        }

        private static void ArenaConfigCopiesCallerOwnedArrays()
        {
            ArenaPoint[] spawns = { new ArenaPoint(-400, 0), new ArenaPoint(400, 0) };
            ArenaRectangle[] obstacles = { new ArenaRectangle(-50, 50, -100, 100) };
            var arena = new ArenaConfig("copy", new ArenaRectangle(-1_000, 1_000, -800, 800), spawns, obstacles);

            spawns[0] = new ArenaPoint(999, 999);
            obstacles[0] = new ArenaRectangle(500, 600, 500, 600);

            TestAssert.Equal(new ArenaPoint(-400, 0), arena.GetSpawn(0));
            TestAssert.Equal(new ArenaRectangle(-50, 50, -100, 100), arena.GetObstacle(0));
        }

        private static void GameplayInitialStateUsesSpawnsAndCompleteDefaults()
        {
            ActiveRoster roster = Roster(11, 22);
            var definition = new BattleDefinition(Config(), Arena());
            BattleState state = BattleState.CreateGameplayInitial(roster, definition);

            TestAssert.Equal(true, state.IsGameplayEnabled);
            TestAssert.Equal(BattlePhase.RoundCountdown, state.Phase);
            TestAssert.Equal(3U, state.PhaseTicksRemaining);
            TestAssert.Equal(1_800U, state.RoundTicksRemaining);
            TestAssert.Equal(RoundResult.None, state.RoundResult);
            TestAssert.Equal(0, state.ProjectileCount);
            TestAssert.Equal(1UL, state.NextProjectileId);
            AssertPlayer(state, 0, -400, 0, 100, 0, 0, 0);
            AssertPlayer(state, 1, 400, 0, 100, 0, 0, 32_768);
        }

        private static void LegacyInputFrameDefaultsFireToFalse()
        {
            var legacy = new InputFrame(7, new PlayerSlot(0), 1, 0, 123);
            var firing = new InputFrame(7, new PlayerSlot(0), 1, 0, 123, true);

            TestAssert.Equal(false, legacy.Fire);
            TestAssert.Equal(true, firing.Fire);
        }

        private static void GameplayInitialStateUsesRosterSlotsWithoutPlayerBranches()
        {
            ActiveRoster roster = Roster(9003, 42);
            BattleState state = BattleState.CreateGameplayInitial(roster, new BattleDefinition(Config(), Arena()));

            TestAssert.Equal(new PlayerId(9003), state.Roster.GetPlayerId(new PlayerSlot(0)));
            TestAssert.Equal(new PlayerId(42), state.Roster.GetPlayerId(new PlayerSlot(1)));
            TestAssert.Equal(-400, state.GetPlayerState(new PlayerSlot(0)).PositionX);
            TestAssert.Equal(400, state.GetPlayerState(new PlayerSlot(1)).PositionX);
        }

        private static void GameplayInitialStateFacesVerticalSpawnsTowardEachOther()
        {
            var arena = new ArenaConfig(
                "vertical-facing",
                new ArenaRectangle(-1_000, 1_000, -1_000, 1_000),
                new[] { new ArenaPoint(0, -400), new ArenaPoint(0, 400) },
                Array.Empty<ArenaRectangle>());

            BattleState state = BattleState.CreateGameplayInitial(
                Roster(11, 22),
                new BattleDefinition(Config(), arena));

            AssertPlayer(state, 0, 0, -400, 100, 0, 0, 16_384);
            AssertPlayer(state, 1, 0, 400, 100, 0, 0, 49_152);
        }

        private static void BattleConfigHashMatchesEqualDefinitionsAndChangesWithGameplayOrArena()
        {
            BattleDefinition first = BattleDefinition.CreateDefault();
            BattleDefinition second = BattleDefinition.CreateDefault();
            ulong expected = BattleConfigHash.Compute(first);

            TestAssert.Equal(expected, BattleConfigHash.Compute(second));
            TestAssert.NotEqual(expected, BattleConfigHash.Compute(new BattleDefinition(
                new GameplayConfig(101, 25, 100, 6, 300, 45, 100, 20, 70, 5, 1_800, 90, 30, 2),
                second.Arena)));
            TestAssert.NotEqual(expected, BattleConfigHash.Compute(new BattleDefinition(
                second.Gameplay,
                new ArenaConfig(
                    "symmetric-v2",
                    second.Arena.Bounds,
                    new[] { second.Arena.GetSpawn(0), second.Arena.GetSpawn(1) },
                    new[]
                    {
                        second.Arena.GetObstacle(0),
                        second.Arena.GetObstacle(1),
                        second.Arena.GetObstacle(2),
                    }))));
        }

        internal static GameplayConfig Config(
            int moveUnitsPerTick = 100,
            uint projectileLifetimeTicks = 10,
            int playerRadiusUnits = 100,
            int projectileRadiusUnits = 20,
            int muzzleOffsetUnits = 70,
            int muzzleSafetyMarginUnits = 5,
            int projectileUnitsPerTick = 250,
            uint fireIntervalTicks = 3,
            uint roundDurationTicks = 1_800,
            uint roundCountdownTicks = 3,
            uint roundEndDelayTicks = 2,
            int maxHitPoints = 100,
            int projectileDamage = 25,
            int roundsToWin = 2)
        {
            return new GameplayConfig(
                maxHitPoints,
                projectileDamage,
                moveUnitsPerTick,
                fireIntervalTicks,
                projectileUnitsPerTick,
                projectileLifetimeTicks,
                playerRadiusUnits,
                projectileRadiusUnits,
                muzzleOffsetUnits,
                muzzleSafetyMarginUnits,
                roundDurationTicks,
                roundCountdownTicks,
                roundEndDelayTicks,
                roundsToWin);
        }

        internal static ArenaConfig Arena(params ArenaRectangle[] obstacles)
        {
            return new ArenaConfig(
                "test-arena",
                new ArenaRectangle(-1_000, 1_000, -800, 800),
                new[] { new ArenaPoint(-400, 0), new ArenaPoint(400, 0) },
                obstacles);
        }

        internal static ActiveRoster Roster(params ulong[] ids)
        {
            var players = new PlayerId[ids.Length];
            for (int index = 0; index < ids.Length; index++) players[index] = new PlayerId(ids[index]);
            return new ActiveRoster(players);
        }

        internal static void AssertPlayer(BattleState state, int slot, int x, int z, int hp, int wins, uint cooldown, ushort aim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slot));
            TestAssert.Equal(x, player.PositionX);
            TestAssert.Equal(z, player.PositionZ);
            TestAssert.Equal(hp, player.HitPoints);
            TestAssert.Equal(wins, player.RoundWins);
            TestAssert.Equal(cooldown, player.FireCooldownTicks);
            TestAssert.Equal(aim, player.Aim);
        }
    }
}
