using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleStdinHeeler
{
    public static partial class CommandCustom
    {
        private static float _kickDisconnectTime = -1f;

        /// <summary>
        /// Teleports a player to another player's position or to specific world coordinates.
        /// Syntax: tp <player|"player name"> <target|"target player"|coords>
        /// Coordinates must strictly follow 0,0 or 0,0,0 mask with no spaces.
        /// Supports quoted names for players with spaces (e.g. tp "Viking Bob" "Viking Alice").
        /// </summary>
        public static void HandleTeleportCommand(string arguments)
        {
            if (ZNet.instance == null)
            {
                Plugin.Log.LogWarning("Cannot teleport: Server world is not loaded.");
                return;
            }

            if (string.IsNullOrWhiteSpace(arguments))
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: tp <player|\"player name\"> <target|\"target player\"|coords>");
                Plugin.Log.LogInfo("Coordinates must strictly follow 0,0 or 0,0,0 mask with no spaces.");
                return;
            }

            List<string> parts = TerminalUtils.ParseArguments(arguments.Trim());
            if (parts.Count != 2)
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: tp <player|\"player name\"> <target|\"target player\"|coords>");
                Plugin.Log.LogInfo("Coordinates must strictly follow 0,0 or 0,0,0 mask with no spaces.");
                return;
            }

            if (!CommandServer.TryResolvePeer(parts[0], out ZNetPeer sourcePeer, "Source player"))
            {
                return;
            }

            string targetArg = parts[1];
            if (!CommandServer.TryResolveTarget(targetArg, out CommandTarget target))
            {
                return;
            }

            if (!target.IsCoordinates && target.Peer == sourcePeer)
            {
                Plugin.Log.LogWarning("Source player and destination player cannot be the same.");
                return;
            }

            Vector3 targetPosition = target.IsCoordinates
                ? target.Position
                : target.Position + target.Forward * 1.5f + Vector3.up * 0.2f;

            string destinationDescription = target.Description;

            // Dispatch routed RPC to client peer to execute distant teleportation and UI transition
            if (ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, sourcePeer.m_characterID, "RPC_TeleportTo", targetPosition, Quaternion.identity, true);
                ZRoutedRpc.instance.InvokeRoutedRPC(sourcePeer.m_uid, "ShowMessage", (int)MessageHud.MessageType.Center, "Teleported by administrator");
            }

            sourcePeer.m_refPos = targetPosition;
            Plugin.Log.LogInfo($"Teleported '{TerminalColor.Cyan}{sourcePeer.m_playerName}{TerminalColor.Reset}' to {destinationDescription}.");
        }

        /// <summary>
        /// Saves world and player profiles, displays an announcement banner,
        /// and disconnects all connected players after an optional grace delay (default: 3 seconds).
        /// Usage: kickall [seconds] [reason]
        /// </summary>
        public static void HandleKickAllCommand(string arguments)
        {
            if (ZNet.instance == null)
            {
                Plugin.Log.LogWarning("Cannot kick players: Server is not running.");
                return;
            }

            var connectedPeers = ZNet.instance.GetConnectedPeers();
            if (connectedPeers.Count == 0)
            {
                Plugin.Log.LogInfo("No players are currently connected.");
                return;
            }

            string[] parts = arguments.Trim().Split([' '], 2, StringSplitOptions.RemoveEmptyEntries);
            float delay = 3f;
            string kickReason = "Kicked by administrator";

            if (parts.Length > 0 && float.TryParse(parts[0], out float parsed))
            {
                delay = Math.Max(0f, parsed);
                kickReason = parts.Length > 1 ? parts[1] : kickReason;
            }
            else if (!string.IsNullOrWhiteSpace(arguments))
            {
                kickReason = arguments.Trim();
            }

            Plugin.Log.LogInfo($"Kicking {TerminalColor.Cyan}{connectedPeers.Count}{TerminalColor.Reset} connected player(s) in {delay:G}s. Reason: {TerminalColor.Yellow}{kickReason}{TerminalColor.Reset}");

            if (ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.Center, $"Server: {kickReason}");
            }

            Plugin.Log.LogInfo("Triggering world and player profile save prior to kicking all players...");
            ZNet.instance.Save(sync: false, saveOtherPlayerProfiles: true, waitForNextFrame: false);
            Plugin.Log.LogInfo($"{TerminalColor.Green}World and player profile save triggered successfully.{TerminalColor.Reset}");

            _kickDisconnectTime = Time.realtimeSinceStartup + delay;
        }

        private static void UpdateKickAll()
        {
            if (_kickDisconnectTime >= 0f && Time.realtimeSinceStartup >= _kickDisconnectTime)
            {
                _kickDisconnectTime = -1f;

                if (ZNet.instance == null)
                {
                    return;
                }

                foreach (ZNetPeer peer in new List<ZNetPeer>(ZNet.instance.GetConnectedPeers()))
                {
                    try
                    {
                        peer.m_rpc?.Invoke("Kicked");
                        ZNet.instance.Disconnect(peer);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogDebug($"Exception disconnecting peer {peer.m_playerName}: {ex.Message}");
                    }
                }

                Plugin.Log.LogInfo($"{TerminalColor.Green}All connected players successfully disconnected.{TerminalColor.Reset}");
            }
        }
    }
}
