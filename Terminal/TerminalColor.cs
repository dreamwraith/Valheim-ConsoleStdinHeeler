namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Terminal ANSI escape sequences for text coloring and formatting in console logs.
    /// Supports standard 16-color ANSI formatting, Unity RichText color aliases, and 24-bit TrueColor RGB.
    /// Automatically respects the user's Plugin.ColorFormatting configuration setting.
    /// </summary>
    public static class TerminalColor
    {
        private static bool Enabled => Plugin.ColorFormatting == null || Plugin.ColorFormatting.Value == Plugin.ColorTagMode.Ansi;

        // Control sequences
        public static string Reset => Enabled ? "\u001b[0m" : string.Empty;
        public static string Bold => Enabled ? "\u001b[1m" : string.Empty;
        public static string BoldReset => Enabled ? "\u001b[22m" : string.Empty;
        public static string Default => Enabled ? "\u001b[39m" : string.Empty;

        // Standard high-intensity / bright colors
        public static string Black => Enabled ? "\u001b[30m" : string.Empty;
        public static string Gray => Enabled ? "\u001b[90m" : string.Empty; // Dark gray
        public static string DarkGray => Gray;
        public static string Red => Enabled ? "\u001b[91m" : string.Empty;
        public static string Green => Enabled ? "\u001b[92m" : string.Empty;
        public static string Yellow => Enabled ? "\u001b[93m" : string.Empty;
        public static string Blue => Enabled ? "\u001b[94m" : string.Empty;
        public static string Magenta => Enabled ? "\u001b[95m" : string.Empty;
        public static string Purple => Magenta;
        public static string Fuchsia => Magenta;
        public static string Cyan => Enabled ? "\u001b[96m" : string.Empty;
        public static string Aqua => Cyan;
        public static string White => Enabled ? "\u001b[97m" : string.Empty;

        // Normal-intensity standard colors
        public static string DarkRed => Enabled ? "\u001b[31m" : string.Empty;
        public static string Maroon => DarkRed;
        public static string DarkGreen => Enabled ? "\u001b[32m" : string.Empty;
        public static string DarkYellow => Enabled ? "\u001b[33m" : string.Empty;
        public static string DarkBlue => Enabled ? "\u001b[34m" : string.Empty;
        public static string Navy => DarkBlue;
        public static string DarkMagenta => Enabled ? "\u001b[35m" : string.Empty;
        public static string DarkCyan => Enabled ? "\u001b[36m" : string.Empty;
        public static string Teal => DarkCyan;
        public static string LightGray => Enabled ? "\u001b[37m" : string.Empty;
        public static string Silver => LightGray;

        // 24-bit TrueColor presets and extended Unity palette colors
        public static string Orange => Enabled ? "\u001b[38;2;255;165;0m" : string.Empty;
        public static string Gold => Enabled ? "\u001b[38;2;234;168;0m" : string.Empty;
        public static string Amber => Gold;
        public static string Lime => Enabled ? "\u001b[38;2;0;255;0m" : string.Empty;
        public static string Olive => Enabled ? "\u001b[38;2;128;128;0m" : string.Empty;
        public static string Brown => Enabled ? "\u001b[38;2;165;42;42m" : string.Empty;
        public static string LightBlue => Enabled ? "\u001b[38;2;173;216;230m" : string.Empty;

        /// <summary>
        /// Generates a 24-bit RGB ANSI true-color escape sequence.
        /// </summary>
        public static string FromRgb(int r, int g, int b) =>
            Enabled ? $"\u001b[38;2;{r};{g};{b}m" : string.Empty;

        /// <summary>
        /// Resolves an ANSI escape sequence from a hex color string (e.g. "#FFA500" or "FFA500").
        /// </summary>
        public static string? FromHex(string hex)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(hex))
            {
                return string.Empty;
            }

            hex = hex.TrimStart('#');
            if (hex.Length == 6 &&
                byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                return FromRgb(r, g, b);
            }

            return null;
        }

        /// <summary>
        /// Resolves an ANSI escape sequence for a known named color, Unity RichText color, or hex code.
        /// </summary>
        public static string? FromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string lower = name.Trim().ToLowerInvariant();
            if (lower.StartsWith("#") || (lower.Length == 6 && IsValidHex(lower)))
            {
                string? hexColor = FromHex(lower);
                if (hexColor != null)
                {
                    return hexColor;
                }
            }

            return lower switch
            {
                "red" => Red,
                "darkred" or "maroon" => DarkRed,
                "green" => Green,
                "darkgreen" => DarkGreen,
                "lime" => Lime,
                "yellow" => Yellow,
                "darkyellow" => DarkYellow,
                "orange" => Orange,
                "gold" or "amber" => Gold,
                "blue" => Blue,
                "darkblue" or "navy" => DarkBlue,
                "lightblue" => LightBlue,
                "magenta" or "fuchsia" => Magenta,
                "purple" => Purple,
                "darkmagenta" => DarkMagenta,
                "cyan" or "aqua" => Cyan,
                "darkcyan" or "teal" => DarkCyan,
                "white" => White,
                "grey" or "gray" or "darkgray" or "darkgrey" => Gray,
                "lightgray" or "lightgrey" or "silver" => LightGray,
                "black" => Black,
                "olive" => Olive,
                "brown" => Brown,
                "default" => Default,
                _ => null,
            };
        }

        private static bool IsValidHex(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
