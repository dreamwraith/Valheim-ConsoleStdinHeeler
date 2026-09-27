using System;
using BepInEx.Logging;

namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Transparent wrapper around BepInEx's ManualLogSource providing ANSI color formatting,
    /// console highlighter keywords, and configuration-driven debug filtering.
    /// Callers interact with this seamlessly as Plugin.Log.
    /// </summary>
    public class PluginLogger
    {
        private readonly ManualLogSource _underlying;

        public PluginLogger(ManualLogSource underlying)
        {
            _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
        }

        public void LogInfo(object data) => _underlying.LogInfo(data);

        public void LogWarning(object data)
        {
            string message = data?.ToString() ?? string.Empty;
            string formatted = message.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase) ||
                               message.StartsWith("[Warning]", StringComparison.OrdinalIgnoreCase)
                ? message
                : $"Warning: {message}";
            _underlying.LogWarning($"{TerminalColor.Yellow}{formatted}{TerminalColor.Reset}");
        }

        public void LogError(object data)
        {
            string message = data?.ToString() ?? string.Empty;
            string formatted = message.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                               message.StartsWith("[Error]", StringComparison.OrdinalIgnoreCase) ||
                               message.StartsWith("Failed:", StringComparison.OrdinalIgnoreCase) ||
                               message.StartsWith("Exception", StringComparison.OrdinalIgnoreCase)
                ? message
                : $"Error: {message}";
            _underlying.LogError($"{TerminalColor.Red}{formatted}{TerminalColor.Reset}");
        }

        public void LogDebug(object data)
        {
            if (Plugin.EnableDebugLogs?.Value == true)
            {
                _underlying.LogInfo($"[DEBUG] {data}");
            }
        }

        public void LogMessage(object data) => _underlying.LogMessage(data);

        public void LogFatal(object data) => _underlying.LogFatal(data);

        public void Log(LogLevel level, object data)
        {
            switch (level)
            {
                case LogLevel.Warning:
                    LogWarning(data);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    LogError(data);
                    break;
                case LogLevel.Debug:
                    LogDebug(data);
                    break;
                default:
                    LogInfo(data);
                    break;
            }
        }
    }
}
