using System.Reflection;

namespace ConsoleStdinHeeler
{
    /// <summary>
    /// Implements dedicated-server safe overrides and network synchronization for vanilla Valheim
    /// console commands that are otherwise broken, crash on headless servers, or fail to sync to clients.
    /// </summary>
    public static partial class CommandOverrides
    {
        private static readonly MethodInfo? SendNetTimeMethod = typeof(ZNet).GetMethod(
            "SendNetTime",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );
    }
}
