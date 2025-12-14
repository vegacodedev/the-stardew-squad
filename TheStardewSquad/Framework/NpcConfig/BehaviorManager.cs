using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Abstractions.Data;
using TheStardewSquad.Abstractions.Utilities;
using TheStardewSquad.Framework.NpcConfig.Models;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.NpcConfig
{
    /// <summary>
    /// Manages NPC behavior including idle animations, task restrictions, and recruitment.
    /// Implements pool-all-matches for idle animations and first-match-wins for allowed tasks.
    /// </summary>
    public class BehaviorManager
    {
        private const float MillisecondsPerTick = 16.666f;

        private readonly NpcConfigManager _configManager;
        private readonly INpcConfigDataProvider _dataProvider;
        private readonly IMonitor _monitor;
        private readonly IRandomSelector _randomSelector;
        private readonly IGameStateChecker _gameStateChecker;
        private readonly IGameContext _gameContext;
        private readonly INpcSpriteService _npcSpriteService;
        private readonly INpcDialogueService _npcDialogueService;

        public BehaviorManager(
            NpcConfigManager configManager,
            INpcConfigDataProvider dataProvider,
            IMonitor monitor,
            IRandomSelector randomSelector,
            IGameStateChecker gameStateChecker,
            IGameContext gameContext,
            INpcSpriteService npcSpriteService,
            INpcDialogueService npcDialogueService)
        {
            _configManager = configManager;
            _dataProvider = dataProvider;
            _monitor = monitor;
            _randomSelector = randomSelector;
            _gameStateChecker = gameStateChecker;
            _gameContext = gameContext;
            _npcSpriteService = npcSpriteService;
            _npcDialogueService = npcDialogueService;
        }

        #region Idle Animations

        /// <summary>
        /// Gets a random idle animation for an NPC based on current conditions.
        /// Pools all matching conditional animations from both Generic and NPC-specific configs.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <returns>Animation specification, or null if no animations available</returns>
        public IdleAnimationSpec GetRandomIdleAnimation(NPC npc)
        {
            _monitor.Log($"[Behavior] Getting idle animation for {npc.Name}", LogLevel.Trace);

            var genericConfig = _configManager.GetGenericConfig();
            var npcConfig = _configManager.GetConfig(npc);

            // Collect all matching animations from both configs
            var allAnimations = new List<IdleAnimationSpec>();

            // Note: Generic configs don't have Behavior section, so skip Generic

            // Add NPC-specific animations
            if (npcConfig.Behavior?.IdleAnimations != null)
            {
                var npcAnimations = GetMatchingIdleAnimations(npcConfig.Behavior.IdleAnimations, npc);
                allAnimations.AddRange(npcAnimations);
                _monitor.Log($"[Behavior] {npc.Name} has {npcAnimations.Count} idle animation(s) available after condition matching", LogLevel.Debug);
            }
            else
            {
                _monitor.Log($"[Behavior] {npc.Name} has no idle animations defined", LogLevel.Debug);
            }

            if (!allAnimations.Any())
            {
                _monitor.Log($"[Behavior] No idle animations available for {npc.Name}", LogLevel.Trace);
                return null;
            }

            // Randomly select one animation
            var selected = _randomSelector.ChooseFrom(allAnimations);
            _monitor.Log($"[Behavior] Selected idle animation '{selected.Id}' (Loop={selected.Loop}) for {npc.Name} from pool of {allAnimations.Count}", LogLevel.Trace);
            return selected;
        }

        /// <summary>
        /// Extracts all matching idle animations from a priority array.
        /// Implements pool-all-matches: includes all unconditional entries and all entries whose conditions match.
        /// </summary>
        private List<IdleAnimationSpec> GetMatchingIdleAnimations(List<object> animationsArray, NPC npc)
        {
            var matchingAnimations = new List<IdleAnimationSpec>();

            foreach (var item in animationsArray)
            {
                // Handle simple string entries (animation ID, always included)
                if (item is string simpleAnimId)
                {
                    matchingAnimations.Add(new IdleAnimationSpec { Id = simpleAnimId, Loop = true });
                    continue;
                }

                // Handle JObject entries (can be conditional or simple animation spec)
                if (item is JObject jObj)
                {
                    try
                    {
                        var condition = jObj["Condition"]?.ToString();
                        var animationsToken = jObj["Animations"];
                        var idToken = jObj["Id"];

                        // Case 1: This is a conditional entry with Animations array
                        if (animationsToken != null)
                        {
                            // Replace {CurrentNpc} placeholder with actual NPC name
                            if (!string.IsNullOrWhiteSpace(condition) && condition.Contains("{CurrentNpc}"))
                            {
                                condition = condition.Replace("{CurrentNpc}", npc.Name);
                                _monitor.Log($"[Behavior] Replaced {{CurrentNpc}} placeholder in idle animation: {condition}", LogLevel.Trace);
                            }

                            bool conditionMet = string.IsNullOrWhiteSpace(condition) ||
                                               _gameStateChecker.CheckConditions(condition, _gameContext.CurrentLocation, _gameContext.Player);

                            if (conditionMet)
                            {
                                // Process each animation in the Animations array
                                if (animationsToken is JArray animArray)
                                {
                                    foreach (var anim in animArray)
                                    {
                                        if (anim.Type == JTokenType.String)
                                        {
                                            string animId = anim.ToString();
                                            matchingAnimations.Add(new IdleAnimationSpec { Id = animId, Loop = true });
                                        }
                                        else if (anim is JObject animObj)
                                        {
                                            var spec = animObj.ToObject<IdleAnimationSpec>();
                                            if (spec != null)
                                                matchingAnimations.Add(spec);
                                        }
                                    }
                                }
                            }
                        }
                        // Case 2: This is a simple animation spec object { "Id": "...", "Loop": true }
                        else if (idToken != null)
                        {
                            var spec = jObj.ToObject<IdleAnimationSpec>();
                            if (spec != null)
                                matchingAnimations.Add(spec);
                        }
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log($"Error processing idle animation entry: {ex.Message}", LogLevel.Warn);
                    }
                }
            }

            return matchingAnimations;
        }

        /// <summary>
        /// Makes a squad mate perform a given idle animation.
        /// </summary>
        public void PlayIdleAnimation(ISquadMate mate, IdleAnimationSpec animationSpec)
        {
            var npc = mate.Npc;

            var allDescriptions = _dataProvider.LoadAnimationDescriptions();
            if (!allDescriptions.TryGetValue(animationSpec.Id, out var description))
            {
                _monitor.Log($"Could not find animation '{animationSpec.Id}' in Data/animationDescriptions.", LogLevel.Warn);
                return;
            }

            var animationFrames = ParseAnimation(description, animationSpec.Loop, _npcSpriteService.GetFacingDirection(npc), npc);
            if (animationFrames == null || !animationFrames.Any())
            {
                _monitor.Log($"Failed to parse animation description for '{animationSpec.Id}'.", LogLevel.Warn);
                return;
            }

            _npcSpriteService.SetCurrentAnimation(npc, animationFrames);
            mate.IsAnimating = true;

            if (animationSpec.Loop)
            {
                // Set an "infinite" cooldown that will be broken by the next Halt() call.
                mate.ActionCooldown = int.MaxValue;
            }
            else
            {
                int animationDurationMs = 0;
                foreach (var frame in animationFrames)
                {
                    animationDurationMs += frame.milliseconds;
                }

                // Set a cooldown to match the animation's length
                int cooldownInTicks = (int)(animationDurationMs / MillisecondsPerTick) + 2; // Add extra ticks for safety
                mate.ActionCooldown = cooldownInTicks;
            }
        }

        /// <summary>
        /// Parses an animation description string into a list of animation frames.
        /// </summary>
        private List<FarmerSprite.AnimationFrame> ParseAnimation(string description, bool looping, int facingDirection, NPC npc)
        {
            var parts = description.Split('/');
            if (parts.Length < 2) return null;

            var windUpFrames = new List<FarmerSprite.AnimationFrame>();
            var mainFramesList = new List<FarmerSprite.AnimationFrame>();

            // Part 0: The "wind-up" or beginning animation
            var startFrames = parts[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string frameStr in startFrames)
            {
                if (int.TryParse(frameStr, out int frame))
                {
                    windUpFrames.Add(new FarmerSprite.AnimationFrame(frame, 200));
                }
            }

            // Part 1: The main animation loop
            var mainFrames = parts[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string frameStr in mainFrames)
            {
                if (int.TryParse(frameStr, out int frame))
                {
                    mainFramesList.Add(new FarmerSprite.AnimationFrame(frame, 200, secondaryArm: false, flip: false));
                }
            }

            if (looping)
            {
                // For looping animations, play the wind-up once, then loop the main animation.
                if (windUpFrames.Any() && mainFramesList.Any())
                {
                    // Set a completion behavior on the last frame of the wind-up to start the main loop.
                    var lastWindUpFrame = windUpFrames.Last();
                    windUpFrames[windUpFrames.Count - 1] = new FarmerSprite.AnimationFrame(
                        lastWindUpFrame.frame,
                        lastWindUpFrame.milliseconds,
                        false, // secondary arm
                        false, // flip
                        (_) => npc.Sprite.setCurrentAnimation(mainFramesList)
                    );
                    return windUpFrames;
                }
                // If there's no wind-up, just return the main loop.
                return mainFramesList;
            }
            else
            {
                // For non-looping animations, combine all parts into a single sequence.
                var allFrames = new List<FarmerSprite.AnimationFrame>();
                allFrames.AddRange(windUpFrames);
                allFrames.AddRange(mainFramesList);

                if (parts.Length >= 3)
                {
                    var endFrames = parts[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string frameStr in endFrames)
                    {
                        if (int.TryParse(frameStr, out int frame))
                        {
                            allFrames.Add(new FarmerSprite.AnimationFrame(frame, 400));
                        }
                    }
                }

                // Add a final frame to return to a standard idle pose.
                allFrames.Add(new FarmerSprite.AnimationFrame(GetIdleFrame(facingDirection), 200));

                return allFrames;
            }
        }

        /// <summary>
        /// Gets the standard idle frame for a character based on their facing direction.
        /// </summary>
        public static int GetIdleFrame(int facingDirection)
        {
            return facingDirection switch
            {
                0 => 8,  // Up
                1 => 4,  // Right
                2 => 0,  // Down
                3 => 12, // Left
                _ => 0
            };
        }

        #endregion

        #region Allowed Tasks

        /// <summary>
        /// Gets the allowed tasks for an NPC.
        /// Implements first-match-wins logic for conditional task arrays.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <returns>Comma-separated list of allowed tasks</returns>
        public string GetAllowedTasks(NPC npc)
        {
            _monitor.Log($"[Behavior] Getting allowed tasks for {npc.Name}", LogLevel.Trace);
            var npcConfig = _configManager.GetConfig(npc);

            if (npcConfig.Behavior?.AllowedTasks == null)
            {
                // Default: all tasks allowed
                var defaultTasks = "Watering, Lumbering, Mining, Attacking, Harvesting, Foraging, Fishing, Petting, Sitting";
                return defaultTasks;
            }

            var allowedTasksValue = npcConfig.Behavior.AllowedTasks;

            // Case 1: Simple string (no conditions)
            if (allowedTasksValue is string simpleTasks)
            {
                _monitor.Log($"[Behavior] {npc.Name} AllowedTasks: {simpleTasks}", LogLevel.Debug);
                return simpleTasks;
            }

            // Case 2: Priority array (first-match-wins)
            if (allowedTasksValue is JArray tasksArray)
            {
                _monitor.Log($"[Behavior] {npc.Name} has conditional AllowedTasks (first-match-wins), evaluating...", LogLevel.Trace);

                foreach (var item in tasksArray)
                {
                    if (item.Type == JTokenType.String)
                    {
                        var tasks = item.ToString();
                        _monitor.Log($"[Behavior] {npc.Name} matched unconditional AllowedTasks: {tasks}", LogLevel.Debug);
                        return tasks;
                    }

                    if (item is JObject jObj)
                    {
                        var condition = jObj["Condition"]?.ToString();
                        var tasks = jObj["Tasks"]?.ToString();

                        // Replace {CurrentNpc} placeholder with actual NPC name
                        if (!string.IsNullOrWhiteSpace(condition) && condition.Contains("{CurrentNpc}"))
                        {
                            condition = condition.Replace("{CurrentNpc}", npc.Name);
                            _monitor.Log($"[Behavior] Replaced {{CurrentNpc}} placeholder in allowed tasks: {condition}", LogLevel.Trace);
                        }

                        // No condition = default/fallback (always matches)
                        bool conditionMet = string.IsNullOrWhiteSpace(condition) ||
                                           _gameStateChecker.CheckConditions(condition, _gameContext.CurrentLocation, _gameContext.Player);

                        if (conditionMet && !string.IsNullOrWhiteSpace(tasks))
                        {
                            _monitor.Log($"[Behavior] {npc.Name} matched condition '{condition}', AllowedTasks: {tasks}", LogLevel.Debug);
                            return tasks; // First match wins!
                        }
                    }
                }
            }

            // Fallback: all tasks allowed
            var fallbackTasks = "Watering, Lumbering, Mining, Attacking, Harvesting, Foraging, Fishing, Petting, Sitting";
            _monitor.Log($"[Behavior] {npc.Name} no conditions matched, using fallback (all tasks)", LogLevel.Debug);
            return fallbackTasks;
        }

        #endregion

        #region Recruitment

        /// <summary>
        /// Checks if an NPC can be recruited based on their configuration.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <returns>True if recruitment is allowed, false otherwise</returns>
        public bool CanRecruit(NPC npc)
        {
            _monitor.Log($"[Behavior] Checking if {npc.Name} can be recruited", LogLevel.Trace);
            var npcConfig = _configManager.GetConfig(npc);

            // If no Behavior or Recruitment config, default to enabled
            if (npcConfig.Behavior?.Recruitment == null)
            {
                return true;
            }

            var recruitment = npcConfig.Behavior.Recruitment;

            // Check if recruitment is explicitly disabled
            if (!recruitment.Enabled)
            {
                _monitor.Log($"[Behavior] {npc.Name} recruitment is disabled", LogLevel.Debug);
                return false;
            }

            // Check condition (if specified)
            if (!string.IsNullOrWhiteSpace(recruitment.Condition))
            {
                var condition = recruitment.Condition;

                // Replace {CurrentNpc} placeholder with actual NPC name
                if (condition.Contains("{CurrentNpc}"))
                {
                    condition = condition.Replace("{CurrentNpc}", npc.Name);
                    _monitor.Log($"[Behavior] Replaced {{CurrentNpc}} placeholder in recruitment: {condition}", LogLevel.Trace);
                }

                bool conditionMet = _gameStateChecker.CheckConditions(condition, _gameContext.CurrentLocation, _gameContext.Player);
                _monitor.Log($"[Behavior] {npc.Name} recruitment condition '{condition}' evaluated to: {conditionMet}", LogLevel.Debug);
                return conditionMet;
            }

            // No condition, recruitment enabled
            _monitor.Log($"[Behavior] {npc.Name} recruitment is enabled (no condition)", LogLevel.Debug);
            return true;
        }

        /// <summary>
        /// Gets the refusal dialogue key for an NPC when recruitment fails.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <returns>Refusal dialogue key, or null if not configured</returns>
        public string GetRecruitmentRefusalDialogueKey(NPC npc)
        {
            var npcConfig = _configManager.GetConfig(npc);
            return npcConfig.Behavior?.Recruitment?.RefusalDialogueKey;
        }

        #endregion
    }
}
