#nullable enable
using System;
using LockstepArena.Client.Demo;
using LockstepArena.Simulation;

namespace LockstepArena.Demo
{
    public static class LockstepDiagnosticsFormatter
    {
        public static string Format(DemoClientSnapshot snapshot, BattleReplayPlayer? replay = null, uint? inputDelayTicks = null)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            if (replay is not null)
                return $"LOCKSTEP DIAGNOSTICS\n\nREPLAY MODE\nFrame: {replay.CurrentFrame} / {replay.TotalFrames}\n" +
                    $"Source: AUTHORITATIVE\nNetwork: OFFLINE\nDigest: {StateDigest.Compute(replay.CurrentState):X16}\n" +
                    $"Paused: {replay.IsPaused}   Complete: {replay.IsComplete}";

            string delay = inputDelayTicks.HasValue ? $"{inputDelayTicks.Value} ticks" : "N/A";
            string match = snapshot.AuthoritativeTick == snapshot.PredictedTick
                ? $"{snapshot.AuthoritativeDigest == snapshot.PredictedDigest} (same tick)"
                : "N/A (different ticks)";
            return "LOCKSTEP DIAGNOSTICS\n\nNETWORK\n" +
                $"Server Tick: {snapshot.ServerStateTick}   Authoritative Tick: {snapshot.AuthoritativeTick}   Predicted Tick: {snapshot.PredictedTick}\n" +
                $"Input Delay: {delay}\n\nPREDICTION\n" +
                $"Pending Prediction: {snapshot.PendingPredictionCount}   Pending Authority: {snapshot.PendingAuthoritativeFrameCount}\n" +
                $"Latest Dirty: {snapshot.LatestDirty}   Rollback Count (dirty frames): {snapshot.CumulativeDirtyFrameCount}\n\nDETERMINISM\n" +
                $"Authority Digest: {snapshot.AuthoritativeDigest:X16}   Predicted Digest: {snapshot.PredictedDigest:X16}\n" +
                $"Digest Match: {match}\n\nREPLAY\nAuthoritative Frames: {snapshot.ReplayFrameCount}   Replay Verified: {snapshot.SettlementVerified}\n\nMATCH\n" +
                $"Phase: {snapshot.Phase}   Settlement Verified: {snapshot.SettlementVerified}\n" +
                $"Session: {snapshot.SessionId}   Room: {snapshot.RoomId}   Battle: {snapshot.BattleId}\n" +
                $"Settlement: {snapshot.SettlementReason}   Winner: {snapshot.WinnerPlayerId}   Score: {snapshot.Slot0RoundWins}-{snapshot.Slot1RoundWins}";
        }
    }
}
