using System;
using UnityEngine;

namespace ConsoleStdinHeeler
{
    public static partial class CommandCustom
    {
        private static bool _isShutdownScheduled;
        private static float _shutdownTargetTime;
        private static string _shutdownReason = string.Empty;
        private static int _lastAnnouncedSecondsLeft = -1;
        private static readonly int[] ShutdownMilestones = [600, 300, 180, 120, 60, 30, 10, 5];

        /// <summary>
        /// Triggers world and player profile saves on dedicated servers.
        /// </summary>
        public static void HandleSaveCommand()
        {
            if (ZNet.instance == null || Game.instance == null || ZoneSystem.instance == null)
            {
                Plugin.Log.LogWarning("Cannot save: Server world is still loading. Please wait until startup completes.");
                return;
            }

            Plugin.Log.LogInfo("Triggering world and player profile save...");
            ZNet.instance.Save(sync: false, saveOtherPlayerProfiles: true, waitForNextFrame: false);
            Plugin.Log.LogInfo($"{TerminalColor.Green}World and player profile save triggered successfully.{TerminalColor.Reset}");
        }

        /// <summary>
        /// Gracefully saves world and player state and initiates server shutdown.
        /// Supports immediate shutdown (no arguments), countdown timer (e.g. 'stop 5 Server update'),
        /// or cancellation ('stop cancel').
        /// </summary>
        public static void HandleShutdownCommand(string arguments)
        {
            string trimmed = arguments.Trim();

            // Cancel active countdown
            if (trimmed.Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("abort", StringComparison.OrdinalIgnoreCase))
            {
                if (!_isShutdownScheduled)
                {
                    Plugin.Log.LogInfo("No shutdown countdown is currently active.");
                    return;
                }

                _isShutdownScheduled = false;
                _lastAnnouncedSecondsLeft = -1;
                if (ZRoutedRpc.instance != null)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.Center, "Server shutdown countdown canceled.");
                }
                Plugin.Log.LogInfo($"{TerminalColor.Green}Server shutdown countdown successfully canceled.{TerminalColor.Reset}");
                return;
            }

            // Immediate shutdown if no parameters provided
            if (string.IsNullOrEmpty(trimmed))
            {
                ExecuteImmediateShutdown();
                return;
            }

            // Parse [minutes] [reason]
            string[] parts = trimmed.Split([' '], 2, StringSplitOptions.RemoveEmptyEntries);
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float minutes) || minutes <= 0f)
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: stop [minutes] [reason] OR stop cancel");
                return;
            }

            string reason = parts.Length > 1 ? parts[1].Trim() : "Server maintenance";
            _shutdownReason = reason;
            _shutdownTargetTime = Time.realtimeSinceStartup + (minutes * 60f);
            _isShutdownScheduled = true;
            _lastAnnouncedSecondsLeft = (int)(minutes * 60f);

            string initialMsg = $"Server shutting down in {minutes:F0} min! Reason: {reason}";
            if (ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.Center, initialMsg);
            }
            Plugin.Log.LogInfo($"{TerminalColor.Orange}{initialMsg}{TerminalColor.Reset}");
        }

        /// <summary>
        /// Displays currently connected players, network endpoints, round-trip latency, and character IDs.
        /// </summary>
        public static void HandlePlayersCommand()
        {
            if (ZNet.instance == null || Game.instance == null)
            {
                Plugin.Log.LogWarning("Cannot list players: Server world is still loading.");
                return;
            }

            var connectedPeers = ZNet.instance.GetConnectedPeers();
            Plugin.Log.LogInfo($"{TerminalColor.Bold}{TerminalColor.Orange}=== Connected Players ({connectedPeers.Count}) ==={TerminalColor.Reset}");

            int playerNumber = 1;
            foreach (ZNetPeer peer in connectedPeers)
            {
                string networkEndpoint = peer.m_socket?.GetHostName() ?? "unknown";
                string playerName = string.IsNullOrEmpty(peer.m_playerName) ? "<connecting>" : peer.m_playerName;
                float pingSeconds = peer.m_rpc != null ? peer.m_rpc.GetTimeSinceLastPing() : -1f;
                string pingColor = pingSeconds < 0 ? TerminalColor.Gray : (pingSeconds < 0.1f ? TerminalColor.Green : (pingSeconds < 0.2f ? TerminalColor.Yellow : TerminalColor.Red));
                string pingDisplay = pingSeconds >= 0 ? $"{pingSeconds * 1000f:F0}ms" : "n/a";

                Plugin.Log.LogInfo($"  [{playerNumber}] {TerminalColor.Cyan}{playerName}{TerminalColor.Reset} | Ping: {pingColor}{pingDisplay}{TerminalColor.Reset} | Endpoint: {TerminalColor.Gray}{networkEndpoint}{TerminalColor.Reset} | ID: {TerminalColor.Gray}{peer.m_characterID}{TerminalColor.Reset}");
                playerNumber++;
            }
        }

        /// <summary>
        /// Called per-frame to drive active background countdown timers (shutdown, kickall).
        /// </summary>
        internal static void Update()
        {
            UpdateShutdown();
            UpdateKickAll();
        }

        private static void UpdateShutdown()
        {
            if (!_isShutdownScheduled)
            {
                return;
            }

            float secondsRemaining = _shutdownTargetTime - Time.realtimeSinceStartup;

            if (secondsRemaining <= 0f)
            {
                _isShutdownScheduled = false;
                if (ZRoutedRpc.instance != null)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.Center, "Server shutting down NOW!");
                }

                ExecuteImmediateShutdown();
                return;
            }

            // Milestone announcements: 10m, 5m, 3m, 2m, 1m, 30s, 10s, 5s
            int currentSeconds = (int)secondsRemaining;

            foreach (int milestone in ShutdownMilestones)
            {
                if (_lastAnnouncedSecondsLeft > milestone && currentSeconds <= milestone)
                {
                    _lastAnnouncedSecondsLeft = milestone;
                    string timeText = milestone >= 60 ? $"{milestone / 60} minute(s)" : $"{milestone} seconds";
                    string alert = $"Server shutting down in {timeText}! ({_shutdownReason})";

                    if (ZRoutedRpc.instance != null)
                    {
                        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.Center, alert);
                    }
                    Plugin.Log.LogInfo($"{TerminalColor.Orange}{alert}{TerminalColor.Reset}");
                    break;
                }
            }
        }

        private static void ExecuteImmediateShutdown()
        {
            Plugin.Log.LogInfo("Initiating graceful server shutdown via Application.Quit()...");
            Plugin.Instance?.StopListener();

            // Trigger Valheim's native shutdown pipeline (identical to SIGINT / graceful stop / server_exit.drp).
            // Game.OnApplicationQuit() synchronously executes SaveWorld(true), disconnects peers, and flushes OS buffers.
            Application.Quit();
        }
    }
}
