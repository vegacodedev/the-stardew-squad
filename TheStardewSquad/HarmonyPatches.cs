using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using TheStardewSquad.Framework;
using TheStardewSquad.Framework.Gathering;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Patches
{
    internal static class HarmonyPatches
    {
        #region Fields

        private static InteractionManager _interactionManager;
        private static SquadManager _squadManager;
        private static ModConfig _config;
        private static FollowerManager _followerManager;
        private static DebrisCollector _debrisCollector;
        private static ModEntry _modEntry;
        private static int _lastPlayerPettingTick = 0;
        private static int _lastPlayerHarvestingTick = 0;
        private static int _ignoreHarvestDepth = 0;
        private static int _ignorePettingDepth = 0;
        private static long? _playerSittingStartTime = null;
        private static bool _wasPlayerSitting = false;

        #endregion

        #region Initialization and Utility Methods

        public static void Initialize(InteractionManager interactionManager, SquadManager squadManager, ModConfig config, FollowerManager followerManager, DebrisCollector debrisCollector, ModEntry modEntry)
        {
            _interactionManager = interactionManager;
            _squadManager = squadManager;
            _config = config;
            _followerManager = followerManager;
            _debrisCollector = debrisCollector;
            _modEntry = modEntry;
        }

        /// <summary>Gets the last game tick when the player petted an animal.</summary>
        public static int GetLastPlayerPettingTick() => _lastPlayerPettingTick;

        /// <summary>Gets the last game tick when the player harvested a crop.</summary>
        public static int GetLastPlayerHarvestingTick() => _lastPlayerHarvestingTick;

        /// <summary>Begins ignoring harvest actions (for NPC harvests). Use in try/finally with EndIgnoreHarvest().</summary>
        public static void BeginIgnoreHarvest() => _ignoreHarvestDepth++;

        /// <summary>Ends ignoring harvest actions. Always call in finally block.</summary>
        public static void EndIgnoreHarvest() => _ignoreHarvestDepth = Math.Max(0, _ignoreHarvestDepth - 1);

        /// <summary>Begins ignoring petting actions (for NPC petting). Use in try/finally with EndIgnorePetting().</summary>
        public static void BeginIgnorePetting() => _ignorePettingDepth++;

        /// <summary>Ends ignoring petting actions. Always call in finally block.</summary>
        public static void EndIgnorePetting() => _ignorePettingDepth = Math.Max(0, _ignorePettingDepth - 1);

        /// <summary>
        /// Updates player sitting state tracking. Call this from FollowerManager.OnUpdateTicked
        /// to ensure sitting start time is recorded when player actually sits down.
        /// </summary>
        public static void UpdatePlayerSittingState()
        {
            var player = Game1.player;
            if (player == null) return;

            bool isCurrentlySitting = player.sittingFurniture != null || player.isSitting.Value;

            // Detect when player starts sitting
            if (isCurrentlySitting && !_wasPlayerSitting)
            {
                _playerSittingStartTime = (long)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
            }
            // Detect when player stops sitting
            else if (!isCurrentlySitting && _wasPlayerSitting)
            {
                _playerSittingStartTime = null;
            }

            _wasPlayerSitting = isCurrentlySitting;
        }

        /// <summary>Logs all Harmony patches applied to a method for debugging conflicts.</summary>
        public static void LogPatchInfo(Harmony harmony, MethodBase method, string methodName)
        {
            var patches = Harmony.GetPatchInfo(method);
            if (patches == null)
            {
                _modEntry.Monitor.Log($"No patches found for {methodName}", StardewModdingAPI.LogLevel.Debug);
                return;
            }

            _modEntry.Monitor.Log($"=== Patches for {methodName} ===", StardewModdingAPI.LogLevel.Info);

            if (patches.Prefixes.Any())
            {
                _modEntry.Monitor.Log($"Prefixes ({patches.Prefixes.Count}):", StardewModdingAPI.LogLevel.Info);
                foreach (var patch in patches.Prefixes)
                {
                    _modEntry.Monitor.Log($"  - {patch.owner} (Priority: {patch.priority}, Index: {patch.index})", StardewModdingAPI.LogLevel.Info);
                    _modEntry.Monitor.Log($"    Method: {patch.PatchMethod.DeclaringType?.FullName}.{patch.PatchMethod.Name}", StardewModdingAPI.LogLevel.Debug);
                }
            }

            if (patches.Postfixes.Any())
            {
                _modEntry.Monitor.Log($"Postfixes ({patches.Postfixes.Count}):", StardewModdingAPI.LogLevel.Info);
                foreach (var patch in patches.Postfixes)
                {
                    _modEntry.Monitor.Log($"  - {patch.owner} (Priority: {patch.priority})", StardewModdingAPI.LogLevel.Info);
                }
            }

            if (patches.Transpilers.Any())
            {
                _modEntry.Monitor.Log($"Transpilers ({patches.Transpilers.Count}):", StardewModdingAPI.LogLevel.Info);
                foreach (var patch in patches.Transpilers)
                {
                    _modEntry.Monitor.Log($"  - {patch.owner} (Priority: {patch.priority})", StardewModdingAPI.LogLevel.Info);
                }
            }

            if (patches.Finalizers.Any())
            {
                _modEntry.Monitor.Log($"Finalizers ({patches.Finalizers.Count}):", StardewModdingAPI.LogLevel.Info);
                foreach (var patch in patches.Finalizers)
                {
                    _modEntry.Monitor.Log($"  - {patch.owner} (Priority: {patch.priority})", StardewModdingAPI.LogLevel.Info);
                }
            }
        }

        public static void Apply(Harmony harmony)
        {
            harmony.Patch(
                original: AccessTools.PropertyGetter(typeof(NPC), nameof(NPC.IsVillager)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(IsVillager_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.pressUseToolButton)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(PressUseToolButton_Prefix)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(PressUseToolButton_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CheckAction_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Utility), nameof(Utility.checkForCharacterInteractionAtTile)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(CheckForCharacterInteractionAtTile_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.returnHomeFromFarmPosition)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(ReturnHomeFromFarmPosition_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Pet), nameof(Pet.RunState)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(RunState_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Pet), nameof(Pet.warpToFarmHouse)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(WarpToFarmHouse_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.CheckGarbage)),
                transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(CheckGarbage_Transpiler))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Debris), nameof(Debris.updateChunks)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Debris_UpdateChunks_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.getHitByPlayer)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(GetHitByPlayer_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.update), new Type[] { typeof(GameTime), typeof(GameLocation) }),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(NPC_Update_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Pet), nameof(Pet.draw), new Type[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Pet_Draw_Prefix)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Pet_Draw_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(BathHousePool), nameof(BathHousePool.draw)),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(BathHousePool_Draw_Prefix)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(BathHousePool_Draw_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Tools.FishingRod), nameof(StardewValley.Tools.FishingRod.playerCaughtFishEndFunction)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(PlayerCaughtFishEndFunction_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(FarmAnimal), nameof(FarmAnimal.pet)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(FarmAnimal_Pet_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Pet), nameof(Pet.checkAction)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Pet_CheckAction_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Crop), nameof(Crop.harvest)),
                postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Crop_Harvest_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.draw), new Type[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch), typeof(float) }),
                prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(NPC_Draw_Prefix))
            );

            // Patch Farmer.StopSitting to prevent involuntary ejection when NPCs walk nearby
            var stopSittingMethod = AccessTools.Method(typeof(Farmer), nameof(Farmer.StopSitting));
            if (stopSittingMethod != null)
            {
                harmony.Patch(
                    original: stopSittingMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Farmer_StopSitting_Prefix))
                    {
                        priority = Priority.High  // Run before other mods to catch involuntary ejections. Should play nice with PreciseFurniture that way
                    }
                );
            }
            else
            {
                _modEntry.Monitor.Log("Failed to find Farmer.StopSitting() method", StardewModdingAPI.LogLevel.Error);
            }

            // Patch Furniture.HasSittingFarmers() to also check for sitting NPCs
            var furnitureMethod = AccessTools.Method(typeof(StardewValley.Objects.Furniture), nameof(StardewValley.Objects.Furniture.HasSittingFarmers));
            if (furnitureMethod != null)
            {
                harmony.Patch(
                    original: furnitureMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(Furniture_HasSittingFarmers_Postfix))
                );
            }
            else
            {
                _modEntry.Monitor.Log("Failed to find Furniture.HasSittingFarmers() method", StardewModdingAPI.LogLevel.Error);
            }

            // Patch MapSeat.HasSittingFarmers() to also check for sitting NPCs
            var mapSeatMethod = AccessTools.Method(typeof(StardewValley.MapSeat), nameof(StardewValley.MapSeat.HasSittingFarmers));
            if (mapSeatMethod != null)
            {
                harmony.Patch(
                    original: mapSeatMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(MapSeat_HasSittingFarmers_Postfix))
                );
            }
            else
            {
                _modEntry.Monitor.Log("Failed to find MapSeat.HasSittingFarmers() method", StardewModdingAPI.LogLevel.Error);
            }

            // Patch MapSeat.Draw() to debug and modify rendering
            var mapSeatDrawMethod = AccessTools.Method(typeof(StardewValley.MapSeat), "Draw", new Type[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) });
            if (mapSeatDrawMethod != null)
            {
                harmony.Patch(
                    original: mapSeatDrawMethod,
                    prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(MapSeat_Draw_Prefix))
                );
            }
            else
            {
                _modEntry.Monitor.Log("Failed to find MapSeat.Draw() method", StardewModdingAPI.LogLevel.Error);
            }

            // Patch NPC.draw() to adjust layer depth for sitting NPCs
            var npcDrawMethod = AccessTools.Method(typeof(StardewValley.NPC), nameof(StardewValley.NPC.draw), new Type[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch), typeof(float) });
            if (npcDrawMethod != null)
            {
                harmony.Patch(
                    original: npcDrawMethod,
                    transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(NPC_Draw_LogDepth_Transpiler))
                );
            }

            // Patch Pet.draw() to adjust layer depth for sitting pets
            // Pet overrides draw(SpriteBatch) with 1 parameter, not draw(SpriteBatch, float)
            var petDrawMethod = AccessTools.Method(typeof(Pet), nameof(Pet.draw), new Type[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch) });
            if (petDrawMethod != null)
            {
                harmony.Patch(
                    original: petDrawMethod,
                    transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(NPC_Draw_LogDepth_Transpiler))
                );
            }
        }

        #endregion

        #region Debris Collection Patches

        /// <summary>
        /// This prefix patch intercepts the update logic for items on the ground (Debris).
        /// If an item is being targeted by a squad member, this patch takes over its movement,
        /// making it fly towards the NPC. It then handles the collection.
        /// </summary>
        /// <param name="__instance">The Debris instance being updated.</param>
        /// <returns>
        /// 'false' to skip the original game logic for this debris instance (we're in control).
        /// 'true' to let the original game logic run as normal.
        /// </returns>
        private static bool Debris_UpdateChunks_Prefix(Debris __instance)
        {
            // If we don't have a DebrisCollector, or the menu is open, do nothing.
            if (_debrisCollector == null || Game1.activeClickableMenu is not null)
                return true;

            // Check if our DebrisCollector has assigned this specific debris to an NPC.
            if (_debrisCollector.TryGetTarget(__instance, out ISquadMate mate))
            {
                // This debris is ours to control.
                var chunk = __instance.Chunks.FirstOrDefault();
                if (chunk == null)
                {
                    // No chunk to move, let the game clean it up.
                    _debrisCollector.RemoveTarget(__instance);
                    return true;
                }

                // This is the homing logic, adapted from Debris.cs, but targeting the NPC.
                Vector2 targetPosition = mate.Npc.Position;

                if (chunk.position.X < targetPosition.X - 12f)
                    chunk.xVelocity.Value = Math.Min(chunk.xVelocity.Value + 0.8f, 8f);
                else if (chunk.position.X > targetPosition.X + 12f)
                    chunk.xVelocity.Value = Math.Max(chunk.xVelocity.Value - 0.8f, -8f);

                int targetStandingY = mate.Npc.StandingPixel.Y;
                if (chunk.position.Y + 32f < (float)(targetStandingY - 12))
                    chunk.yVelocity.Value = Math.Max(chunk.yVelocity.Value - 0.8f, -8f);
                else if (chunk.position.Y + 32f > (float)(targetStandingY + 12))
                    chunk.yVelocity.Value = Math.Min(chunk.yVelocity.Value + 0.8f, 8f);

                chunk.position.X += chunk.xVelocity.Value;
                chunk.position.Y -= chunk.yVelocity.Value;

                // Check if the debris is close enough to be collected.
                if (Math.Abs(chunk.position.X + 32f - mate.Npc.StandingPixel.X) <= 64f &&
                    Math.Abs(chunk.position.Y + 32f - mate.Npc.StandingPixel.Y) <= 64f)
                {
                    // Tell the DebrisCollector to perform the collection logic.
                    _debrisCollector.CollectDebris(__instance);
                }

                // We handled this debris, so skip the original method.
                return false;
            }

            // This debris is not targeted by our mod, so let the game handle it.
            return true;
        }

        #endregion

        #region NPC Interaction Patches

        /// <summary>Prevents talking/gifting followers in combat zones if the setting is enabled.</summary>
        private static bool CheckAction_Prefix(NPC __instance, Farmer who)
        {
            if (!_squadManager.IsRecruited(__instance))
                return true;

            // Check DisableInteraction config FIRST (before any exceptions)
            switch (_config.DisableInteraction)
            {
                case InteractionPreventionMode.Always:
                    return false;

                case InteractionPreventionMode.CombatOnly:
                    if (Game1.currentLocation is MineShaft or VolcanoDungeon)
                        return false;
                    break;

                case InteractionPreventionMode.Never:
                default:
                    break;
            }

            // Special handling for kissing a recruited spouse (only runs if interaction allowed per config)
            if (__instance.isMarried() &&
                __instance.getSpouse() == who &&
                !__instance.hasTemporaryMessageAvailable() &&
                !__instance.isMoving())
            {
                // Put the follower on a short cooldown to allow the animation to play
                // without being interrupted by the FollowerManager's logic.
                var mate = _squadManager.GetMember(__instance);
                if (mate != null)
                {
                    mate.ActionCooldown = 60; // 1 second
                }
            }

            return true;
        }

        /// <summary>Prevents the talk/gift cursor from appearing over followers in combat zones if the setting is enabled.</summary>
        private static bool CheckForCharacterInteractionAtTile_Prefix(Vector2 tileLocation)
        {
            var character = Game1.currentLocation.isCharacterAtTile(tileLocation);
            if (character is not NPC npc || !_squadManager.IsRecruited(npc))
                return true;

            switch (_config.DisableInteraction)
            {
                case InteractionPreventionMode.Always:
                    return false;

                case InteractionPreventionMode.CombatOnly:
                    if (Game1.currentLocation is MineShaft or VolcanoDungeon)
                        return false;
                    return true;

                case InteractionPreventionMode.Never:
                default:
                    return true;
            }
        }

        /// <summary>Prevents a recruited spouse from pathing home at 1 PM when they are on the farm porch. This caused their currentLocation to change and led to a bug about them warping back to the player unnecessarily.</summary>
        private static bool ReturnHomeFromFarmPosition_Prefix(NPC __instance)
        {
            // If the NPC (spouse) is recruited, block them from pathing home automatically.
            if (_squadManager.IsRecruited(__instance))
            {
                return false;
            }

            return true;
        }

        private static void PressUseToolButton_Prefix()
        {
            _interactionManager.PlayerIsAttemptingToUseTool = true;
        }

        private static void PressUseToolButton_Postfix()
        {
            _interactionManager.PlayerIsAttemptingToUseTool = false;
        }

        /// <summary>
        /// This patch exists to bypass the restriction where the player cannot use a tool/weapon when facing a villager NPC (for some reason).
        /// It's kind of dirty, but you gotta do what you gotta do
        /// </summary>
        private static void IsVillager_Postfix(NPC __instance, ref bool __result)
        {
            // If the game already determined this isn't a villager, do nothing.
            if (!__result)
                return;

            // If the player is using a tool/weapon and the NPC is recruited
            if (_interactionManager.PlayerIsAttemptingToUseTool && _squadManager.IsRecruited(__instance))
            {
                // Set IsVillager to false
                __result = false;
            }
        }

        /// <summary>
        /// Prevents the player from being forced to stop sitting when squad members are nearby.
        /// Allows voluntary standing (player input) while blocking involuntary ejection (NPC proximity).
        /// Also enforces a minimum sitting duration (0.5 seconds) to prevent accidental immediate standing.
        /// Uses Priority.High to run before other mods' StopSitting patches (e.g., PreciseFurniture).
        /// </summary>
        private static bool Farmer_StopSitting_Prefix(Farmer __instance, bool animate)
        {
            // Early exit if no squad members exist - no need to check for involuntary ejection
            if (_squadManager == null || !_squadManager.Members.Any())
                return true;

            // Enforce minimum sitting duration (0.1 seconds = 100ms)
            // If timer is null (slow tick hasn't run yet), treat as "just sat down" and block
            if (!_playerSittingStartTime.HasValue)
            {
                return false; // Timer not set yet - block until slow tick runs
            }

            long currentTime = (long)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
            long elapsedTime = currentTime - _playerSittingStartTime.Value;

            if (elapsedTime < 100) // Less than 0.1 seconds has passed
            {
                return false; // Block StopSitting - too soon (involuntary)
            }

            // Check if any recruited NPCs are in the current location
            bool hasRecruitedNPCsNearby = _squadManager.Members
                .Any(mate => mate.Npc.currentLocation == Game1.currentLocation);

            if (!hasRecruitedNPCsNearby)
                return true; // No squad members nearby, allow normal standing

            // Check if player is actively providing input (trying to move or act)
            // Voluntary standing happens when player presses movement keys or action button
            var input = Game1.input.GetGamePadState();
            var keyboardState = Game1.input.GetKeyboardState();
            var mouseState = Game1.input.GetMouseState();

            bool hasPlayerInput =
                // Mouse/Touch input (mobile taps are translated to left mouse button)
                mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ||
                mouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed ||
                // Action buttons
                keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C) ||
                keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.X) ||
                // Gamepad movement
                input.ThumbSticks.Left.Length() > 0.2f ||
                input.DPad.Up == Microsoft.Xna.Framework.Input.ButtonState.Pressed ||
                input.DPad.Down == Microsoft.Xna.Framework.Input.ButtonState.Pressed ||
                input.DPad.Left == Microsoft.Xna.Framework.Input.ButtonState.Pressed ||
                input.DPad.Right == Microsoft.Xna.Framework.Input.ButtonState.Pressed ||
                // Gamepad action buttons
                input.Buttons.A == Microsoft.Xna.Framework.Input.ButtonState.Pressed ||
                input.Buttons.X == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            if (!hasPlayerInput)
            {
                // No player input - this is an involuntary ejection from NPC proximity
                return false; // Block StopSitting to prevent ejection
            }

            // Player is intentionally standing - allow it (and let other mods' patches handle the details)
            return true;
        }

        #endregion

        #region Pet Behavior Patches

        /// <summary>
        /// This patch prevents pets from trying to move while they're recruited, which avoids sprite flickers.
        /// </summary>
        private static bool RunState_Prefix(Pet __instance)
        {
            // If the pet is recruited, we prevent its internal AI from trying to change its animation.
            if (_squadManager.IsRecruited(__instance))
            {
                return false;
            }

            // Otherwise, let the pet behave as normal when not recruited.
            return true;
        }

        /// <summary>
        /// Prevents a recruited pet from being warped to the player's bed by vanilla logic when entering the FarmHouse.
        /// </summary>
        private static bool WarpToFarmHouse_Prefix(Pet __instance)
        {
            // If the pet is recruited, we prevent the game from warping it to a fixed spot.
            if (_squadManager.IsRecruited(__instance))
            {
                return false;
            }

            // Otherwise, let the pet warp as normal when not recruited.
            return true;
        }

        #endregion

        #region Garbage Can Patches

        /// <summary>This helper method is called by our transpiler. It gets the original list of witnesses and filters out squad members based on config.</summary>
        private static IEnumerable<NPC> FilterGarbageWitnesses(Vector2 centerTile, int tilesAway, GameLocation location)
        {
            List<NPC> witnesses = Utility.GetNpcsWithinDistance(centerTile, tilesAway, location).ToList();
            if (witnesses == null || witnesses.Count == 0)
                return witnesses;

            // Apply filtering based on the user's configuration.
            switch (_config.DisableTrashRummagingReaction)
            {
                case TrashReactionMode.Never:
                    // Do nothing, return the original list.
                    break;
                    
                case TrashReactionMode.Everyone:
                    // Remove all recruited squad members from the witness list.
                    witnesses.RemoveAll(npc => _squadManager.IsRecruited(npc));
                    break;

                case TrashReactionMode.PetsOnly:
                    // Remove only recruited pets from the witness list.
                    witnesses.RemoveAll(npc => npc is Pet && _squadManager.IsRecruited(npc));
                    break;
            }

            return witnesses;
        }

        /// <summary>
        /// This Harmony transpiler modifies the GameLocation.CheckGarbage method.
        /// It finds the call to Utility.GetNpcsWithinDistance and replaces it with a call to our own FilterGarbageWitnesses method.
        /// This prevents recruited followers from ever being considered "witnesses" to trash rummaging.
        /// </summary>
        private static IEnumerable<CodeInstruction> CheckGarbage_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Be specific about the method overload we want to replace.
            var originalMethod = AccessTools.Method(typeof(Utility), nameof(Utility.GetNpcsWithinDistance),
                new[] { typeof(Vector2), typeof(int), typeof(GameLocation) });

            // Be specific about our replacement method's signature too.
            var newMethod = AccessTools.Method(typeof(HarmonyPatches), nameof(FilterGarbageWitnesses),
                new[] { typeof(Vector2), typeof(int), typeof(GameLocation) });

            foreach (var instruction in instructions)
            {
                // Safely check if the operand is a MethodInfo before casting and comparing.
                // This prevents the InvalidCastException if the operand is a constructor.
                if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo methodInfo && methodInfo == originalMethod)
                {
                    // Replace the original call with a call to our new method.
                    yield return new CodeInstruction(OpCodes.Call, newMethod);
                }
                else
                {
                    // Keep all other instructions as they are.
                    yield return instruction;
                }
            }
        }

        #endregion

        #region NPC Behavior Patches

        /// <summary>Prevents recruited squad members from reacting negatively to being hit by the player's slingshot.</summary>
        private static bool GetHitByPlayer_Prefix(NPC __instance)
        {
            // If the NPC is recruited, block the original method (return false).
            if (_squadManager.IsRecruited(__instance))
            {
                return false;
            }

            // Otherwise, let the original method run for non-squad members.
            return true;
        }

        #endregion


        #region Swimming Patches

        /// <summary>
        /// Updates swimming state and yOffset for recruited NPCs to create the bobbing swimming animation.
        /// This mimics vanilla swimming behavior where characters bob up and down in water.
        /// </summary>
        private static void NPC_Update_Postfix(NPC __instance, GameTime time)
        {
            if (!_squadManager.IsRecruited(__instance))
                return;

            var mate = _squadManager.GetMember(__instance);
            if (mate == null)
                return;

            // Get current tile and location
            Point currentTile = __instance.TilePoint;
            var location = __instance.currentLocation;

            // Check if NPC stepped onto a PoolEntrance tile (toggle pool state for Spa)
            if (mate.LastTilePoint.HasValue && mate.LastTilePoint.Value != currentTile)
            {
                string touchAction = location?.doesTileHaveProperty(currentTile.X, currentTile.Y, "TouchAction", "Back");
                if (touchAction != null && touchAction.StartsWith("PoolEntrance"))
                {
                    mate.IsInPool = !mate.IsInPool; // Toggle pool state
                }
            }
            mate.LastTilePoint = currentTile;

            // Check if NPC is on a water tile (Back layer property for oceans/rivers/lakes)
            bool hasWaterProperty = location?.doesTileHaveProperty(currentTile.X, currentTile.Y, "Water", "Back") != null;

            // NPC should swim if on water tile OR in pool (Spa)
            bool shouldSwim = hasWaterProperty || mate.IsInPool;

            // Track previous swimming state to detect transitions
            bool wasSwimming = __instance.swimming.Value;

            if (shouldSwim)
            {
                __instance.swimming.Value = true;

                // Switch to beach sprite when starting to swim (if available)
                if (!wasSwimming)
                {
                    // Capture whether they were already wearing island attire before we change it
                    mate.WasWearingIslandAttireBeforeSwimming = __instance.shouldWearIslandAttire.Value;

                    // Only load beach sprites if not already wearing island attire
                    // We can't use shouldWearIslandAttire in Valley locations because the game resets it
                    // (NPC.cs line 3110 checks InValleyContext() and forces it to false)
                    // Instead, directly load the beach sprite texture
                    if (!mate.WasWearingIslandAttireBeforeSwimming)
                    {
                        string beachAsset = "Characters/" + __instance.getTextureName() + "_Beach";
                        if (Game1.content.DoesAssetExist<Microsoft.Xna.Framework.Graphics.Texture2D>(beachAsset))
                        {
                            __instance.TryLoadSprites(beachAsset, out _);
                        }
                    }
                }

                if (__instance is Pet)
                {
                    // Apply bobbing animation using cosine wave (vanilla formula), but with offset to show more of sprite
                    // Pets need more offset since we only want to cover their legs
                    float bobbing = (float)(Math.Cos(time.TotalGameTime.TotalMilliseconds / 2000.0) * 4.0);
                    float baseOffset = -30.0f;
                    __instance.yOffset = bobbing + baseOffset;
                }
            }
            else
            {
                __instance.swimming.Value = false;
                __instance.yOffset = 0f;

                // Switch back to normal clothes when stopping swimming (only if we were the ones who changed it)
                if (wasSwimming && !mate.WasWearingIslandAttireBeforeSwimming)
                {
                    // Load the normal sprite texture back
                    string normalAsset = "Characters/" + __instance.getTextureName();
                    __instance.TryLoadSprites(normalAsset, out _);
                }

                // Reset the tracking state
                mate.WasWearingIslandAttireBeforeSwimming = false;
            }
        }

        // Store original and modified sprite rects for swimming pets
        private static Rectangle? _originalPetSpriteRect = null;
        private static Rectangle _modifiedPetSpriteRect;

        // Track which pets need custom swim shadows during BathHousePool drawing
        private static Dictionary<Pet, bool> _originalPetSwimmingStates = new Dictionary<Pet, bool>();

        // Flag to indicate we're in the middle of BathHousePool drawing, so Pet_Draw should check this instead of swimming.Value
        private static HashSet<Pet> _petsForcedSwimming = new HashSet<Pet>();

        /// <summary>
        /// Calculates the modified sprite rect height for a swimming pet based on facing direction.
        /// </summary>
        private static Rectangle GetSwimmingPetSourceRect(Pet pet)
        {
            Rectangle sourceRect = pet.Sprite.SourceRect;

            // Cut sprite based on facing direction (pets need custom cuts)
            if (pet.FacingDirection == 2)
            {
                if (pet.petType.ToString() == "Dog")
                {
                    sourceRect.Height = (int)(sourceRect.Height * 0.55f);
                }
                if (pet.petType.ToString() == "Turtle")
                {
                    sourceRect.Height = (int)(sourceRect.Height * 0.70f);
                }
                else
                {
                    sourceRect.Height = (int)(sourceRect.Height * 0.65f);
                }
            }
            else
            {
                sourceRect.Height /= 2;
            }
            sourceRect.Height -= (int)pet.yOffset / 4;

            return sourceRect;
        }

        /// <summary>
        /// Modifies Pet sprite SourceRect before drawing to create submerged appearance when swimming.
        /// </summary>
        private static void Pet_Draw_Prefix(Pet __instance)
        {
            // Check if pet is swimming (either naturally or forced during BathHousePool drawing)
            bool isSwimming = __instance.swimming.Value || _petsForcedSwimming.Contains(__instance);

            // Only handle recruited pets that are swimming
            if (!_squadManager.IsRecruited(__instance) || !isSwimming)
                return;

            // Store original SourceRect
            _originalPetSpriteRect = __instance.Sprite.SourceRect;

            // Calculate and store modified rect
            _modifiedPetSpriteRect = GetSwimmingPetSourceRect(__instance);
            __instance.Sprite.SourceRect = _modifiedPetSpriteRect;

            // Adjust position for swimming
            __instance.Position = new Vector2(__instance.Position.X, __instance.Position.Y + 64f + __instance.yOffset);
        }

        /// <summary>
        /// Restores original sprite rect and draws water ripple after Pet drawing.
        /// </summary>
        private static void Pet_Draw_Postfix(Pet __instance, Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            // Check if pet is swimming (either naturally or forced during BathHousePool drawing)
            bool isSwimming = __instance.swimming.Value || _petsForcedSwimming.Contains(__instance);

            // Only handle recruited pets that are swimming
            if (!_squadManager.IsRecruited(__instance) || !isSwimming)
                return;

            // Restore original position first
            __instance.Position = new Vector2(__instance.Position.X, __instance.Position.Y - 64f - __instance.yOffset);

            // Draw water ripple using the stored modified rect
            Vector2 position = __instance.getLocalPosition(Game1.viewport)
                + new Vector2(__instance.Sprite.SpriteWidth * 4 / 2, __instance.GetBoundingBox().Height / 2);

            position.Y += 64f + __instance.yOffset;

            int rippleWidth = 60;
            int rippleHeight = 4;
            int baseRippleOffset = 61;
            int rippleYPosition = (int)position.Y - 128 + _modifiedPetSpriteRect.Height * 4 + (int)__instance.yOffset + baseRippleOffset;

            b.Draw(
                Game1.staminaRect,
                new Rectangle(
                    (int)position.X - rippleWidth / 2,
                    rippleYPosition,
                    rippleWidth,
                    rippleHeight
                ),
                Game1.staminaRect.Bounds,
                Color.White * 0.75f,
                0f,
                Vector2.Zero,
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
                0.9f  // Very high layer depth to draw on top
            );

            // Restore original SourceRect last
            if (_originalPetSpriteRect.HasValue)
            {
                __instance.Sprite.SourceRect = _originalPetSpriteRect.Value;
                _originalPetSpriteRect = null;
            }
        }

        /// <summary>
        /// Disables swimming state for recruited pets to prevent vanilla from drawing misaligned swim shadows.
        /// Uses a flag so our Pet_Draw patches still apply swimming visuals.
        /// </summary>
        private static void BathHousePool_Draw_Prefix(GameLocation __instance)
        {
            _originalPetSwimmingStates.Clear();
            _petsForcedSwimming.Clear();

            foreach (var character in __instance.characters)
            {
                if (character is Pet pet && _squadManager.IsRecruited(pet) && pet.swimming.Value)
                {
                    _originalPetSwimmingStates[pet] = true;
                    _petsForcedSwimming.Add(pet);  // Tell Pet_Draw to treat this as swimming
                    pet.swimming.Value = false;  // Prevent vanilla from drawing swim shadow
                }
            }
        }

        /// <summary>
        /// Restores swimming state and draws properly centered swim shadows for recruited pets.
        /// </summary>
        private static void BathHousePool_Draw_Postfix(BathHousePool __instance, Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            if (_originalPetSwimmingStates.Count == 0)
                return;

            // Get the swimShadow texture and swimShadowFrame from the BathHousePool instance
            var swimShadowField = AccessTools.Field(typeof(BathHousePool), "swimShadow");
            var swimShadowFrameField = AccessTools.Field(typeof(BathHousePool), "swimShadowFrame");

            if (swimShadowField == null || swimShadowFrameField == null)
            {
                // Restore swimming state even if we can't draw shadows
                foreach (var kvp in _originalPetSwimmingStates)
                    kvp.Key.swimming.Value = true;
                _originalPetSwimmingStates.Clear();
                return;
            }

            var swimShadow = swimShadowField.GetValue(__instance) as Microsoft.Xna.Framework.Graphics.Texture2D;
            var swimShadowFrame = (int)swimShadowFrameField.GetValue(__instance);

            if (swimShadow == null)
            {
                // Restore swimming state even if we can't draw shadows
                foreach (var kvp in _originalPetSwimmingStates)
                    kvp.Key.swimming.Value = true;
                _originalPetSwimmingStates.Clear();
                return;
            }

            foreach (var kvp in _originalPetSwimmingStates)
            {
                var pet = kvp.Key;

                // Restore swimming state
                pet.swimming.Value = true;

                // Draw custom swim shadow with proper horizontal centering
                // The shadow texture is 16x16, drawn at 4x scale = 64x64 pixels
                float shadowWidth = 64f;  // 16 * 4
                float petSpriteWidth = pet.Sprite.SpriteWidth * 4;  // Pet sprite width at game scale
                float horizontalOffset = (petSpriteWidth - shadowWidth) / 2;

                // Use vanilla Y offset for vertical positioning
                float verticalOffset = pet.Sprite.SpriteHeight / 3 * 4 + 4;

                Vector2 offset = new Vector2(horizontalOffset, verticalOffset);

                b.Draw(
                    swimShadow,
                    Game1.GlobalToLocal(Game1.viewport, pet.Position + offset),
                    new Rectangle(swimShadowFrame * 16, 0, 16, 16),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    4f,
                    Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
                    0.001f
                );
            }

            _originalPetSwimmingStates.Clear();
            _petsForcedSwimming.Clear();
        }

        #endregion

        #region Fishing Patches

        /// <summary>
        /// Postfix patch for FishingRod.playerCaughtFishEndFunction.
        /// Called right after the player catches a fish, allowing us to trigger NPC fish catching.
        /// </summary>
        private static void PlayerCaughtFishEndFunction_Postfix(StardewValley.Tools.FishingRod __instance)
        {
            // Notify the FollowerManager that the player just caught a fish
            _followerManager?.OnPlayerCaughtFish();
        }

        /// <summary>Prefix patch for NPC.draw to draw fishing lines behind NPCs when they face up.</summary>
        private static void NPC_Draw_Prefix(NPC __instance, Microsoft.Xna.Framework.Graphics.SpriteBatch b)
        {
            // Only handle recruited NPCs
            if (!_squadManager.IsRecruited(__instance))
                return;

            var mate = _squadManager.GetMember(__instance);
            if (mate == null)
                return;

            // Handle fishing line rendering
            if (mate.Task?.Type == TaskType.Fishing)
            {
                // Only draw in prefix for NPCs facing up (to draw behind them)
                if (__instance.FacingDirection == 0)
                {
                    // Only draw when NPC is at their fishing spot
                    if (__instance.TilePoint == mate.Task.InteractionTile)
                    {
                        // Calculate bobber position using shared helper method
                        var waterTile = mate.Task.Tile;
                        Vector2 bobberPos = TaskManager.CalculateBobberPosition(__instance, waterTile);

                        // Get rod tip position using shared helper method
                        Vector2 rodTipPosition = TaskManager.GetRodTipPosition(__instance);

                        // Draw behind the NPC
                        float layerDepth = (__instance.Position.Y / 10000f) - 0.001f;
                        TaskManager.DrawFishingLine(b, rodTipPosition, bobberPos, layerDepth);
                    }
                }
            }
        }

        #endregion

        #region Animal and Pet Petting Patches

        /// <summary>
        /// Postfix patch for FarmAnimal.pet.
        /// Called right after the player pets a farm animal (cow, chicken, etc.), allowing us to detect petting action.
        /// </summary>
        private static void FarmAnimal_Pet_Postfix(Farmer who, bool is_auto_pet)
        {
            // Skip if we're ignoring petting (NPC is petting)
            if (_ignorePettingDepth > 0)
                return;

            // Only track manual petting by the player (not auto-petters)
            if (who == Game1.player && !is_auto_pet)
            {
                _lastPlayerPettingTick = Game1.ticks;
            }
        }

        /// <summary>
        /// Postfix patch for Pet.checkAction.
        /// Called after the player interacts with their pet (cat/dog), detecting when they pet it.
        /// </summary>
        private static void Pet_CheckAction_Postfix(Pet __instance, Farmer who, bool __result)
        {
            // Skip if we're ignoring petting (NPC is petting)
            if (_ignorePettingDepth > 0)
                return;

            // Only track if the interaction was successful and done by the player
            if (__result && who == Game1.player)
            {
                // Verify it was actually a petting action by checking if lastPetDay was updated
                bool wasPetted = __instance.lastPetDay.TryGetValue(who.UniqueMultiplayerID, out var lastDay)
                    && lastDay == Game1.Date.TotalDays;

                if (wasPetted)
                {
                    _lastPlayerPettingTick = Game1.ticks;
                }
            }
        }

        #endregion

        #region Crop Harvesting Patches

        /// <summary>
        /// Postfix patch for Crop.harvest.
        /// Called after a crop is harvested, allowing us to detect player/NPC harvesting actions.
        /// Note: Crop.harvest() returns false for regrowable crops even when successfully harvested,
        /// so we check both __result and whether the crop regrows to catch all successful harvests.
        /// </summary>
        private static void Crop_Harvest_Postfix(HoeDirt soil, JunimoHarvester junimoHarvester, bool __result)
        {
            // Only track harvests that aren't from Junimo harvesters
            if (junimoHarvester != null)
                return;

            // Skip if we're ignoring harvests (NPC is harvesting)
            if (_ignoreHarvestDepth > 0)
                return;

            // Check if harvest was successful:
            // - __result == true for non-regrowable crops
            // - For regrowable crops, __result is false but crop still exists and regrows
            bool isSuccessfulHarvest = __result || (soil?.crop != null && soil.crop.RegrowsAfterHarvest());

            if (isSuccessfulHarvest)
            {
                _lastPlayerHarvestingTick = Game1.ticks;
            }
        }

        #endregion

        #region NPC Sitting Rendering Patches

        /// <summary>
        /// Postfix patch for Furniture.HasSittingFarmers() to also check if NPCs are sitting on the furniture.
        /// This enables proper split rendering (base/front layers) when NPCs sit.
        /// </summary>
        private static void Furniture_HasSittingFarmers_Postfix(StardewValley.Objects.Furniture __instance, ref bool __result)
        {
            // If already true (vanilla farmers sitting), keep it true
            if (__result)
                return;

            // Check if any NPCs are sitting on this furniture
            if (TaskManager.HasSittingNpcs(__instance))
                __result = true;
        }

        /// <summary>
        /// Postfix patch for MapSeat.HasSittingFarmers() to also check if NPCs are sitting on the map seat.
        /// This enables proper split rendering when NPCs sit on benches, picnic tables, etc.
        /// </summary>
        private static void MapSeat_HasSittingFarmers_Postfix(StardewValley.MapSeat __instance, ref bool __result)
        {
            // If already true (vanilla farmers sitting), keep it true
            if (__result)
                return;

            // Check if any NPCs are sitting on this map seat
            if (TaskManager.HasSittingNpcs(__instance))
                __result = true;
        }

        /// <summary>
        /// Prefix patch for MapSeat.Draw() to force overlay rendering when NPCs are sitting.
        /// If drawTilePosition is invalid but NPCs are sitting, we set it to a valid value
        /// to trigger the overlay rendering code path.
        /// </summary>
        private static void MapSeat_Draw_Prefix(StardewValley.MapSeat __instance)
        {
            bool hasNpcs = TaskManager.HasSittingNpcs(__instance);
            if (hasNpcs)
            {
                var drawTilePos = __instance.drawTilePosition.Value;

                // If drawTilePosition is invalid (< 0), force it to (0, 0) to enable overlay rendering
                if (drawTilePos.X < 0f)
                    __instance.drawTilePosition.Value = new Vector2(0f, 0f);
            }
        }

        /// <summary>
        /// Helper method to get sitting object info for an NPC.
        /// Returns the ISittable object (Furniture or MapSeat) the NPC is sitting on, or null.
        /// </summary>
        private static object GetSittingObject(StardewValley.NPC npc)
        {
            // Access the _sittingNpcs dictionary via reflection
            var sittingNpcsField = typeof(TaskManager).GetField("_sittingNpcs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (sittingNpcsField == null)
                return null;

            var dict = sittingNpcsField.GetValue(null) as System.Collections.IDictionary;
            if (dict == null)
                return null;

            // Find which ISittable (Furniture or MapSeat) this NPC is sitting on
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                var npcList = entry.Value as System.Collections.Generic.List<StardewValley.NPC>;
                if (npcList != null && npcList.Contains(npc))
                {
                    return entry.Key; // Return the Furniture or MapSeat object
                }
            }

            return null; // NPC not found in sitting dictionary
        }

        /// <summary>
        /// Helper method to adjust NPC depth for sitting using furniture-relative depth calculation.
        /// Called by transpiler - modifies depth for sitting NPCs to fix rendering issues.
        /// Uses different overlay depth formulas for Furniture vs MapSeat to position NPCs just below the overlay.
        /// Only applies when: player is sitting, there's at least one squad member, and the NPC is a sitting squad member.
        /// </summary>
        public static float AdjustSittingNpcDepth(float npcDepth, StardewValley.Character character)
        {
            // Only apply if player is sitting
            var player = Game1.player;
            if (player == null || (player.sittingFurniture == null && !player.isSitting.Value))
                return npcDepth;

            // Only apply if there's at least one squad member
            if (_squadManager == null || _squadManager.Count == 0)
                return npcDepth;

            // Only apply to NPCs that are squad members
            if (!_squadManager.IsRecruited(character))
                return npcDepth;

            if (character is StardewValley.NPC npc)
            {
                // Only apply if the NPC is currently sitting
                var sittingObject = GetSittingObject(npc);
                if (sittingObject != null)
                {
                    // Priority check: If NPC is sitting very close to player (<0.5 tiles), use player-relative depth
                    // This ensures proper rendering order when NPC and player sit near each other
                    // (player is already validated as sitting in the early checks above)
                    float distance = Vector2.Distance(npc.Position, player.Position);

                    // If within 1 tile (72 pixels), calculate depth relative to player
                    if (distance < 72f)
                    {
                        float playerDepth = player.Position.Y / 10000f;

                        // NPC above player (lower Y) should render behind player
                        if (npc.Position.Y < player.Position.Y)
                        {
                            return playerDepth + 0.0001f;
                        }
                    }

                    // Fall back to furniture-relative depth for normal cases
                    // Handle Furniture objects (placed chairs, couches, etc.)
                    if (sittingObject is StardewValley.Objects.Furniture furniture)
                    {
                        // Furniture uses boundingBox-based depth calculation
                        // Vanilla overlay depth: (boundingBox.Bottom - 8) / 10000
                        // Position NPC at -12 pixels to render below overlay
                        float boundingBoxBottom = furniture.boundingBox.Bottom;
                        float targetDepth = (boundingBoxBottom - 12f) / 10000f;

                        return targetDepth;
                    }
                    // Handle MapSeat objects (benches, picnic tables, etc.)
                    else if (sittingObject is StardewValley.MapSeat mapSeat)
                    {
                        // MapSeat uses tile-based depth calculation
                        // Vanilla overlay depth: ((tileY + sizeY) + 0.1) * 64 / 10000
                        // Position NPC at +0.05 offset to render below overlay (overlay uses +0.1)
                        float furnitureTileY = mapSeat.tilePosition.Value.Y;
                        float furnitureSize = mapSeat.size.Value.Y;
                        float targetDepth = ((furnitureTileY + furnitureSize) + 0.05f) * 64f / 10000f;

                        return targetDepth;
                    }
                }
            }
            return npcDepth; // Return unchanged for non-sitting NPCs
        }

        /// <summary>
        /// Transpiler for NPC.draw() to adjust layer depth for sitting NPCs.
        /// Injects call to AdjustSittingNpcDepth() which calculates furniture-relative depth
        /// to ensure NPCs render between base furniture and overlay layers.
        /// </summary>
        private static IEnumerable<CodeInstruction> NPC_Draw_LogDepth_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var adjustDepthMethod = AccessTools.Method(typeof(HarmonyPatches), nameof(AdjustSittingNpcDepth));
            int injectionsFound = 0;

            for (int i = 0; i < codes.Count - 2; i++)
            {
                // Look for the pattern: conv.r4, ldc.r4 10000, div
                // This is the layer depth calculation: (Y value) / 10000f
                if (codes[i].opcode == OpCodes.Conv_R4 &&
                    codes[i + 1].opcode == OpCodes.Ldc_R4 &&
                    codes[i + 1].operand?.ToString() == "10000" &&
                    codes[i + 2].opcode == OpCodes.Div)
                {
                    injectionsFound++;

                    // After the div, the depth value is on the stack
                    // Stack: [depth]
                    // We need to call AdjustSittingNpcDepth(depth, this) which will consume both and return adjusted depth
                    // AdjustSittingNpcDepth(float npcDepth, Character character) expects: [float, Character] on stack
                    var adjustInstructions = new List<CodeInstruction>
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),              // Load 'this' (NPC instance) -> stack: [depth, this]
                        new CodeInstruction(OpCodes.Call, adjustDepthMethod) // Call adjuster -> consumes [depth, this], returns adjusted depth
                    };

                    // Insert right after the div instruction
                    codes.InsertRange(i + 3, adjustInstructions);

                    // Skip past what we just inserted
                    i += 2 + adjustInstructions.Count;
                }
            }

            return codes;
        }

        #endregion
    }
}
