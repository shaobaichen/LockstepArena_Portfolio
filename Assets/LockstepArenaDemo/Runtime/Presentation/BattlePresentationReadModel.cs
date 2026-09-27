#nullable enable

using System;
using LockstepArena.Simulation;
using UnityEngine;

namespace LockstepArena.Demo
{
    public enum BattleAnnouncementKind
    {
        None,
        Countdown3,
        Countdown2,
        Countdown1,
        Fight,
        RoundWin,
        Draw,
        MatchVictory,
    }

    public readonly struct BattlePresentationSnapshot
    {
        public BattlePresentationSnapshot(
            int player1HitPoints,
            int player2HitPoints,
            int player1RoundWins,
            int player2RoundWins,
            int roundNumber,
            uint secondsRemaining,
            BattleAnnouncementKind announcement,
            string announcementText,
            int winnerSlot)
        {
            Player1HitPoints = player1HitPoints;
            Player2HitPoints = player2HitPoints;
            Player1RoundWins = player1RoundWins;
            Player2RoundWins = player2RoundWins;
            RoundNumber = roundNumber;
            SecondsRemaining = secondsRemaining;
            Announcement = announcement;
            AnnouncementText = announcementText;
            WinnerSlot = winnerSlot;
        }

        public int Player1HitPoints { get; }
        public int Player2HitPoints { get; }
        public int Player1RoundWins { get; }
        public int Player2RoundWins { get; }
        public int RoundNumber { get; }
        public uint SecondsRemaining { get; }
        public BattleAnnouncementKind Announcement { get; }
        public string AnnouncementText { get; }
        public int WinnerSlot { get; }
    }

    public static class BattlePresentationReadModel
    {
        public static Color GetTeamColor(PlayerSlot slot)
        {
            return slot.Value switch
            {
                0 => new Color(0.08f, 0.72f, 1f, 1f),
                1 => new Color(1f, 0.18f, 0.12f, 1f),
                _ => throw new ArgumentOutOfRangeException(nameof(slot)),
            };
        }

        public static BattlePresentationSnapshot Create(BattleState state)
        {
            if (state is null) throw new ArgumentNullException(nameof(state));
            if (!state.IsGameplayEnabled || state.PlayerCount != 2)
                throw new ArgumentException("A two-player gameplay state is required.", nameof(state));

            PlayerState first = state.GetPlayerState(new PlayerSlot(0));
            PlayerState second = state.GetPlayerState(new PlayerSlot(1));
            int round = Math.Min(
                state.Definition!.Gameplay.RoundsToWin * 2 - 1,
                first.RoundWins + second.RoundWins + 1);
            BattleAnnouncementKind announcement = BattleAnnouncementKind.None;
            string text = string.Empty;
            int winner = -1;

            if (state.Phase == BattlePhase.MatchEnded)
            {
                announcement = BattleAnnouncementKind.MatchVictory;
                winner = state.MatchWinnerSlot?.Value ?? -1;
                text = winner >= 0 ? $"MATCH\nVICTORY\nP{winner + 1}" : "MATCH\nDRAW";
            }
            else if (state.Phase == BattlePhase.RoundEnded)
            {
                if (state.RoundResult.HasWinner)
                {
                    announcement = BattleAnnouncementKind.RoundWin;
                    winner = state.RoundResult.WinnerSlot.Value;
                    text = $"ROUND WIN\nP{winner + 1}";
                }
                else if (state.RoundResult.Kind == RoundResultKind.Draw)
                {
                    announcement = BattleAnnouncementKind.Draw;
                    text = "DRAW";
                }
            }
            else if (state.Phase == BattlePhase.RoundCountdown)
            {
                uint count = Math.Max(1U, (state.PhaseTicksRemaining + SimulationConfig.TickRate - 1U) /
                    SimulationConfig.TickRate);
                announcement = count >= 3 ? BattleAnnouncementKind.Countdown3 :
                    count == 2 ? BattleAnnouncementKind.Countdown2 : BattleAnnouncementKind.Countdown1;
                text = Math.Min(3U, count).ToString();
            }
            else if (state.Phase == BattlePhase.Playing &&
                     state.RoundTicksRemaining + SimulationConfig.TickRate >
                     state.Definition.Gameplay.RoundDurationTicks)
            {
                announcement = BattleAnnouncementKind.Fight;
                text = "FIGHT";
            }

            uint seconds = (state.RoundTicksRemaining + SimulationConfig.TickRate - 1U) /
                SimulationConfig.TickRate;
            return new BattlePresentationSnapshot(
                first.HitPoints,
                second.HitPoints,
                first.RoundWins,
                second.RoundWins,
                round,
                seconds,
                announcement,
                text,
                winner);
        }
    }
}
