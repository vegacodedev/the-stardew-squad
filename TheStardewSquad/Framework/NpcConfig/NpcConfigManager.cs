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
    /// Combines the baseline content addons with Content Patcher contributions
    /// </summary>
    public class NpcConfigManager
    {
        private readonly INpcConfigDataProvider _dataProvider;
        private readonly IReadOnlyDictionary<string, NpcConfigData> _baseline;
        private readonly IMonitor _monitor;
        private Dictionary<string, NpcConfigData> _npcConfigs;
        private bool _configLoadAttempted = false;

        public NpcConfigManager(
            INpcConfigDataProvider dataProvider,
            IReadOnlyDictionary<string, NpcConfigData> baseline,
            IMonitor monitor)
        {
            this._dataProvider = dataProvider;
            this._baseline = baseline;
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
        public virtual NpcConfigData GetConfig(NPC npc)
        {
            EnsureConfigLoaded();

            if (this._npcConfigs.TryGetValue(npc.Name, out var config))
            {
                this._monitor.Log($"[NpcConfig] Found custom config for '{npc.Name}'", LogLevel.Trace);
                return config;
            }

            if (npc is Pet pet)
            {
                string breedKey = $"{pet.petType}_{pet.whichBreed.Value}";
                if (this._npcConfigs.TryGetValue(breedKey, out var breedConfig))
                {
                    this._monitor.Log($"[NpcConfig] Found breed config '{breedKey}' for pet '{npc.Name}'", LogLevel.Trace);
                    return breedConfig;
                }

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
        public virtual NpcConfigData GetGenericConfig()
        {
            EnsureConfigLoaded();
            return this._npcConfigs.TryGetValue("Generic", out var config) ? config : new NpcConfigData();
        }

        /// <summary>
        /// Checks if a specific NPC has a custom configuration defined.
        /// For Pets, checks exact name, breed-specific, and species-wide keys.
        /// </summary>
        public virtual bool HasConfig(NPC npc)
        {
            EnsureConfigLoaded();

            if (this._npcConfigs.ContainsKey(npc.Name))
                return true;

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
        /// Loads community Content Patcher contributions and merges them on top of the baseline.
        /// Called lazily on first access.
        /// </summary>
        private void EnsureConfigLoaded()
        {
            if (this._configLoadAttempted)
                return;

            this._configLoadAttempted = true;
            this._monitor.Log("[NpcConfig] Loading NpcConfig data (baseline + community overrides)...", LogLevel.Info);

            Dictionary<string, NpcConfigData> community;
            try
            {
                community = this._dataProvider.LoadNpcConfigData() ?? new Dictionary<string, NpcConfigData>();
            }
            catch (Microsoft.Xna.Framework.Content.ContentLoadException ex)
            {
                this._monitor.Log(
                    $"[NpcConfig] NpcConfig asset not found: {ex.Message}. Using baseline only.",
                    LogLevel.Warn);
                community = new Dictionary<string, NpcConfigData>();
            }
            catch (Exception ex)
            {
                this._monitor.Log(
                    $"[NpcConfig] Error loading NpcConfig: {ex.Message}\n{ex.StackTrace}",
                    LogLevel.Error);
                community = new Dictionary<string, NpcConfigData>();
            }

            this._npcConfigs = MergeBaselineAndCommunity(this._baseline, community);

            this._monitor.Log(
                $"[NpcConfig] Loaded {this._baseline.Count} baseline entries, "
                + $"{community.Count} community overrides, "
                + $"{this._npcConfigs.Count} merged entries.",
                LogLevel.Info);

            if (!this._npcConfigs.ContainsKey("Generic"))
            {
                this._monitor.Log(
                    "[NpcConfig] WARNING: No Generic defaults defined. NPCs without custom configs will have no dialogue/animations.",
                    LogLevel.Warn);
            }
        }

        private static Dictionary<string, NpcConfigData> MergeBaselineAndCommunity(
            IReadOnlyDictionary<string, NpcConfigData> baseline,
            IReadOnlyDictionary<string, NpcConfigData> community)
        {
            var merged = new Dictionary<string, NpcConfigData>(baseline.Count + community.Count, StringComparer.Ordinal);

            foreach (var kv in baseline)
                merged[kv.Key] = kv.Value;

            foreach (var kv in community)
            {
                merged[kv.Key] = merged.TryGetValue(kv.Key, out var baseEntry)
                    ? MergeEntry(baseEntry, kv.Value)
                    : kv.Value;
            }

            return merged;
        }

        private static NpcConfigData MergeEntry(NpcConfigData baseEntry, NpcConfigData community)
        {
            return new NpcConfigData
            {
                NpcType = community.NpcType ?? baseEntry.NpcType,
                Dialogue = community.Dialogue ?? baseEntry.Dialogue,
                Behavior = community.Behavior ?? baseEntry.Behavior,
                Sprites = community.Sprites ?? baseEntry.Sprites
            };
        }
    }
}
