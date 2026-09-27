#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LockstepArena.Demo.Editor
{
    public static class V3AWindowsBuild
    {
        private const string LobbyScene = "Assets/LockstepArenaDemo/Scenes/LobbyScene.unity";
        private const string BattleScene = "Assets/LockstepArenaDemo/Scenes/BattleScene.unity";

        [MenuItem("Lockstep Arena/Build v3-A Windows Player")]
        public static void BuildWindowsPlayer()
        {
            string repositoryRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Repository root could not be resolved from Application.dataPath.");
            string artifactsRoot = Path.Combine(repositoryRoot, ".artifacts");
            string playerRoot = Path.Combine(artifactsRoot, "V3APlayer");
            string publishRoot = Path.Combine(artifactsRoot, "V3AServerPublish");
            string serverProject = Path.Combine(
                repositoryRoot,
                "Server",
                "LockstepArena.Server.DemoHost",
                "LockstepArena.Server.DemoHost.csproj");

            RecreateOutputDirectory(playerRoot, artifactsRoot);
            RecreateOutputDirectory(publishRoot, artifactsRoot);
            PublishServer(repositoryRoot, serverProject, publishRoot);

            string playerExecutable = Path.Combine(playerRoot, "LockstepArena.exe");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { LobbyScene, BattleScene },
                locationPathName = playerExecutable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows Player build failed: {report.summary.result}, " +
                    $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}.");
            }

            string packagedServer = Path.Combine(playerRoot, "Server");
            CopyDirectory(publishRoot, packagedServer);
            string packagedExecutable = Path.Combine(packagedServer, "LockstepArena.Server.DemoHost.exe");
            if (!File.Exists(packagedExecutable))
                throw new FileNotFoundException("The self-contained DemoHost executable was not packaged.", packagedExecutable);

            Debug.Log($"v3-A Windows package completed: {playerExecutable}");
        }

        private static void PublishServer(string repositoryRoot, string serverProject, string publishRoot)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{serverProject}\" -c Release -r win-x64 --self-contained true -o \"{publishRoot}\"",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("dotnet publish could not be started.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WhenAll(standardOutput, standardError).GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"DemoHost publish failed with exit code {process.ExitCode}.\n" +
                    standardOutput.Result + "\n" + standardError.Result);
            }
            Debug.Log(standardOutput.Result);
        }

        private static void RecreateOutputDirectory(string path, string artifactsRoot)
        {
            string fullPath = Path.GetFullPath(path);
            string fullArtifactsRoot = Path.GetFullPath(artifactsRoot) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(fullArtifactsRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Build output escaped the repository artifacts directory.");
            if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive: true);
            Directory.CreateDirectory(fullPath);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }
    }
}
