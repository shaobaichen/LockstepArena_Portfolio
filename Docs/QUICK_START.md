# Quick Start

## Play the Windows demo

Keep the whole `LockstepArena-Windows-x64` folder together. No Unity, Visual Studio, or .NET SDK is required.

### Host

1. Launch `LockstepArena.exe`.
2. Choose Create LAN, enter a nickname and room name, then create the room.
3. Share the displayed host IPv4 with the guest. The bundled server starts automatically.

### Guest

1. Launch `LockstepArena.exe`.
2. Choose Join LAN, enter a different nickname and the host's IPv4.
3. Select the room and join.
4. Both players choose Ready; the host chooses Start.

On one PC, run two copies and use `127.0.0.1`. On different PCs, use the active LAN adapter's IPv4, not the guest's address or a disconnected VPN adapter. If Windows Firewall asks, allow Private Network access. This demo is intended for a trusted private LAN.

## Controls

| Key | Action |
| --- | --- |
| WASD | Move |
| Mouse | Aim |
| Held left mouse | Fire |
| F1 | Show/hide diagnostics |
| Space in replay | Pause/resume |
| Escape in replay | Return to result |

Win two rounds to finish the match. After verified settlement, Watch Replay re-simulates authoritative inputs offline. Replay can be watched repeatedly. Completion holds the final frame until Back To Result; Return To Lobby is a separate result action.

## Build from source

Use Unity `6000.3.10f1` and .NET SDK `8.0.419`. Open the saved LobbyScene, close game clients, and select **Lockstep Arena → Build v3-A Windows Player**. The current build appears under `.artifacts/V3APlayer/`, including `Server/`. Do not regenerate presentation assets to build a manually adjusted scene.

See [Testing](TESTING.md) for automatic and manual checks.

## Troubleshooting

- **Data folder not found:** copy/rebuild the complete output; `LockstepArena_Data` must sit next to the executable. Do not mix files from different builds.
- **Port conflict/server startup error:** ports 46000 and 46001 must be free on the host. Close your known old host/server first. Create LAN does not silently reuse an unknown listener or kill a manually started server.
- **Cannot connect:** check the host's active IPv4 and Private Network firewall permissions for Player and server. Do not expose these ports to the public Internet.
- **Start unavailable:** both slots must be occupied and both players Ready; only the host can start.
- **Watch Replay unavailable:** complete a match and wait for verified settlement. Aborted/unverified matches are not offered as verified replays.
- **Battle time stalls:** authority requires both players' input. Keep both clients running; background pumping is enabled, but a missing/disconnected peer is not replaced by a bot.
- **Digests differ:** first compare ticks. Different ticks may have different valid states; F1 shows N/A instead of claiming a mismatch in that case.

Credits and notices are in the package's `THIRD_PARTY_LICENSES.txt` and bundled server license files.
