using System;

namespace ConsoleStdinHeeler
{
    public static partial class CommandCustom
    {
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
        /// Syntax: announce <@all|*|player|"player name"> <message>
        /// </summary>
        public static void HandleAnnounceCommand(string arguments)
        {
            if (!TryParseMessageTarget(arguments, out string? targetPlayer, out string message))
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: announce <@all|*|player|\"player name\"> <message>");
                return;
            }

            if (ZRoutedRpc.instance == null)
            {
                Plugin.Log.LogWarning("Cannot send message: ZRoutedRpc is not ready.");
                return;
            }

            if (targetPlayer == null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "ShowMessage", (int)MessageHud.MessageType.Center, message);
                Plugin.Log.LogInfo($"Sent center announcement banner to all clients: {TerminalColor.Gold}\"{message}\"{TerminalColor.Reset}");
                return;
            }

            if (!CommandServer.TryResolvePeer(targetPlayer, out ZNetPeer targetPeer, "Target player"))
            {
                return;
            }

            ZRoutedRpc.instance.InvokeRoutedRPC(targetPeer.m_uid, "ShowMessage", (int)MessageHud.MessageType.Center, message);
            Plugin.Log.LogInfo($"Sent center announcement banner to '{TerminalColor.Cyan}{targetPeer.m_playerName}{TerminalColor.Reset}': {TerminalColor.Gold}\"{message}\"{TerminalColor.Reset}");
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
