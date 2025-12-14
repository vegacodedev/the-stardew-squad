using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Abstractions.Utilities;
using TheStardewSquad.Framework.NpcConfig.Models;

namespace TheStardewSquad.Framework.NpcConfig
{
    /// <summary>
    /// Manages dialogue selection for NPCs using the new unified configuration system.
    /// Implements pool-all-matches logic for conditional dialogue arrays.
    /// </summary>
    public class DialogueManager
    {
        private readonly NpcConfigManager _configManager;
        private readonly IRandomSelector _randomSelector;
        private readonly IGameStateChecker _gameStateChecker;
        private readonly INpcDialogueService _npcDialogueService;
        private readonly IGameContext _gameContext;
        private readonly IMonitor _monitor;

        public DialogueManager(
            NpcConfigManager configManager,
            IRandomSelector randomSelector,
            IGameStateChecker gameStateChecker,
            INpcDialogueService npcDialogueService,
            IGameContext gameContext,
            IMonitor monitor)
        {
            _configManager = configManager;
            _randomSelector = randomSelector;
            _gameStateChecker = gameStateChecker;
            _npcDialogueService = npcDialogueService;
            _gameContext = gameContext;
            _monitor = monitor;
        }

        /// <summary>
        /// Gets a random dialogue line for an NPC of a specific type.
        /// NPC-specific dialogue completely overrides Generic defaults for that dialogue type.
        /// Generic is only used as fallback when NPC has no dialogue defined for that type.
        /// Pools all matching conditional dialogue from the selected config (NPC-specific OR Generic).
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <param name="dialogueType">Type of dialogue (Recruit, Idle, Attacking, etc.)</param>
        /// <returns>Dialogue string with tokens replaced, or null if no dialogue found</returns>
        public string GetDialogue(NPC npc, string dialogueType)
        {
            _monitor.Log($"[Dialogue] Getting {dialogueType} dialogue for {npc.Name}", LogLevel.Trace);

            var genericConfig = _configManager.GetGenericConfig();
            var npcConfig = _configManager.GetConfig(npc);

            var allDialogueKeys = new List<string>();
            bool usingGeneric = false;

            // Check if NPC has ANY dialogue defined for this type
            // If yes, use ONLY NPC dialogue (completely overrides Generic)
            if (npcConfig.Dialogue != null)
            {
                var npcKeys = GetDialogueKeysForType(npcConfig.Dialogue, dialogueType, npc);
                if (npcKeys.Any())
                {
                    allDialogueKeys.AddRange(npcKeys);
                    _monitor.Log($"[Dialogue] {npc.Name} has {npcKeys.Count} {dialogueType} dialogue line(s) (using NPC-specific)", LogLevel.Debug);
                }
            }

            // If NPC has no dialogue for this type, use Generic as fallback
            if (!allDialogueKeys.Any() && genericConfig.Dialogue != null)
            {
                var genericKeys = GetDialogueKeysForType(genericConfig.Dialogue, dialogueType, npc);
                allDialogueKeys.AddRange(genericKeys);
                usingGeneric = true;

                if (genericKeys.Any())
                {
                    _monitor.Log($"[Dialogue] {npc.Name} has no {dialogueType} dialogue, using {genericKeys.Count} Generic line(s) as fallback", LogLevel.Debug);
                }
            }

            // If no dialogue found, return null
            if (!allDialogueKeys.Any())
            {
                _monitor.Log($"[Dialogue] No {dialogueType} dialogue found for {npc.Name} (neither NPC-specific nor Generic)", LogLevel.Warn);
                return null;
            }

            // Randomly select one dialogue key
            var chosenKey = _randomSelector.ChooseFrom(allDialogueKeys);
            _monitor.Log($"[Dialogue] Selected dialogue: '{chosenKey}' from pool of {allDialogueKeys.Count} option(s) {(usingGeneric ? "(Generic)" : "(NPC-specific)")}", LogLevel.Trace);

            return ParseText(chosenKey, npc);
        }

        /// <summary>
        /// Extracts all matching dialogue keys for a specific dialogue type.
        /// Implements pool-all-matches logic: includes all unconditional entries and all entries whose conditions match.
        /// Supports {CurrentNpc} placeholder in conditions which gets replaced with the NPC's name.
        /// </summary>
        /// <param name="dialogueConfig">The dialogue configuration</param>
        /// <param name="dialogueType">The dialogue type to extract</param>
        /// <param name="npc">The NPC being evaluated (used for {CurrentNpc} placeholder replacement)</param>
        /// <returns>List of dialogue keys that match current conditions</returns>
        private List<string> GetDialogueKeysForType(DialogueConfig dialogueConfig, string dialogueType, NPC npc)
        {
            List<object> dialogueArray = dialogueType switch
            {
                "Recruit" => dialogueConfig.Recruit,
                "Dismiss" => dialogueConfig.Dismiss,
                "Idle" => dialogueConfig.Idle,
                "Attacking" => dialogueConfig.Attacking,
                "Mining" => dialogueConfig.Mining,
                "Fishing" => dialogueConfig.Fishing,
                "Watering" => dialogueConfig.Watering,
                "Lumbering" => dialogueConfig.Lumbering,
                "Harvesting" => dialogueConfig.Harvesting,
                "Foraging" => dialogueConfig.Foraging,
                "Petting" => dialogueConfig.Petting,
                "FriendshipTooLow" => dialogueConfig.FriendshipTooLow,
                "RecruitmentRefusal" => dialogueConfig.RecruitmentRefusal,
                _ => null
            };

            if (dialogueArray == null || !dialogueArray.Any())
                return new List<string>();

            var matchingKeys = new List<string>();

            foreach (var item in dialogueArray)
            {
                // Handle simple string entries (always included)
                if (item is string simpleKey)
                {
                    matchingKeys.Add(simpleKey);
                    continue;
                }

                // Handle conditional entries (JObject from Content Patcher deserialization)
                if (item is JObject jObj)
                {
                    try
                    {
                        var condition = jObj["Condition"]?.ToString();
                        var linesToken = jObj["Lines"];

                        // Replace {CurrentNpc} placeholder with actual NPC name
                        if (!string.IsNullOrWhiteSpace(condition) && condition.Contains("{CurrentNpc}"))
                        {
                            condition = condition.Replace("{CurrentNpc}", npc.Name);
                            _monitor.Log($"[Dialogue] Replaced {{CurrentNpc}} placeholder: {condition}", LogLevel.Trace);
                        }

                        // If no condition, always include (default/fallback)
                        bool conditionMet = string.IsNullOrWhiteSpace(condition) ||
                                           _gameStateChecker.CheckConditions(condition, _gameContext.CurrentLocation, _gameContext.Player);

                        if (conditionMet && linesToken != null)
                        {
                            // Lines can be either an array or a single string
                            if (linesToken is JArray linesArray)
                            {
                                foreach (var line in linesArray)
                                {
                                    matchingKeys.Add(line.ToString());
                                }
                            }
                            else
                            {
                                matchingKeys.Add(linesToken.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other entries
                        System.Diagnostics.Debug.WriteLine($"Error processing dialogue entry: {ex.Message}");
                    }
                }
            }

            return matchingKeys;
        }

        /// <summary>
        /// Displays a speech bubble above an NPC's head.
        /// </summary>
        public void ShowDialogueBubble(NPC npc, string text)
        {
            _npcDialogueService.ShowTextAboveHead(npc, text);
        }

        /// <summary>
        /// Replaces standard tokens in a dialogue string.
        /// Supported tokens:
        /// - {{endearment}}: Term of spousal endearment (e.g., "honey", "dear")
        /// - {{name}}: Player's name
        /// </summary>
        private string ParseText(string text, NPC npc)
        {
            if (text is null) return null;
            text = text.Replace("{{endearment}}", _npcDialogueService.GetTermOfSpousalEndearment(npc));
            text = text.Replace("{{name}}", _gameContext.Player?.Name ?? "");
            return text;
        }
    }
}
