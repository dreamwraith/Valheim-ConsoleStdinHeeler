using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConsoleStdinHeeler
{
    public static partial class CommandOverrides
    {
        /// <summary>
        /// Replaces vanilla 'spawn' command on dedicated servers to spawn prefabs (items, creatures, objects)
        /// near a connected player or at specified coordinates with optional count and stars/rank settings.
        /// Syntax: spawn <prefab> <player|"player name"|coords> [count] [stars/rank] [-s]
        /// </summary>
        public static void HandleSpawnCommand(string arguments)
        {
            if (ZNetScene.instance == null || ZNet.instance == null)
            {
                Plugin.Log.LogWarning("Cannot spawn: ZNetScene or server world is not loaded.");
                return;
            }

            if (string.IsNullOrWhiteSpace(arguments))
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: spawn <prefab> <player|\"player name\"|coords> [count] [stars/rank] [-s]");
                Plugin.Log.LogInfo("Coordinates must strictly follow 0,0 or 0,0,0 mask with no spaces.");
                return;
            }

            List<string> parts = TerminalUtils.ParseArguments(arguments.Trim());

            bool isSilent = false;
            if (parts.Count > 0 && parts[parts.Count - 1].Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                isSilent = true;
                parts.RemoveAt(parts.Count - 1);
            }

            if (parts.Count < 2 || parts.Count > 4 || parts.Exists(p => p.Equals("-s", StringComparison.OrdinalIgnoreCase)))
            {
                Plugin.Log.LogWarning("Invalid parameters. Usage: spawn <prefab> <player|\"player name\"|coords> [count] [stars/rank] [-s]");
                Plugin.Log.LogInfo("Coordinates must strictly follow 0,0 or 0,0,0 mask with no spaces.");
                return;
            }

            string prefabName = parts[0];
            string targetToken = parts[1];
            int amount = 1;
            int level = 1;

            // Position 3: [count, optional]
            if (parts.Count > 2)
            {
                if (!int.TryParse(parts[2], out int parsedAmount) || parsedAmount < 1)
                {
                    Plugin.Log.LogWarning($"Invalid count '{parts[2]}'. Count must be a positive integer.");
                    return;
                }

                amount = Math.Min(1000, parsedAmount);
            }

            // Position 4: [stars/rank, optional]
            if (parts.Count > 3)
            {
                if (!int.TryParse(parts[3], out int parsedLevel) || parsedLevel < 1)
                {
                    Plugin.Log.LogWarning($"Invalid stars/rank '{parts[3]}'. Level must be a positive integer.");
                    return;
                }

                level = Math.Min(10, parsedLevel);
            }

            // Look up prefab (exact match first, then single-pass case-insensitive and suggestion fallback)
            GameObject? prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                var allPrefabNames = ZNetScene.instance.GetPrefabNames();
                var suggestions = new List<string>(8);

                if (allPrefabNames != null)
                {
                    foreach (string registeredName in allPrefabNames)
                    {
                        if (string.Equals(registeredName, prefabName, StringComparison.OrdinalIgnoreCase))
                        {
                            prefab = ZNetScene.instance.GetPrefab(registeredName);
                            break;
                        }

                        if (suggestions.Count < 8 && registeredName.IndexOf(prefabName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            suggestions.Add(registeredName);
                        }
                    }
                }

                if (prefab == null)
                {
                    Plugin.Log.LogWarning($"Prefab '{prefabName}' not found in ZNetScene.");
                    if (suggestions.Count > 0)
                    {
                        Plugin.Log.LogInfo($"Matching prefabs: {TerminalColor.Yellow}{string.Join($"{TerminalColor.Reset}, {TerminalColor.Yellow}", suggestions)}{TerminalColor.Reset}");
                    }
                    return;
                }
            }

            if (!CommandServer.TryResolveTarget(targetToken, out CommandTarget target))
            {
                return;
            }

            Vector3 spawnCenter = target.Position;
            Vector3 spawnForward = target.Forward;
            string locationDescription = target.Description;

            ItemDrop? itemDropTemplate = prefab.GetComponent<ItemDrop>();
            int maxStackSize = itemDropTemplate != null && itemDropTemplate.m_itemData?.m_shared != null
                ? Math.Max(1, itemDropTemplate.m_itemData.m_shared.m_maxStackSize)
                : 1;

            int remainingToSpawn = amount;
            int totalEntitiesCreated = 0;

            while (remainingToSpawn > 0)
            {
                int currentStack = Math.Min(remainingToSpawn, maxStackSize);
                remainingToSpawn -= currentStack;

                Vector3 spread = totalEntitiesCreated == 0
                    ? Vector3.zero
                    : UnityEngine.Random.insideUnitSphere * Mathf.Min(8f, 0.5f + totalEntitiesCreated * 0.3f);
                spread.y = Mathf.Abs(spread.y) * 0.2f;

                Vector3 spawnPos = target.IsCoordinates
                    ? spawnCenter + Vector3.up * 0.5f + spread
                    : spawnCenter + spawnForward * 2f + Vector3.up * 0.5f + spread;
                GameObject spawnedObject = UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity);
                totalEntitiesCreated++;

                // Configure ItemDrop
                ItemDrop? spawnedItemDrop = spawnedObject.GetComponent<ItemDrop>();
                if (spawnedItemDrop != null)
                {
                    ItemDrop.OnCreateNew(spawnedObject, true);
                    if (spawnedItemDrop.m_itemData != null)
                    {
                        spawnedItemDrop.m_itemData.m_durability = spawnedItemDrop.m_itemData.GetMaxDurability();
                        spawnedItemDrop.m_itemData.m_stack = currentStack;
                        if (level > 1)
                        {
                            spawnedItemDrop.SetQuality(Mathf.Min(level, 4));
                        }
                    }
                }

                // Configure Character level
                Character? spawnedCharacter = spawnedObject.GetComponent<Character>();
                if (spawnedCharacter != null && level > 1)
                {
                    spawnedCharacter.SetLevel(level);
                }

                // Mark cheated ZDO flag
                ZNetView? netView = spawnedObject.GetComponent<ZNetView>();
                if (netView != null && netView.IsValid())
                {
                    netView.GetZDO().Set(ZDOVars.s_cheated, true);
                }
            }

            // Notify player if in range/scene, or via routed RPC if distant (unless silent mode is active)
            if (!isSilent)
            {
                if (target.Player != null)
                {
                    target.Player.Message(MessageHud.MessageType.TopLeft, $"Spawning object {prefab.name}" + (amount > 1 ? $" x{amount}" : ""));
                }
                else if (target.Peer != null && ZRoutedRpc.instance != null)
                {
                    ZRoutedRpc.instance.InvokeRoutedRPC(target.Peer.m_uid, "ShowMessage", (int)MessageHud.MessageType.TopLeft, $"Spawning object {prefab.name}" + (amount > 1 ? $" x{amount}" : ""));
                }
            }

            string levelDisplay = level > 1 ? $" (Level {level})" : string.Empty;
            string silentDisplay = isSilent ? $" {TerminalColor.Gray}(silent){TerminalColor.Reset}" : string.Empty;
            Plugin.Log.LogInfo($"Spawned {TerminalColor.Green}{prefab.name}{levelDisplay} x{amount}{TerminalColor.Reset} at {locationDescription}{silentDisplay}.");
        }
    }
}
