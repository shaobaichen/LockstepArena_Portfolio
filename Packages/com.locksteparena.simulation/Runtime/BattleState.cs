using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation
{
    public sealed class BattleState
    {
        private readonly PlayerState[] _players;
        private readonly ProjectileState[] _projectiles;

        public BattleState(
            uint tick,
            ActiveRoster roster,
            IReadOnlyList<PlayerState> statesInSlotOrder)
        {
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (statesInSlotOrder is null)
            {
                throw new ArgumentNullException(nameof(statesInSlotOrder));
            }

            if (statesInSlotOrder.Count != roster.Count)
            {
                throw new ArgumentException(
                    "Player state count must match the active roster.",
                    nameof(statesInSlotOrder));
            }

            Tick = tick;
            Definition = null;
            Phase = BattlePhase.Playing;
            RoundResult = RoundResult.None;
            MatchWinnerSlot = null;
            _projectiles = Array.Empty<ProjectileState>();
            _players = new PlayerState[statesInSlotOrder.Count];
            for (int index = 0; index < statesInSlotOrder.Count; index++)
            {
                _players[index] = statesInSlotOrder[index];
            }
        }

        public uint Tick { get; }

        public ActiveRoster Roster { get; }

        public bool IsGameplayEnabled => Definition is not null;

        public BattleDefinition? Definition { get; }

        public BattlePhase Phase { get; }

        public uint PhaseTicksRemaining { get; }

        public uint RoundTicksRemaining { get; }

        public RoundResult RoundResult { get; }

        public PlayerSlot? MatchWinnerSlot { get; }

        public int ProjectileCount => _projectiles.Length;

        public ulong NextProjectileId { get; }

        public int PlayerCount => _players.Length;

        public static BattleState CreateInitial(
            ActiveRoster roster,
            IReadOnlyList<PlayerState> statesInSlotOrder)
        {
            return new BattleState(0, roster, statesInSlotOrder);
        }

        public static BattleState CreateGameplayInitial(ActiveRoster roster, BattleDefinition definition)
        {
            if (roster is null) throw new ArgumentNullException(nameof(roster));
            if (definition is null) throw new ArgumentNullException(nameof(definition));
            if (roster.Count != 2) throw new ArgumentException("Gameplay Sample v1 supports exactly two active players.", nameof(roster));
            if (definition.Arena.SpawnCount < roster.Count) throw new ArgumentException("Arena does not contain enough spawns.", nameof(definition));
            var players = new PlayerState[roster.Count];
            for (int index = 0; index < players.Length; index++)
            {
                ArenaPoint spawn = definition.Arena.GetSpawn(index);
                ArenaPoint otherSpawn = definition.Arena.GetSpawn(1 - index);
                ushort aim = BattleSimulation.GetAimToward(spawn, otherSpawn);
                players[index] = new PlayerState(spawn.X, spawn.Z, aim, definition.Gameplay.MaxHitPoints, 0, 0);
            }
            return new BattleState(0, roster, players, definition, BattlePhase.RoundCountdown,
                definition.Gameplay.RoundCountdownTicks, definition.Gameplay.RoundDurationTicks,
                RoundResult.None, null, Array.Empty<ProjectileState>(), 1);
        }

        public BattleState(
            uint tick,
            ActiveRoster roster,
            IReadOnlyList<PlayerState> statesInSlotOrder,
            BattleDefinition definition,
            BattlePhase phase,
            uint phaseTicksRemaining,
            uint roundTicksRemaining,
            RoundResult roundResult,
            PlayerSlot? matchWinnerSlot,
            IReadOnlyList<ProjectileState> projectiles,
            ulong nextProjectileId)
        {
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (statesInSlotOrder is null) throw new ArgumentNullException(nameof(statesInSlotOrder));
            if (projectiles is null) throw new ArgumentNullException(nameof(projectiles));
            if (statesInSlotOrder.Count != roster.Count) throw new ArgumentException("Player state count must match the active roster.", nameof(statesInSlotOrder));
            if (nextProjectileId == 0) throw new ArgumentOutOfRangeException(nameof(nextProjectileId));
            if (matchWinnerSlot.HasValue && matchWinnerSlot.Value.Value >= roster.Count) throw new ArgumentOutOfRangeException(nameof(matchWinnerSlot));
            Tick = tick;
            Phase = phase;
            PhaseTicksRemaining = phaseTicksRemaining;
            RoundTicksRemaining = roundTicksRemaining;
            RoundResult = roundResult;
            MatchWinnerSlot = matchWinnerSlot;
            NextProjectileId = nextProjectileId;
            _players = new PlayerState[statesInSlotOrder.Count];
            for (int index = 0; index < _players.Length; index++) _players[index] = statesInSlotOrder[index];
            _projectiles = new ProjectileState[projectiles.Count];
            for (int index = 0; index < _projectiles.Length; index++) _projectiles[index] = projectiles[index];
            Array.Sort(_projectiles, (left, right) => left.ProjectileId.CompareTo(right.ProjectileId));
            for (int index = 1; index < _projectiles.Length; index++)
                if (_projectiles[index - 1].ProjectileId == _projectiles[index].ProjectileId)
                    throw new ArgumentException("Projectile ids must be unique.", nameof(projectiles));
        }

        public PlayerState GetPlayerState(PlayerSlot slot)
        {
            if (slot.Value >= _players.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "Player slot is outside the battle roster.");
            }

            return _players[slot.Value];
        }

        public ProjectileState GetProjectile(int index)
        {
            if (index < 0 || index >= _projectiles.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _projectiles[index];
        }
    }
}
