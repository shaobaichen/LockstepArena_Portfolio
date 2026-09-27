namespace LockstepArena.Server.DemoHost
{
    public readonly struct DemoServerPumpResult
    {
        public DemoServerPumpResult(int acceptedControlConnections, int acceptedBattleConnections, int processedControlCommands, int publishedAuthoritativeFrames, int completedBattles, int abortedBattles)
        {
            AcceptedControlConnections = acceptedControlConnections;
            AcceptedBattleConnections = acceptedBattleConnections;
            ProcessedControlCommands = processedControlCommands;
            PublishedAuthoritativeFrames = publishedAuthoritativeFrames;
            CompletedBattles = completedBattles;
            AbortedBattles = abortedBattles;
        }

        public int AcceptedControlConnections { get; }
        public int AcceptedBattleConnections { get; }
        public int ProcessedControlCommands { get; }
        public int PublishedAuthoritativeFrames { get; }
        public int CompletedBattles { get; }
        public int AbortedBattles { get; }
    }
}
