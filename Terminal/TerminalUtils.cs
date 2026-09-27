using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Utility class for scrubbing Unity rich-text markup and translating color tags to ANSI escape sequences.
    /// </summary>
    public static class TerminalUtils
    {
        private static readonly Regex TagRegex = new(@"<([^>]+)>", RegexOptions.Compiled);

        /// <summary>
        /// Processes input text according to the specified ColorTagMode.
        /// </summary>
        public static string ProcessText(string text, Plugin.ColorTagMode mode) =>
            string.IsNullOrEmpty(text) ? text : mode switch
            {
                Plugin.ColorTagMode.Strip => StripRichTextTags(text),
                Plugin.ColorTagMode.Ansi => ConvertRichTextToAnsi(text),
                _ => text,
            };

        /// <summary>
        /// Strips all XML/Unity rich-text markup tags from the input string.
        /// </summary>
        public static string StripRichTextTags(string input) =>
            string.IsNullOrEmpty(input) || input.IndexOf('<') < 0 ? input : TagRegex.Replace(input, string.Empty);

        /// <summary>
        /// Converts Unity rich-text markup (&lt;color=...&gt;, &lt;b&gt;, &lt;/b&gt;, &lt;/color&gt;) into standard ANSI terminal escape sequences.
        /// </summary>
        public static string ConvertRichTextToAnsi(string input)
        {
            if (string.IsNullOrEmpty(input) || input.IndexOf('<') < 0)
            {
                return input;
            }

            bool appliedFormatting = false;

            string processed = TagRegex.Replace(input, match =>
            {
                string tag = match.Groups[1].Value.Trim();

                if (tag.StartsWith("color=", StringComparison.OrdinalIgnoreCase))
                {
                    string colorSpec = tag.Substring(6).Trim().Trim('"');
                    string? ansiCode = GetAnsiColorCode(colorSpec);
                    if (ansiCode != null)
                    {
                        appliedFormatting = true;
                        return ansiCode;
                    }

                    return string.Empty;
                }

                if (tag.Equals("/color", StringComparison.OrdinalIgnoreCase))
                {
                    return TerminalColor.Reset;
                }

                if (tag.Equals("b", StringComparison.OrdinalIgnoreCase))
                {
                    appliedFormatting = true;
                    return TerminalColor.Bold;
                }

                if (tag.Equals("/b", StringComparison.OrdinalIgnoreCase))
                {
                    return TerminalColor.BoldReset;
                }

                return string.Empty;
            });

            if (appliedFormatting && !string.IsNullOrEmpty(TerminalColor.Reset) && !processed.EndsWith(TerminalColor.Reset))
            {
                processed += TerminalColor.Reset;
            }

            return processed;
        }

        /// <summary>
        /// Parses a command argument string into tokens, preserving quoted arguments containing spaces.
        /// </summary>
        public static List<string> ParseArguments(string input)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(input))
            {
                return list;
            }

            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        list.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                list.Add(current.ToString());
            }

            return list;
        }

        private static string? GetAnsiColorCode(string colorSpec)
        {
            if (colorSpec.StartsWith("#"))
            {
                string hex = colorSpec.Substring(1);
                if (hex.Length == 3) // #RGB
                {
                    int r = Convert.ToInt32(new string(hex[0], 2), 16);
                    int g = Convert.ToInt32(new string(hex[1], 2), 16);
                    int b = Convert.ToInt32(new string(hex[2], 2), 16);
                    return TerminalColor.FromRgb(r, g, b);
                }
                if (hex.Length >= 6) // #RRGGBB or #RRGGBBAA
                {
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    return TerminalColor.FromRgb(r, g, b);
                }

                return null;
            }

            return TerminalColor.FromName(colorSpec);
        }
    }
}
