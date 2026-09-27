using System;

namespace LockstepArena.Simulation
{
    public readonly struct PlayerSlot : IEquatable<PlayerSlot>
    {
        public PlayerSlot(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Player slot cannot be negative.");
            }

            Value = value;
        }

        public int Value { get; }

        public bool Equals(PlayerSlot other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is PlayerSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(PlayerSlot left, PlayerSlot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerSlot left, PlayerSlot right)
        {
            return !left.Equals(right);
        }
    }
}
