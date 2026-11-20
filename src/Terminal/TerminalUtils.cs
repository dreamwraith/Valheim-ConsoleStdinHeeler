using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ConsoleStdinHeeler.Configuration;

namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Utility class for scrubbing Unity rich-text markup and translating color tags to ANSI escape sequences.
    /// </summary>
    public static class TerminalUtils
    {
        // known rich text tags only, so usage text like <name> survives
        private static readonly Regex TagRegex = new(
            @"<(/?(?:color|b|i|size|material|quad|sprite)(?:[=\s][^>]*)?)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Processes input text according to the specified ColorTagMode.
        /// </summary>
        public static string ProcessText(string text, ColorTagMode mode) =>
            string.IsNullOrEmpty(text) ? text : mode switch
            {
                ColorTagMode.Strip => StripRichTextTags(text),
                ColorTagMode.Ansi => ConvertRichTextToAnsi(text),
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
                    string colorSpecification = tag.Substring(6).Trim().Trim('"');
                    string? ansiCode = GetAnsiColorCode(colorSpecification);
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
            List<string> list = new List<string>();
            if (string.IsNullOrWhiteSpace(input))
            {
                return list;
            }

            bool inQuotes = false;
            StringBuilder current = new StringBuilder();

            for (int index = 0; index < input.Length; index++)
            {
                char character = input[index];
                if (character == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (character == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        list.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(character);
                }
            }

            if (current.Length > 0)
            {
                list.Add(current.ToString());
            }

            return list;
        }

        private static string? GetAnsiColorCode(string colorSpecification)
        {
            if (colorSpecification.StartsWith("#"))
            {
                string hexDigits = colorSpecification.Substring(1);
                if (hexDigits.Length == 3) // #RGB
                {
                    int red = Convert.ToInt32(new string(hexDigits[0], 2), 16);
                    int green = Convert.ToInt32(new string(hexDigits[1], 2), 16);
                    int blue = Convert.ToInt32(new string(hexDigits[2], 2), 16);
                    return TerminalColor.FromRgb(red, green, blue);
                }
                if (hexDigits.Length >= 6) // #RRGGBB or #RRGGBBAA
                {
                    int red = Convert.ToInt32(hexDigits.Substring(0, 2), 16);
                    int green = Convert.ToInt32(hexDigits.Substring(2, 2), 16);
                    int blue = Convert.ToInt32(hexDigits.Substring(4, 2), 16);
                    return TerminalColor.FromRgb(red, green, blue);
                }

                return null;
            }

            return TerminalColor.FromName(colorSpecification);
        }
    }
}
