using System;
using System.Globalization;
using System.Threading;
using LockstepArena.Simulation;

namespace LockstepArena.Server.DemoHost
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string bindAddress = GetOption(args, "--bind", "0.0.0.0");
            int controlPort = ParsePort(GetOption(args, "--control-port", "46000"));
            int battlePort = ParsePort(GetOption(args, "--battle-port", "46001"));
            BattleDefinition definition = BattleDefinition.CreateDefault();
            PlayerState[] spawns =
            {
                new PlayerState(definition.Arena.GetSpawn(0).X, definition.Arena.GetSpawn(0).Z, 0),
                new PlayerState(definition.Arena.GetSpawn(1).X, definition.Arena.GetSpawn(1).Z, 32768),
            };
            var options = new DemoServerOptions(
                controlPort, battlePort, 8, 8, 2, spawns, 2U, 8U, 4096, 54_000U,
                4096, 32768, 1024, 7, 257, 8, 512,
                4096, 1024, 11, 251,
                bindAddress, definition, TimeSpan.FromSeconds(15));
            using var server = new TcpDemoServer(options);
            Console.WriteLine($"Lockstep Arena Gameplay Sample: BIND={server.BindAddress} CONTROL={server.ControlPort} BATTLE={server.BattlePort}");
            Console.WriteLine("Press Q to stop.");
            while (true)
            {
                server.PumpOnce();
                if (!Console.IsInputRedirected && Console.KeyAvailable &&
                    Console.ReadKey(true).Key == ConsoleKey.Q) break;
                Thread.Sleep(1);
            }
            return 0;
        }

        private static string GetOption(string[] args, string name, string fallback)
        {
            for (int index = 0; index < args.Length; index++)
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    if (index + 1 >= args.Length) throw new ArgumentException($"Missing value for {name}.", nameof(args));
                    return args[index + 1];
                }
            return fallback;
        }

        private static int ParsePort(string value)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int port) ||
                port < 1 || port > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "Port must be from 1 through 65535.");
            return port;
        }
    }
}
