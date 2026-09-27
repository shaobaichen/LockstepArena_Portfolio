#nullable enable
using System;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Simulation;

namespace LockstepArena.Demo
{
    public sealed class BattleReplayPlayer
    {
        private readonly BattleReplaySnapshot _snapshot;
        private BattleSimulation _simulation;
        private double _seconds;

        public BattleReplayPlayer(BattleReplaySnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _simulation = new BattleSimulation(snapshot.InitialState);
        }

        public BattleState CurrentState => _simulation.State;
        public int CurrentFrame { get; private set; }
        public int TotalFrames => _snapshot.AuthoritativeFrames.Count;
        public bool IsPaused { get; private set; }
        public bool IsComplete => CurrentFrame == TotalFrames;

        public bool AdvanceTime(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (IsPaused || IsComplete) return false;
            _seconds += elapsedSeconds;
            double tickSeconds = 1d / SimulationConfig.TickRate;
            if (_seconds < tickSeconds) return false;
            _seconds -= tickSeconds;
            // Observe every replay frame through presentation; do not skip frames to catch up.
            return Tick();
        }

        public bool Tick()
        {
            if (IsPaused || IsComplete) return false;
            _simulation.Step(_snapshot.AuthoritativeFrames[CurrentFrame]);
            CurrentFrame++;
            return true;
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;
        public void TogglePause() => IsPaused = !IsPaused;

        public void Restart()
        {
            _simulation = new BattleSimulation(_snapshot.InitialState);
            CurrentFrame = 0;
            IsPaused = false;
            _seconds = 0d;
        }
    }
}
