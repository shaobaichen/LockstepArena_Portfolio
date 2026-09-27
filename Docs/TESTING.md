# Testing

## Environment

Use Unity `6000.3.10f1` with Windows build support and .NET SDK `8.0.419` from `global.json`. Ordinary users of the complete Windows package do not need either tool.

## Focused .NET suites

Run serially from the repository root:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
```

After restore, `--no-restore -p:UseSharedCompilation=false -p:BuildInParallel=false` may be used for serial local verification. On a fresh copy, allow restore first. Check the runner's failed count and exit code; starting the runner is not evidence of passing tests.

| Suite | Important contracts |
| --- | --- |
| Simulation | Deterministic frames/state, integer movement/collision, projectile damage/lifetime, rounds/results, digest. |
| DemoFlow | Real CONTROL/room/ready/start/settlement/lobby lifecycle, verified replay retention, offline player, diagnostics. |
| Client Prediction | Bounded timeline, complete-input dirty detection, rollback and re-simulation. |
| Live Prediction | Real TCP authority/prediction/replay convergence and snapshot capture. |

Other `Tests/` projects cover protocol, framing, and server scheduling/transport independently. Shared project references, not historical naming, determine required build dependencies.

## Unity EditMode

Open **Window → General → Test Runner**, clear the search box, choose **EditMode → Run All**, and inspect the complete result. Export Results can preserve the current run's XML.

Presentation coverage includes:

- Authored scene, URP material, robot/weapon geometry, mouse camera binding and editable presentation anchors.
- HP bars, announcement precedence, result/lobby actions, and no persistent formal controls row.
- Simultaneous projectile removal/HP decrease cannot misclassify all removals as player impacts.
- Watch Replay requires verified settlement and a snapshot.
- Replay suppresses gameplay input, uses offline state, holds completion, returns to the same result, and starts again cleanly.
- Full presentation reset allows the same projectile/HP feedback again and removes owned transient effects.
- Successful live/replay presentation clears a recovered scene-binding diagnostic error.

Tests must exercise current code. Do not substitute a previous run's counts or a preview build for final verification.

## Windows build

Close game clients. In Unity select **Lockstep Arena → Build v3-A Windows Player**. The historical menu name builds current saved Lobby/Battle scenes and a self-contained Windows x64 DemoHost.

Check the build result and fresh output under `.artifacts/V3APlayer/`. Keep the executable, `_Data`, Unity runtime libraries, Mono runtime, D3D12 files, and `Server/` together. The final demo folder also includes QUICK_START and third-party notices. Debug-information folders marked `DoNotShip` are not runtime dependencies.

## Two-client manual acceptance

Use the final complete Windows folder, with different nicknames:

1. Main Menu → Create LAN on the host; Join LAN on the guest (`127.0.0.1` on one PC, host IPv4 on LAN).
2. Select/join the room, ready both players, and start as host.
3. Check movement, mouse facing/aim, projectiles, HP decrease, death feedback, round reset, and first-to-two-wins result.
4. Toggle F1 in live play. Find authority/predicted ticks, dirty-frame count, same-tick digests, and replay/settlement status.
5. At verified result, select Watch Replay. Check characters, projectiles, HP and death replay.
6. Press Space to pause/resume. WASD/mouse fire must not control replay.
7. Toggle F1 in replay: AUTHORITATIVE, OFFLINE, frame progress, and replay digest.
8. Press Escape to return to the same result, then Watch Replay again. Effects/audio must still appear.
9. Let replay complete. It must hold the final state until Back To Result; it must not auto-return to lobby.
10. Return To Lobby and start a second real match without restarting clients/server.
11. Check that the second match has no replay mode, stale feedback, or recovered error text.

## Independent source-copy validation

A portable source copy is valid only when its own directory opens in Unity, imports without missing scripts/materials/textures/prefabs, passes selected .NET and full EditMode tests, builds Windows with the self-contained server, and passes two-client/replay/lobby smoke testing. Do not copy the development directory's Library/Temp caches or use its build as proof of the copy.

Run `git diff --check` for the actual change scope. Keep unrelated pre-existing workspace changes separate from new whitespace errors.
