using System;
using System.Collections;
using UnityEngine;

namespace ConsoleStdinHeeler
{
    public static partial class CommandCustom
    {
        private static Coroutine? _activeAnnouncementCoroutine;
        private const float MaxAnnouncementDuration = 60f;
        private const float AnnouncementPulseInterval = 1f;

        /// <summary>
        /// Displays a top-left notification banner to all connected players or a specific player via ZRoutedRpc.
        /// Syntax: say <@all|*|player|"player name"> <message>
        /// </summary>
        public static void HandleSayCommand(string arguments)
        {
            if (!TryParseMessageTarget(arguments, out string? targetPlayer, out string message))
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: say <@all|*|player|\"player name\"> <message>");
                return;
            }

            if (ZRoutedRpc.instance == null)
            {
                Plugin.Log.LogWarning("Cannot send message: ZRoutedRpc is not ready.");
                return;
            }

            if (targetPlayer == null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.TopLeft, message);
                Plugin.Log.LogInfo($"Sent top-left notification to all clients: {TerminalColor.Cyan}\"{message}\"{TerminalColor.Reset}");
                return;
            }

            if (!CommandServer.TryResolvePeer(targetPlayer, out ZNetPeer targetPeer, "Target player"))
            {
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(targetPeer.m_uid, "ShowMessage", (int)MessageHud.MessageType.TopLeft, message);
            Plugin.Log.LogInfo($"Sent top-left notification to '{TerminalColor.Cyan}{targetPeer.m_playerName}{TerminalColor.Reset}': {TerminalColor.Cyan}\"{message}\"{TerminalColor.Reset}");
        }

        /// <summary>
        /// Displays a prominent center-screen announcement to all connected players or a specific player via ZRoutedRpc.
        /// Supports optional persistence duration via '-t <seconds>' (capped at 60s), pulsed every 1.0s via a non-blocking coroutine.
        /// Syntax: announce [-t <seconds>] <@all|*|player|"player name"> <message> OR announce cancel
        /// </summary>
        public static void HandleAnnounceCommand(string arguments)
        {
            string trimmed = arguments.Trim();

            if (trimmed.Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("abort", StringComparison.OrdinalIgnoreCase))
            {
                CancelActiveAnnouncement();
                return;
            }

            float duration = 0f;
            if (trimmed.StartsWith("-t ", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("-t", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = trimmed.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out duration) || duration <= 0f)
                {
                    Plugin.Log.LogWarning("Invalid parameters. The -t flag requires a duration in seconds between 1 and 60 (e.g. -t 15 <target> <message>).");
                    return;
                }

                duration = Math.Min(duration, MaxAnnouncementDuration);
                trimmed = parts[2];
            }

            if (!TryParseMessageTarget(trimmed, out string? targetPlayer, out string message))
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: announce [-t seconds] <@all|*|player|\"player name\"> <message> OR announce cancel");
                return;
            }

            if (ZRoutedRpc.instance == null)
            {
                Plugin.Log.LogWarning("Cannot send message: ZRoutedRpc is not ready.");
                return;
            }

            long targetUid = ZRoutedRpc.Everybody;
            string targetDescription = "all clients";

            if (targetPlayer != null)
            {
                if (!CommandServer.TryResolvePeer(targetPlayer, out ZNetPeer targetPeer, "Target player"))
                {
                    return;
                }

                targetUid = targetPeer.m_uid;
                targetDescription = $"'{TerminalColor.Cyan}{targetPeer.m_playerName}{TerminalColor.Reset}'";
            }

            StopActiveAnnouncement();

            ZRoutedRpc.instance.InvokeRoutedRPC(targetUid, "ShowMessage", (int)MessageHud.MessageType.Center, message);

            string durationText = duration > 1f ? $" ({duration:F0}s duration)" : string.Empty;
            Plugin.Log.LogInfo($"Sent center announcement banner to {targetDescription}{durationText}: {TerminalColor.Gold}\"{message}\"{TerminalColor.Reset}");

            if (duration > 1f && Plugin.Instance != null)
            {
                _activeAnnouncementCoroutine = Plugin.Instance.StartCoroutine(PulseAnnouncementRoutine(targetUid, message, duration));
            }
        }

        private static void StopActiveAnnouncement()
        {
            if (_activeAnnouncementCoroutine != null)
            {
                Plugin.Instance?.StopCoroutine(_activeAnnouncementCoroutine);
                _activeAnnouncementCoroutine = null;
            }
        }

        private static void CancelActiveAnnouncement()
        {
            if (_activeAnnouncementCoroutine != null)
            {
                StopActiveAnnouncement();
                Plugin.Log.LogInfo($"{TerminalColor.Green}Active persistent announcement canceled.{TerminalColor.Reset}");
            }
            else
            {
                Plugin.Log.LogInfo("No persistent announcement is currently active.");
            }
        }

        private static IEnumerator PulseAnnouncementRoutine(long targetUid, string message, float duration)
        {
            float elapsed = 0f;
            var waitPulse = new WaitForSeconds(AnnouncementPulseInterval);

            while (elapsed < duration)
            {
                yield return waitPulse;
                elapsed += AnnouncementPulseInterval;

                if (ZRoutedRpc.instance == null || (targetUid != ZRoutedRpc.Everybody && ZNet.instance?.GetPeer(targetUid) == null))
                {
                    yield break;
                }

                ZRoutedRpc.instance.InvokeRoutedRPC(targetUid, "ShowMessage", (int)MessageHud.MessageType.Center, message);
            }

            _activeAnnouncementCoroutine = null;
        }

        /// <summary>
        /// Parses a messaging argument string into a target (first token: @all, *, player name, or "quoted name")
        /// and the trailing message text.
        /// If the target is '@all' or '*', sets targetPlayer to null (global broadcast).
        /// </summary>
        private static bool TryParseMessageTarget(string arguments, out string? targetPlayer, out string message)
        {
            targetPlayer = null;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(arguments))
            {
                return false;
            }

            string trimmed = arguments.Trim();
            string rawTarget;

            if (trimmed.StartsWith("\""))
            {
                int closingQuote = trimmed.IndexOf('"', 1);
                if (closingQuote < 0)
                {
                    return false;
                }

                rawTarget = trimmed.Substring(1, closingQuote - 1).Trim();
                message = trimmed.Substring(closingQuote + 1).Trim();
            }
            else
            {
                int firstSpace = trimmed.IndexOf(' ');
                if (firstSpace < 0)
                {
                    return false;
                }

                rawTarget = trimmed.Substring(0, firstSpace).Trim();
                message = trimmed.Substring(firstSpace + 1).Trim();
            }

            if (string.IsNullOrWhiteSpace(rawTarget) || string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            if (string.Equals(rawTarget, "@all", StringComparison.OrdinalIgnoreCase) || rawTarget == "*")
            {
                targetPlayer = null;
            }
            else
            {
                targetPlayer = rawTarget;
            }

            return true;
        }
    }
}
