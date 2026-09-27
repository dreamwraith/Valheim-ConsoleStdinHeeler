using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Dispatches and routes dedicated server console commands entered via STDIN
    /// to custom handlers, dedicated-server command overrides, or native terminal execution.
    /// </summary>
    public static class CommandServer
    {
        private static readonly Regex CoordRegex =
            new(@"^-?\d+(?:\.\d+)?,-?\d+(?:\.\d+)?(?:,-?\d+(?:\.\d+)?)?$", RegexOptions.Compiled);

        /// <summary>
        /// Executes a raw command line input from the STDIN queue.
        /// </summary>
        public static void Execute(string rawCommandLine)
        {
            if (string.IsNullOrWhiteSpace(rawCommandLine))
            {
                return;
            }

            try
            {
                string prompt = Plugin.ConsolePrompt?.Value ?? "> ";
                Plugin.Log.LogInfo($"{TerminalColor.Gray}{prompt}{TerminalColor.Cyan}{rawCommandLine}{TerminalColor.Reset}");

                // Cleanly split into command name and trailing arguments
                string trimmedCommandLine = rawCommandLine.Trim();
                int firstSpaceIndex = trimmedCommandLine.IndexOf(' ');

                string commandName = (firstSpaceIndex < 0 ? trimmedCommandLine : trimmedCommandLine.Substring(0, firstSpaceIndex)).ToLowerInvariant();
                string commandArguments = firstSpaceIndex < 0 ? string.Empty : trimmedCommandLine.Substring(firstSpaceIndex + 1).Trim();

                if (string.IsNullOrEmpty(commandName))
                {
                    return;
                }

                // Guard against commands that explicitly require a client or local player character
                if (CommandFilter.IsServerIncompatible(commandName))
                {
                    Plugin.Log.LogWarning($"Command '{commandName}' requires an active game client or in-game player character and cannot be run from a dedicated server console.");
                    return;
                }

                switch (commandName)
                {
                    case "save":
                        CommandCustom.HandleSaveCommand();
                        return;

                    case "shutdown":
                    case "stop":
                    case "exit":
                    case "quit":
                        CommandCustom.HandleShutdownCommand(commandArguments);
                        return;

                    case "kickall":
                        CommandCustom.HandleKickAllCommand(commandArguments);
                        return;

                    case "tp":
                    case "teleport":
                        CommandCustom.HandleTeleportCommand(commandArguments);
                        return;

                    case "players":
                    case "list":
                        CommandCustom.HandlePlayersCommand();
                        return;

                    case "say":
                        CommandCustom.HandleSayCommand(commandArguments);
                        return;

                    case "announce":
                    case "broadcast":
                    case "alert":
                        CommandCustom.HandleAnnounceCommand(commandArguments);
                        return;

                    case "tod":
                        CommandOverrides.HandleTodCommand(commandArguments);
                        return;

                    case "event":
                        CommandOverrides.HandleEventCommand(commandArguments);
                        return;

                    case "stopevent":
                        CommandOverrides.HandleStopEventCommand();
                        return;

                    case "sleep":
                        CommandOverrides.HandleSleepCommand();
                        return;

                    case "spawn":
                        CommandOverrides.HandleSpawnCommand(commandArguments);
                        return;

                    case "help":
                        CommandCustom.HandleHelpCommand(commandArguments);
                        return;

                    default:
                        // All other commands (native Valheim or modded e.g. WorldEditCommands)
                        ExecuteTerminalCommand(rawCommandLine);
                        return;
                }
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"Error executing STDIN command '{rawCommandLine}': {exception.Message}");
                Plugin.Log.LogDebug(exception.ToString());
            }
        }

        public static void ExecuteTerminalCommand(string commandLine)
        {
            if (Console.instance == null)
            {
                Plugin.Log.LogWarning("Valheim Console/Terminal instance is not available yet.");
                return;
            }

            bool previousCheatState = Terminal.m_cheat;
            try
            {
                if (Plugin.EnableCheatsByDefault != null && Plugin.EnableCheatsByDefault.Value)
                {
                    Terminal.m_cheat = true;
                }

                // silentFail: false, skipAllowedCheck: true (allows admin/dev commands on dedicated server)
                Console.instance.TryRunCommand(commandLine, silentFail: false, skipAllowedCheck: true);
            }
            finally
            {
                Terminal.m_cheat = previousCheatState;
            }
        }

        /// <summary>
        /// Finds an active connected peer matching the specified player name, network endpoint, Steam ID, PlayFab ID, or user ID (case-insensitive).
        /// If '@random' is provided, selects an active connected peer at random.
        /// </summary>
        public static ZNetPeer? FindPeer(string playerIdentifier)
        {
            if (string.IsNullOrWhiteSpace(playerIdentifier) || ZNet.instance == null)
            {
                return null;
            }

            string cleanId = playerIdentifier.Trim();
            if (string.IsNullOrEmpty(cleanId))
            {
                return null;
            }

            var connectedPeers = ZNet.instance.GetConnectedPeers();
            if (connectedPeers == null || connectedPeers.Count == 0)
            {
                return null;
            }

            // Intercept '@random' player targeting: pick a random connected peer
            if (string.Equals(cleanId, "@random", StringComparison.OrdinalIgnoreCase))
            {
                var validPeers = connectedPeers.FindAll(p => p != null);
                if (validPeers.Count == 0)
                {
                    return null;
                }

                int randomIndex = UnityEngine.Random.Range(0, validPeers.Count);
                return validPeers[randomIndex];
            }

            return connectedPeers.Find(p =>
            {
                if (p == null)
                {
                    return false;
                }

                // Match character name (exact match, case-insensitive)
                if (string.Equals(p.m_playerName, cleanId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Match Steam ID (m_uid is Steam ID in dedicated server networking)
                if (p.m_uid.ToString() == cleanId)
                {
                    return true;
                }

                // Match PlayFab ID if present
                if (!string.IsNullOrEmpty(p.m_playfabId) && string.Equals(p.m_playfabId, cleanId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Match player ID or character user ID
                if (p.m_playerID.ToString() == cleanId || p.m_characterID.UserID.ToString() == cleanId)
                {
                    return true;
                }

                // Match socket endpoint (e.g. "Steam_76561197970546107" or "192.168.1.5:1234")
                string? hostName = p.m_socket?.GetHostName();
                if (!string.IsNullOrEmpty(hostName) &&
                    (string.Equals(hostName, cleanId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(hostName, "Steam_" + cleanId, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// Strictly resolves an active connected peer by player name (exact/quote-delimited), Steam ID, PlayFab ID, or user ID.
        /// Outputs descriptive warnings if no players are connected or the player is not found.
        /// </summary>
        public static bool TryResolvePeer(string playerIdentifier, out ZNetPeer peer, string roleDescription = "Player")
        {
            peer = null!;
            if (string.IsNullOrWhiteSpace(playerIdentifier))
            {
                return false;
            }

            if (ZNet.instance == null)
            {
                Plugin.Log.LogWarning("Cannot find player: Server world is not loaded.");
                return false;
            }

            var connectedPeers = ZNet.instance.GetConnectedPeers();
            if (connectedPeers.Count == 0)
            {
                Plugin.Log.LogWarning("Cannot find player: No players are currently connected to the server.");
                return false;
            }

            ZNetPeer? match = FindPeer(playerIdentifier);
            if (match == null)
            {
                Plugin.Log.LogWarning($"{roleDescription} '{playerIdentifier.Trim()}' not found among connected peers.");
                return false;
            }

            peer = match;
            return true;
        }

        /// <summary>
        /// Attempts to parse a strict coordinate mask (0,0 or 0,0,0) without spaces.
        /// </summary>
        public static bool TryParseCoordinates(string text, out Vector3 coords)
        {
            coords = Vector3.zero;
            if (string.IsNullOrWhiteSpace(text) || !CoordRegex.IsMatch(text))
            {
                return false;
            }

            string[] tokens = text.Split([','], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 3 &&
                float.TryParse(tokens[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x3) &&
                float.TryParse(tokens[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y3) &&
                float.TryParse(tokens[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z3))
            {
                coords = new Vector3(x3, y3, z3);
                return true;
            }

            if (tokens.Length == 2 &&
                float.TryParse(tokens[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x2) &&
                float.TryParse(tokens[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z2))
            {
                float y = ZoneSystem.instance != null ? ZoneSystem.instance.GetSolidHeight(new Vector3(x2, 0f, z2)) + 0.5f : 30f;
                coords = new Vector3(x2, y, z2);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Strictly resolves a target token as either coordinates (0,0 or 0,0,0 mask) or an exact connected peer.
        /// Fails and outputs a warning if neither matches.
        /// </summary>
        public static bool TryResolveTarget(string targetToken, out CommandTarget target)
        {
            target = default;
            if (string.IsNullOrWhiteSpace(targetToken))
            {
                return false;
            }

            if (TryParseCoordinates(targetToken, out Vector3 coords))
            {
                target = new CommandTarget(
                    position: coords,
                    forward: Vector3.forward,
                    peer: null,
                    player: null,
                    description: $"coordinates ({coords.x:F1}, {coords.y:F1}, {coords.z:F1})",
                    isCoordinates: true
                );
                return true;
            }

            if (!TryResolvePeer(targetToken, out ZNetPeer peer, "Target player or coordinates"))
            {
                return false;
            }

            Player? player = null;
            var allPlayers = Player.GetAllPlayers();
            if (allPlayers != null)
            {
                player = allPlayers.Find(p => p != null &&
                    (string.Equals(p.GetPlayerName(), peer.m_playerName, StringComparison.OrdinalIgnoreCase) ||
                     p.GetPlayerID() == peer.m_playerID));
            }

            Vector3 pos;
            Vector3 fwd;
            string desc;

            if (player != null)
            {
                pos = player.transform.position;
                fwd = player.transform.forward;
                desc = $"player '{TerminalColor.Cyan}{player.GetPlayerName()}{TerminalColor.Reset}' at {pos}";
            }
            else
            {
                pos = peer.m_refPos;
                fwd = Vector3.forward;
                desc = $"player '{TerminalColor.Cyan}{peer.m_playerName}{TerminalColor.Reset}' at {pos}";
            }

            target = new CommandTarget(
                position: pos,
                forward: fwd,
                peer: peer,
                player: player,
                description: desc,
                isCoordinates: false
            );
            return true;
        }
    }

    /// <summary>
    /// Represents a resolved command target, which can be either explicit coordinates or a connected player.
    /// </summary>
    public readonly struct CommandTarget
    {
        public Vector3 Position { get; }
        public Vector3 Forward { get; }
        public ZNetPeer? Peer { get; }
        public Player? Player { get; }
        public string Description { get; }
        public bool IsCoordinates { get; }

        public CommandTarget(Vector3 position, Vector3 forward, ZNetPeer? peer, Player? player, string description, bool isCoordinates)
        {
            Position = position;
            Forward = forward;
            Peer = peer;
            Player = player;
            Description = description;
            IsCoordinates = isCoordinates;
        }
    }
}
