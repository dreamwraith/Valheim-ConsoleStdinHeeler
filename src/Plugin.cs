using System;
using BepInEx;
using BepInEx.Logging;
using ConsoleStdinHeeler.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConsoleStdinHeeler
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGUID = "dreamwraith.ConsoleStdinHeeler";
        public const string ModName = "ConsoleStdinHeeler";
        public const string ModVersion = VersionInfo.Version;

        internal static Plugin? Instance { get; private set; }
        internal static PluginLogger Log { get; private set; } = null!;

        private Harmony? _harmony;
        private bool _hasAnnouncedServerReady;

        private void Awake()
        {
            Instance = this;
            Log = new PluginLogger(Logger);

            ModConfig.Initialize(Config);

            if (!ModConfig.EnableMod.Value)
            {
                Log.LogInfo($"{ModName} is disabled in config.");
                enabled = false;
                return;
            }

            if (!IsDedicatedServerEnvironment())
            {
                Log.LogWarning($"{ModName} is designed strictly for dedicated servers and cannot run on an interactive game client. Disabling mod.");
                enabled = false;
                return;
            }

            try
            {
                _harmony = new Harmony(ModGUID);
                _harmony.PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to initialize Harmony patches: {ex.Message}");
            }

            StdinListener.Start();
        }

        private void Update()
        {
            // Announce when the server world has finished loading and is ready for commands
            if (!_hasAnnouncedServerReady && Game.instance != null && ZoneSystem.instance != null)
            {
                _hasAnnouncedServerReady = true;
                Log.LogInfo($"{TerminalColor.Bold}{TerminalColor.Green}Server world loaded successfully. STDIN console ready for commands.{TerminalColor.Reset}");
            }

            // Drive active background countdown timers (e.g. shutdown countdown)
            CommandCustom.Update();

            while (StdinListener.TryDequeue(out string queuedCommand))
            {
                CommandServer.Execute(queuedCommand);
            }
        }

        private void OnDestroy()
        {
            StdinListener.Stop();
            StdinListener.Interrupt();
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch
            {
                // Ignored on teardown
            }
        }

        private static bool IsDedicatedServerEnvironment()
        {
            bool isHeadlessGraphics = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
            string commandLine = Environment.CommandLine;
            bool hasServerBinary = commandLine.IndexOf("valheim_server", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasBatchModeFlag = commandLine.IndexOf("-batchmode", StringComparison.OrdinalIgnoreCase) >= 0;

            return isHeadlessGraphics || hasServerBinary || hasBatchModeFlag;
        }
    }
}
