using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation
{
    public readonly struct ArenaPoint : IEquatable<ArenaPoint>
    {
        public ArenaPoint(int x, int z) { X = x; Z = z; }
        public int X { get; }
        public int Z { get; }
        public bool Equals(ArenaPoint other) => X == other.X && Z == other.Z;
        public override bool Equals(object? obj) => obj is ArenaPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Z);
        public static bool operator ==(ArenaPoint left, ArenaPoint right) => left.Equals(right);
        public static bool operator !=(ArenaPoint left, ArenaPoint right) => !left.Equals(right);
    }

    public readonly struct ArenaRectangle : IEquatable<ArenaRectangle>
    {
        public ArenaRectangle(int minX, int maxX, int minZ, int maxZ)
        {
            if (minX >= maxX) throw new ArgumentException("Rectangle minimum X must be below maximum X.", nameof(minX));
            if (minZ >= maxZ) throw new ArgumentException("Rectangle minimum Z must be below maximum Z.", nameof(minZ));
            MinX = minX; MaxX = maxX; MinZ = minZ; MaxZ = maxZ;
        }
        public int MinX { get; }
        public int MaxX { get; }
        public int MinZ { get; }
        public int MaxZ { get; }
        public bool Equals(ArenaRectangle other) => MinX == other.MinX && MaxX == other.MaxX && MinZ == other.MinZ && MaxZ == other.MaxZ;
        public override bool Equals(object? obj) => obj is ArenaRectangle other && Equals(other);
        public override int GetHashCode() => unchecked((((MinX * 397) ^ MaxX) * 397 ^ MinZ) * 397 ^ MaxZ);
        public static bool operator ==(ArenaRectangle left, ArenaRectangle right) => left.Equals(right);
        public static bool operator !=(ArenaRectangle left, ArenaRectangle right) => !left.Equals(right);
    }

    public sealed class ArenaConfig
    {
        private readonly ArenaPoint[] _spawns;
        private readonly ArenaRectangle[] _obstacles;

        public ArenaConfig(string arenaId, ArenaRectangle bounds, IReadOnlyList<ArenaPoint> spawns, IReadOnlyList<ArenaRectangle> obstacles)
        {
            if (string.IsNullOrWhiteSpace(arenaId)) throw new ArgumentException("Arena id is required.", nameof(arenaId));
            if (spawns is null) throw new ArgumentNullException(nameof(spawns));
            if (obstacles is null) throw new ArgumentNullException(nameof(obstacles));
            if (spawns.Count < 1) throw new ArgumentException("At least one spawn is required.", nameof(spawns));
            ArenaId = arenaId;
            Bounds = bounds;
            _spawns = new ArenaPoint[spawns.Count];
            for (int i = 0; i < spawns.Count; i++)
            {
                ArenaPoint spawn = spawns[i];
                if (spawn.X < bounds.MinX || spawn.X > bounds.MaxX || spawn.Z < bounds.MinZ || spawn.Z > bounds.MaxZ)
                    throw new ArgumentException("Spawn must be inside arena bounds.", nameof(spawns));
                _spawns[i] = spawn;
            }
            _obstacles = new ArenaRectangle[obstacles.Count];
            for (int i = 0; i < obstacles.Count; i++) _obstacles[i] = obstacles[i];
        }

        public string ArenaId { get; }
        public ArenaRectangle Bounds { get; }
        public int SpawnCount => _spawns.Length;
        public int ObstacleCount => _obstacles.Length;
        public ArenaPoint GetSpawn(int index) => _spawns[index];
        public ArenaRectangle GetObstacle(int index) => _obstacles[index];

        public static ArenaConfig CreateDefault()
        {
            return new ArenaConfig("symmetric-v1", new ArenaRectangle(-5_000, 5_000, -3_000, 3_000),
                new[] { new ArenaPoint(-3_500, 0), new ArenaPoint(3_500, 0) },
                new[]
                {
                    new ArenaRectangle(-600, 600, -900, 900),
                    new ArenaRectangle(-2_200, -1_700, -2_000, -1_200),
                    new ArenaRectangle(1_700, 2_200, 1_200, 2_000),
                });
        }
    }
}
