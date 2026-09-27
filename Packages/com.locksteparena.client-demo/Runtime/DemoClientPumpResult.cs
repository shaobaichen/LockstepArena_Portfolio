namespace LockstepArena.Client.Demo
{
    public readonly struct DemoClientPumpResult
    {
        public DemoClientPumpResult(int controlBytesSent, int controlEventsProcessed, int authoritativeFramesProcessed, bool predictionSent)
        {
            ControlBytesSent = controlBytesSent;
            ControlEventsProcessed = controlEventsProcessed;
            AuthoritativeFramesProcessed = authoritativeFramesProcessed;
            PredictionSent = predictionSent;
        }
        public int ControlBytesSent { get; }
        public int ControlEventsProcessed { get; }
        public int AuthoritativeFramesProcessed { get; }
        public bool PredictionSent { get; }
    }
}
