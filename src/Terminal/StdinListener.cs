using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Reads STDIN lines on a background thread and queues them for the main thread.
    /// </summary>
    internal static class StdinListener
    {
        private const int MaxConsecutiveEofRetries = 3;
        private const int EofRetryIntervalMs = 1000;

        private static readonly ConcurrentQueue<string> _commandQueue = new();
        private static Thread? _stdinThread;
        private static volatile bool _isRunning;

        public static void Start()
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

            Plugin.Log.LogInfo($"{TerminalColor.Green}{Plugin.ModName} v{Plugin.ModVersion} loaded successfully. STDIN listener started.{TerminalColor.Reset}");
        }

        public static void Stop()
        {
            _isRunning = false;
        }

        public static void Interrupt()
        {
            _stdinThread?.Interrupt();
        }

        public static bool TryDequeue(out string command) => _commandQueue.TryDequeue(out command);

        private static void ReadStdinLoop()
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
                            Plugin.Log.LogInfo("STDIN stream closed or detached (EOF detected). Listener thread shutting down.");
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
                    Plugin.Log.LogWarning($"Exception in STDIN reader thread: {exception.Message}");
                    Thread.Sleep(EofRetryIntervalMs);
                }
            }
        }
    }
}
