using System;

namespace LockstepArena.Simulation
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public bool Equals(PlayerId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is PlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(PlayerId left, PlayerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerId left, PlayerId right)
        {
            return !left.Equals(right);
        }
    }
}
