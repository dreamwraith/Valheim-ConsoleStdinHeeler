using System;
using UnityEngine;

namespace ConsoleStdinHeeler
{
    public static partial class CommandOverrides
    {
        /// <summary>
        /// Replaces vanilla 'tod' command on dedicated servers to set network time and immediately broadcast
        /// the updated time of day (0.0 to 1.0) to all connected game clients.
        /// </summary>
        public static void HandleTodCommand(string arguments)
        {
            if (ZNet.instance == null)
            {
                Plugin.Log.LogWarning("Cannot set time of day: Server world is not loaded.");
                return;
            }

            if (string.IsNullOrWhiteSpace(arguments) ||
                !float.TryParse(arguments.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float param) ||
                param < 0f || param > 1f)
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: tod <0-1> (e.g. tod 0.5 for noon)");
                return;
            }

            double currentTime = ZNet.instance.GetTimeSeconds();
            long dayLength = EnvMan.instance != null && EnvMan.instance.m_dayLengthSec > 0
                ? EnvMan.instance.m_dayLengthSec
                : 1800L;

            long currentDay = (long)(currentTime / dayLength);
            float targetFraction = Mathf.Clamp01(param);

            double newTime = (currentDay * dayLength) + (targetFraction * dayLength);
            if (newTime < currentTime)
            {
                newTime += dayLength;
            }

            if (EnvMan.instance != null)
            {
                EnvMan.instance.m_debugTimeOfDay = false;
            }

            ZNet.instance.SetNetTime(newTime);
            SynchronizeNetworkTime();

            Plugin.Log.LogInfo($"Setting time of day: {TerminalColor.Cyan}{param:G}{TerminalColor.Reset}");
        }

        /// <summary>
        /// Replaces vanilla 'sleep' command on dedicated servers to skip to morning and synchronize network time.
        /// </summary>
        public static void HandleSleepCommand()
        {
            if (EnvMan.instance == null || ZNet.instance == null)
            {
                Plugin.Log.LogWarning("Cannot skip time to morning: EnvMan/ZNet is not ready.");
                return;
            }

            EnvMan.instance.SkipToMorning();
            SynchronizeNetworkTime();

            int currentDay = EnvMan.instance.GetDay();
            Plugin.Log.LogInfo($"Skipping time to morning (Day {TerminalColor.Cyan}{currentDay + 1}{TerminalColor.Reset})...");
        }

        private static void SynchronizeNetworkTime()
        {
            try
            {
                SendNetTimeMethod?.Invoke(ZNet.instance, null);
            }
            catch
            {
                // Ignored; periodic net time sync will send shortly
            }
        }
    }
}
