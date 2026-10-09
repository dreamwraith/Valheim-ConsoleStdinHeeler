# Changelog

All notable changes to **ConsoleStdinHeeler** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.2] - 2026-10-08

### Fixed
- Restored bundled CHANGELOG.md inside release distribution archive.
- Removed debug .pdb symbol files from release distribution archive.

## [1.1.1] - 2026-10-08

### Changed
- Centralized build and release automation tooling to shared `DW.ValheimModTools`.
- Added support for automated Nexus Mods packaging and publishing.

## [1.1.0] - 2026-10-08

### Added
- Persistent center-screen announcements via optional `-t <seconds>` flag for `announce`, `broadcast`, and `alert` commands (capped at 60s), refreshed every 1.0s via a non-blocking coroutine to eliminate banner fading.
- Announcement cancellation support (`announce cancel` / `announce abort`) to immediately terminate active announcement pulses.
- Updated dedicated server `help` catalog and documentation for `-t` flag and cancellation syntax.

## [1.0.2] - 2026-10-02

### Added
- Silent mode flag (`-s`) to the `spawn` command to suppress the in-game UI notification banner on targeted player clients.
- Updated command reference documentation and dedicated server `help` catalog for `spawn` syntax.

## [1.0.1] - 2026-09-26

### Fixed
- Corrected project repository and website URLs across package metadata, manifest, and assembly info.
- Revised documentation and changelog formatting.

## [1.0.0] - 2026-09-25

### Added
- **Initial Release of ConsoleStdinHeeler**.
- **Non-Blocking STDIN Console Architecture**:
  - Background reader thread (`ConsoleStdinHeeler_Reader`) reading `Console.ReadLine()` without blocking the Unity main thread.
  - Three-strike consecutive EOF detection automatically terminating the reader thread when the input stream closes (e.g. systemd without interactive TTY or detached Docker containers), preventing CPU spin loops.
  - Thread-safe command queue (`ConcurrentQueue<string>`) drained sequentially on the Unity main thread during `Update()`.
  - Defensive execution wrapping command dispatches in exception handlers to prevent unhandled errors from interrupting the server loop.
- **Dedicated Server Management Commands**:
  - `save`: Triggers an immediate synchronous save of world data and player profiles.
  - `shutdown` / `stop` / `exit` / `quit`: Saves world state and initiates graceful server termination, supporting optional countdown delays in minutes with custom broadcast reasons and cancellation (`stop cancel`).
  - `players` / `list`: Displays connected players, character names, IP endpoints, round-trip ping latency, and peer IDs.
  - `kick` / `ban` / `unban` / `banned`: Dedicated player moderation commands supporting player name, IP, Steam ID, and PlayFab ID matching.
  - `kickall`: Saves world data, displays an announcement banner, and disconnects all connected players after a configurable delay (default: 3 seconds) with custom kick messages.
- **In-Game Broadcasts & Remote Administration**:
  - `say`: Sends top-left notification banners to all players (`@all` or `*`) or a targeted individual via `ZRoutedRpc`.
  - `announce` / `broadcast` / `alert`: Displays center-screen announcement banners to all players or a targeted player via `ZRoutedRpc`.
  - `tp` / `teleport`: Teleports a connected player to another player's position or to world coordinates (`x,y,z`), with target position validation and client UI feedback.
  - `spawn`: Server-side prefab and creature spawning near players or coordinates with optional quantity and star level parameters.
  - `event` / `stopevent` / `randomevent`: Server-side raid event triggering, termination, and random triggering.
  - `tod` / `skiptime` / `sleep`: World time of day adjustment, skipping forward in seconds, and sleeping to morning with network synchronization across all clients.
  - Dynamic `@random` player targeting for commands accepting player targets (`tp`, `say`, `announce`, `spawn`, `event`).
- **Command Routing & Mod Compatibility**:
  - Automatic passthrough of unrecognized commands to Valheim's central `Terminal.commands` registry, enabling terminal execution for mods like **WorldEditCommands**, **ServerDevcommands**, and **InfinityHammer**.
  - Automatic `Terminal.m_cheat` elevation during command dispatch, allowing administrative and developer commands to execute on dedicated servers.
  - Server command filter intercepting client-only commands (emotes, rendering settings, local player cheats) with clear warnings instead of throwing exceptions.
  - Curated `help` command listing dedicated server commands, installed mod commands, and native utilities, with `help all` providing an unfiltered catalog.
- **Rich-Text ANSI Terminal Translation**:
  - Conversion of Unity `<color=...>` tags into terminal ANSI color escape sequences with configurable display modes (`Ansi`, `Strip`, `Raw`).
  - Configurable console prompt prefix (default: `> `) displayed in server logs when commands are received.
