using System;
using System.Collections.Generic;
using LockstepArena.Simulation;

namespace LockstepArena.Simulation.Tests
{
    internal static class GameplaySimulationTests
    {
        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(CountdownIgnoresMoveAimAndFire), CountdownIgnoresMoveAimAndFire),
            new TestCase(nameof(ArenaBoundsAndObstacleBlockMovement), ArenaBoundsAndObstacleBlockMovement),
            new TestCase(nameof(DiagonalMovementSlidesAlongBlockedAxis), DiagonalMovementSlidesAlongBlockedAxis),
            new TestCase(nameof(HeldFireUsesCooldownAndSpawnTickMovement), HeldFireUsesCooldownAndSpawnTickMovement),
            new TestCase(nameof(ProjectileLifetimeExpiresAfterConfiguredTicks), ProjectileLifetimeExpiresAfterConfiguredTicks),
            new TestCase(nameof(ProjectileSweepPreventsHighSpeedTunneling), ProjectileSweepPreventsHighSpeedTunneling),
            new TestCase(nameof(WallBeforePlayerConsumesProjectileWithoutDamage), WallBeforePlayerConsumesProjectileWithoutDamage),
            new TestCase(nameof(PlayerBeforeWallTakesOneDamageOnly), PlayerBeforeWallTakesOneDamageOnly),
            new TestCase(nameof(PlayerSlightlyEarlierInsideOneOldSamplingCellWins), PlayerSlightlyEarlierInsideOneOldSamplingCellWins),
            new TestCase(nameof(ObstacleSlightlyEarlierInsideOneOldSamplingCellWins), ObstacleSlightlyEarlierInsideOneOldSamplingCellWins),
            new TestCase(nameof(ExactObstaclePlayerTiePrefersObstacle), ExactObstaclePlayerTiePrefersObstacle),
            new TestCase(nameof(ProjectileOwnerIsIgnored), ProjectileOwnerIsIgnored),
            new TestCase(nameof(NonCardinalAimsProduceDistinctFixedDirections), NonCardinalAimsProduceDistinctFixedDirections),
            new TestCase(nameof(SameNonCardinalAimProducesIdenticalState), SameNonCardinalAimProducesIdenticalState),
            new TestCase(nameof(AimWrapAroundUsesAdjacentDirections), AimWrapAroundUsesAdjacentDirections),
            new TestCase(nameof(SameTickDoubleKnockoutIsDraw), SameTickDoubleKnockoutIsDraw),
            new TestCase(nameof(TimeoutChoosesUniqueHighestHpOrDraw), TimeoutChoosesUniqueHighestHpOrDraw),
            new TestCase(nameof(LethalHitWinsBeforeSameTickTimeoutComparison), LethalHitWinsBeforeSameTickTimeoutComparison),
            new TestCase(nameof(RoundResetRestoresRoundFieldsAndPreservesWins), RoundResetRestoresRoundFieldsAndPreservesWins),
            new TestCase(nameof(RoundResetFacesDiagonalSpawnsTowardEachOther), RoundResetFacesDiagonalSpawnsTowardEachOther),
            new TestCase(nameof(SecondRoundWinEndsBestOfThree), SecondRoundWinEndsBestOfThree),
            new TestCase(nameof(GameplayTwinSimulationsRemainIdentical), GameplayTwinSimulationsRemainIdentical),
        };

        private static void CountdownIgnoresMoveAimAndFire()
        {
            BattleState initial = Initial();
            var simulation = new BattleSimulation(initial);
            simulation.Step(Frame(initial, Input(initial, 0, 1, 1, 16_384, true), Input(initial, 1, -1, 0, 0, true)));
            BattleState state = simulation.State;
            GameplayContractTests.AssertPlayer(state, 0, -400, 0, 100, 0, 0, 0);
            TestAssert.Equal(0, state.ProjectileCount);
            TestAssert.Equal(2U, state.PhaseTicksRemaining);
            TestAssert.Equal(1_800U, state.RoundTicksRemaining);
        }

        private static void ArenaBoundsAndObstacleBlockMovement()
        {
            BattleDefinition definition = Definition(obstacles: new[] { new ArenaRectangle(-50, 50, -200, 200) });
            BattleState state = State(definition, BattlePhase.Playing, 100, 0,
                Player(-200, 0), Player(850, 400));
            var simulation = new BattleSimulation(state);
            simulation.Step(Frame(state, Input(state, 0, 1, 0), Input(state, 1, 1, 0)));
            TestAssert.Equal(-150, simulation.State.GetPlayerState(new PlayerSlot(0)).PositionX);
            TestAssert.Equal(900, simulation.State.GetPlayerState(new PlayerSlot(1)).PositionX);
        }

        private static void DiagonalMovementSlidesAlongBlockedAxis()
        {
            BattleDefinition definition = Definition(obstacles: new[] { new ArenaRectangle(-50, 50, -200, 200) });
            BattleState state = State(definition, BattlePhase.Playing, 100, 0,
                Player(-200, 0), Player(400, 400));
            var simulation = new BattleSimulation(state);
            simulation.Step(Frame(state, Input(state, 0, 1, 1), Input(state, 1)));
            PlayerState moved = simulation.State.GetPlayerState(new PlayerSlot(0));
            TestAssert.Equal(-150, moved.PositionX);
            TestAssert.Equal(70, moved.PositionZ);
        }

        private static void HeldFireUsesCooldownAndSpawnTickMovement()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 100, fireIntervalTicks: 3, projectileLifetimeTicks: 20);
            BattleState state = State(definition, BattlePhase.Playing, 100, 0, Player(-700, 400), Player(700, 400));
            var simulation = new BattleSimulation(state);

            simulation.Step(Frame(state, Input(state, 0, aim: 0, fire: true), Input(state, 1)));
            TestAssert.Equal(1, simulation.State.ProjectileCount);
            TestAssert.Equal(-530, simulation.State.GetProjectile(0).PositionX);
            TestAssert.Equal(3U, simulation.State.GetPlayerState(new PlayerSlot(0)).FireCooldownTicks);
            for (int index = 0; index < 2; index++)
            {
                BattleState current = simulation.State;
                simulation.Step(Frame(current, Input(current, 0, aim: 0, fire: true), Input(current, 1)));
            }
            TestAssert.Equal(1, simulation.State.ProjectileCount);
            BattleState beforeSecond = simulation.State;
            simulation.Step(Frame(beforeSecond, Input(beforeSecond, 0, aim: 0, fire: true), Input(beforeSecond, 1)));
            TestAssert.Equal(2, simulation.State.ProjectileCount);
            TestAssert.Equal(2UL, simulation.State.GetProjectile(1).ProjectileId);
        }

        private static void ProjectileLifetimeExpiresAfterConfiguredTicks()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 100);
            ProjectileState projectile = Projectile(1, 11, -500, 500, 1000, 0, 2);
            BattleState state = State(definition, BattlePhase.Playing, 100, 0, new[] { Player(-700, 500), Player(700, 500) }, new[] { projectile }, 2);
            var simulation = new BattleSimulation(state);
            simulation.Step(NeutralFrame(state));
            TestAssert.Equal(1, simulation.State.ProjectileCount);
            TestAssert.Equal(-400, simulation.State.GetProjectile(0).PositionX);
            simulation.Step(NeutralFrame(simulation.State));
            TestAssert.Equal(0, simulation.State.ProjectileCount);
        }

        private static void ProjectileSweepPreventsHighSpeedTunneling()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 1_000);
            BattleState state = State(definition, BattlePhase.Playing, 100, 0,
                new[] { Player(-800, 0), Player(0, 0) },
                new[] { Projectile(1, 11, -500, 0, 1000, 0, 5) }, 2);
            BattleState result = Step(state);
            TestAssert.Equal(75, result.GetPlayerState(new PlayerSlot(1)).HitPoints);
            TestAssert.Equal(0, result.ProjectileCount);
        }

        private static void WallBeforePlayerConsumesProjectileWithoutDamage()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 1_000,
                obstacles: new[] { new ArenaRectangle(-100, 100, -100, 100) });
            BattleState state = State(definition, BattlePhase.Playing, 100, 0,
                new[] { Player(-800, 0), Player(350, 0) },
                new[] { Projectile(1, 11, -500, 0, 1000, 0, 5) }, 2);
            BattleState result = Step(state);
            TestAssert.Equal(100, result.GetPlayerState(new PlayerSlot(1)).HitPoints);
            TestAssert.Equal(0, result.ProjectileCount);
        }

        private static void PlayerBeforeWallTakesOneDamageOnly()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 1_000,
                obstacles: new[] { new ArenaRectangle(300, 400, -100, 100) });
            BattleState state = State(definition, BattlePhase.Playing, 100, 0,
                new[] { Player(-800, 0), Player(0, 0) },
                new[] { Projectile(1, 11, -500, 0, 1000, 0, 5) }, 2);
            BattleState result = Step(state);
            TestAssert.Equal(75, result.GetPlayerState(new PlayerSlot(1)).HitPoints);
            TestAssert.Equal(0, result.ProjectileCount);
            result = Step(result);
            TestAssert.Equal(75, result.GetPlayerState(new PlayerSlot(1)).HitPoints);
        }

        private static void PlayerSlightlyEarlierInsideOneOldSamplingCellWins()
        {
            BattleState result = Step(CloseCollisionOrderingState(playerX: 100, playerZ: 66));

            TestAssert.Equal(75, result.GetPlayerState(new PlayerSlot(1)).HitPoints);
            TestAssert.Equal(0, result.ProjectileCount);
        }

        private static void ObstacleSlightlyEarlierInsideOneOldSamplingCellWins()
        {
            BattleState result = Step(CloseCollisionOrderingState(playerX: 100, playerZ: 67));

            TestAssert.Equal(100, result.GetPlayerState(new PlayerSlot(1)).HitPoints);
            TestAssert.Equal(0, result.ProjectileCount);
        }

        private static void ExactObstaclePlayerTiePrefersObstacle()
        {
            BattleState result = Step(CloseCollisionOrderingState(playerX: 96, playerZ: 72));

            TestAssert.Equal(100, result.GetPlayerState(new PlayerSlot(1)).HitPoints);
            TestAssert.Equal(0, result.ProjectileCount);
        }

        private static void ProjectileOwnerIsIgnored()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 300);
            BattleState state = State(definition, BattlePhase.Playing, 100, 0,
                new[] { Player(0, 0), Player(800, 0) },
                new[] { Projectile(1, 11, -200, 0, 1000, 0, 5) }, 2);
            BattleState result = Step(state);
            TestAssert.Equal(100, result.GetPlayerState(new PlayerSlot(0)).HitPoints);
            TestAssert.Equal(1, result.ProjectileCount);
        }

        private static void NonCardinalAimsProduceDistinctFixedDirections()
        {
            AssertProjectileDirection(4_096, 924, 383);
            AssertProjectileDirection(8_192, 707, 707);
            AssertProjectileDirection(12_288, 383, 924);
        }

        private static void SameNonCardinalAimProducesIdenticalState()
        {
            BattleState left = FireOnce(12_345);
            BattleState right = FireOnce(12_345);

            TestAssert.Equal(true, BattleStateValueComparer.HaveSameValue(left, right));
            TestAssert.Equal(StateDigest.Compute(left), StateDigest.Compute(right));
        }

        private static void AimWrapAroundUsesAdjacentDirections()
        {
            AssertProjectileDirection(0, 1000, 0);
            AssertProjectileDirection(ushort.MaxValue, 1000, 0);
            AssertProjectileDirection(64_512, 995, -98);
        }

        private static void SameTickDoubleKnockoutIsDraw()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 300, projectileDamage: 25);
            BattleState state = State(definition, BattlePhase.Playing, 100, 0,
                new[] { Player(-200, 0, 25), Player(200, 0, 25) },
                new[] { Projectile(1, 11, -100, 0, 1000, 0, 5), Projectile(2, 22, 100, 0, -1000, 0, 5) }, 3);
            BattleState result = Step(state);
            TestAssert.Equal(RoundResult.Draw, result.RoundResult);
            TestAssert.Equal(BattlePhase.RoundEnded, result.Phase);
            TestAssert.Equal(0, result.GetPlayerState(new PlayerSlot(0)).RoundWins);
            TestAssert.Equal(0, result.GetPlayerState(new PlayerSlot(1)).RoundWins);
            TestAssert.Equal(0, result.ProjectileCount);
        }

        private static void TimeoutChoosesUniqueHighestHpOrDraw()
        {
            BattleDefinition definition = Definition();
            BattleState winnerState = State(definition, BattlePhase.Playing, 1, 0, Player(-400, 0, 75), Player(400, 0, 50));
            BattleState winner = Step(winnerState);
            TestAssert.Equal(RoundResult.PlayerWin(new PlayerSlot(0)), winner.RoundResult);
            BattleState drawState = State(definition, BattlePhase.Playing, 1, 0, Player(-400, 0, 50), Player(400, 0, 50));
            TestAssert.Equal(RoundResult.Draw, Step(drawState).RoundResult);
        }

        private static void LethalHitWinsBeforeSameTickTimeoutComparison()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 500, projectileDamage: 25);
            BattleState state = State(definition, BattlePhase.Playing, 1, 0,
                new[] { Player(-400, 0, 1), Player(0, 0, 25) },
                new[] { Projectile(1, 11, -300, 0, 1000, 0, 5) }, 2);
            BattleState result = Step(state);
            TestAssert.Equal(RoundResult.PlayerWin(new PlayerSlot(0)), result.RoundResult);
        }

        private static void RoundResetRestoresRoundFieldsAndPreservesWins()
        {
            BattleDefinition definition = Definition();
            BattleState state = State(definition, BattlePhase.RoundEnded, 100, 1,
                new[] { Player(100, 100, 20, 1, 4, 123), Player(-100, -100, 0, 0, 2, 456) },
                Array.Empty<ProjectileState>(), 8, RoundResult.PlayerWin(new PlayerSlot(0)));
            BattleState result = Step(state);
            TestAssert.Equal(BattlePhase.RoundCountdown, result.Phase);
            TestAssert.Equal(3U, result.PhaseTicksRemaining);
            TestAssert.Equal(1_800U, result.RoundTicksRemaining);
            TestAssert.Equal(RoundResult.None, result.RoundResult);
            GameplayContractTests.AssertPlayer(result, 0, -400, 0, 100, 1, 0, 0);
            GameplayContractTests.AssertPlayer(result, 1, 400, 0, 100, 0, 0, 32_768);
            TestAssert.Equal(8UL, result.NextProjectileId);
        }

        private static void RoundResetFacesDiagonalSpawnsTowardEachOther()
        {
            var arena = new ArenaConfig(
                "diagonal-reset",
                new ArenaRectangle(-1_000, 1_000, -1_000, 1_000),
                new[] { new ArenaPoint(-400, -400), new ArenaPoint(400, 400) },
                Array.Empty<ArenaRectangle>());
            var definition = new BattleDefinition(GameplayContractTests.Config(), arena);
            BattleState state = State(
                definition,
                BattlePhase.RoundEnded,
                100,
                1,
                Player(100, 100, aim: 999),
                Player(-100, -100, aim: 888));

            BattleState result = Step(state);

            GameplayContractTests.AssertPlayer(result, 0, -400, -400, 100, 0, 0, 8_192);
            GameplayContractTests.AssertPlayer(result, 1, 400, 400, 100, 0, 0, 40_960);
        }

        private static void SecondRoundWinEndsBestOfThree()
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 500, projectileDamage: 25);
            BattleState state = State(definition, BattlePhase.Playing, 100, 0,
                new[] { Player(-400, 0, 100, 1), Player(0, 0, 25) },
                new[] { Projectile(1, 11, -300, 0, 1000, 0, 5) }, 2);
            BattleState result = Step(state);
            TestAssert.Equal(BattlePhase.MatchEnded, result.Phase);
            TestAssert.Equal(new PlayerSlot(0), result.MatchWinnerSlot!.Value);
            TestAssert.Equal(2, result.GetPlayerState(new PlayerSlot(0)).RoundWins);
        }

        private static void GameplayTwinSimulationsRemainIdentical()
        {
            BattleState initial = Initial();
            var left = new BattleSimulation(initial);
            var right = new BattleSimulation(initial);
            for (int index = 0; index < 120; index++)
            {
                BattleState current = left.State;
                bool fire = (index % 4) == 0;
                FrameData frame = Frame(current,
                    Input(current, 0, (sbyte)(index % 3 - 1), 1, 0, fire),
                    Input(current, 1, (sbyte)(1 - index % 3), -1, 32_768, fire));
                left.Step(frame);
                right.Step(frame);
                TestAssert.Equal(StateDigest.Compute(left.State), StateDigest.Compute(right.State));
            }
        }

        private static BattleState Initial() => BattleState.CreateGameplayInitial(GameplayContractTests.Roster(11, 22), Definition());

        private static BattleState CloseCollisionOrderingState(int playerX, int playerZ)
        {
            BattleDefinition definition = Definition(
                projectileUnitsPerTick: 250,
                obstacles: new[] { new ArenaRectangle(20, 30, -5, 5) });
            return State(
                definition,
                BattlePhase.Playing,
                100,
                0,
                new[] { Player(-800, 0), Player(playerX, playerZ) },
                new[] { Projectile(1, 11, -10, 0, 1000, 0, 5) },
                2);
        }

        private static BattleState FireOnce(ushort aim)
        {
            BattleDefinition definition = Definition(projectileUnitsPerTick: 1);
            BattleState state = State(
                definition,
                BattlePhase.Playing,
                100,
                0,
                Player(-700, 400),
                Player(700, -400));
            var simulation = new BattleSimulation(state);
            simulation.Step(Frame(state, Input(state, 0, aim: aim, fire: true), Input(state, 1)));
            return simulation.State;
        }

        private static void AssertProjectileDirection(ushort aim, int expectedX, int expectedZ)
        {
            ProjectileState projectile = FireOnce(aim).GetProjectile(0);
            TestAssert.Equal(expectedX, projectile.DirectionX);
            TestAssert.Equal(expectedZ, projectile.DirectionZ);
        }

        private static BattleDefinition Definition(
            int projectileUnitsPerTick = 250,
            uint fireIntervalTicks = 3,
            uint projectileLifetimeTicks = 10,
            int projectileDamage = 25,
            ArenaRectangle[]? obstacles = null)
        {
            return new BattleDefinition(
                GameplayContractTests.Config(projectileUnitsPerTick: projectileUnitsPerTick, fireIntervalTicks: fireIntervalTicks,
                    projectileLifetimeTicks: projectileLifetimeTicks, projectileDamage: projectileDamage),
                GameplayContractTests.Arena(obstacles ?? Array.Empty<ArenaRectangle>()));
        }

        private static PlayerState Player(int x, int z, int hp = 100, int wins = 0, uint cooldown = 0, ushort aim = 0) =>
            new PlayerState(x, z, aim, hp, wins, cooldown);

        private static ProjectileState Projectile(ulong id, ulong owner, int x, int z, int dx, int dz, uint life) =>
            new ProjectileState(id, new PlayerId(owner), x, z, dx, dz, life);

        private static BattleState State(BattleDefinition definition, BattlePhase phase, uint roundTicks, uint phaseTicks, params PlayerState[] players) =>
            State(definition, phase, roundTicks, phaseTicks, players, Array.Empty<ProjectileState>(), 1);

        private static BattleState State(BattleDefinition definition, BattlePhase phase, uint roundTicks, uint phaseTicks,
            IReadOnlyList<PlayerState> players, IReadOnlyList<ProjectileState> projectiles, ulong nextProjectileId,
            RoundResult? roundResult = null) =>
            new BattleState(0, GameplayContractTests.Roster(11, 22), players, definition, phase, phaseTicks, roundTicks,
                roundResult ?? RoundResult.None, null, projectiles, nextProjectileId);

        private static BattleState Step(BattleState state)
        {
            var simulation = new BattleSimulation(state);
            simulation.Step(NeutralFrame(state));
            return simulation.State;
        }

        private static FrameData NeutralFrame(BattleState state) => Frame(state, Input(state, 0), Input(state, 1));
        private static FrameData Frame(BattleState state, params InputFrame[] inputs) => FrameData.Create(state.Roster, state.Tick, inputs);
        private static InputFrame Input(BattleState state, int slot, sbyte moveX = 0, sbyte moveZ = 0, ushort aim = 0, bool fire = false) =>
            new InputFrame(state.Tick, new PlayerSlot(slot), moveX, moveZ, aim, fire);
    }
}
