using System;

namespace LockstepArena.Simulation
{
    public readonly struct ProjectileState : IEquatable<ProjectileState>
    {
        public ProjectileState(ulong projectileId, PlayerId ownerPlayerId, int positionX, int positionZ, int directionX, int directionZ, uint lifetimeTicksRemaining)
        {
            if (projectileId == 0) throw new ArgumentOutOfRangeException(nameof(projectileId));
            if (directionX == 0 && directionZ == 0) throw new ArgumentException("Projectile direction cannot be zero.", nameof(directionX));
            if (lifetimeTicksRemaining == 0) throw new ArgumentOutOfRangeException(nameof(lifetimeTicksRemaining));
            ProjectileId = projectileId;
            OwnerPlayerId = ownerPlayerId;
            PositionX = positionX;
            PositionZ = positionZ;
            DirectionX = directionX;
            DirectionZ = directionZ;
            LifetimeTicksRemaining = lifetimeTicksRemaining;
        }

        public ulong ProjectileId { get; }
        public PlayerId OwnerPlayerId { get; }
        public int PositionX { get; }
        public int PositionZ { get; }
        public int DirectionX { get; }
        public int DirectionZ { get; }
        public uint LifetimeTicksRemaining { get; }
        public bool Equals(ProjectileState other) => ProjectileId == other.ProjectileId && OwnerPlayerId == other.OwnerPlayerId && PositionX == other.PositionX && PositionZ == other.PositionZ && DirectionX == other.DirectionX && DirectionZ == other.DirectionZ && LifetimeTicksRemaining == other.LifetimeTicksRemaining;
        public override bool Equals(object? obj) => obj is ProjectileState other && Equals(other);
        public override int GetHashCode() => ProjectileId.GetHashCode();
        public static bool operator ==(ProjectileState left, ProjectileState right) => left.Equals(right);
        public static bool operator !=(ProjectileState left, ProjectileState right) => !left.Equals(right);
    }
}
