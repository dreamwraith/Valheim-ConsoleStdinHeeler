# ConsoleStdinHeeler

[![GitHub Release](https://img.shields.io/github/v/release/dreamwraith/Valheim-ConsoleStdinHeeler?logo=github&color=1081c2)](https://github.com/dreamwraith/Valheim-ConsoleStdinHeeler/releases)
[![GitHub Downloads](https://img.shields.io/github/downloads/dreamwraith/Valheim-ConsoleStdinHeeler/total?logo=github&color=1081c2)](https://github.com/dreamwraith/Valheim-ConsoleStdinHeeler/releases)
[![Last Commit](https://img.shields.io/github/last-commit/dreamwraith/Valheim-ConsoleStdinHeeler?logo=git)](https://github.com/dreamwraith/Valheim-ConsoleStdinHeeler/commits/main)
[![Publish Status](https://img.shields.io/github/actions/workflow/status/dreamwraith/Valheim-ConsoleStdinHeeler/publish.yml?label=Publish%20Portals&logo=githubactions)](https://github.com/dreamwraith/Valheim-ConsoleStdinHeeler/actions)
[![Thunderstore](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fthunderstore.io%2Fapi%2Fexperimental%2Fpackage%2FDreamWraith%2FConsoleStdinHeeler%2F&query=%24.latest.version_number&label=Thunderstore&logo=thunderstore&color=2980b9)](https://thunderstore.io/c/valheim/p/DreamWraith/ConsoleStdinHeeler/)
[![Hexium](https://img.shields.io/badge/Hexium-ConsoleStdinHeeler-6c5ce7)](https://valheim.hexium.gg/mods/DreamWraith/ConsoleStdinHeeler)
[![Nexus Mods](https://img.shields.io/badge/Nexus_Mods-4329-da8e35?logo=nexusmods&logoColor=white)](https://www.nexusmods.com/valheim/mods/4329)
[![Game: Valheim](https://img.shields.io/badge/Valheim-Deep_North_%2F_1.x-1b2838?logo=steam&logoColor=white)](https://store.steampowered.com/app/892970/Valheim/)
[![BepInEx Pack](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fthunderstore.io%2Fapi%2Fexperimental%2Fpackage%2Fdenikson%2FBepInExPack_Valheim%2F&query=%24.latest.version_number&label=BepInEx&color=5B57E7)](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
[![Target: .NET 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet)](ConsoleStdinHeeler.csproj)
[![Dedicated Server Only](https://img.shields.io/badge/Type-Dedicated%20Server-orange)](README.md#the-solution)
[![License](https://img.shields.io/github/license/dreamwraith/Valheim-ConsoleStdinHeeler?color=blue)](LICENSE.md)
[![AI Philosophy](https://img.shields.io/badge/AI%20Philosophy-Software%20Craft-2ea44f?logo=github)](https://gist.github.com/dreamwraith/77c91d656c842611bf8c40febf8056f2)

Non-blocking STDIN console input and native server management commands for Valheim dedicated servers.

> [!NOTE]
> **Dedicated Server Focused:** This mod is intended specifically for headless Valheim servers (Linux or Windows) running in interactive terminals, multiplexers (`screen`, `tmux`), systemd services, or Docker containers. It automatically remains dormant if loaded in a standard game client.

---

## The Problem

In vanilla Valheim, dedicated servers running in headless mode (`valheim_server.exe` on Windows or `valheim_server.x86_64` on Linux) do not read from Standard Input (`STDIN`). Although logs and status messages print to standard output, server hosts cannot enter commands into the terminal or pipe commands into the server process.

Furthermore:

- **No Native Server Console Input**: Dedicated servers do not process interactive terminal input or piped streams. Server hosts cannot run commands locally without installing external RCON mods, game client admin connections, or remote management tools.
- **Missing Operational Commands**: The dedicated server binary lacks standard operational commands such as `shutdown`, `stop`, `kickall`, and countdown timers. Halting the server typically requires sending `SIGINT` (Ctrl+C) or terminating the process, which risks incomplete saves.
- **Main-Thread Blocking & EOF Spin Risks**: Synchronous reading on the main thread blocks the Unity game loop. Conversely, naive background readers without stream closure handling can rapidly enter spin loops and spike CPU usage when detached or running under non-interactive environments (e.g. systemd services or detached containers) where STDIN reaches EOF.

---

## The Solution

**ConsoleStdinHeeler** implements a non-blocking background STDIN listener paired with a main-thread execution queue and dedicated server command suite:

- **Non-Blocking Background Reader**: Runs a dedicated background thread (`ConsoleStdinHeeler_Reader`) reading `Console.ReadLine()` without blocking the Unity main thread.
- **EOF Stream Closure Guard**: Detects consecutive EOF read attempts and terminates the reader thread when the input stream closes, preventing CPU spin in detached environments.
- **Main-Thread Queue Dispatch**: Commands from the background reader are enqueued to a thread-safe `ConcurrentQueue<string>` and executed sequentially during the Unity `Update()` cycle.
- **Dedicated Server Management Suite**: Provides essential server administration commands, including clean saves, graceful shutdowns with countdown timers, player roster diagnostics, moderation, teleports, broadcasts, raids, and time controls.
- **Mod Command Routing**: Routes unrecognized commands to Valheim's central `Terminal.commands` dictionary with elevated permissions (`Terminal.m_cheat`), allowing commands from mods like **WorldEditCommands**, **ServerDevcommands**, and **InfinityHammer** to execute directly from the terminal.
- **ANSI Terminal Translation**: Translates Unity `<color=...>` rich-text markup into terminal ANSI color codes for readable, formatted console logs.

---

## Features

### Non-Blocking STDIN Architecture

- **Background Worker Thread**: Reads `Console.ReadLine()` on a separate thread (`Thread.IsBackground = true`), keeping the main game loop uninterrupted.
- **EOF Stream Detection**: Monitored read loop tracks consecutive null reads. If STDIN reaches stream closure (e.g. systemd without an interactive TTY, or Docker without `-i`), the listener logs the condition and halts the reader thread.
- **Main-Thread Execution**: Commands are pushed to a `ConcurrentQueue<string>` and dequeued on the Unity main thread during `Update()`, ensuring thread safety with engine and mod APIs.
- **Exception Handling**: Individual command execution is encapsulated in exception handlers to prevent malformed or failing commands from interrupting the server tick.

### Dedicated Server Administration

- **Graceful Shutdown (`shutdown` / `stop` / `exit` / `quit`)**: Triggers a world and player profile save (`ZNet.instance.Save`), followed by clean server termination. Supports optional countdown delays in minutes with broadcast reasons (e.g. `stop 5 Maintenance`) and cancellation (`stop cancel`).
- **Immediate World Save (`save`)**: Triggers an immediate synchronous save of world data and connected player profiles (`ZNet.instance.Save(saveOtherPlayerProfiles: true)`).
- **Player Roster (`players` / `list`)**: Displays connected players, character names, IP endpoints, round-trip latency (ping), and internal player IDs.
- **Player Moderation (`kick`, `ban`, `unban`, `banned`)**: Moderates players by character name, IP, Steam ID, or PlayFab ID.
- **Mass Disconnect (`kickall`)**: Saves world and player data, displays an announcement banner, and disconnects all connected players after a configurable delay (default: 3 seconds) with custom kick reasons.

### Remote In-Game Notifications & Controls

- **Banner Notifications (`say`)**: Sends top-left notification banners to all players (`@all` or `*`) or a targeted player via `ZRoutedRpc`.
- **Center Announcements (`announce` / `broadcast` / `alert`)**: Displays center-screen announcement banners to all players or a targeted player. Supports optional persistence duration via `-t <seconds>` (up to 60s) refreshed every second without fading, and cancellation (`announce cancel`).
- **Teleportation (`tp` / `teleport`)**: Teleports a player to another player's position or to coordinates (`x,y,z`). Supports quoted names for player names containing spaces.
- **Player Targeting & `@random`**: Supports `@random` as a player parameter (`tp`, `say`, `announce`, `spawn`, `event`) to select a connected player at random.
- **Prefab Spawning (`spawn`)**: Spawns prefabs near a player or coordinates with optional quantity, star level, and silent flag (`-s`), placing entities safely on the surface.
- **Raid & Event Controls (`event`, `stopevent`, `randomevent`)**: Starts specific or random raids near players or coordinates, or halts active events.
- **World Time Controls (`tod`, `skiptime`, `sleep`)**: Adjusts time of day (`0.0` to `1.0`), skips forward in seconds, or advances time to morning with client synchronization.

### Terminal & Mod Compatibility

- **Mod Command Passthrough**: Unrecognized commands are passed to `Terminal.commands`, allowing console execution for installed developer and administration mods.
- **Cheat Permission Elevation**: Sets `Terminal.m_cheat = true` during command dispatch so cheat-flagged and administrative commands execute on dedicated servers.
- **Client Command Filter**: Intercepts commands that require a local player character or rendering camera (e.g. `god`, `fly`, `ghost`, `pos`, `beard`, `hair`, `fov`) with clear warnings instead of throwing `NullReferenceException` errors.
- **ANSI Color Formatting**: Translates Unity `<color=...>` tags into terminal ANSI colors with configurable display modes (`Ansi`, `Strip`, `Raw`).
- **Command Reference (`help`)**: Displays dedicated server commands and auto-discovered mod commands, with `help all` exposing the full native command catalog.

---

## Available Commands

| Command | Arguments | Description |
| :--- | :--- | :--- |
| `save` | *(none)* | Triggers a save of world and player profiles. |
| `shutdown` / `stop` / `exit` / `quit` | `[min] [reason]` / `cancel` | Graceful shutdown (optional countdown/cancellation), saves world data before terminating. |
| `players` / `list` | *(none)* | Lists connected players, character names, IP endpoints, and ping latency. |
| `kick` | `<name/ip/userID>` | Kicks a player from the server. |
| `kickall` | `[seconds] [reason]` | Saves world/player profiles and disconnects all connected players after a grace delay. |
| `ban` | `<name/ip/userID>` | Bans a player by name, IP, or platform user ID. |
| `unban` | `<name/ip/userID>` | Removes a player from the ban list. |
| `banned` | *(none)* | Displays the current ban list. |
| `say` | `<@all/*\|player> <message>` | Displays a top-left notification banner to all clients or a targeted player. |
| `announce` / `broadcast` / `alert` | `[-t sec] <@all/*\|player> <message>` / `cancel` | Displays a center-screen announcement (optional `-t` persistence up to 60s, or `cancel`). |
| `tp` / `teleport` | `<player> <target/x,y,z>` | Teleports a player to another player or coordinates. |
| `spawn` | `<prefab> <player/coords> [amt] [lvl] [-s]` | Spawns prefabs near a player or coordinates with count, star level, and optional silent mode (`-s`). |
| `event` | `<name> <player/coords>` | Starts a raid/event near a player or coordinates. |
| `stopevent` | *(none)* | Stops any currently active raid/event. |
| `randomevent` | *(none)* | Starts a random raid/event near a player. |
| `tod` | `<0-1>` | Sets time of day for all connected clients (e.g. `0.5` for noon). |
| `skiptime` | `[gameseconds]` | Skips ahead in seconds (default: 240) for all connected clients. |
| `sleep` | *(none)* | Fast-forwards time to next morning. |
| `time` | *(none)* | Displays current world time, day count, and sleep status. |
| `timescale` | `[scale]` | Adjusts game simulation speed (default: 1.0). |
| `ping` | *(none)* | Prints server ping. |
| `info` | *(none)* | Prints server memory and system specifications. |
| `help` | *(optional: `all`)* | Shows dedicated server and mod commands. Use `help all` for the complete native command catalog. |
| *Modded Commands* | *(varies)* | Commands registered by installed BepInEx mods execute directly. |

> [!TIP]
> **Player Targeting & `@random`:** Any command accepting a player target (`tp`, `say`, `announce`, `spawn`, `event`) supports `@random` to select a connected player at random.
>
> **Client-Only Command Safeguard:** Commands that require an active local player character (e.g. `god`, `fly`, `goto`, `pos`, `beard`) are intercepted with a warning rather than throwing `NullReferenceException` errors in the server log.

---

## Configuration

Settings can be customized in `BepInEx/config/dreamwraith.ConsoleStdinHeeler.cfg`:

| Section | Key | Type | Default | Description |
| :--- | :--- | :--- | :--- | :--- |
| `1 - General` | `EnableMod` | `bool` | `true` | Master switch to enable or disable the STDIN console listener. |
| `1 - General` | `EnableCheatsByDefault` | `bool` | `true` | Allows modded and admin cheat commands to execute on the dedicated server. |
| `2 - Display` | `ConsolePrompt` | `string` | `> ` | Prefix displayed in logs when an STDIN command is received. |
| `2 - Display` | `ColorFormatting` | `enum` | `Ansi` | How Unity rich-text color tags from console outputs are handled: Ansi (render in terminal colors), Strip (clean plain text), or Raw (keep tags unchanged). |
| `9 - Debug` | `EnableDebugLogs` | `bool` | `false` | Enables verbose diagnostic logging. |

---

## Server Deployment Notes

### Linux `screen` or `tmux`
Launch the Valheim server inside a `screen` or `tmux` session to enter commands interactively:
```bash
screen -S valheim ./start_server_bepinex.sh
# Type 'save', 'players', or 'shutdown' directly into the console
```

### systemd Services
When running under systemd without an interactive terminal (`StandardInput=null`), ConsoleStdinHeeler detects stream closure (EOF) and cleanly halts the reader thread without spinning CPU. To pass commands into a systemd service, configure a FIFO pipe or interactive TTY.

### Docker
When running inside Docker, allocate standard input:
```bash
docker run -i -t ...
# Or attach to an existing container:
docker attach <container_id>
```
If run detached without `-i`, the reader detects EOF and terminates the thread.

---

## Compatibility

- **Dedicated Servers**: Built specifically for headless dedicated servers (`valheim_server.exe` on Windows or `valheim_server.x86_64` on Linux). Safely disables itself if launched within a standard game client.
- **Server Administration & Dev Mods**: Compatible with server management and developer mods, including **WorldEditCommands**, **ServerDevcommands**, and **InfinityHammer**.
- **Vanilla Game Terminal**: Passes through native Valheim terminal and server utilities (`save`, `ping`, `info`, `genloc`, `optterrain`, `listkeys`, etc.).
- **Interactive Multiplexers & Containers**: Compatible with `screen`, `tmux`, systemd service units, Windows Command Prompt/PowerShell, and interactive Docker containers.
- **Mod Managers**: Fully compatible with **Gale**, **Thunderstore Mod Manager**, and **r2modman**.

---

## Installation

### Manual Installation
1. Ensure **BepInExPack Valheim** is installed on your dedicated server.
2. Download and extract the latest release archive.
3. Place `ConsoleStdinHeeler.dll` into the `BepInEx/plugins/` directory on your server.
4. Launch the dedicated server.

### Mod Manager
1. Install via **Gale**, **Thunderstore Mod Manager**, or **r2modman**.
2. Launch the dedicated server.

---

## Building from Source

The project source code is available on [GitHub](https://github.com/dreamwraith/Valheim-ConsoleStdinHeeler) and uses a portable MSBuild configuration that auto-detects standard Steam paths.

```bash
dotnet build -c Release
```

The compiled assembly will be placed in `bin/Release/net48/ConsoleStdinHeeler.dll`. Building in `Release` configuration also automatically packages the distribution ZIP to `bin/Publish/ConsoleStdinHeeler-<Version>.zip`.

### Custom & CI Paths
For non-standard Steam library locations or mod manager profiles, copy `ConsoleStdinHeeler.csproj.user.example` to `ConsoleStdinHeeler.csproj.user` in the project root (this file is git-ignored and automatically loaded by MSBuild):

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <GamePath>D:\SteamLibrary\steamapps\common\Valheim</GamePath>
    <BepInExCorePath>$(UserProfile)\AppData\Roaming\com.kesomannen.gale\valheim\profiles\<ProfileName>\BepInEx\core</BepInExCorePath>
  </PropertyGroup>
</Project>
```

---

## Packaging, Publishing & Releases

All developer automation tools for release management, packaging, and publishing to **Thunderstore**, **Hexium**, and **Nexus Mods** are consolidated into the single PowerShell 7 entrypoint [.scripts/modtools.ps1](.scripts/modtools.ps1), powered by the shared [**DW.ValheimModTools**](https://github.com/DreamWraith/DW-ValheimModTools) module:

- **Release Management**: pwsh ./.scripts/modtools.ps1 release compiles in Release, creates mod & source archives, extracts changelog notes, and publishes GitHub Releases (Draft by default, or published with -Publish) using the gh CLI.
- **Packaging & Version Bumping**: pwsh ./.scripts/modtools.ps1 package -Bump Patch increments SemVer in ConsoleStdinHeeler.csproj, updates manifest.json, and bundles distribution archives.
- **Portal Publishing**: pwsh ./.scripts/modtools.ps1 publish -Target All uploads directly to Thunderstore, Hexium, and Nexus Mods APIs.
- **CI/CD Workflow**: [.github/workflows/publish.yml](.github/workflows/publish.yml) provides an automated GitHub Actions workflow to publish to Thunderstore, Hexium, and Nexus Mods whenever a GitHub Release is published.

For detailed documentation on flags, workflows, and secret configuration, see [.scripts/README.md](.scripts/README.md).

---

## License, Author Notes
- This project is licensed under the GNU General Public License v3.0 - see the [LICENSE.md](LICENSE.md) file for details. Source code and issue tracking are available at [https://github.com/dreamwraith/Valheim-ConsoleStdinHeeler](https://github.com/dreamwraith/Valheim-ConsoleStdinHeeler).
- [**A Small Note from Me about Valheim Modding Specifically**](https://gist.github.com/dreamwraith/98564f8441dc234bfadd7e2b605c694c) - Thoughts on open-source modding, community inclusivity, and anti-gatekeeping.
- [**A Note on AI, Software Craft, and Why This Code Exists**](https://gist.github.com/dreamwraith/77c91d656c842611bf8c40febf8056f2) - Personal essay on software craft, human agency, and engineering responsibility.

