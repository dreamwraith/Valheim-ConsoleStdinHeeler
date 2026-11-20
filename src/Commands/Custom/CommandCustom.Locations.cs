using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleStdinHeeler
{
    public static partial class CommandCustom
    {
        /// <summary>
        /// Prints the coordinates of the closest location with the given name to a player, coordinates, or world spawn.
        /// Location names are case-sensitive.
        /// </summary>
        public static void HandleFindLocationCommand(string arguments)
        {
            if (!ZNet.instance || !Game.instance || !ZoneSystem.instance)
            {
                Plugin.Log.LogWarning("Cannot find location: Server world is still loading.");
                return;
            }

            List<string> parts = TerminalUtils.ParseArguments(arguments.Trim());
            if (parts.Count != 2)
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: findlocation <location> <player|\"player name\"|coords|spawn>");
                Plugin.Log.LogInfo("Coordinates must strictly follow 0,0 or 0,0,0 mask with no spaces.");
                return;
            }

            string locationName = parts[0];
            string referenceToken = parts[1];
            Vector3 referencePosition;
            string referenceDescription;

            if (referenceToken.Equals("spawn", StringComparison.OrdinalIgnoreCase))
            {
                // same lookup Game.FindSpawnPoint uses for the start temple
                if (!ZoneSystem.instance.GetLocationIcon(Game.instance.m_StartLocation, out referencePosition))
                {
                    Plugin.Log.LogWarning($"World spawn location '{Game.instance.m_StartLocation}' not found.");
                    return;
                }

                referenceDescription = "world spawn";
            }
            else
            {
                if (!CommandServer.TryResolveTarget(referenceToken, out CommandTarget target))
                {
                    return;
                }

                referencePosition = target.Position;
                referenceDescription = target.Description;
            }

            if (!ZoneSystem.instance.FindClosestLocation(locationName, referencePosition, out ZoneSystem.LocationInstance closest))
            {
                Plugin.Log.LogWarning($"No '{locationName}' location found in this world. Location names are case-sensitive.");
                return;
            }

            Vector3 position = closest.m_position;
            string coordinates = FormattableString.Invariant($"{position.x:F1},{position.y:F1},{position.z:F1}");
            Plugin.Log.LogInfo($"Closest {TerminalColor.Green}{locationName}{TerminalColor.Reset} to {referenceDescription}: {TerminalColor.Cyan}{coordinates}{TerminalColor.Reset}");
        }
    }
}
