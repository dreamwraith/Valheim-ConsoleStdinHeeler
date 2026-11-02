using System;
using BepInEx.Configuration;

namespace ConsoleStdinHeeler.Configuration
{
    public enum ColorTagMode
    {
        Ansi,
        Strip,
        Raw
    }

    internal static class ModConfig
    {
        public static ConfigEntry<bool> EnableMod = null!;
        public static ConfigEntry<bool> EnableCheatsByDefault = null!;
        public static ConfigEntry<ColorTagMode> ColorFormatting = null!;
        public static ConfigEntry<string> ConsolePrompt = null!;
        public static ConfigEntry<bool> EnableDebugLogs = null!;

        public static void Initialize(ConfigFile config)
        {
            EnableMod = config.Bind(
                "1 - General",
                "EnableMod",
                true,
                new ConfigDescription("Master switch to enable or disable the STDIN console listener.")
            );

            EnableCheatsByDefault = config.Bind(
                "1 - General",
                "EnableCheatsByDefault",
                true,
                new ConfigDescription("Allows modded and admin cheat commands to execute on the dedicated server.")
            );

            ConsolePrompt = config.Bind(
                "2 - Display",
                "ConsolePrompt",
                "> ",
                new ConfigDescription("Prefix displayed in logs when an STDIN command is received.")
            );

            ColorFormatting = config.Bind(
                "2 - Display",
                "ColorFormatting",
                ColorTagMode.Ansi,
                new ConfigDescription("How Unity rich-text color tags from console outputs are handled: Ansi (render in terminal colors), Strip (clean plain text), or Raw (keep tags unchanged).")
            );

            EnableDebugLogs = config.Bind(
                "9 - Debug",
                "EnableDebugLogs",
                false,
                new ConfigDescription(
                    "Enables verbose diagnostic logging.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true })
            );
        }
    }

    /// <summary>
    /// ConfigurationManager integration attribute definition.
    /// </summary>
    internal class ConfigurationManagerAttributes
    {
        public bool? IsAdvanced { get; set; }
        public int? Order { get; set; }
        public bool? Browsable { get; set; }
        public Action<ConfigEntryBase>? CustomDrawer { get; set; }
    }
}
