using System;
using System.Collections.Generic;

namespace ConsoleStdinHeeler
{
    public static partial class CommandOverrides
    {
        /// <summary>
        /// Replaces vanilla 'event' command on dedicated servers to trigger a raid near a connected player or coordinates
        /// without throwing NullReferenceException on Player.m_localPlayer.
        /// Syntax: event <event_name> <player|"player name"|coords>
        /// </summary>
        public static void HandleEventCommand(string arguments)
        {
            if (!RandEventSystem.instance || !ZNet.instance)
            {
                Plugin.Log.LogWarning("Cannot trigger event: RandEventSystem is not ready or world is still loading.");
                return;
            }

            if (string.IsNullOrWhiteSpace(arguments))
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: event <event_name> <player|\"player name\"|coords>");
                Plugin.Log.LogInfo("Coordinates must strictly follow 0,0 or 0,0,0 mask with no spaces.");
                ListAvailableEvents();
                return;
            }

            List<string> parts = TerminalUtils.ParseArguments(arguments.Trim());
            if (parts.Count != 2)
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: event <event_name> <player|\"player name\"|coords>");
                Plugin.Log.LogInfo("Coordinates must strictly follow 0,0 or 0,0,0 mask with no spaces.");
                ListAvailableEvents();
                return;
            }

            string eventName = parts[0];

            RandomEvent? targetEvent = null;
            if (RandEventSystem.instance.m_events != null)
            {
                foreach (RandomEvent randomEvent in RandEventSystem.instance.m_events)
                {
                    if (randomEvent != null && randomEvent.m_enabled && string.Equals(randomEvent.m_name, eventName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetEvent = randomEvent;
                        break;
                    }
                }
            }

            if (targetEvent == null)
            {
                Plugin.Log.LogWarning($"Random event '{eventName}' not found or is disabled.");
                ListAvailableEvents();
                return;
            }

            string targetToken = parts[1];
            if (!CommandServer.TryResolveTarget(targetToken, out CommandTarget target))
            {
                return;
            }

            RandEventSystem.instance.SetRandomEvent(targetEvent, target.Position);
            Plugin.Log.LogInfo($"Triggered event '{TerminalColor.Yellow}{targetEvent.m_name}{TerminalColor.Reset}' at {target.Description}.");
        }

        /// <summary>
        /// Replaces vanilla 'stopevent' command on dedicated servers with clear console confirmation.
        /// </summary>
        public static void HandleStopEventCommand()
        {
            if (!RandEventSystem.instance)
            {
                Plugin.Log.LogWarning("Cannot stop event: RandEventSystem is not ready.");
                return;
            }

            RandomEvent? currentEvent = RandEventSystem.instance.GetCurrentRandomEvent();
            if (currentEvent == null)
            {
                Plugin.Log.LogInfo("No random event/raid is currently active.");
                return;
            }

            string eventName = currentEvent.m_name;
            RandEventSystem.instance.ResetRandomEvent();
            Plugin.Log.LogInfo($"Stopped active random event: '{TerminalColor.Yellow}{eventName}{TerminalColor.Reset}'.");
        }

        private static void ListAvailableEvents()
        {
            if (!RandEventSystem.instance || RandEventSystem.instance.m_events == null)
            {
                return;
            }

            List<string> available = new List<string>();
            foreach (RandomEvent randomEvent in RandEventSystem.instance.m_events)
            {
                if (randomEvent != null && randomEvent.m_enabled)
                {
                    available.Add(randomEvent.m_name);
                }
            }
            available.Sort(StringComparer.OrdinalIgnoreCase);
            Plugin.Log.LogInfo($"Available events: {TerminalColor.Yellow}{string.Join($"{TerminalColor.Reset}, {TerminalColor.Yellow}", available)}{TerminalColor.Reset}");
        }
    }
}
