using System;

namespace LockstepArena.Simulation
{
    public enum BattlePhase { RoundCountdown, Playing, RoundEnded, MatchEnded }
    public enum RoundResultKind { None, PlayerWin, Draw }

    public readonly struct RoundResult : IEquatable<RoundResult>
    {
        private RoundResult(RoundResultKind kind, PlayerSlot winnerSlot)
        {
            Kind = kind;
            WinnerSlot = winnerSlot;
        }

        public RoundResultKind Kind { get; }
        public PlayerSlot WinnerSlot { get; }
        public bool HasWinner => Kind == RoundResultKind.PlayerWin;
        public static RoundResult None => default;
        public static RoundResult Draw => new RoundResult(RoundResultKind.Draw, default);
        public static RoundResult PlayerWin(PlayerSlot winnerSlot) => new RoundResult(RoundResultKind.PlayerWin, winnerSlot);
        public bool Equals(RoundResult other) => Kind == other.Kind && (!HasWinner || WinnerSlot == other.WinnerSlot);
        public override bool Equals(object? obj) => obj is RoundResult other && Equals(other);
        public override int GetHashCode() => HasWinner ? unchecked(((int)Kind * 397) ^ WinnerSlot.GetHashCode()) : (int)Kind;
        public static bool operator ==(RoundResult left, RoundResult right) => left.Equals(right);
        public static bool operator !=(RoundResult left, RoundResult right) => !left.Equals(right);
    }
}
