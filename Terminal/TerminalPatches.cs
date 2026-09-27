using System;
using HarmonyLib;

namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Harmony patches for Terminal output processing and ANSI color formatting.
    /// </summary>
    [HarmonyPatch(typeof(Terminal))]
    public static class TerminalPatches
    {
        /// <summary>
        /// Intercepts Terminal.AddString(string) to convert Unity rich-text markup to ANSI terminal colors.
        /// </summary>
        [HarmonyPatch(nameof(Terminal.AddString), new Type[] { typeof(string) })]
        [HarmonyPrefix]
        private static void AddStringPrefix(ref string text)
        {
            if (string.IsNullOrEmpty(text) || Plugin.ColorFormatting == null)
            {
                return;
            }

            text = TerminalUtils.ProcessText(text, Plugin.ColorFormatting.Value);
        }
    }
}
