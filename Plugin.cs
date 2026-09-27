using System;
using System.Collections.Concurrent;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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


        public enum ColorTagMode
        {
            Ansi,
            Strip,
            Raw
        }

        // Configuration entries
        public static ConfigEntry<bool> EnableMod = null!;
        public static ConfigEntry<bool> EnableCheatsByDefault = null!;
        public static ConfigEntry<ColorTagMode> ColorFormatting = null!;
        public static ConfigEntry<string> ConsolePrompt = null!;
        public static ConfigEntry<bool> EnableDebugLogs = null!;

        private Harmony? _harmony;
        private const int MaxConsecutiveEofRetries = 3;
        private const int EofRetryIntervalMs = 1000;


        private readonly ConcurrentQueue<string> _commandQueue = new();
        private Thread? _stdinThread;
        private volatile bool _isRunning;
        private bool _hasAnnouncedServerReady;

        private void Awake()
        {
            Instance = this;
            Log = new PluginLogger(Logger);

            EnableMod = Config.Bind(
                "1 - General",
                "EnableMod",
                true,
                new ConfigDescription("Master switch to enable or disable the STDIN console listener.")
            );

            EnableCheatsByDefault = Config.Bind(
                "1 - General",
                "EnableCheatsByDefault",
                true,
                new ConfigDescription("Allows modded and admin cheat commands to execute on the dedicated server.")
            );

            ConsolePrompt = Config.Bind(
                "2 - Display",
                "ConsolePrompt",
                "> ",
                new ConfigDescription("Prefix displayed in logs when an STDIN command is received.")
            );

            ColorFormatting = Config.Bind(
                "2 - Display",
                "ColorFormatting",
                ColorTagMode.Ansi,
                new ConfigDescription("How Unity rich-text color tags from console outputs are handled: Ansi (render in terminal colors), Strip (clean plain text), or Raw (keep tags unchanged).")
            );

            EnableDebugLogs = Config.Bind(
                "9 - Debug",
                "EnableDebugLogs",
                false,
                new ConfigDescription(
                    "Enables verbose diagnostic logging.",
                    null,
                    new ConfigurationManagerAttributes { IsAdvanced = true })
            );

            if (!EnableMod.Value)
            {
                Log.LogInfo($"{ModName} is disabled in config.");
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

            // Primary detection at startup: check for headless graphics or server executable flags
            if (IsDedicatedServerEnvironment())
            {
                StartStdinListener();
            }
            else
            {
                Log.LogInfo("Dedicated server environment not detected at Awake; listener deferred until networking initializes.");
            }
        }

        private void Update()
        {
            // Deferred check: If listener was not started at Awake due to custom launchers/wrappers, check ZNet
            if (!_isRunning)
            {
                // Throttle early-boot polling to once every 30 frames
                if (Time.frameCount % 30 != 0 || ZNet.instance == null)
                {
                    return;
                }

                if (!ZNet.instance.IsDedicated())
                {
                    // Active client instance detected (e.g. running in game client / local play).
                    // ConsoleStdinHeeler is dedicated-server-only; disable update loop and stay dormant.
                    Log.LogInfo("Client environment detected (ZNet.instance.IsDedicated() is false). ConsoleStdinHeeler is disabling itself.");
                    enabled = false;
                    return;
                }

                Log.LogInfo("Dedicated server confirmed via ZNet.instance.IsDedicated(); starting deferred STDIN listener.");
                StartStdinListener();
            }

            // Announce when the server world has finished loading and is ready for commands
            if (!_hasAnnouncedServerReady && Game.instance != null && ZoneSystem.instance != null)
            {
                _hasAnnouncedServerReady = true;
                Log.LogInfo($"{TerminalColor.Bold}{TerminalColor.Green}Server world loaded successfully. STDIN console ready for commands.{TerminalColor.Reset}");
            }

            // Drive active background countdown timers (e.g. shutdown countdown)
            CommandCustom.Update();

            while (_commandQueue.TryDequeue(out string queuedCommand))
            {
                CommandServer.Execute(queuedCommand);
            }
        }

        private void OnDestroy()
        {
            StopListener();
            _stdinThread?.Interrupt();
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch
            {
                // Ignored on teardown
            }
        }

        internal void StopListener()
        {
            _isRunning = false;
        }

        private void StartStdinListener()
        {
            if (_isRunning)
            {
                return;
            }

            Console.SetConsoleEnabledForThisSession();

            _isRunning = true;
            _stdinThread = new Thread(ReadStdinLoop)
            {
                Name = "ConsoleStdinHeeler_Reader",
                IsBackground = true
            };
            _stdinThread.Start();

            Log.LogInfo($"{TerminalColor.Green}{ModName} v{ModVersion} loaded successfully. STDIN listener started.{TerminalColor.Reset}");
        }

        private void ReadStdinLoop()
        {
            int consecutiveEofCount = 0;

            while (_isRunning)
            {
                try
                {
                    string? inputLine = System.Console.ReadLine();

                    if (inputLine == null)
                    {
                        consecutiveEofCount++;
                        if (consecutiveEofCount >= MaxConsecutiveEofRetries)
                        {
                            Log.LogInfo("STDIN stream closed or detached (EOF detected). Listener thread shutting down.");
                            break;
                        }

                        Thread.Sleep(EofRetryIntervalMs);
                        continue;
                    }

                    consecutiveEofCount = 0;
                    inputLine = inputLine.Trim();
                    if (inputLine.Length > 0)
                    {
                        _commandQueue.Enqueue(inputLine);
                    }
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (ThreadInterruptedException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Log.LogWarning($"Exception in STDIN reader thread: {exception.Message}");
                    Thread.Sleep(EofRetryIntervalMs);
                }
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

