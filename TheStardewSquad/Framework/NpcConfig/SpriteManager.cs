using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Framework.NpcConfig.Models;

namespace TheStardewSquad.Framework.NpcConfig
{
    /// <summary>
    /// Manages sprite animations for NPCs during tasks.
    /// Implements first-match-wins logic for conditional sprite arrays.
    /// </summary>
    public class SpriteManager
    {
        private readonly NpcConfigManager _configManager;
        private readonly IMonitor _monitor;
        private readonly IGameStateChecker _gameStateChecker;
        private readonly IGameContext _gameContext;
        private readonly VanillaSpriteDetector _vanillaSpriteDetector;
        private Multiplayer.MessageDispatcher? _dispatcher;

        public SpriteManager(
            NpcConfigManager configManager,
            IMonitor monitor,
            IGameStateChecker gameStateChecker,
            IGameContext gameContext,
            VanillaSpriteDetector vanillaSpriteDetector)
        {
            _configManager = configManager;
            _monitor = monitor;
            _gameStateChecker = gameStateChecker;
            _gameContext = gameContext;
            _vanillaSpriteDetector = vanillaSpriteDetector;
        }

        /// <summary>
        /// Post-construction injection of MessageDispatcher (constructed after SpriteManager).
        /// Required so host-side ApplyTaskAnimation / TryApplyTaskSpriteSheet calls can broadcast
        /// to peers; vanilla doesn't propagate Sprite.CurrentAnimation, currentFrame, flip, or
        /// the texture-swap state, so peers need an explicit replay.
        /// </summary>
        public void AttachDispatcher(Multiplayer.MessageDispatcher dispatcher) => _dispatcher = dispatcher;

        /// <summary>
        /// Gets the sprite animation configuration for a specific task type.
        /// Checks NPC-specific config first, then falls back to Generic defaults.
        /// Returns null if no sprite config is found.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <param name="taskType">The task type (Attacking, Mining, Fishing, etc.)</param>
        /// <returns>Sprite animation config, or null if not found</returns>
        public SpriteAnimationConfig GetTaskSpriteConfig(NPC npc, string taskType)
        {
            _monitor.LogOnce($"[Sprite] Getting sprite config for {npc.Name} - {taskType}", LogLevel.Trace);

            // Try NPC-specific config first
            var npcConfig = _configManager.GetConfig(npc);
            if (npcConfig.Sprites != null)
            {
                var npcSprite = GetSpriteForTask(npcConfig.Sprites, taskType, npc);
                if (npcSprite != null)
                {
                    // Check if the sprite texture matches - either fully vanilla or fully retextured.
                    // Pets are exempt: retexture mods conventionally preserve the vanilla pet frame
                    // layout (e.g. sitting at frames 28-29), so the same config works regardless.
                    if (npc is Pet || npcSprite.IsVanilla == _vanillaSpriteDetector.IsVanillaSprite(npc))
                    {
                        return npcSprite;
                    }
                    // Otherwise skip this sprite, fall through to Generic config
                }
            }

            // Fall back to Generic defaults
            var genericConfig = _configManager.GetGenericConfig();
            if (genericConfig.Sprites != null)
            {
                var genericSprite = GetSpriteForTask(genericConfig.Sprites, taskType, npc);
                if (genericSprite != null)
                {
                    return genericSprite;
                }
            }

            _monitor.LogOnce($"[Sprite] No sprite config found for {npc.Name} - {taskType}", LogLevel.Trace);
            return null;
        }

        /// <summary>
        /// Extracts the sprite configuration for a specific task from the SpriteConfig object.
        /// </summary>
        private object GetTaskSpriteData(SpriteConfig spriteConfig, string taskType)
        {
            return taskType switch
            {
                "Attacking" => spriteConfig.Attacking,
                "Mining" => spriteConfig.Mining,
                "Fishing" => spriteConfig.Fishing,
                "Watering" => spriteConfig.Watering,
                "Lumbering" => spriteConfig.Lumbering,
                "Harvesting" => spriteConfig.Harvesting,
                "Foraging" => spriteConfig.Foraging,
                "Idle" => spriteConfig.Idle,
                "Sitting" => spriteConfig.Sitting,
                "Petting" => spriteConfig.Petting,
                "Shearing" => spriteConfig.Shearing,
                "Milking" => spriteConfig.Milking,
                _ => null
            };
        }

        /// <summary>
        /// Gets the sprite animation config for a task.
        /// Implements first-match-wins logic for conditional sprite arrays.
        /// </summary>
        private SpriteAnimationConfig GetSpriteForTask(SpriteConfig spriteConfig, string taskType, NPC npc)
        {
            var taskSpriteData = GetTaskSpriteData(spriteConfig, taskType);
            if (taskSpriteData == null)
                return null;

            // Case 1: Simple object (single sprite config, no conditions)
            if (taskSpriteData is JObject simpleObj && !simpleObj.ContainsKey("Condition"))
            {
                try
                {
                    var config = simpleObj.ToObject<SpriteAnimationConfig>();
                    if (config != null)
                    {
                        _monitor.LogOnce($"[Sprite] Found simple sprite object for {taskType}", LogLevel.Trace);
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[Sprite] Error parsing simple sprite object for {taskType}: {ex.Message}", LogLevel.Warn);
                }
                return null;
            }

            // Case 2: Priority array (first-match-wins)
            if (taskSpriteData is JArray spriteArray)
            {
                _monitor.LogOnce($"[Sprite] Evaluating sprite array for {taskType} (first-match-wins)", LogLevel.Trace);

                foreach (var item in spriteArray)
                {
                    if (item is JObject jObj)
                    {
                        try
                        {
                            var condition = jObj["Condition"]?.ToString();

                            // Replace {CurrentNpc} placeholder with actual NPC name
                            if (!string.IsNullOrWhiteSpace(condition) && condition.Contains("{CurrentNpc}"))
                            {
                                condition = condition.Replace("{CurrentNpc}", npc.Name);
                                _monitor.LogOnce($"[Sprite] Replaced {{CurrentNpc}} placeholder: {condition}", LogLevel.Trace);
                            }

                            // No condition = default/fallback (always matches)
                            bool conditionMet = string.IsNullOrWhiteSpace(condition) ||
                                              _gameStateChecker.CheckConditions(condition, _gameContext.CurrentLocation, _gameContext.Player);

                            if (conditionMet)
                            {
                                var config = jObj.ToObject<SpriteAnimationConfig>();
                                if (config != null)
                                {
                                    _monitor.LogOnce($"[Sprite] Matched sprite config for {taskType} with condition: {condition ?? "(none)"}", LogLevel.Trace);
                                    return config; // First match wins!
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _monitor.Log($"[Sprite] Error processing sprite array entry for {taskType}: {ex.Message}", LogLevel.Warn);
                        }
                    }
                }
            }

            // No match found
            return null;
        }

        /// <summary>
        /// Checks if a sprite configuration has an extension sheet defined.
        /// </summary>
        public bool HasExtensionSheet(SpriteAnimationConfig config)
        {
            return config != null && !string.IsNullOrWhiteSpace(config.ExtensionSheet);
        }

        /// <summary>
        /// Gets the frame indices and flip states for a specific direction from a sprite config.
        /// Supports mixed array formats: simple integers, frame objects with flip, or a mix of both.
        /// </summary>
        /// <returns>List of tuples containing (frameIndex, flip) for each frame, or null if not found.</returns>
        public List<(int frameIndex, bool flip)> GetFramesForDirection(SpriteAnimationConfig config, int facingDirection)
        {
            if (config?.FramesByDirection == null)
                return null;

            string directionKey = facingDirection switch
            {
                0 => "Up",
                1 => "Right",
                2 => "Down",
                3 => "Left",
                _ => "Down" // Default fallback
            };

            if (!config.FramesByDirection.TryGetValue(directionKey, out var frameData))
            {
                // Fallback to Down direction if current direction not found
                if (!config.FramesByDirection.TryGetValue("Down", out frameData))
                    return null;

                _monitor.LogOnce($"[Sprite] Direction {directionKey} not found, using Down as fallback", LogLevel.Trace);
            }

            var result = new List<(int, bool)>();

            foreach (var item in frameData)
            {
                if (item is long || item is int)
                {
                    // Simple integer frame - no flip
                    result.Add((Convert.ToInt32(item), false));
                }
                else if (item is Newtonsoft.Json.Linq.JObject jObj)
                {
                    // Frame object with optional flip
                    int frame = jObj["Frame"]?.ToObject<int>() ?? 0;
                    bool flip = jObj["Flip"]?.ToObject<bool>() ?? false;
                    result.Add((frame, flip));
                }
                else
                {
                    _monitor.Log($"[Sprite] Unexpected frame data type: {item?.GetType().Name}", LogLevel.Warn);
                }
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Applies a task animation to an NPC, using custom sprite config if available,
        /// otherwise falling back to vanilla frames.
        /// Supports horizontal sprite flipping via the flip parameter in animation frames.
        /// </summary>
        /// <param name="npc">The NPC to animate</param>
        /// <param name="taskType">The task type (Attacking, Mining, etc.)</param>
        /// <param name="frameDuration">Default frame duration in milliseconds (used for fallback)</param>
        /// <param name="mate">Optional mate context. When non-null and called on the host, broadcasts a PlayTaskAnim so peers replay the same animation locally.</param>
        public void ApplyTaskAnimation(NPC npc, string taskType, int frameDuration = 400, Squad.ISquadMate? mate = null)
        {
            // Host broadcasts so peers replay; vanilla doesn't propagate Sprite.CurrentAnimation.
            if (mate != null && Context.IsMainPlayer)
                _dispatcher?.BroadcastPlayTaskAnim(mate, taskType, npc.FacingDirection, mate.AppliedTaskTexture);

            // Try to get custom sprite configuration
            var spriteConfig = GetTaskSpriteConfig(npc, taskType);

            if (spriteConfig != null)
            {
                // Custom sprite config found - use configured frames
                var frames = GetFramesForDirection(spriteConfig, npc.FacingDirection);

                if (frames != null && frames.Count > 0)
                {
                    var animationFrames = new List<FarmerSprite.AnimationFrame>();
                    int[] durations = spriteConfig.FrameDuration ?? new[] { frameDuration };
                    bool perFrame = durations.Length == frames.Count;

                    // Create animation frames from config with flip support
                    for (int i = 0; i < frames.Count; i++)
                    {
                        var (frameIndex, flip) = frames[i];
                        bool isLastFrame = (i == frames.Count - 1);
                        int thisDuration = perFrame ? durations[i] : durations[0];

                        if (isLastFrame && spriteConfig.Loop)
                        {
                            animationFrames.Add(new FarmerSprite.AnimationFrame(
                                frameIndex,
                                thisDuration,
                                false, // secondaryArm
                                flip,
                                (_) => npc.Sprite.setCurrentAnimation(animationFrames) // end function callback
                            ));
                        }
                        else if (isLastFrame)
                        {
                            // Loop: false — pin the last frame so Stardew's animator doesn't wrap back to frame 0.
                            int freezeFrame = frameIndex;
                            animationFrames.Add(new FarmerSprite.AnimationFrame(
                                frameIndex,
                                thisDuration,
                                false, // secondaryArm
                                flip,
                                (_) =>
                                {
                                    npc.Sprite.StopAnimation();
                                    npc.Sprite.currentFrame = freezeFrame;
                                    npc.Sprite.CurrentFrame = freezeFrame;
                                },
                                behaviorAtEndOfFrame: true
                            ));
                        }
                        else
                        {
                            animationFrames.Add(new FarmerSprite.AnimationFrame(
                                frameIndex,
                                thisDuration,
                                false, // secondaryArm
                                flip
                            ));
                        }
                    }

                    npc.Sprite.setCurrentAnimation(animationFrames);
                    _monitor.LogOnce($"[TaskAnimation] Applied custom sprite animation for {npc.Name} - {taskType} ({frames.Count} frames)", LogLevel.Trace);
                    return;
                }
            }

            // Fallback to vanilla frames
            int baseFrame = GetAnimationBaseFrame(npc);
            npc.Sprite.setCurrentAnimation(new List<FarmerSprite.AnimationFrame>
            {
                new(baseFrame, frameDuration),
                new(baseFrame - 1, frameDuration)
            });
            // Vanilla setCurrentAnimation does not reset Sprite.loop (default true). Without
            // this, the [walk, stand] fallback loops forever on peers; with ClearTaskAnim
            // no longer broadcast for non-sustained tasks, the animation must self-terminate
            // so the NPC reverts to its prior frame.
            npc.Sprite.loop = false;
        }

        /// <summary>
        /// Forces the correct task sprite on an NPC using manual frame management.
        /// This is used for continuous tasks (like Sitting and Fishing) where frames need to be
        /// consistently updated each tick rather than using the animation system.
        /// Handles frame cycling and sprite flipping properly for all NPC types.
        /// </summary>
        /// <param name="npc">The NPC to apply sprite to</param>
        /// <param name="taskType">The task type (Sitting, Fishing, etc.)</param>
        public void ForceApplyTaskAnimation(NPC npc, string taskType)
        {
            // Get the sprite config to determine which frames to use
            var spriteConfig = GetTaskSpriteConfig(npc, taskType);
            if (spriteConfig == null)
                return;

            var frames = GetFramesForDirection(spriteConfig, npc.FacingDirection);
            if (frames == null || frames.Count == 0)
                return;

            // Calculate which frame we should be on based on time
            int frameCount = frames.Count;
            int[] durations = spriteConfig.FrameDuration ?? new[] { 400 };
            long elapsedMs = (long)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;

            int frameIndex;
            if (durations.Length == frameCount)
            {
                // Per-frame durations: find which frame we're in within the cycle
                int cycleMs = 0;
                for (int i = 0; i < durations.Length; i++) cycleMs += durations[i];
                long within = elapsedMs % cycleMs;
                frameIndex = 0;
                long acc = 0;
                for (int i = 0; i < durations.Length; i++)
                {
                    acc += durations[i];
                    if (within < acc) { frameIndex = i; break; }
                }
            }
            else
            {
                // Single duration applied to all frames
                frameIndex = (int)((elapsedMs / durations[0]) % frameCount);
            }

            var (targetFrame, flip) = frames[frameIndex];

            // Manually set the sprite frame (bypassing animation system)
            npc.Sprite.currentFrame = targetFrame;
            npc.Sprite.CurrentFrame = targetFrame;

            // Apply horizontal flipping (NPC.flip property exists on all NPCs)
            npc.flip = flip;
        }

        /// <summary>Gets the base animation frame for an NPC based on their facing direction.</summary>
        private static int GetAnimationBaseFrame(NPC npc)
        {
            return npc.FacingDirection switch
            {
                0 => 9,  // Up
                1 => 5,  // Right
                2 => 1,  // Down
                3 => 13, // Left
                _ => 1   // Default
            };
        }

        #region ExtensionSheet Variant Detection

        /// <summary>
        /// Resolves the full ExtensionSheet path with auto-variant detection.
        /// Tries loading {ExtensionSheet}{variantSuffix} first, falls back to base ExtensionSheet.
        /// </summary>
        /// <param name="baseExtensionSheet">The base ExtensionSheet path from NpcConfig</param>
        /// <param name="npc">The NPC (used for name and texture variant detection)</param>
        /// <returns>The resolved asset path, or null if neither variant nor base exists</returns>
        public string? ResolveExtensionSheetWithVariant(string baseExtensionSheet, NPC npc)
        {
            if (string.IsNullOrEmpty(baseExtensionSheet))
                return null;

            // Detect NPC's texture variant suffix (e.g., "Abigail_Winter" -> "_Winter")
            string npcTextureName = npc.getTextureName();
            string? variantSuffix = ExtractVariantSuffix(npc.Name, npcTextureName);

            // Try loading variant path first
            if (!string.IsNullOrEmpty(variantSuffix))
            {
                string variantPath = baseExtensionSheet + variantSuffix;
                if (Game1.content.DoesAssetExist<Microsoft.Xna.Framework.Graphics.Texture2D>(variantPath))
                {
                    _monitor.LogOnce($"[Sprite] Found variant ExtensionSheet: {variantPath}", LogLevel.Trace);
                    return variantPath;
                }
            }

            // Fall back to base ExtensionSheet
            if (Game1.content.DoesAssetExist<Microsoft.Xna.Framework.Graphics.Texture2D>(baseExtensionSheet))
            {
                _monitor.LogOnce($"[Sprite] Using base ExtensionSheet: {baseExtensionSheet}", LogLevel.Trace);
                return baseExtensionSheet;
            }

            _monitor.LogOnce($"[Sprite] ExtensionSheet not found: {baseExtensionSheet}", LogLevel.Trace);
            return null;
        }

        /// <summary>
        /// Extracts the variant suffix from an NPC's texture name.
        /// Example: npcName="Abigail", textureName="Abigail_Winter" -> "_Winter"
        /// </summary>
        private static string? ExtractVariantSuffix(string npcName, string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || string.IsNullOrEmpty(npcName))
                return null;

            // If texture name is longer than NPC name and starts with it, extract suffix
            if (textureName.Length > npcName.Length && textureName.StartsWith(npcName, StringComparison.OrdinalIgnoreCase))
            {
                return textureName[npcName.Length..]; // e.g., "_Winter", "_Beach"
            }

            return null;
        }

        #endregion

        #region Task Sprite Sheet Methods

        /// <summary>
        /// Attempts to apply a task-specific sprite sheet to the NPC using NpcConfig.
        /// Uses ExtensionSheet from the task's sprite config with auto-variant detection.
        /// Works for any task type (Sitting, Attacking, Mining, etc.).
        /// </summary>
        /// <param name="npc">The NPC to apply the sprite sheet to</param>
        /// <param name="mate">The squad mate (for tracking original texture)</param>
        /// <param name="taskType">The task type (Sitting, Attacking, Mining, etc.)</param>
        /// <param name="facingDirection">The direction the NPC is facing (0=Up, 1=Right, 2=Down, 3=Left)</param>
        /// <returns>True if custom sprite sheet was applied, false otherwise</returns>
        public bool TryApplyTaskSpriteSheet(NPC npc, Squad.ISquadMate mate, string taskType, int facingDirection)
        {
            // Already using a custom sprite sheet AND no other mod has replaced
            // the NPC's texture since we set it:
            // When the texture has been replaced under us, re-apply once.
            if (!string.IsNullOrEmpty(mate.OriginalTexture))
            {
                if (string.IsNullOrEmpty(mate.AppliedTaskTexture)
                    || string.Equals(npc.Sprite?.Texture?.Name, mate.AppliedTaskTexture, StringComparison.OrdinalIgnoreCase))
                {
                    SetTaskFrame(npc, taskType, facingDirection);
                    return true;
                }
                _monitor.Log($"[Sprite] {npc.Name}'s {taskType} sprite was overwritten externally (now '{npc.Sprite?.Texture?.Name}'); re-applying.", LogLevel.Trace);
                // Keep OriginalTexture so RestoreOriginalTexture still has a target — only the loaded sheet is stale.
            }

            // Get sprite config for this task from NpcConfig
            var spriteConfig = GetTaskSpriteConfig(npc, taskType);
            if (spriteConfig == null || !HasExtensionSheet(spriteConfig))
            {
                _monitor.LogOnce($"[Sprite] No {taskType} sprite config with ExtensionSheet for {npc.Name}", LogLevel.Trace);
                return false;
            }

            // Resolve ExtensionSheet path with auto-variant detection
            string? assetPath = ResolveExtensionSheetWithVariant(spriteConfig.ExtensionSheet, npc);
            if (assetPath == null)
            {
                _monitor.LogOnce($"[Sprite] No {taskType} sprite found for {npc.Name}", LogLevel.Trace);
                return false;
            }

            // Store the original texture path for restoration (pet-aware).
            // Skip if already set — re-applying after an external replacement must keep the existing restore target.
            if (string.IsNullOrEmpty(mate.OriginalTexture))
            {
                mate.OriginalTexture = NpcTextureHelper.GetTextureAssetPath(npc);
            }

            // Load the task sprite sheet
            if (npc.TryLoadSprites(assetPath, out string error))
            {
                _monitor.Log($"[Sprite] Applied {taskType} sprite sheet for {npc.Name}: {assetPath}", LogLevel.Trace);
                mate.AppliedTaskTexture = assetPath;

                // Set the correct frame based on direction using NpcConfig
                SetTaskFrame(npc, taskType, facingDirection);

                // Host broadcasts the texture swap so peers load the same sheet locally.
                if (Context.IsMainPlayer)
                    _dispatcher?.BroadcastPlayTaskAnim(mate, taskType, facingDirection, assetPath);

                return true;
            }
            else
            {
                _monitor.Log($"[Sprite] Failed to load {taskType} sprite for {npc.Name}: {error}", LogLevel.Warn);
                // Only clear OriginalTexture on a first-time failure. If a prior application succeeded
                // and we're failing to re-apply after an external texture replacement, keep the restore target intact.
                if (string.IsNullOrEmpty(mate.AppliedTaskTexture))
                {
                    mate.OriginalTexture = null;
                }
                return false;
            }
        }

        /// <summary>
        /// Convenience wrapper for sitting sprites (maintains backward compatibility).
        /// </summary>
        public bool TryApplySittingSpriteSheet(NPC npc, Squad.ISquadMate mate, int sittingDirection)
        {
            return TryApplyTaskSpriteSheet(npc, mate, "Sitting", sittingDirection);
        }

        /// <summary>
        /// Sets the sprite frame for a task based on direction using NpcConfig.
        /// Uses FramesByDirection from the task's sprite config with flip support.
        /// </summary>
        /// <param name="npc">The NPC to set the frame for</param>
        /// <param name="taskType">The task type (Sitting, Attacking, Mining, etc.)</param>
        /// <param name="facingDirection">The direction the NPC is facing (0=Up, 1=Right, 2=Down, 3=Left)</param>
        public void SetTaskFrame(NPC npc, string taskType, int facingDirection)
        {
            // Get sprite config for this task from NpcConfig
            var spriteConfig = GetTaskSpriteConfig(npc, taskType);

            if (spriteConfig?.FramesByDirection != null)
            {
                // Use NpcConfig frame mapping with flip support
                var frames = GetFramesForDirection(spriteConfig, facingDirection);
                if (frames != null && frames.Count > 0)
                {
                    var (frameIndex, flip) = frames[0]; // Use first frame
                    npc.Sprite.currentFrame = frameIndex;
                    npc.Sprite.CurrentFrame = frameIndex;
                    npc.flip = flip;
                    return;
                }
            }

            // Fallback to default idle frame based on direction
            int defaultFrame = facingDirection switch
            {
                0 => 8,   // Up
                1 => 4,   // Right
                2 => 0,   // Down
                3 => 12,  // Left
                _ => 0    // Default to Down
            };

            npc.Sprite.currentFrame = defaultFrame;
            npc.Sprite.CurrentFrame = defaultFrame;
            npc.flip = false;
        }

        /// <summary>
        /// Restores the NPC's original texture after a task with custom sprite sheet.
        /// </summary>
        /// <param name="npc">The NPC to restore</param>
        /// <param name="mate">The squad mate (for retrieving original texture)</param>
        public void RestoreOriginalTexture(NPC npc, Squad.ISquadMate mate)
        {
            if (string.IsNullOrEmpty(mate.OriginalTexture))
            {
                return; // Nothing to restore
            }

            string originalTexture = mate.OriginalTexture;
            mate.OriginalTexture = null; // Clear before restoring
            mate.AppliedTaskTexture = null;

            if (npc.TryLoadSprites(originalTexture, out string error))
            {
                _monitor.Log($"[Sprite] Restored original texture for {npc.Name}: {originalTexture}", LogLevel.Trace);
            }
            else
            {
                _monitor.Log($"[Sprite] Failed to restore original texture for {npc.Name}: {error}", LogLevel.Warn);
            }
        }

        #endregion
    }
}
