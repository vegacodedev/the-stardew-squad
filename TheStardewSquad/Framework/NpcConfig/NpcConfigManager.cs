using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Data;
using TheStardewSquad.Framework.NpcConfig.Models;

namespace TheStardewSquad.Framework.NpcConfig
{
    /// <summary>
    /// Manages loading and access to unified NPC configuration data.
    /// Provides both Generic defaults and NPC-specific configurations.
    /// </summary>
    public class NpcConfigManager
    {
        private readonly INpcConfigDataProvider _dataProvider;
        private readonly IMonitor _monitor;
        private Dictionary<string, NpcConfigData> _npcConfigs;
        private bool _configLoadAttempted = false;

        public NpcConfigManager(INpcConfigDataProvider dataProvider, IMonitor monitor)
        {
            this._dataProvider = dataProvider;
            this._monitor = monitor;
        }

        /// <summary>
        /// Gets the configuration for a specific NPC.
        /// For Pets, supports three lookup levels:
        /// 1. Exact name match (e.g., "Fluffy" for custom pet names)
        /// 2. Breed-specific (e.g., "Cat_1", "Dog_2" based on pet.petType and pet.whichBreed)
        /// 3. Species-wide (e.g., "All_Cat", "All_Dog" based on pet.petType)
        /// Returns an empty config if not found.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <returns>NPC configuration or empty config if not found</returns>
        public virtual NpcConfigData GetConfig(NPC npc)
        {
            EnsureConfigLoaded();

            // First, try exact name match (for standard NPCs and custom-named pets)
            if (this._npcConfigs.TryGetValue(npc.Name, out var config))
            {
                this._monitor.Log($"[NpcConfig] Found custom config for '{npc.Name}'", LogLevel.Trace);
                return config;
            }

            // For Pets, try breed-specific pattern (e.g., "Cat_1", "Dog_2")
            if (npc is Pet pet)
            {
                string breedKey = $"{pet.petType}_{pet.whichBreed.Value}";
                if (this._npcConfigs.TryGetValue(breedKey, out var breedConfig))
                {
                    this._monitor.Log($"[NpcConfig] Found breed config '{breedKey}' for pet '{npc.Name}'", LogLevel.Trace);
                    return breedConfig;
                }

                // Try species-wide pattern (e.g., "All_Cat", "All_Dog")
                string speciesKey = $"All_{pet.petType}";
                if (this._npcConfigs.TryGetValue(speciesKey, out var speciesConfig))
                {
                    this._monitor.Log($"[NpcConfig] Found species config '{speciesKey}' for pet '{npc.Name}'", LogLevel.Trace);
                    return speciesConfig;
                }
            }

            this._monitor.Log($"[NpcConfig] No custom config for '{npc.Name}', returning empty config", LogLevel.Trace);
            return new NpcConfigData();
        }

        /// <summary>
        /// Gets the Generic default configuration that applies to all NPCs.
        /// Returns an empty config if Generic defaults are not defined.
        /// </summary>
        /// <returns>Generic configuration or empty config</returns>
        public virtual NpcConfigData GetGenericConfig()
        {
            EnsureConfigLoaded();
            return this._npcConfigs.TryGetValue("Generic", out var config) ? config : new NpcConfigData();
        }

        /// <summary>
        /// Checks if a specific NPC has a custom configuration defined.
        /// For Pets, checks exact name, breed-specific, and species-wide keys.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <returns>True if NPC has custom config, false otherwise</returns>
        public virtual bool HasConfig(NPC npc)
        {
            EnsureConfigLoaded();

            // Check exact name match
            if (this._npcConfigs.ContainsKey(npc.Name))
                return true;

            // For Pets, check breed-specific and species-wide patterns
            if (npc is Pet pet)
            {
                string breedKey = $"{pet.petType}_{pet.whichBreed.Value}";
                if (this._npcConfigs.ContainsKey(breedKey))
                    return true;

                string speciesKey = $"All_{pet.petType}";
                return this._npcConfigs.ContainsKey(speciesKey);
            }

            return false;
        }

        /// <summary>
        /// Ensures configuration data is loaded from Content Patcher.
        /// Uses lazy loading pattern - only loads once on first access.
        /// </summary>
        private void EnsureConfigLoaded()
        {
            if (!this._configLoadAttempted)
            {
                this._configLoadAttempted = true;
                this._monitor.Log("[NpcConfig] Loading unified NpcConfig data from Content Patcher...", LogLevel.Info);

                try
                {
                    this._npcConfigs = this._dataProvider.LoadNpcConfigData();
                    this._monitor.Log($"[NpcConfig] Successfully loaded {this._npcConfigs.Count} NPC configuration(s)", LogLevel.Info);

                    // Warn if Generic defaults are missing
                    if (!this._npcConfigs.ContainsKey("Generic"))
                    {
                        this._monitor.Log("[NpcConfig] WARNING: No Generic defaults defined. NPCs without custom configs will have no dialogue/animations.", LogLevel.Warn);
                    }
                }
                catch (Microsoft.Xna.Framework.Content.ContentLoadException ex)
                {
                    this._monitor.Log($"[NpcConfig] NpcConfig asset not found: {ex.Message}. Using empty configuration for all NPCs.", LogLevel.Warn);
                    this._npcConfigs = new Dictionary<string, NpcConfigData>();
                }
                catch (Exception ex)
                {
                    this._monitor.Log($"[NpcConfig] Error loading NpcConfig: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
                    this._npcConfigs = new Dictionary<string, NpcConfigData>();
                }
            }
        }
    }
}
