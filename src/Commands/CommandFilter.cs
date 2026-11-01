using System;
using System.Collections.Generic;

namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Filters and suppresses commands that are incompatible with headless dedicated servers
    /// (e.g. requiring a local player character, client rendering, GUI windows, or being silent no-ops).
    /// </summary>
    public static class CommandFilter
    {
        /// <summary>
        /// Commands that require a local player character, client rendering, GUI menus, or are silent no-ops on dedicated servers.
        /// </summary>
        private static readonly HashSet<string> ClientAndPlayerCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            // Emotes (all invoke Player.m_localPlayer.StartEmote())
            "blowkiss", "bow", "challenge", "cheer", "comehere", "cower", "cry", "dance",
            "despair", "flex", "headbang", "kneel", "laugh", "loveyou", "nonono", "point",
            "relax", "rest", "roar", "shrug", "sit", "thumbsup", "toast", "vibe", "wave",

            // Player character states & actions
            "god", "ghost", "fly", "nocost", "die", "puke", "heal", "pos", "goto",
            "beard", "hair", "model", "raiseskill", "resetskill", "freefly",
            "ffsmooth", "tame", "killall", "killenemies", "killenemycreatures", "killtame",
            "exploremap", "resetmap", "resetsharedmap", "tombstone",
            "itemset", "catch", "resetcharacter", "resetknownitems", "tutorialreset",
            "clearstatus", "addstatus", "setpower", "repairall", "sortcraft", "filtercraft",
            "inventoryclean", "inventorysize", "nomap", "resetspawn", "s",
            "w", "whisper", "msg", "tell",
            "debugmode", "dpsdebug", "adrenaline", "aggravate", "achievements",
            "getstat", "stats", "setkeyplayer", "removekeyplayer",
            "yesiuseddevcommandsbutiwantmyachievementsanyway",

            // Player-position dependent commands (require Player.m_localPlayer.transform.position)
            "biomeinfo", "haslocation", "printlocations", "printnetobj", "printcreatures", "recall",
            "vegetation", "height", "forcedelete", "snow", "location",
            "find", "findbiome", "findbiometp", "findtp",

            // Silent no-ops, late-crashing, or dangerous commands on dedicated server
            "stopfire", "stopsmoke", "setfuel", "printseeds", "noportals",
            "removedrops", "removebirds", "removefish", "nextseed",

            // Client GUI menu dependent (ServerOptionsGUI.m_instance is null on headless)
            "setworldmodifier", "setworldpreset",

            // Client UI / window / keybind / rendering commands
            "clear", "clearpopups", "fov", "lodbias", "maxfps", "cr",
            "unlockcinematics", "cinematic", "cinematicsleep", "xb:version",
            "resetbuildui", "resetplayerprefs", "bind", "unbind", "printbinds", "resetbinds",
            "exclusivefullscreen", "hidebetatext", "devcommands", "confirmcheats", "test",

            // Client-only environment/rendering overrides (not networked to peers in vanilla)
            "env", "wind", "resetenv", "resetwind"
        };

        /// <summary>
        /// Commands already handled and formatted explicitly in the dedicated server help listing.
        /// </summary>
        private static readonly HashSet<string> BuiltInServerCommands = InitializeBuiltInCommands();

        /// <summary>
        /// Returns true if the command is known to be incompatible with or invalid on a headless dedicated server.
        /// </summary>
        public static bool IsServerIncompatible(string commandName, Terminal.ConsoleCommand? commandInstance = null) =>
            ClientAndPlayerCommands.Contains(commandName) ||
            commandInstance?.Description?.StartsWith("emote:", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Returns true if the command is a built-in server command handled directly by the plugin.
        /// </summary>
        public static bool IsBuiltInServerCommand(string commandName) =>
            BuiltInServerCommands.Contains(commandName);

        private static HashSet<string> InitializeBuiltInCommands()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "help", "broadcast", "alert", "teleport" };
            foreach (var (syntax, _) in CommandCustom.DedicatedCommands)
            {
                string commandPart = syntax.Split('[', '<')[0];
                foreach (string token in commandPart.Split('/'))
                {
                    string cleaned = token.Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                    {
                        set.Add(cleaned);
                    }
                }
            }

            return set;
        }
    }
}
