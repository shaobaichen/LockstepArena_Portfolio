#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace LockstepArena.Demo
{
    public static class LanServerPathResolver
    {
        public static string Resolve()
        {
            return ResolveFromApplicationDataPath(Application.dataPath, Application.isEditor);
        }

        public static string ResolveFromApplicationDataPath(string applicationDataPath, bool isEditor)
        {
            if (string.IsNullOrWhiteSpace(applicationDataPath))
            {
                throw new ArgumentException("Application data path is required.", nameof(applicationDataPath));
            }

            DirectoryInfo? dataDirectory = Directory.GetParent(Path.GetFullPath(applicationDataPath));
            if (dataDirectory == null)
            {
                throw new InvalidOperationException("Application data path has no parent directory.");
            }

            return isEditor
                ? Path.Combine(
                    dataDirectory.FullName,
                    "Server",
                    "LockstepArena.Server.DemoHost",
                    "bin",
                    "Release",
                    "net8.0",
                    "LockstepArena.Server.DemoHost.exe")
                : Path.Combine(dataDirectory.FullName, "Server", "LockstepArena.Server.DemoHost.exe");
        }
    }
}
