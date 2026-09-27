using System;

namespace LockstepArena.Simulation
{
    public sealed class BattleDefinition
    {
        public BattleDefinition(GameplayConfig gameplay, ArenaConfig arena)
        {
            Gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            Arena = arena ?? throw new ArgumentNullException(nameof(arena));
        }

        public GameplayConfig Gameplay { get; }
        public ArenaConfig Arena { get; }

        public static BattleDefinition CreateDefault() => new BattleDefinition(GameplayConfig.CreateDefault(), ArenaConfig.CreateDefault());
    }
}
