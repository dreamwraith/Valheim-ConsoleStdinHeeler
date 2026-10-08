using System;
using System.Collections.Generic;
using System.Reflection;

namespace ConsoleStdinHeeler
{
    public static partial class CommandCustom
    {
        private static readonly FieldInfo? TerminalCommandsField = typeof(Terminal).GetField(
            "commands",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
        );

        private static bool _hasLoggedReflectionWarning;

        internal static readonly (string Syntax, string Description)[] DedicatedCommands =
        [
            ("save", "Force save world and player profile data"),
            ("stop / shutdown [min] [msg]", "Stop server (optional countdown, e.g. 'stop 5 Reboot')"),
            ("stop cancel", "Cancel an active shutdown countdown"),
            ("players / list", "List all connected players, endpoints, ping, and IDs"),
            ("kick <name/ip/userID>", "Kick a single player from the server"),
            ("kickall [seconds] [reason]", "Save world and kick all connected players (default: 3s grace delay)"),
            ("ban <name/ip/userID>", "Ban a player by name, IP, or user ID"),
            ("unban <name/ip/userID>", "Unban a player"),
            ("banned", "List all currently banned players"),
            ("say <@all/*|player> <msg>", "Display top-left notification to all clients or a specific player"),
            ("announce [-t sec] <@all/*|player> <msg>", "Display prominent center banner (optional -t duration up to 60s)"),
            ("announce cancel", "Cancel any active persistent announcement"),
            ("tp <player> <target/x,y,z>", "Teleport a player to another player or coordinates"),
            ("time", "Display current world time, day count, and sleep status"),
            ("tod <0-1>", "Set time of day for all connected clients (e.g. tod 0.5)"),
            ("skiptime [gameseconds]", "Skip ahead in seconds (default: 240)"),
            ("sleep", "Fast-forward time to next morning"),
            ("timescale [scale]", "Adjust game simulation speed (default: 1.0)"),
            ("event <name> <target/x,y,z>", "Start a raid/event near a player or coordinates (e.g. event army_eikthyr Thor)"),
            ("stopevent", "Stop any currently active raid/event"),
            ("randomevent", "Start a random raid/event near a player"),
            ("pevents [list/start/stop/here]", "Manage player events and raids (subcommands: list, start, stop, here)"),
            ("spawn <prefab> <target/x,y,z> [amt] [lvl] [-s]", "Spawn near a player or coordinates (e.g. spawn Boar Thor 3 2 -s)"),
            ("listkeys", "List active world progress keys, modifiers, and presets"),
            ("setkey <name>", "Set a global world progress key (e.g. boss keys)"),
            ("removekey <name>", "Remove an active global world progress key"),
            ("resetkeys", "Reset all global world progress keys"),
            ("resetworldkeys", "Reset all world modifiers to default settings"),
            ("optterrain", "Optimize older terrain modifications across loaded sectors"),
            ("genloc", "Regenerate missing world locations"),
            ("updatecover", "Force refresh of shelter and roof cover calculations"),
            ("nospawn", "Toggle natural spawning of monsters on/off"),
            ("respawntime <seconds>", "Set player respawn delay timer in seconds"),
            ("restartparty", "Restart PlayFab Party network connection"),
            ("altbioms", "List biomes with active modifiers"),
            ("gc", "Run garbage collection and report freed memory"),
            ("ping", "Show server ping"),
            ("info", "Print server memory & system specs"),
        ];

        /// <summary>
        /// Displays curated dedicated server command catalog and automatically discovers installed modded commands.
        /// </summary>
        public static void HandleHelpCommand(string helpArguments)
        {
            // If the administrator explicitly asks for full dump: 'help all'
            if (helpArguments.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                helpArguments.Equals("-all", StringComparison.OrdinalIgnoreCase) ||
                helpArguments.Equals("--all", StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Log.LogInfo($"{TerminalColor.Bold}{TerminalColor.Orange}=== Complete Native Command Catalog (Unfiltered) ==={TerminalColor.Reset}");
                CommandServer.ExecuteTerminalCommand("help");
                return;
            }

            Plugin.Log.LogInfo($"{TerminalColor.Bold}{TerminalColor.Orange}=== Dedicated Server Commands ==={TerminalColor.Reset}");
            foreach (var (syntax, desc) in DedicatedCommands)
            {
                Plugin.Log.LogInfo($"  {FormatCommandSyntax(syntax)}{TerminalColor.Gray}-{TerminalColor.Reset} {desc}");
            }

            // Dynamic discovery of installed modded commands and non-client native commands
            var registeredCommands = GetRegisteredTerminalCommands();
            if (registeredCommands != null && registeredCommands.Count > 0)
            {
                var serverRelevantCommands = new List<KeyValuePair<string, Terminal.ConsoleCommand>>();

                foreach (var entry in registeredCommands)
                {
                    string registeredCommandName = entry.Key;
                    Terminal.ConsoleCommand commandInstance = entry.Value;

                    // Skip commands already documented above or that are client-only / secret
                    if (CommandFilter.IsBuiltInServerCommand(registeredCommandName) ||
                        CommandFilter.IsServerIncompatible(registeredCommandName, commandInstance) ||
                        commandInstance.IsSecret)
                    {
                        continue;
                    }

                    serverRelevantCommands.Add(entry);
                }

                if (serverRelevantCommands.Count > 0)
                {
                    Plugin.Log.LogInfo($"{TerminalColor.Bold}{TerminalColor.Orange}=== Modded & Additional Available Commands ==={TerminalColor.Reset}");
                    serverRelevantCommands.Sort((first, second) => string.Compare(first.Key, second.Key, StringComparison.OrdinalIgnoreCase));

                    foreach (var entry in serverRelevantCommands)
                    {
                        string description = string.IsNullOrWhiteSpace(entry.Value.Description) ? "No description provided." : entry.Value.Description;
                        Plugin.Log.LogInfo($"  {FormatCommandSyntax(entry.Key)}{TerminalColor.Gray}-{TerminalColor.Reset} {description}");
                    }
                }
            }

            Plugin.Log.LogInfo($"{TerminalColor.Gray}Tip: Type 'help all' to display the complete, unfiltered native command catalog.{TerminalColor.Reset}");
        }

        private static Dictionary<string, Terminal.ConsoleCommand>? GetRegisteredTerminalCommands()
        {
            if (TerminalCommandsField != null)
            {
                try
                {
                    if (TerminalCommandsField.GetValue(null) is Dictionary<string, Terminal.ConsoleCommand> registeredCommands)
                    {
                        return registeredCommands;
                    }
                }
                catch (Exception exception)
                {
                    Plugin.Log.LogDebug($"Could not read Terminal.commands dictionary: {exception.Message}");
                }
            }

            if (!_hasLoggedReflectionWarning)
            {
                _hasLoggedReflectionWarning = true;
                Plugin.Log.LogWarning("Could not reflect Terminal.commands dictionary. Mod command auto-discovery in 'help' will be disabled.");
            }

            return null;
        }

        private static string FormatCommandSyntax(string syntax, int padWidth = 28)
        {
            int spaceIndex = syntax.IndexOf(' ');
            if (spaceIndex < 0)
            {
                string padded = syntax.Length < padWidth ? syntax.PadRight(padWidth) : syntax + " ";
                return $"{TerminalColor.Yellow}{padded}{TerminalColor.Reset}";
            }

            string command = syntax.Substring(0, spaceIndex);
            string args = syntax.Substring(spaceIndex);
            string paddedArgs = syntax.Length < padWidth
                ? args.PadRight(padWidth - command.Length)
                : args + " ";
            return $"{TerminalColor.Yellow}{command}{TerminalColor.Cyan}{paddedArgs}{TerminalColor.Reset}";
        }
    }
}
