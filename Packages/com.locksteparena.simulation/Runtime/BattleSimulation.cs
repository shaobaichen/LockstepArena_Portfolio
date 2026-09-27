using System;
using System.Collections.Generic;
using System.Numerics;

namespace LockstepArena.Simulation
{
    public sealed class BattleSimulation
    {
        public BattleSimulation(BattleState initialState)
        {
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
        }

        public BattleState State { get; private set; }

        public void Step(FrameData frame)
        {
            if (frame is null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            BattleState current = State;
            if (frame.Tick != current.Tick)
            {
                throw new ArgumentException("Frame tick must match the current simulation tick.", nameof(frame));
            }

            if (!current.Roster.HasSameStructure(frame.Roster))
            {
                throw new ArgumentException("Frame roster must match the simulation roster.", nameof(frame));
            }

            if (current.IsGameplayEnabled)
            {
                State = StepGameplay(current, frame);
                return;
            }

            PlayerState[] nextPlayers = new PlayerState[current.PlayerCount];
            for (int index = 0; index < nextPlayers.Length; index++)
            {
                PlayerSlot slot = new PlayerSlot(index);
                nextPlayers[index] = Move(current.GetPlayerState(slot), frame.GetInput(slot));
            }

            uint nextTick = checked(current.Tick + 1);
            BattleState nextState = new BattleState(nextTick, current.Roster, nextPlayers);
            State = nextState;
        }

        private static BattleState StepGameplay(BattleState current, FrameData frame)
        {
            uint nextTick = checked(current.Tick + 1);
            BattleDefinition definition = current.Definition!;
            GameplayConfig config = definition.Gameplay;

            if (current.Phase == BattlePhase.MatchEnded)
                return Copy(current, nextTick, current.Phase, current.PhaseTicksRemaining, current.RoundTicksRemaining,
                    current.RoundResult, current.MatchWinnerSlot, CopyPlayers(current), CopyProjectiles(current), current.NextProjectileId);

            if (current.Phase == BattlePhase.RoundCountdown)
            {
                uint remaining = current.PhaseTicksRemaining > 0 ? current.PhaseTicksRemaining - 1 : 0;
                BattlePhase phase = remaining == 0 ? BattlePhase.Playing : BattlePhase.RoundCountdown;
                return Copy(current, nextTick, phase, remaining, current.RoundTicksRemaining,
                    current.RoundResult, null, CopyPlayers(current), CopyProjectiles(current), current.NextProjectileId);
            }

            if (current.Phase == BattlePhase.RoundEnded)
            {
                uint remaining = current.PhaseTicksRemaining > 0 ? current.PhaseTicksRemaining - 1 : 0;
                if (remaining == 0) return ResetRound(current, nextTick);
                return Copy(current, nextTick, BattlePhase.RoundEnded, remaining, current.RoundTicksRemaining,
                    current.RoundResult, null, CopyPlayers(current), Array.Empty<ProjectileState>(), current.NextProjectileId);
            }

            PlayerState[] players = CopyPlayers(current);
            for (int index = 0; index < players.Length; index++)
            {
                InputFrame input = frame.GetInput(new PlayerSlot(index));
                PlayerState player = players[index];
                ArenaPoint moved = MoveGameplay(player, input, definition);
                uint cooldown = player.FireCooldownTicks > 0 ? player.FireCooldownTicks - 1 : 0;
                players[index] = new PlayerState(moved.X, moved.Z, input.Aim, player.HitPoints, player.RoundWins, cooldown);
            }

            var projectiles = new List<ProjectileState>(current.ProjectileCount + current.PlayerCount);
            for (int index = 0; index < current.ProjectileCount; index++) projectiles.Add(current.GetProjectile(index));
            ulong nextProjectileId = current.NextProjectileId;
            for (int index = 0; index < players.Length; index++)
            {
                InputFrame input = frame.GetInput(new PlayerSlot(index));
                PlayerState player = players[index];
                if (!input.Fire || player.FireCooldownTicks != 0) continue;
                GetAimDirection(input.Aim, out int directionX, out int directionZ);
                int spawnX = AddScaled(player.PositionX, directionX, config.MuzzleOffsetUnits);
                int spawnZ = AddScaled(player.PositionZ, directionZ, config.MuzzleOffsetUnits);
                projectiles.Add(new ProjectileState(nextProjectileId, current.Roster.GetPlayerId(new PlayerSlot(index)),
                    spawnX, spawnZ, directionX, directionZ, config.ProjectileLifetimeTicks));
                nextProjectileId = checked(nextProjectileId + 1);
                players[index] = new PlayerState(player.PositionX, player.PositionZ, player.Aim,
                    player.HitPoints, player.RoundWins, config.FireIntervalTicks);
            }

            var survivors = new List<ProjectileState>(projectiles.Count);
            var damage = new long[players.Length];
            for (int index = 0; index < projectiles.Count; index++)
            {
                ProjectileState projectile = projectiles[index];
                int deltaX = Scale(projectile.DirectionX, config.ProjectileUnitsPerTick);
                int deltaZ = Scale(projectile.DirectionZ, config.ProjectileUnitsPerTick);
                int hitSlot = Sweep(projectile, deltaX, deltaZ, current.Roster, players, definition);
                if (hitSlot >= 0)
                {
                    damage[hitSlot] = checked(damage[hitSlot] + config.ProjectileDamage);
                    continue;
                }
                if (hitSlot == ObstacleHit) continue;
                uint lifetime = projectile.LifetimeTicksRemaining - 1;
                if (lifetime == 0) continue;
                survivors.Add(new ProjectileState(projectile.ProjectileId, projectile.OwnerPlayerId,
                    checked(projectile.PositionX + deltaX), checked(projectile.PositionZ + deltaZ),
                    projectile.DirectionX, projectile.DirectionZ, lifetime));
            }

            bool anyDead = false;
            for (int index = 0; index < players.Length; index++)
            {
                PlayerState player = players[index];
                int hitPoints = damage[index] >= player.HitPoints ? 0 : player.HitPoints - checked((int)damage[index]);
                players[index] = new PlayerState(player.PositionX, player.PositionZ, player.Aim,
                    hitPoints, player.RoundWins, player.FireCooldownTicks);
                anyDead |= hitPoints == 0;
            }

            if (anyDead)
                return EndRound(current, nextTick, players, DetermineDeathResult(players), nextProjectileId);

            uint roundTicks = current.RoundTicksRemaining > 0 ? current.RoundTicksRemaining - 1 : 0;
            if (roundTicks == 0)
                return EndRound(current, nextTick, players, DetermineTimeoutResult(players), nextProjectileId);

            return Copy(current, nextTick, BattlePhase.Playing, 0, roundTicks, RoundResult.None, null,
                players, survivors.ToArray(), nextProjectileId);
        }

        private const int NoHit = -1;
        private const int ObstacleHit = -2;

        private static int Sweep(
            ProjectileState projectile,
            int deltaX,
            int deltaZ,
            ActiveRoster roster,
            PlayerState[] players,
            BattleDefinition definition)
        {
            int projectileRadius = definition.Gameplay.ProjectileRadiusUnits;
            RationalTime? obstacleTime = FindEarliestObstacleTime(
                projectile.PositionX,
                projectile.PositionZ,
                deltaX,
                deltaZ,
                projectileRadius,
                definition.Arena);
            CircleHit? playerHit = null;
            for (int slotValue = 0; slotValue < players.Length; slotValue++)
            {
                var slot = new PlayerSlot(slotValue);
                if (roster.GetPlayerId(slot) == projectile.OwnerPlayerId) continue;
                PlayerState player = players[slotValue];
                if (player.HitPoints == 0) continue;
                if (!TryGetCircleHit(
                    projectile.PositionX,
                    projectile.PositionZ,
                    deltaX,
                    deltaZ,
                    player.PositionX,
                    player.PositionZ,
                    checked(projectileRadius + definition.Gameplay.PlayerRadiusUnits),
                    slotValue,
                    out CircleHit candidate))
                {
                    continue;
                }

                if (!playerHit.HasValue || CompareCircleHits(candidate, playerHit.Value) < 0)
                    playerHit = candidate;
            }

            if (!playerHit.HasValue) return obstacleTime.HasValue ? ObstacleHit : NoHit;
            if (!obstacleTime.HasValue) return playerHit.Value.PlayerSlot;
            return CompareCircleToRational(playerHit.Value, obstacleTime.Value) < 0
                ? playerHit.Value.PlayerSlot
                : ObstacleHit;
        }

        private static RationalTime? FindEarliestObstacleTime(
            int startX,
            int startZ,
            int deltaX,
            int deltaZ,
            int radius,
            ArenaConfig arena)
        {
            ArenaRectangle bounds = arena.Bounds;
            RationalTime? earliest = FindArenaExitTime(
                startX,
                startZ,
                deltaX,
                deltaZ,
                (BigInteger)bounds.MinX + radius,
                (BigInteger)bounds.MaxX - radius,
                (BigInteger)bounds.MinZ + radius,
                (BigInteger)bounds.MaxZ - radius);
            for (int index = 0; index < arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = arena.GetObstacle(index);
                if (TryGetAabbEntryTime(
                    startX,
                    startZ,
                    deltaX,
                    deltaZ,
                    (BigInteger)obstacle.MinX - radius,
                    (BigInteger)obstacle.MaxX + radius,
                    (BigInteger)obstacle.MinZ - radius,
                    (BigInteger)obstacle.MaxZ + radius,
                    out RationalTime candidate) &&
                    (!earliest.HasValue || candidate.CompareTo(earliest.Value) < 0))
                {
                    earliest = candidate;
                }
            }

            return earliest;
        }

        private static RationalTime? FindArenaExitTime(
            int startX,
            int startZ,
            int deltaX,
            int deltaZ,
            BigInteger minX,
            BigInteger maxX,
            BigInteger minZ,
            BigInteger maxZ)
        {
            if (startX < minX || startX > maxX || startZ < minZ || startZ > maxZ)
                return RationalTime.Zero;

            RationalTime? earliest = null;
            AddArenaExitCandidate(startX, deltaX, minX, maxX, ref earliest);
            AddArenaExitCandidate(startZ, deltaZ, minZ, maxZ, ref earliest);
            return earliest;
        }

        private static void AddArenaExitCandidate(
            int start,
            int delta,
            BigInteger minimum,
            BigInteger maximum,
            ref RationalTime? earliest)
        {
            BigInteger end = (BigInteger)start + delta;
            RationalTime? candidate = null;
            if (end < minimum)
                candidate = new RationalTime((BigInteger)start - minimum, -delta);
            else if (end > maximum)
                candidate = new RationalTime(maximum - start, delta);

            if (candidate.HasValue &&
                candidate.Value.CompareTo(RationalTime.Zero) >= 0 &&
                candidate.Value.CompareTo(RationalTime.One) <= 0 &&
                (!earliest.HasValue || candidate.Value.CompareTo(earliest.Value) < 0))
            {
                earliest = candidate;
            }
        }

        private static bool TryGetAabbEntryTime(
            int startX,
            int startZ,
            int deltaX,
            int deltaZ,
            BigInteger minX,
            BigInteger maxX,
            BigInteger minZ,
            BigInteger maxZ,
            out RationalTime entry)
        {
            RationalTime enter = RationalTime.Zero;
            RationalTime exit = RationalTime.One;
            if (!ClipAxis(startX, deltaX, minX, maxX, ref enter, ref exit) ||
                !ClipAxis(startZ, deltaZ, minZ, maxZ, ref enter, ref exit) ||
                enter.CompareTo(exit) > 0)
            {
                entry = default;
                return false;
            }

            entry = enter;
            return true;
        }

        private static bool ClipAxis(
            int start,
            int delta,
            BigInteger minimum,
            BigInteger maximum,
            ref RationalTime enter,
            ref RationalTime exit)
        {
            if (delta == 0) return start >= minimum && start <= maximum;
            var first = new RationalTime(minimum - start, delta);
            var second = new RationalTime(maximum - start, delta);
            if (first.CompareTo(second) > 0)
            {
                RationalTime swap = first;
                first = second;
                second = swap;
            }

            if (first.CompareTo(enter) > 0) enter = first;
            if (second.CompareTo(exit) < 0) exit = second;
            return enter.CompareTo(exit) <= 0;
        }

        private static bool TryGetCircleHit(
            int startX,
            int startZ,
            int deltaX,
            int deltaZ,
            int centerX,
            int centerZ,
            int radius,
            int playerSlot,
            out CircleHit hit)
        {
            BigInteger offsetX = (BigInteger)startX - centerX;
            BigInteger offsetZ = (BigInteger)startZ - centerZ;
            BigInteger radiusWide = radius;
            BigInteger c = (offsetX * offsetX) + (offsetZ * offsetZ) - (radiusWide * radiusWide);
            if (c <= 0)
            {
                hit = CircleHit.AtStart(playerSlot);
                return true;
            }

            BigInteger dx = deltaX;
            BigInteger dz = deltaZ;
            BigInteger a = (dx * dx) + (dz * dz);
            if (a == 0)
            {
                hit = default;
                return false;
            }

            BigInteger b = 2 * ((offsetX * dx) + (offsetZ * dz));
            if (b >= 0)
            {
                hit = default;
                return false;
            }

            BigInteger discriminant = (b * b) - (4 * a * c);
            if (discriminant < 0)
            {
                hit = default;
                return false;
            }

            BigInteger atEnd = (2 * a) + b;
            if (atEnd < 0 && (atEnd * atEnd) > discriminant)
            {
                hit = default;
                return false;
            }

            hit = new CircleHit(playerSlot, a, b, discriminant, false);
            return true;
        }

        private static int CompareCircleHits(CircleHit left, CircleHit right)
        {
            if (left.IsAtStart) return right.IsAtStart ? 0 : -1;
            if (right.IsAtStart) return 1;

            return CompareBaseMinusSquareRoot(
                -left.B,
                left.Discriminant,
                -right.B,
                right.Discriminant);
        }

        private static int CompareBaseMinusSquareRoot(
            BigInteger leftBase,
            BigInteger leftRadicand,
            BigInteger rightBase,
            BigInteger rightRadicand)
        {
            BigInteger difference = leftBase - rightBase;
            if (difference == 0) return rightRadicand.CompareTo(leftRadicand);
            if (difference < 0)
                return -CompareBaseMinusSquareRoot(
                    rightBase,
                    rightRadicand,
                    leftBase,
                    leftRadicand);

            BigInteger residual = leftRadicand - (difference * difference) - rightRadicand;
            if (residual < 0) return 1;
            if (residual == 0) return rightRadicand == 0 ? 0 : 1;
            int comparison = (residual * residual).CompareTo(
                4 * difference * difference * rightRadicand);
            return comparison < 0 ? 1 : comparison == 0 ? 0 : -1;
        }

        private static int CompareCircleToRational(CircleHit circle, RationalTime rational)
        {
            if (circle.IsAtStart)
                return rational.CompareTo(RationalTime.Zero) == 0 ? 0 : -1;

            BigInteger scaled =
                (2 * circle.A * rational.Numerator) +
                (circle.B * rational.Denominator);
            if (scaled >= 0) return -1;
            int comparison = (scaled * scaled).CompareTo(
                circle.Discriminant * rational.Denominator * rational.Denominator);
            return comparison < 0 ? -1 : comparison == 0 ? 0 : 1;
        }

        private readonly struct RationalTime : IComparable<RationalTime>
        {
            public RationalTime(BigInteger numerator, BigInteger denominator)
            {
                if (denominator == 0) throw new DivideByZeroException();
                if (denominator < 0)
                {
                    numerator = -numerator;
                    denominator = -denominator;
                }
                Numerator = numerator;
                Denominator = denominator;
            }

            public BigInteger Numerator { get; }
            public BigInteger Denominator { get; }
            public static RationalTime Zero => new RationalTime(0, 1);
            public static RationalTime One => new RationalTime(1, 1);
            public int CompareTo(RationalTime other) =>
                (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
        }

        private readonly struct CircleHit
        {
            public CircleHit(
                int playerSlot,
                BigInteger a,
                BigInteger b,
                BigInteger discriminant,
                bool isAtStart)
            {
                PlayerSlot = playerSlot;
                A = a;
                B = b;
                Discriminant = discriminant;
                IsAtStart = isAtStart;
            }

            public int PlayerSlot { get; }
            public BigInteger A { get; }
            public BigInteger B { get; }
            public BigInteger Discriminant { get; }
            public bool IsAtStart { get; }
            public static CircleHit AtStart(int playerSlot) =>
                new CircleHit(playerSlot, 0, 0, 0, true);
        }

        private static ArenaPoint MoveGameplay(PlayerState player, InputFrame input, BattleDefinition definition)
        {
            int speed = definition.Gameplay.MoveUnitsPerTick;
            int deltaX = input.MoveX == 0 ? 0 : input.MoveX * (input.MoveZ == 0 ? speed : speed * 707 / 1000);
            int deltaZ = input.MoveZ == 0 ? 0 : input.MoveZ * (input.MoveX == 0 ? speed : speed * 707 / 1000);
            int x = ResolveX(player.PositionX, player.PositionZ, deltaX, definition);
            int z = ResolveZ(x, player.PositionZ, deltaZ, definition);
            return new ArenaPoint(x, z);
        }

        private static int ResolveX(int oldX, int z, int delta, BattleDefinition definition)
        {
            int radius = definition.Gameplay.PlayerRadiusUnits;
            ArenaRectangle bounds = definition.Arena.Bounds;
            int candidate = Clamp(checked(oldX + delta), checked(bounds.MinX + radius), checked(bounds.MaxX - radius));
            if (delta == 0) return candidate;
            for (int index = 0; index < definition.Arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = definition.Arena.GetObstacle(index);
                if ((long)z < (long)obstacle.MinZ - radius || (long)z > (long)obstacle.MaxZ + radius) continue;
                int minimum = checked(obstacle.MinX - radius);
                int maximum = checked(obstacle.MaxX + radius);
                if (delta > 0 && oldX <= minimum && candidate > minimum) candidate = Math.Min(candidate, minimum);
                else if (delta < 0 && oldX >= maximum && candidate < maximum) candidate = Math.Max(candidate, maximum);
            }
            return candidate;
        }

        private static int ResolveZ(int x, int oldZ, int delta, BattleDefinition definition)
        {
            int radius = definition.Gameplay.PlayerRadiusUnits;
            ArenaRectangle bounds = definition.Arena.Bounds;
            int candidate = Clamp(checked(oldZ + delta), checked(bounds.MinZ + radius), checked(bounds.MaxZ - radius));
            if (delta == 0) return candidate;
            for (int index = 0; index < definition.Arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = definition.Arena.GetObstacle(index);
                if ((long)x < (long)obstacle.MinX - radius || (long)x > (long)obstacle.MaxX + radius) continue;
                int minimum = checked(obstacle.MinZ - radius);
                int maximum = checked(obstacle.MaxZ + radius);
                if (delta > 0 && oldZ <= minimum && candidate > minimum) candidate = Math.Min(candidate, minimum);
                else if (delta < 0 && oldZ >= maximum && candidate < maximum) candidate = Math.Max(candidate, maximum);
            }
            return candidate;
        }

        private static RoundResult DetermineDeathResult(PlayerState[] players)
        {
            int survivor = -1;
            for (int index = 0; index < players.Length; index++)
            {
                if (players[index].HitPoints == 0) continue;
                if (survivor >= 0) return RoundResult.Draw;
                survivor = index;
            }
            return survivor < 0 ? RoundResult.Draw : RoundResult.PlayerWin(new PlayerSlot(survivor));
        }

        private static RoundResult DetermineTimeoutResult(PlayerState[] players)
        {
            int winner = -1;
            int highest = -1;
            bool tied = false;
            for (int index = 0; index < players.Length; index++)
            {
                if (players[index].HitPoints > highest) { highest = players[index].HitPoints; winner = index; tied = false; }
                else if (players[index].HitPoints == highest) tied = true;
            }
            return tied ? RoundResult.Draw : RoundResult.PlayerWin(new PlayerSlot(winner));
        }

        private static BattleState EndRound(BattleState current, uint nextTick, PlayerState[] players, RoundResult result, ulong nextProjectileId)
        {
            PlayerSlot? matchWinner = null;
            if (result.HasWinner)
            {
                int winnerIndex = result.WinnerSlot.Value;
                PlayerState winner = players[winnerIndex];
                winner = new PlayerState(winner.PositionX, winner.PositionZ, winner.Aim,
                    winner.HitPoints, checked(winner.RoundWins + 1), winner.FireCooldownTicks);
                players[winnerIndex] = winner;
                if (winner.RoundWins >= current.Definition!.Gameplay.RoundsToWin) matchWinner = result.WinnerSlot;
            }
            BattlePhase phase = matchWinner.HasValue ? BattlePhase.MatchEnded : BattlePhase.RoundEnded;
            uint delay = matchWinner.HasValue ? 0 : current.Definition!.Gameplay.RoundEndDelayTicks;
            return Copy(current, nextTick, phase, delay, 0, result, matchWinner,
                players, Array.Empty<ProjectileState>(), nextProjectileId);
        }

        private static BattleState ResetRound(BattleState current, uint nextTick)
        {
            BattleDefinition definition = current.Definition!;
            var players = new PlayerState[current.PlayerCount];
            for (int index = 0; index < players.Length; index++)
            {
                ArenaPoint spawn = definition.Arena.GetSpawn(index);
                ArenaPoint otherSpawn = definition.Arena.GetSpawn(1 - index);
                int wins = current.GetPlayerState(new PlayerSlot(index)).RoundWins;
                players[index] = new PlayerState(spawn.X, spawn.Z, GetAimToward(spawn, otherSpawn),
                    definition.Gameplay.MaxHitPoints, wins, 0);
            }
            return Copy(current, nextTick, BattlePhase.RoundCountdown, definition.Gameplay.RoundCountdownTicks,
                definition.Gameplay.RoundDurationTicks, RoundResult.None, null, players,
                Array.Empty<ProjectileState>(), current.NextProjectileId);
        }

        private static BattleState Copy(BattleState current, uint tick, BattlePhase phase, uint phaseTicks,
            uint roundTicks, RoundResult roundResult, PlayerSlot? matchWinner, PlayerState[] players,
            ProjectileState[] projectiles, ulong nextProjectileId)
        {
            return new BattleState(tick, current.Roster, players, current.Definition!, phase, phaseTicks,
                roundTicks, roundResult, matchWinner, projectiles, nextProjectileId);
        }

        private static PlayerState[] CopyPlayers(BattleState state)
        {
            var players = new PlayerState[state.PlayerCount];
            for (int index = 0; index < players.Length; index++) players[index] = state.GetPlayerState(new PlayerSlot(index));
            return players;
        }

        private static ProjectileState[] CopyProjectiles(BattleState state)
        {
            var projectiles = new ProjectileState[state.ProjectileCount];
            for (int index = 0; index < projectiles.Length; index++) projectiles[index] = state.GetProjectile(index);
            return projectiles;
        }

        public static void GetAimDirection(ushort aim, out int x, out int z)
        {
            int directionIndex = (int)((((uint)aim + 512U) >> 10) & 63U);
            int quadrant = directionIndex >> 4;
            int offset = directionIndex & 15;
            int forward = QuarterCos[offset];
            int lateral = QuarterCos[16 - offset];
            switch (quadrant)
            {
                case 0: x = forward; z = lateral; break;
                case 1: x = -lateral; z = forward; break;
                case 2: x = -forward; z = -lateral; break;
                default: x = lateral; z = -forward; break;
            }
        }

        public static ushort GetAimToward(ArenaPoint from, ArenaPoint to)
        {
            long deltaX = (long)to.X - from.X;
            long deltaZ = (long)to.Z - from.Z;
            long bestDot = long.MinValue;
            int bestIndex = 0;
            for (int index = 0; index < 64; index++)
            {
                ushort aim = checked((ushort)(index << 10));
                GetAimDirection(aim, out int directionX, out int directionZ);
                long dot = checked((deltaX * directionX) + (deltaZ * directionZ));
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = index;
                }
            }
            return checked((ushort)(bestIndex << 10));
        }

        private static readonly short[] QuarterCos =
        {
            1000, 995, 981, 957, 924, 882, 831, 773, 707,
            634, 556, 471, 383, 290, 195, 98, 0,
        };

        private static int AddScaled(int origin, int direction, int distance) => checked(origin + Scale(direction, distance));
        private static int Scale(int direction, int distance) => checked((int)((long)direction * distance / 1000));

        private static PlayerState Move(PlayerState player, InputFrame input)
        {
            int positionX = Clamp(
                player.PositionX + (input.MoveX * SimulationConfig.MoveUnitsPerTick),
                SimulationConfig.ArenaMinX,
                SimulationConfig.ArenaMaxX);
            int positionZ = Clamp(
                player.PositionZ + (input.MoveZ * SimulationConfig.MoveUnitsPerTick),
                SimulationConfig.ArenaMinZ,
                SimulationConfig.ArenaMaxZ);

            return new PlayerState(positionX, positionZ, input.Aim);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }
}
