#nullable enable

using System;
using System.Collections.Generic;
using LockstepArena.Simulation;

namespace LockstepArena.Demo
{
    public sealed class BattlePresentationEvents
    {
        public BattlePresentationEvents(ulong[] spawned, ulong[] removed, int[] damaged)
        {
            SpawnedProjectileIds = spawned;
            RemovedProjectileIds = removed;
            DamagedSlots = damaged;
        }

        public ulong[] SpawnedProjectileIds { get; }
        public ulong[] RemovedProjectileIds { get; }
        public int[] DamagedSlots { get; }
        public bool HasAny => SpawnedProjectileIds.Length != 0 ||
                              RemovedProjectileIds.Length != 0 ||
                              DamagedSlots.Length != 0;
    }

    public sealed class BattlePresentationTracker
    {
        private const int RetainedIdentityCount = 512;
        private readonly HashSet<ulong> _activeProjectiles = new HashSet<ulong>();
        private readonly HashSet<ulong> _seenSpawns = new HashSet<ulong>();
        private readonly Queue<ulong> _spawnOrder = new Queue<ulong>();
        private readonly HashSet<ulong> _seenRemovals = new HashSet<ulong>();
        private readonly Queue<ulong> _removalOrder = new Queue<ulong>();
        private readonly HashSet<DamageIdentity> _seenDamage = new HashSet<DamageIdentity>();
        private readonly Queue<DamageIdentity> _damageOrder = new Queue<DamageIdentity>();
        private readonly int[] _previousHitPoints = new int[2];
        private bool _hasPrevious;

        public BattlePresentationEvents Observe(BattleState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (!state.IsGameplayEnabled || state.PlayerCount != 2)
                throw new ArgumentException("A two-player gameplay state is required.", nameof(state));

            var current = new HashSet<ulong>();
            var spawned = new List<ulong>();
            for (int index = 0; index < state.ProjectileCount; index++)
            {
                ulong id = state.GetProjectile(index).ProjectileId;
                current.Add(id);
                if (_activeProjectiles.Contains(id) || !Remember(id, _seenSpawns, _spawnOrder)) continue;
                spawned.Add(id);
            }

            var removed = new List<ulong>();
            foreach (ulong id in _activeProjectiles)
            {
                if (current.Contains(id) || !Remember(id, _seenRemovals, _removalOrder)) continue;
                removed.Add(id);
            }

            var damaged = new List<int>();
            for (int slot = 0; slot < 2; slot++)
            {
                int hitPoints = state.GetPlayerState(new PlayerSlot(slot)).HitPoints;
                if (_hasPrevious && hitPoints < _previousHitPoints[slot])
                {
                    var identity = new DamageIdentity(state.Tick, slot, hitPoints);
                    if (Remember(identity, _seenDamage, _damageOrder)) damaged.Add(slot);
                }
                _previousHitPoints[slot] = hitPoints;
            }

            _activeProjectiles.Clear();
            foreach (ulong id in current) _activeProjectiles.Add(id);
            _hasPrevious = true;
            spawned.Sort();
            removed.Sort();
            damaged.Sort();
            return new BattlePresentationEvents(spawned.ToArray(), removed.ToArray(), damaged.ToArray());
        }

        public void ResetRoundObservation()
        {
            _activeProjectiles.Clear();
            _hasPrevious = false;
        }

        public void Reset()
        {
            ResetRoundObservation();
            _seenSpawns.Clear();
            _spawnOrder.Clear();
            _seenRemovals.Clear();
            _removalOrder.Clear();
            _seenDamage.Clear();
            _damageOrder.Clear();
            Array.Clear(_previousHitPoints, 0, _previousHitPoints.Length);
        }

        private static bool Remember<T>(T identity, HashSet<T> identities, Queue<T> order)
        {
            if (!identities.Add(identity)) return false;
            order.Enqueue(identity);
            while (order.Count > RetainedIdentityCount) identities.Remove(order.Dequeue());
            return true;
        }

        private readonly struct DamageIdentity : IEquatable<DamageIdentity>
        {
            public DamageIdentity(uint tick, int slot, int hitPoints)
            {
                Tick = tick;
                Slot = slot;
                HitPoints = hitPoints;
            }

            private uint Tick { get; }
            private int Slot { get; }
            private int HitPoints { get; }

            public bool Equals(DamageIdentity other) => Tick == other.Tick &&
                                                        Slot == other.Slot &&
                                                        HitPoints == other.HitPoints;
            public override bool Equals(object? obj) => obj is DamageIdentity other && Equals(other);
            public override int GetHashCode() => unchecked(((int)Tick * 397) ^ (Slot * 31) ^ HitPoints);
        }
    }
}
