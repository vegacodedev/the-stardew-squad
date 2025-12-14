using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Monsters;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using TheStardewSquad.Abstractions.Location;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.Wrappers;

namespace TheStardewSquad.Framework
{
    public static class TaskManager
    {
        private static ModConfig _config;
        private static FollowerManager _followerManager;
        private static StardewModdingAPI.IMonitor _monitor;
        private static NpcConfig.SpriteManager _spriteManager;

        // Track NPCs sitting on furniture/mapseats for rendering purposes
        private static readonly Dictionary<ISittable, List<NPC>> _sittingNpcs = new Dictionary<ISittable, List<NPC>>();

        public static void Initialize(ModConfig config, FollowerManager followerManager, NpcConfig.SpriteManager spriteManager)
        {
            _config = config;
            _followerManager = followerManager;
            _spriteManager = spriteManager;
        }

        public static void SetMonitor(StardewModdingAPI.IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// Checks if any NPCs are sitting on the given furniture/mapseat.
        /// Used by Harmony patches to enable split rendering.
        /// </summary>
        public static bool HasSittingNpcs(ISittable sittable)
        {
            return _sittingNpcs.TryGetValue(sittable, out var npcs) && npcs.Count > 0;
        }


        /// <summary>
        /// Registers an NPC as sitting on furniture/mapseat.
        /// </summary>
        private static void AddSittingNpc(ISittable sittable, NPC npc)
        {
            if (!_sittingNpcs.ContainsKey(sittable))
            {
                _sittingNpcs[sittable] = new List<NPC>();
            }
            if (!_sittingNpcs[sittable].Contains(npc))
            {
                _sittingNpcs[sittable].Add(npc);
            }
        }

        /// <summary>
        /// Unregisters an NPC from furniture/mapseat.
        /// </summary>
        public static void RemoveSittingNpc(ISittable sittable, NPC npc)
        {
            if (_sittingNpcs.TryGetValue(sittable, out var npcs))
            {
                npcs.Remove(npc);
                if (npcs.Count == 0)
                {
                    _sittingNpcs.Remove(sittable);
                }
            }
        }

        #region Constants
        private static class FishingConstants
        {
            // Fishing mechanics
            public const int WaterSearchRadius = 12; // Reduced from 25 to improve performance
            public const int MaxPathfindingCandidates = 5; // Max candidates to try pathfinding on
            public const int WaterDepth = 4; // Assumed water depth for fish calculations
            public const int CatchCooldown = 60;
            public const float MinFishingDistance = 0.5f; // Minimum distance between fishing NPCs and from player

            // Fish quality calculation
            public const double SilverChanceDivisor = 30.0;
            public const double SilverChanceMax = 0.5;
            public const double GoldChanceDivisor = 60.0;
            public const double GoldChanceMax = 0.25;
            public const double IridiumChanceDivisor = 120.0;
            public const double IridiumChanceMax = 0.1;

            // Animation - Bobbing
            public const double RodBobbingPeriod = 500.0; // Milliseconds
            public const double BobberBobbingPeriod = 600.0; // Milliseconds (desynchronized from rod)
            public const float BobberBobbingPhaseOffset = 1.5f; // Phase offset for independent movement
            public const float BobbingAmplitude = 4f;

            // Animation - Fish icon
            public const float FishIconAnimationInterval = 2500f;
            public const float FishIconMotionY = -0.5f; // Float upward
            public const float FishIconStopOffset = -32f;
            public const float FishIconScale = 3f;
            public const float FishIconAlphaFade = 0.01f;

            // Animation - Rod sprite
            public const float RodScale = 4f;
            public const float RodAnimationInterval = 100f;

            // Rendering
            public const float BobberTopCenterOffset = -16f;
            public const float FishingLineLayerDepth = 0.9f;
        }
        #endregion

        #region Common Helpers
        /// <summary>Creates a chest instance connected to the squad's global inventory.</summary>
        private static StardewValley.Objects.Chest GetSquadChest()
        {
            return new StardewValley.Objects.Chest(playerChest: true)
            {
                GlobalInventoryId = "TheStardewSquad_SquadInventory"
            };
        }

        #endregion

        private static int GetAxeUpgradeLevel()
        {
            switch (Game1.player.ForagingLevel)
            {
                case >= 10: return 4; // Iridium
                case >= 8: return 3;  // Gold
                case >= 5: return 2;  // Steel
                case >= 2: return 1;  // Copper
                default: return 0;   // Basic
            }
        }

        private static int GetPickaxeUpgradeLevel()
        {
            switch (Game1.player.MiningLevel)
            {
                case >= 10: return 4; // Iridium
                case >= 8: return 3;  // Gold
                case >= 5: return 2;  // Steel
                case >= 2: return 1;  // Copper
                default: return 0;   // Basic
            }
        }

        public static void FacePosition(NPC npc, Vector2 targetPosition)
        {
            Vector2 directionVector = targetPosition - npc.getStandingPosition();
            if (Math.Abs(directionVector.X) >= Math.Abs(directionVector.Y))
            {
                npc.faceDirection(directionVector.X > 0 ? 1 : 3); // 1 = Right, 3 = Left
            }
            else
            {
                npc.faceDirection(directionVector.Y > 0 ? 2 : 0); // 2 = Down, 0 = Up
            }
        }

        public static bool CanSquadInventoryAcceptItem(Item item)
        {
            if (item == null)
                return false;

            var squadInventory = Game1.player.team.GetOrCreateGlobalInventory("TheStardewSquad_SquadInventory");

            // Check if any existing item in the inventory can stack with the new item.
            foreach (Item slot in squadInventory)
            {
                // Is there an item in this slot? Can it stack with our item? Is the stack not full?
                if (slot != null && slot.canStackWith(item) && slot.Stack < slot.maximumStackSize())
                {
                    return true;
                }
            }

            // If no stack has room, check if there is any empty slot.
            if (squadInventory.Count < 36)
            {
                return true;
            }
            
            // If we've checked all slots and found no partial stacks and no empty slots, it's full.
            return false;
        }

        /// <summary>Checks if an item can be accepted based on the UseSquadInventory config setting.</summary>
        public static bool CanAcceptItem(Item item)
        {
            if (item == null)
                return false;

            if (_config.UseSquadInventory)
            {
                return CanSquadInventoryAcceptItem(item);
            }
            else
            {
                return Game1.player.couldInventoryAcceptThisItem(item);
            }
        }

        /// <summary>Tries to add an item to the appropriate inventory based on the UseSquadInventory config setting.</summary>
        /// <returns>True if the item was successfully added; false otherwise.</returns>
        private static bool TryAddItemToInventory(Item item)
        {
            if (item == null)
                return false;

            if (_config.UseSquadInventory)
            {
                var squadChest = GetSquadChest();
                return squadChest.addItem(item) == null;
            }
            else
            {
                return Game1.player.addItemToInventoryBool(item);
            }
        }

        private static (Point target, Point? interactionPoint) CheckForTrellis(GameLocation location, Point target, NPC npc)
        {
            return CheckForTrellis(new LocationInfoWrapper(location, npc), target, npc.TilePoint);
        }

        /// <summary>
        /// Testable version: Checks if a target tile is a trellis crop and finds an interaction point if needed.
        /// </summary>
        /// <param name="locationInfo">Location information provider.</param>
        /// <param name="target">The tile to check for trellis crop.</param>
        /// <param name="npcPosition">The NPC's current position.</param>
        /// <returns>Target tile and interaction point (adjacent for trellis, same as target for non-trellis).</returns>
        public static (Point target, Point? interactionPoint) CheckForTrellis(ILocationInfo locationInfo, Point target, Point npcPosition)
        {
            bool isTrellis = locationInfo.IsTrellisCropAt(target);

            if (isTrellis)
            {
                var interactionPoint = Pathfinding.AStarPathfinder.FindClosestPassableNeighbor(locationInfo, target, npcPosition);
                return (target, interactionPoint);
            }
            return (target, target);
        }

        #region Attacking Task
        /// <summary>Checks if the player is in combat (has weapon equipped and is using it).</summary>
        public static bool IsPlayerInCombat()
        {
            var player = Game1.player;
            if (player == null) return false; // Handle unit test scenarios

            // Check if player has a weapon equipped
            if (player.CurrentTool is not StardewValley.Tools.MeleeWeapon &&
                player.CurrentTool is not StardewValley.Tools.Slingshot)
                return false;

            // Check if weapon is being used
            return player.UsingTool;
        }

        /// <summary>
        /// Calculates attack damage for humanoid squad members based on player combat level and professions.
        /// Level 1: 5 min / 21 max | Level 10: 50 min / 120 max
        /// </summary>
        /// <param name="player">The player whose combat level and professions affect damage.</param>
        /// <returns>Tuple of (minDamage, maxDamage).</returns>
        public static (int minDamage, int maxDamage) CalculateAttackDamage(Farmer player)
        {
            int minDamage = player.CombatLevel * 5;
            int maxDamage = 10 + (player.CombatLevel * 11);

            // Apply Fighter profession: +10% damage
            if (player.professions.Contains(Farmer.fighter))
            {
                minDamage = (int)(minDamage * 1.1f);
                maxDamage = (int)(maxDamage * 1.1f);
            }

            // Apply Brute profession: +15% damage (stacks with Fighter)
            if (player.professions.Contains(Farmer.brute))
            {
                minDamage = (int)(minDamage * 1.15f);
                maxDamage = (int)(maxDamage * 1.15f);
            }

            return (minDamage, maxDamage);
        }

        /// <summary>
        /// Calculates critical hit chance for humanoid squad members.
        /// Uses vanilla weapon mechanics: 2% base crit chance, modified by player buffs and professions.
        /// </summary>
        /// <param name="player">The player whose buffs and professions affect crit chance.</param>
        /// <returns>Effective critical hit chance as a float (0.02 = 2%).</returns>
        public static float CalculateCritChance(Farmer player)
        {
            // Vanilla weapon base crit chance is 0.02 (2%)
            float baseCritChance = 0.02f;

            // Apply player's crit chance buff multiplier (from food, potions, etc.)
            // Matches vanilla: critChance * (1f + player.buffs.CriticalChanceMultiplier)
            float effectiveCritChance = baseCritChance * (1f + player.buffs.CriticalChanceMultiplier);

            // Apply Scout profession: +50% critical strike chance
            if (player.professions.Contains(Farmer.scout))
            {
                effectiveCritChance *= 1.5f;
            }

            return effectiveCritChance;
        }

        /// <summary>
        /// Calculates critical hit damage multiplier.
        /// Uses vanilla weapon mechanics: 3x base damage multiplier, modified by player buffs and professions.
        /// </summary>
        /// <param name="player">The player whose buffs and professions affect crit power.</param>
        /// <returns>Effective critical hit multiplier (3.0 = 3x damage).</returns>
        public static float CalculateCritMultiplier(Farmer player)
        {
            // Vanilla weapon base crit multiplier is 3.0 (3x damage)
            float baseCritMultiplier = 3.0f;

            // Apply player's crit power buff multiplier (from food, potions, etc.)
            // Matches vanilla: critMultiplier * (1f + player.buffs.CriticalPowerMultiplier)
            float effectiveCritMultiplier = baseCritMultiplier * (1f + player.buffs.CriticalPowerMultiplier);

            // Apply Desperado profession: Critical strikes are deadly (2x crit damage)
            if (player.professions.Contains(Farmer.desperado))
            {
                effectiveCritMultiplier *= 2f;
            }

            return effectiveCritMultiplier;
        }

        /// <summary>
        /// Checks if an NPC is adjacent to a monster (within 1 tile, including diagonals).
        /// Uses Manhattan distance to match vanilla melee weapon range.
        /// </summary>
        /// <param name="npc">The NPC performing the attack.</param>
        /// <param name="monster">The target monster.</param>
        /// <returns>True if NPC is within attack range (1 tile), false otherwise.</returns>
        public static bool IsAdjacentToMonster(NPC npc, Monster monster)
        {
            int distanceX = Math.Abs(npc.TilePoint.X - monster.TilePoint.X);
            int distanceY = Math.Abs(npc.TilePoint.Y - monster.TilePoint.Y);

            // Allow attacks from all 8 surrounding tiles (including diagonals)
            return distanceX <= 1 && distanceY <= 1;
        }

        public static Monster FindHostileMonster(NPC npc, IMonitor monitor)
        {
            // Backward-compatible wrapper that uses the testable version
            var location = npc.currentLocation;
            var playerTile = Game1.player.Tile.ToPoint();
            int searchRadius = 8;

            return FindHostileMonster(
                new LocationInfoWrapper(location, npc),
                playerTile,
                npc.TilePoint,
                searchRadius);
        }

        /// <summary>
        /// Testable version of FindHostileMonster that uses ILocationInfo abstraction.
        /// Finds a hostile monster that is targetable and reachable by the NPC.
        /// </summary>
        public static Monster FindHostileMonster(
            ILocationInfo locationInfo,
            Point playerPosition,
            Point npcPosition,
            int searchRadius)
        {
            // Don't attack in SlimeHutch
            if (locationInfo.IsSlimeHutch)
                return null;

            // Get all hostile monsters and filter by targetable state
            var targetableMonsters = locationInfo.GetHostileMonsters(playerPosition, searchRadius)
                .Where(m => m.state.IsTargetable)
                .OrderBy(m => Vector2.DistanceSquared(m.tile.ToVector2(), npcPosition.ToVector2()))
                .ToList();

            foreach (var (monster, tile, state) in targetableMonsters)
            {
                // Check if already adjacent (within 1 tile Manhattan distance)
                bool isAdjacent = Math.Abs(npcPosition.X - tile.X) <= 1 && Math.Abs(npcPosition.Y - tile.Y) <= 1;

                if (isAdjacent)
                {
                    // Even if adjacent, verify that the monster's position has at least one reachable neighbor tile
                    // This prevents targeting monsters that are across walls/obstacles despite being "adjacent"
                    var reachableSpot = Pathfinding.AStarPathfinder.FindClosestPassableNeighbor(
                        locationInfo, tile, npcPosition, claimedSpots: null, validateReachability: true);

                    if (reachableSpot.HasValue)
                    {
                        return monster; // Adjacent AND reachable
                    }
                    // Otherwise, continue to the next monster (this one is adjacent but unreachable)
                }
                else
                {
                    // Not adjacent - verify full path exists from NPC to monster's tile
                    var path = Pathfinding.AStarPathfinder.FindPath(
                        locationInfo, npcPosition, tile);
                    if (path != null)
                    {
                        return monster;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Validates whether a monster is in a targetable state for attacking.
        /// Checks for temporary states where the monster should not be targeted:
        /// - Monster removed from location (e.g., eaten by Frog trinket)
        /// - Mummy is reviving (just got 'killed')
        /// - Duggy is hiding underground
        /// - RockCrab is hiding in shell
        /// </summary>
        /// <param name="monster">The monster to validate.</param>
        /// <param name="location">The game location containing the monster.</param>
        /// <returns>True if the monster can be targeted, false if it should be ignored.</returns>
        public static bool IsMonsterTargetable(Monster monster, GameLocation location)
        {
            // Monster removed from location (e.g., eaten by Frog trinket)
            if (!location.characters.Contains(monster))
                return false;

            // Mummy is reviving (just got 'killed')
            if (monster is Mummy mummy && mummy.reviveTimer.Value > 0)
                return false;

            // Duggy is hiding underground
            if (monster is Duggy duggy && duggy.DamageToFarmer == 0)
                return false;

            // RockCrab is hiding in shell
            if (monster is RockCrab crab && crab.waiter)
                return false;

            return true;
        }

        public static bool ExecuteAttackingTask(ISquadMate mate, Monster monster)
        {
            NPC npc = mate.Npc;

            // Validate monster is still targetable before attacking
            if (!IsMonsterTargetable(monster, npc.currentLocation))
            {
                return true; // Task completed (drop the target)
            }

            // Validate NPC is adjacent to monster before attacking
            // Prevents distance attacks when monster moves away or is out of bounds
            if (!IsAdjacentToMonster(npc, monster))
            {
                return false; // Not adjacent - continue task, NPC will path closer
            }

            var (minDamage, maxDamage) = CalculateAttackDamage(Game1.player);

            // Calculate critical hit parameters using vanilla weapon mechanics
            float critChance = CalculateCritChance(Game1.player);
            float critMultiplier = CalculateCritMultiplier(Game1.player);

            // Use extended damageMonster overload with critical hit support
            npc.currentLocation.damageMonster(
                monster.GetBoundingBox(),
                minDamage,
                maxDamage,
                false,                  // isBomb
                1.0f,                   // knockBackModifier
                0,                      // addedPrecision
                critChance,             // critChance (2% base + player buffs)
                critMultiplier,         // critMultiplier (3x base + player buffs)
                true,                   // triggerMonsterInvincibleTimer
                Game1.player,           // who
                true                    // isProjectile (true to always hit)
            );
            npc.currentLocation.playSound("swordswipe");

            FacePosition(npc, monster.getStandingPosition());
            AnimateAttacking(npc);

            _spriteManager?.ApplyTaskAnimation(npc, "Attacking", 400);
            npc.shake(400);

            mate.ActionCooldown = 48;

            // 1 in 10 chance to say something.
            if (Game1.random.Next(10) == 0)
            {
                mate.Communicate(TaskType.Attacking.ToString());
            }

            return monster.Health <= 0;
        }

        public static void AnimateAttacking(NPC npc)
        {
            var location = npc.currentLocation;
            var sourceRect = new Rectangle(0, 0, 16, 16); // Rusty Sword
            float layerDepth = (npc.GetBoundingBox().Bottom + 2) / 10000f;
            float layerDepthBehind = (npc.GetBoundingBox().Bottom - 32) / 10000f;

            switch (npc.FacingDirection)
            {
                case 1: // Right
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 40f, npc.position.Y - 64f + 8f),
                        flicker: false,
                        flipped: false,
                        layerDepth: layerDepthBehind,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: -(float)Math.PI / 4f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 50 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 64f - 4f, npc.position.Y - 16f),
                        flicker: false,
                        flipped: false,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI / 4f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 100 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 64f - 28f, npc.position.Y + 4f),
                        flicker: false,
                        flipped: false,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI * 5f / 8f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 150 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 64f - 48f, npc.position.Y + 4f),
                        flicker: false,
                        flipped: false,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI * 3f / 4f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 200 });
                    break;
                case 3: // Left
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X - 40f, npc.position.Y - 64f + 8f),
                        flicker: false,
                        flipped: true,
                        layerDepth: layerDepthBehind,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI / 4f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 50 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X - 64f + 4f, npc.position.Y - 16f),
                        flicker: false,
                        flipped: true,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: -(float)Math.PI / 4f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 100 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X - 64f + 28f, npc.position.Y + 4f),
                        flicker: false,
                        flipped: true,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: -(float)Math.PI * 5f / 8f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 150 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X - 64f + 48f, npc.position.Y + 4f),
                        flicker: false,
                        flipped: true,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: -(float)Math.PI * 3f / 4f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 200 });
                    break;
                case 0: // Up
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 32f, npc.position.Y - 32f),
                        flicker: false,
                        flipped: false,
                        layerDepth: layerDepthBehind,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI * -3f / 4f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 50 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 48f, npc.position.Y - 52f),
                        flicker: false,
                        flipped: false,
                        layerDepth: layerDepthBehind,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI * -3f / 8f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 100 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 64f - 8f, npc.position.Y - 40f),
                        flicker: false,
                        flipped: false,
                        layerDepth: layerDepthBehind,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: 0f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 150 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 64f, npc.position.Y - 40f),
                        flicker: false,
                        flipped: false,
                        layerDepth: layerDepthBehind,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI / 8f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 200 });
                    break;
                case 2: // Down
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 56f, npc.position.Y - 16f),
                        flicker: false,
                        flipped: false,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI / 8f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 50 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 40f, npc.position.Y),
                        flicker: false,
                        flipped: false,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI / 2f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 100 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 8f, npc.position.Y + 8f),
                        flicker: false,
                        flipped: false,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: (float)Math.PI,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 150 });
                    location.temporarySprites.Add(new TemporaryAnimatedSprite(
                        textureName: Tool.weaponsTextureName,
                        sourceRect,
                        animationInterval: 50f,
                        animationLength: 1,
                        numberOfLoops: 0,
                        position: new Vector2(npc.position.X + 12f, npc.position.Y),
                        flicker: false,
                        flipped: false,
                        layerDepth,
                        alphaFade: 0f,
                        color: Color.White,
                        scale: 4f,
                        scaleChange: 0f,
                        rotation: 3.5342917f,
                        rotationChange: 0f
                    )
                    { delayBeforeAnimationStart = 200 });
                    break;
                default:
                    break;
            }
        }
        public static Monster FindHostileMonsterAt(GameLocation location, Point tile)
        {
            // Check for monsters on the exact tile or adjacent tiles, as clicking can be imprecise.
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    var checkTile = new Vector2(tile.X + x, tile.Y + y);
                    var monster = location.isCharacterAtTile(checkTile);
                    if (monster is Monster m)
                    {
                        return m;
                    }
                }
            }

            // Fallback for larger monsters whose origin might not be on the tile
            return location.characters.OfType<Monster>()
                .FirstOrDefault(m => m.GetBoundingBox().Contains(tile.X * 64 + 32, tile.Y * 64 + 32));
        }

        public static bool ExecutePetAttackingTask(ISquadMate mate, Monster monster)
        {
            NPC npc = mate.Npc;

            // Validate monster is still targetable before attacking
            if (!IsMonsterTargetable(monster, npc.currentLocation))
            {
                return true; // Task completed (drop the target)
            }

            // Validate NPC is adjacent to monster before attacking
            // Prevents distance attacks when monster moves away or is out of bounds
            if (!IsAdjacentToMonster(npc, monster))
            {
                return false; // Not adjacent - continue task, NPC will path closer
            }

            var (minDamage, maxDamage) = CalculateAttackDamage(Game1.player);

            // Calculate critical hit parameters using vanilla weapon mechanics
            // Pets now use the same crit calculations as humanoid NPCs
            float critChance = CalculateCritChance(Game1.player);
            float critMultiplier = CalculateCritMultiplier(Game1.player);

            // Use extended damageMonster overload with critical hit support
            npc.currentLocation.damageMonster(
                monster.GetBoundingBox(),
                minDamage,
                maxDamage,
                false,                  // isBomb
                1.0f,                   // knockBackModifier
                0,                      // addedPrecision
                critChance,             // critChance (1.5% base + player buffs)
                critMultiplier,         // critMultiplier (3x base + player buffs)
                true,                   // triggerMonsterInvincibleTimer
                Game1.player,           // who
                true                    // isProjectile (true to always hit)
            );
            npc.currentLocation.playSound("daggerswipe");

            FacePosition(npc, monster.getStandingPosition());
            AnimatePetAttacking(npc);
            _spriteManager?.ApplyTaskAnimation(npc, "Attacking", 250);
            npc.shake(250); // A little shake instead of a full animation
            mate.ActionCooldown = 40; // Pets can attack a bit faster

            if (Game1.random.Next(8) == 0)
            {
                mate.Communicate(TaskType.Attacking.ToString());
            }

            return monster.Health <= 0;
        }

        public static void AnimatePetAttacking(NPC npc)
        {
            switch (npc.FacingDirection)
            {
                case 1: // Right
                    npc.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(15, npc.Position + new Vector2(40f, -100f), Color.White, 4, flipped: false, 80f, 0, 128, 1f, 128)
                    {
                        layerDepth = (float)(npc.GetBoundingBox().Bottom + 1) / 10000f
                    });
                    break;
                case 3: // Left
                    npc.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(15, npc.Position + new Vector2(-92f, -100f), Color.White, 4, flipped: true, 80f, 0, 128, 1f, 128)
                    {
                        layerDepth = (float)(npc.GetBoundingBox().Bottom + 1) / 10000f
                    });
                    break;
                case 0: // Up
                    npc.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(18, npc.Position + new Vector2(40f, -132f), Color.White, 4, flipped: false, 100f, 0, 64, 1f, 64)
                    {
                        layerDepth = (float)(npc.StandingPixel.Y - 9) / 10000f
                    });
                    break;
                case 2: // Down
                    npc.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(19, npc.Position + new Vector2(60f, -128f), Color.White, 4, flipped: false, 80f, 0, 128, 1f, 128)
                    {
                        layerDepth = (float)(npc.GetBoundingBox().Bottom + 1) / 10000f
                    });
                    break;
            }
        }
        #endregion

        #region Watering Task
        /// <summary>Checks if the player is currently watering with a watering can.</summary>
        public static bool IsPlayerWatering()
        {
            var player = Game1.player;
            if (player == null) return false; // Handle unit test scenarios

            // Check if player has a watering can equipped
            if (player.CurrentTool is not StardewValley.Tools.WateringCan)
                return false;

            // Check if watering can is being used
            return player.UsingTool;
        }

        public static (Point? target, Point? interactionPoint) FindWaterableTile(NPC npc, ISet<Point> claimedTaskTargets)
        {
            GameLocation location = npc.currentLocation;
            var searchRadius = 5;

            return FindWaterableTile(
                new LocationInfoWrapper(location, npc),
                npc.TilePoint,
                searchRadius,
                claimedTaskTargets
            );
        }

        /// <summary>
        /// Testable version: Finds the nearest dry HoeDirt tile that needs watering within a search radius around the NPC.
        /// </summary>
        /// <param name="locationInfo">Location information provider.</param>
        /// <param name="npcPosition">The NPC's current position (center of search area).</param>
        /// <param name="searchRadius">The radius in tiles to search around the NPC.</param>
        /// <param name="claimedTaskTargets">Set of tiles already claimed by other NPCs.</param>
        /// <returns>Target tile and interaction point, or (null, null) if no waterable tile found.</returns>
        public static (Point? target, Point? interactionPoint) FindWaterableTile(
            ILocationInfo locationInfo,
            Point npcPosition,
            int searchRadius,
            ISet<Point> claimedTaskTargets)
        {
            // Only water crops in Farm or Greenhouse locations
            if (!locationInfo.IsFarmOrGreenhouse)
                return (null, null);

            // Search in a square pattern around the NPC
            for (int x = -searchRadius; x <= searchRadius; x++)
            {
                for (int y = -searchRadius; y <= searchRadius; y++)
                {
                    var tilePoint = new Point(npcPosition.X + x, npcPosition.Y + y);

                    if (claimedTaskTargets.Contains(tilePoint))
                        continue;

                    if (locationInfo.HasDryHoeDirtAt(tilePoint))
                    {
                        var (target, interactionPoint) = CheckForTrellis(locationInfo, tilePoint, npcPosition);
                        if (interactionPoint.HasValue)
                        {
                            return (target, interactionPoint);
                        }
                    }
                }
            }

            return (null, null);
        }

        public static bool ExecuteWateringTask(ISquadMate mate, Point tile)
        {
            NPC npc = mate.Npc;

            var location = npc.currentLocation;
            var tileVector = tile.ToVector2();

            if (location.terrainFeatures.TryGetValue(tileVector, out var feature) && feature is HoeDirt dirt)
            {
                dirt.state.Value = HoeDirt.watered;
                location.playSound("wateringCan");

                location.temporarySprites.Add(new TemporaryAnimatedSprite(
                    Game1.animationsName, new Rectangle(294, 1856, 16, 16), 100f, 4, 1,
                    (tileVector * 64f) + new Vector2(Game1.random.Next(-16, 16), Game1.random.Next(-16, 16)),
                    false, false, (tile.Y * 64 + 32) / 10000f, 0.01f, Color.White, 4f, 0.01f, 0f, 0f));

                var targetWorldPosition = tileVector * 64f + new Vector2(32f, 32f);
                FacePosition(npc, targetWorldPosition);
                AnimateWatering(npc);

                _spriteManager?.ApplyTaskAnimation(npc, "Watering", 400);
                npc.shake(400);

                mate.ActionCooldown = 48;

                // 1 in 10 chance (10%) to say something.
                if (Game1.random.Next(10) == 0)
                {
                    mate.Communicate(TaskType.Watering.ToString());
                }
            }
            return true;
        }

        public static void AnimateWatering(NPC npc)
        {
            var location = npc.currentLocation;
            float animationInterval = (npc.FacingDirection == 0) ? 400f : 200f; // Single frame should last twice as long.
            int numberOfLoops = 0;
            float layerDepth = (npc.FacingDirection == 0) ? (npc.GetBoundingBox().Bottom - 32) / 10000f : (npc.GetBoundingBox().Bottom + 2) / 10000f;
            bool flipped = npc.FacingDirection == 3; // Facing left
            Rectangle sourceRect;
            int animationLength = (npc.FacingDirection == 0) ? 1 : 2;
            Vector2 positionOffset;

            switch (npc.FacingDirection)
            {
                case 0: sourceRect = new Rectangle(64, 208, 16, 32); positionOffset = new Vector2(0, -96); break;
                case 1: sourceRect = new Rectangle(32, 208, 16, 32); positionOffset = new Vector2(48, -64); break;
                case 3: sourceRect = new Rectangle(32, 208, 16, 32); positionOffset = new Vector2(-32, -64); break;
                case 2: default: sourceRect = new Rectangle(0, 208, 16, 32); positionOffset = new Vector2(0, -32); break;
            }

            location.temporarySprites.Add(new TemporaryAnimatedSprite(
                textureName: Game1.toolSpriteSheet.Name,
                sourceRect,
                animationInterval,
                animationLength,
                numberOfLoops,
                position: npc.Position + positionOffset,
                flicker: false,
                flipped: flipped,
                layerDepth,
                alphaFade: 0f,
                color: Color.White,
                scale: Game1.pixelZoom,
                scaleChange: 0f,
                rotation: 0f,
                rotationChange: 0f
            ));
        }
        #endregion

        #region Lumbering Task
        /// <summary>Specifies which types of lumbering targets to search for.</summary>
        public enum LumberingTargetType
        {
            /// <summary>Search for twigs (tree stumps) only.</summary>
            TwigsOnly,
            /// <summary>Search for damaged trees only.</summary>
            TreesOnly,
            /// <summary>Search for both twigs and damaged trees.</summary>
            Both
        }

        /// <summary>Checks if the player is currently lumbering with an axe.</summary>
        public static bool IsPlayerLumbering()
        {
            var player = Game1.player;
            if (player == null) return false; // Handle unit test scenarios

            // Check if player has an axe equipped
            if (player.CurrentTool is not StardewValley.Tools.Axe)
                return false;

            // Check if axe is being used
            return player.UsingTool;
        }

        public static (Point? target, Point? interactionPoint) FindLumberingTarget(
            ILocationInfo locationInfo,
            Point playerPosition,
            Point npcPosition,
            int searchRadius,
            ISet<Point> claimedTaskTargets,
            ISet<Vector2> claimedInteractionSpots,
            IMonitor monitor,
            LumberingTargetType targetType = LumberingTargetType.Both)
        {
            var candidates = new List<Point>();

            // Find damaged trees (health between 0 and full health, no tapper)
            if (targetType == LumberingTargetType.Both || targetType == LumberingTargetType.TreesOnly)
            {
                foreach (var (tile, health, hasTapper) in locationInfo.GetTrees(playerPosition, searchRadius))
                {
                    if (health < 1.0f && health > 0.0f && !hasTapper)
                    {
                        candidates.Add(tile);
                    }
                }
            }

            // Find twigs (tree stumps)
            if (targetType == LumberingTargetType.Both || targetType == LumberingTargetType.TwigsOnly)
            {
                foreach (var twigTile in locationInfo.GetTwigs(playerPosition, searchRadius))
                {
                    if (!claimedTaskTargets.Contains(twigTile))
                    {
                        candidates.Add(twigTile);
                    }
                }
            }

            if (!candidates.Any())
                return (null, null);

            // Try to find a valid candidate with accessible interaction spot
            foreach (var candidateTile in candidates)
            {
                Point? interactionSpot = Pathfinding.AStarPathfinder.FindClosestPassableNeighbor(
                    locationInfo, candidateTile, npcPosition, claimedInteractionSpots);

                if (interactionSpot.HasValue)
                {
                    var path = Pathfinding.AStarPathfinder.FindPath(
                        locationInfo, npcPosition, interactionSpot.Value, monitor);

                    if (path != null && path.Count > 0)
                    {
                        // We found a valid, unclaimed spot with a path. Return both.
                        return (candidateTile, interactionSpot.Value);
                    }
                }
            }

            return (null, null); // No suitable target found
        }

        /// <summary>Finds a lumbering target (damaged tree or twig). (Backward-compatible overload)</summary>
        public static (Point? target, Point? interactionPoint) FindLumberingTarget(NPC npc, IMonitor monitor, ISet<Vector2> claimedInteractionSpots, ISet<Point> claimedTaskTargets)
        {
            var locationInfo = new LocationInfoWrapper(npc.currentLocation, npc);
            return FindLumberingTarget(locationInfo, Game1.player.TilePoint, npc.TilePoint, 10, claimedTaskTargets, claimedInteractionSpots, monitor);
        }

        public static bool ExecuteLumberingTask(ISquadMate mate, Point tile)
        {
            NPC npc = mate.Npc;

            var location = npc.currentLocation;
            var tileVector = tile.ToVector2();
            var tempAxe = new Axe { lastUser = Game1.player, UpgradeLevel = GetAxeUpgradeLevel() };
            bool taskCompleted = true; // Assume done if target vanishes.
            bool targetFound = false;

            if (location.terrainFeatures.TryGetValue(tileVector, out var feature) && feature is Tree tree)
            {
                // Skip trees that have a tapper on them (105 = Tapper, 264 = Heavy Tapper)
                if (location.objects.TryGetValue(tileVector, out var obj) &&
                    (obj.QualifiedItemId == "(BC)105" || obj.QualifiedItemId == "(BC)264"))
                {
                    return true; // Task is complete, skip this tree
                }

                tree.performToolAction(tempAxe, 0, tileVector);
                taskCompleted = tree.health.Value <= 0;
                targetFound = true;
            }
            else if (location.objects.TryGetValue(tileVector, out var obj) && obj.Name == "Twig")
            {
                if (obj.performToolAction(tempAxe))
                {
                    location.Objects.Remove(tileVector);
                    taskCompleted = true;
                }
                else
                {
                    taskCompleted = false;
                }
                targetFound = true;
            }

            if (targetFound)
            {
                var targetWorldPosition = tileVector * 64f + new Vector2(32f, 32f);
                FacePosition(npc, targetWorldPosition);
                AnimateLumbering(npc);
                _spriteManager?.ApplyTaskAnimation(npc, "Lumbering", 400);
                npc.shake(400);
                mate.ActionCooldown = 48;
                if (Game1.random.Next(10) == 0)
                {
                    mate.Communicate(TaskType.Lumbering.ToString());
                }
            }

            return taskCompleted;
        }

        public static void AnimateLumbering(NPC npc)
        {
            var location = npc.currentLocation;
            float layerDepth = (npc.FacingDirection == 0) ? (npc.GetBoundingBox().Bottom - 32) / 10000f : (npc.GetBoundingBox().Bottom + 2) / 10000f;

            switch (npc.FacingDirection)
            {
                case 1: // Right
                    {
                        var strikeRotation = (float)Math.PI / 2f; // 90 degrees
                        var sourceRect = new Rectangle(32, 144, 16, 32);

                        // Frame 1: Wind-up
                        var swingFrame1 = new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 75f, 1, 0, npc.Position + new Vector2(16, -103), false, false, layerDepth, 0f, Color.White, 4f, 0f, 0f, 0f);
                        // Frame 2: Strike
                        var swingFrame2 = new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 325f, 1, 0, npc.Position + new Vector2(64, -48), false, false, layerDepth, 0f, Color.White, 4f, 0f, strikeRotation, 0f) { delayBeforeAnimationStart = 75 };

                        location.temporarySprites.Add(swingFrame1);
                        location.temporarySprites.Add(swingFrame2);
                        break;
                    }
                case 3: // Left
                    {
                        var strikeRotation = (float)-Math.PI / 2f; // 90 degrees
                        var sourceRect = new Rectangle(32, 144, 16, 32);

                        // Frame 1: Wind-up
                        var swingFrame1 = new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 75f, 1, 0, npc.Position + new Vector2(-16, -103), false, true, layerDepth, 0f, Color.White, 4f, 0f, 0f, 0f);
                        // Frame 2: Strike
                        var swingFrame2 = new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 325f, 1, 0, npc.Position + new Vector2(-64, -48), false, true, layerDepth, 0f, Color.White, 4f, 0f, strikeRotation, 0f) { delayBeforeAnimationStart = 75 };

                        location.temporarySprites.Add(swingFrame1);
                        location.temporarySprites.Add(swingFrame2);
                        break;
                    }
                case 0: // Up
                case 2: // Down
                default:
                    {
                        Rectangle sourceRect = (npc.FacingDirection == 0) ? new Rectangle(48, 144, 16, 32) : new Rectangle(0, 144, 16, 32);
                        Vector2 positionOffset = (npc.FacingDirection == 0) ? new Vector2(0, -128) : new Vector2(0, -80);
                        location.temporarySprites.Add(new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 200f, 2, 0, npc.Position + positionOffset, false, false, layerDepth, 0f, Color.White, 4f, 0f, 0f, 0f));
                        break;
                    }
            }
        }
        #endregion

        #region Mining Task
        /// <summary>Checks if the player is currently mining with a pickaxe.</summary>
        public static bool IsPlayerMining()
        {
            var player = Game1.player;
            if (player == null) return false; // Handle unit test scenarios

            // Check if player has a pickaxe equipped and is using it
            if (player.CurrentTool is not StardewValley.Tools.Pickaxe)
                return false;

            return player.UsingTool;
        }

        /// <summary>Finds a minable rock within search radius (testable version accepting ILocationInfo).</summary>
        public static (Point? target, Point? interactionPoint) FindMinableRock(
            ILocationInfo locationInfo,
            Point playerPosition,
            Point npcPosition,
            int searchRadius,
            ISet<Vector2> claimedInteractionSpots,
            IMonitor monitor)
        {
            int pickaxeLevel = locationInfo.GetPlayerPickaxeLevel();

            var candidates = new List<Point>();
            foreach (var (tile, parentSheetIndex, readyForHarvest) in locationInfo.GetRocks(playerPosition, searchRadius))
            {
                // Skip rocks that are ready for harvest (geodes, etc.)
                if (readyForHarvest)
                    continue;

                // Check if rock is breakable with current pickaxe level
                bool isBreakable;
                switch (parentSheetIndex)
                {
                    case 46: isBreakable = pickaxeLevel >= 3; break; // Mystic Stone
                    case 765: case 290: case 764: isBreakable = pickaxeLevel >= 2; break; // Iridium & Tough Geodes
                    case 2: case 4: case 6: case 8: case 10: case 12: case 14: isBreakable = pickaxeLevel >= 1; break; // Gem Nodes
                    default: isBreakable = true; break; // Regular stones
                }

                if (isBreakable)
                {
                    candidates.Add(tile);
                }
            }

            if (!candidates.Any())
                return (null, null);

            // Sort by distance from NPC
            var sortedCandidates = candidates.OrderBy(p => Vector2.DistanceSquared(p.ToVector2(), npcPosition.ToVector2()));

            foreach (var candidateTile in sortedCandidates)
            {
                Point? interactionSpot = Pathfinding.AStarPathfinder.FindClosestPassableNeighbor(
                    locationInfo, candidateTile, npcPosition, claimedInteractionSpots);

                if (interactionSpot.HasValue)
                {
                    var path = Pathfinding.AStarPathfinder.FindPath(
                        locationInfo, npcPosition, interactionSpot.Value, monitor);

                    if (path != null && path.Count > 0)
                    {
                        return (candidateTile, interactionSpot.Value);
                    }
                }
            }

            return (null, null);
        }

        /// <summary>Finds a minable rock within search radius (backward-compatible overload).</summary>
        public static (Point? target, Point? interactionPoint) FindMinableRock(NPC npc, IMonitor monitor, ISet<Vector2> claimedInteractionSpots)
        {
            var locationInfo = new LocationInfoWrapper(npc.currentLocation, npc);
            return FindMinableRock(locationInfo, Game1.player.TilePoint, npc.TilePoint, 15, claimedInteractionSpots, monitor);
        }

        public static bool ExecuteMiningTask(ISquadMate mate, Point tile)
        {
            NPC npc = mate.Npc;

            var location = npc.currentLocation;
            var tileVector = tile.ToVector2();

            if (location.objects.TryGetValue(tileVector, out var rock))
            {
                var tempPickaxe = new Pickaxe { lastUser = Game1.player, UpgradeLevel = GetPickaxeUpgradeLevel() };
                rock.performToolAction(tempPickaxe);

                var targetWorldPosition = tileVector * 64f + new Vector2(32f, 32f);
                FacePosition(npc, targetWorldPosition);
                AnimateMining(npc);
                _spriteManager?.ApplyTaskAnimation(npc, "Mining", 400);
                npc.shake(400);
                mate.ActionCooldown = 48;

                if (rock.minutesUntilReady.Value <= 0 && location.objects.ContainsKey(tileVector))
                {
                    location.OnStoneDestroyed(rock.ItemId, tile.X, tile.Y, Game1.player);
                    rock.performRemoveAction();
                    location.Objects.Remove(tileVector);
                    location.playSound("stoneCrack", tileVector);
                    Game1.createRadialDebris(location, 14, tile.X, tile.Y, Game1.random.Next(4, 7), false);
                }

                if (Game1.random.Next(10) == 0)
                {
                    mate.Communicate(TaskType.Mining.ToString());
                }

                // By returning false here, we ensure the task remains active during the cooldown,
                // allowing the animation to play out completely. The task will complete on the
                // next execution attempt after the cooldown, when this `TryGetValue` fails.
                return false;
            }

            // If we're here, it means the rock at the target tile is already gone. The task is complete.
            return true;
        }

        public static void AnimateMining(NPC npc)
        {
            var location = npc.currentLocation;
            float layerDepth = (npc.FacingDirection == 0) ? (npc.GetBoundingBox().Bottom - 32) / 10000f : (npc.GetBoundingBox().Bottom + 2) / 10000f;

            switch (npc.FacingDirection)
            {
                case 1: // Right
                    {
                        var strikeRotation = (float)Math.PI / 2f;
                        var sourceRect = new Rectangle(32, 80, 16, 32); // Pickaxe sprite
                        location.temporarySprites.Add(new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 75f, 1, 0, npc.Position + new Vector2(16, -103), false, false, layerDepth, 0f, Color.White, 4f, 0f, 0f, 0f));
                        location.temporarySprites.Add(new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 325f, 1, 0, npc.Position + new Vector2(64, -48), false, false, layerDepth, 0f, Color.White, 4f, 0f, strikeRotation, 0f) { delayBeforeAnimationStart = 75 });
                        break;
                    }
                case 3: // Left
                    {
                        var strikeRotation = (float)-Math.PI / 2f;
                        var sourceRect = new Rectangle(32, 80, 16, 32); // Pickaxe sprite
                        location.temporarySprites.Add(new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 75f, 1, 0, npc.Position + new Vector2(-16, -103), false, true, layerDepth, 0f, Color.White, 4f, 0f, 0f, 0f));
                        location.temporarySprites.Add(new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 325f, 1, 0, npc.Position + new Vector2(-64, -48), false, true, layerDepth, 0f, Color.White, 4f, 0f, strikeRotation, 0f) { delayBeforeAnimationStart = 75 });
                        break;
                    }
                case 0: // Up
                case 2: // Down
                default:
                    {
                        Rectangle sourceRect = (npc.FacingDirection == 0) ? new Rectangle(48, 80, 16, 32) : new Rectangle(0, 80, 16, 32); // Pickaxe sprite
                        Vector2 positionOffset = (npc.FacingDirection == 0) ? new Vector2(0, -128) : new Vector2(0, -80);
                        location.temporarySprites.Add(new TemporaryAnimatedSprite(Game1.toolSpriteSheet.Name, sourceRect, 200f, 2, 0, npc.Position + positionOffset, false, false, layerDepth, 0f, Color.White, 4f, 0f, 0f, 0f));
                        break;
                    }
            }
        }

        #endregion

        #region Harvesting Task
        /// <summary>Checks if the player is currently harvesting crops.</summary>
        public static bool IsPlayerHarvesting()
        {
            var player = Game1.player;
            if (player == null) return false; // Handle unit test scenarios

            // Check if a crop has been harvested within the last 3 seconds (180 ticks at 60 FPS)
            // This works with MimickingTaskTimer (10 seconds) to provide smooth task management:
            // - IsPlayerHarvesting() returns true for 3 seconds after harvesting
            // - MimickingTaskTimer gives NPCs 10 seconds grace period to complete their tasks
            return (Game1.ticks - Patches.HarmonyPatches.GetLastPlayerHarvestingTick()) < 180;
        }

        /// <summary>Checks if a crop tile is within the specified radius of any beehouse.</summary>
        private static bool IsNearBeehouse(Point cropTile, Abstractions.Location.ILocationInfo locationInfo, int radius)
        {
            // Check if there are any beehouses within the specified radius of this crop
            var beehouses = locationInfo.GetBeehousesInRadius(cropTile, radius);
            return beehouses.Any();
        }

        public static (Point? target, Point? interactionPoint) FindHarvestableCrop(NPC npc, ISet<Point> claimedTaskTargets)
        {
            GameLocation location = npc.currentLocation;
            var searchRadius = 10;
            var playerTile = Game1.player.TilePoint;

            return FindHarvestableCrop(
                new LocationInfoWrapper(location, npc),
                playerTile,
                npc.TilePoint,
                searchRadius,
                claimedTaskTargets,
                _config?.ProtectBeehouseFlowers
            );
        }

        /// <summary>
        /// Testable version: Finds the nearest harvestable crop within a search radius.
        /// </summary>
        /// <param name="locationInfo">Location information provider.</param>
        /// <param name="searchCenter">The center point of the search area (typically player position).</param>
        /// <param name="npcPosition">The NPC's current position for distance sorting.</param>
        /// <param name="searchRadius">The radius in tiles to search around the center.</param>
        /// <param name="claimedTaskTargets">Set of tiles already claimed by other NPCs.</param>
        /// <param name="protectBeehouseFlowers">Number of tiles around beehouses to protect flowers. 0 or null to disable.</param>
        /// <returns>Target tile and interaction point, or (null, null) if no harvestable crop found.</returns>
        public static (Point? target, Point? interactionPoint) FindHarvestableCrop(
            ILocationInfo locationInfo,
            Point searchCenter,
            Point npcPosition,
            int searchRadius,
            ISet<Point> claimedTaskTargets,
            int? protectBeehouseFlowers = null)
        {
            // Use a list to find all potential candidates first.
            var candidates = new List<Point>();

            foreach (var tilePoint in locationInfo.GetTilesInRadius(searchCenter, searchRadius))
            {
                if (claimedTaskTargets.Contains(tilePoint))
                    continue;

                if (locationInfo.HasHarvestableCropAt(tilePoint))
                {
                    candidates.Add(tilePoint);
                }
            }

            if (!candidates.Any()) return (null, null);

            // Filter out flowers near beehouses if protection is enabled
            // Use parameter if provided, otherwise fall back to config. 0 means disabled.
            int protectionRadius = protectBeehouseFlowers ?? (_config?.ProtectBeehouseFlowers ?? 0);
            if (protectionRadius > 0)
            {
                candidates = candidates.Where(cropTile =>
                {
                    // If it's a flower and near a beehouse, filter it out
                    if (locationInfo.IsFlowerCropAt(cropTile) && IsNearBeehouse(cropTile, locationInfo, protectionRadius))
                    {
                        return false; // Filter out this flower
                    }
                    return true; // Keep this crop
                }).ToList();

                if (!candidates.Any()) return (null, null);
            }

            // Sort candidates by distance to the NPC to find the most efficient target.
            var sortedCandidates = candidates.OrderBy(p => Vector2.DistanceSquared(p.ToVector2(), npcPosition.ToVector2()));

            foreach (var cropTile in sortedCandidates)
            {
                var (target, interactionPoint) = CheckForTrellis(locationInfo, cropTile, npcPosition);
                if (interactionPoint.HasValue)
                {
                    return (target, interactionPoint);
                }
            }

            // No suitable harvestable crop was found.
            return (null, null);
        }

        public static bool ExecuteHarvestingTask(ISquadMate mate, Point tile)
        {
            var npc = mate.Npc;
            var location = npc.currentLocation;
            var tileVector = tile.ToVector2();

            if (!location.terrainFeatures.TryGetValue(tileVector, out var feature) || feature is not HoeDirt { crop: { } } dirt || !dirt.readyForHarvest())
            {
                // Crop is not here or not ready, so the task is "complete".
                return true;
            }

            Patches.HarmonyPatches.BeginIgnoreHarvest();
            try
            {
                dirt.crop.harvest(tile.X, tile.Y, dirt, null, true);

                if (dirt.crop == null || dirt.crop.RegrowsAfterHarvest() == false)
                {
                    dirt.destroyCrop(showAnimation: true);
                }
            }
            finally
            {
                Patches.HarmonyPatches.EndIgnoreHarvest();
            }

            _spriteManager?.ApplyTaskAnimation(npc, "Harvesting", 400);
            npc.shake(400);
            mate.ActionCooldown = 48;
            location.playSound("harvest");

            if (Game1.random.Next(10) == 0)
            {
                mate.Communicate(TaskType.Harvesting.ToString());
            }

            // The task is always "complete" after one attempt, whether it succeeded or not.
            return true;
        }
        #endregion

        #region Foraging Task
        /// <summary>
        /// Finds the nearest forageable item, animal product, or harvestable bush within a search radius.
        /// Includes wild forage (berries, mushrooms), animal products (eggs, wool, feathers, truffles), and berry bushes.
        /// </summary>
        /// <param name="locationInfo">Location information provider.</param>
        /// <param name="searchCenter">The center point of the search area (typically player position).</param>
        /// <param name="npcPosition">The NPC's current position for distance sorting.</param>
        /// <param name="searchRadius">The radius in tiles to search around the center.</param>
        /// <param name="claimedTaskTargets">Set of tiles already claimed by other NPCs.</param>
        /// <returns>Target tile and interaction point, or (null, null) if no forageable target found.</returns>
        public static (Point? target, Point? interactionPoint) FindForageableTarget(
            ILocationInfo locationInfo,
            Point searchCenter,
            Point npcPosition,
            int searchRadius,
            ISet<Point> claimedTaskTargets)
        {
            var candidates = new List<Point>();

            // Find loose forage items and animal products (always collected when Foraging task is active)
            // GetForageableItems() returns both wild forage and animal products (eggs, wool, feathers, truffles)
            foreach (var foragePoint in locationInfo.GetForageableItems(searchCenter, searchRadius))
            {
                if (claimedTaskTargets.Contains(foragePoint))
                    continue;

                // Check if the inventory can accept this item before considering it
                // If GetObjectAt returns null (test environment), skip the check
                var obj = locationInfo.GetObjectAt(foragePoint);
                if (obj == null || CanAcceptItem(obj))
                {
                    candidates.Add(foragePoint);
                }
            }

            // Find berry bushes (always collected when Foraging task is active)
            // Note: Berry bushes drop items as debris, so DebrisCollector will handle inventory checks
            foreach (var bushPoint in locationInfo.GetHarvestableBushes(searchCenter, searchRadius))
            {
                if (!claimedTaskTargets.Contains(bushPoint))
                {
                    candidates.Add(bushPoint);
                }
            }

            if (!candidates.Any())
                return (null, null);

            // Sort candidates by distance to the NPC to find the most efficient target
            var sortedCandidates = candidates.OrderBy(p => Vector2.DistanceSquared(p.ToVector2(), npcPosition.ToVector2()));

            foreach (var candidateTile in sortedCandidates)
            {
                Point? interactionSpot = Pathfinding.AStarPathfinder.FindClosestPassableNeighbor(locationInfo, candidateTile, npcPosition);

                if (interactionSpot.HasValue)
                {
                    return (candidateTile, interactionSpot.Value);
                }
            }

            return (null, null);
        }

        public static bool ExecuteForagingTask(ISquadMate mate, Point tile)
        {
            var npc = mate.Npc;
            var location = npc.currentLocation;
            var tileVector = tile.ToVector2();

            bool performedAction = false;

            var bushFeature = location.largeTerrainFeatures.FirstOrDefault(b => b.Tile == tileVector);
            // Check for a berry bush first
            if (bushFeature is Bush bush && bush.tileSheetOffset.Value == 1 && bush.readyForHarvest() && bush.inBloom() && !bush.townBush.Value)
            {
                // The shake method handles dropping the item and changing the bush state.
                bush.shake(bush.Tile, true);
                location.playSound("leafrustle");
                performedAction = true;
            }
            // If not a bush, check for a loose forage item or animal product
            else if (location.objects.TryGetValue(tileVector, out var obj))
            {
                bool isForageItem = obj.isForage();
                bool isAnimalProduct = obj.isAnimalProduct();

                if (isForageItem || isAnimalProduct)
                {
                    bool success = isAnimalProduct
                        ? ExecuteAnimalProductPickup(mate, obj, location)
                        : ExecuteForagePickup(mate, obj);

                    if (success)
                    {
                        location.objects.Remove(tileVector);
                        location.localSound("pickUpItem");
                        performedAction = true;
                    }
                }
            }
            else
            {
                // Target is gone, task is complete
                return true; 
            }

            if (performedAction)
            {
                // Animate and set cooldown
                _spriteManager?.ApplyTaskAnimation(npc, "Foraging", 400);
                npc.shake(400);
                mate.ActionCooldown = 48;

                if (Game1.random.Next(7) == 0)
                {
                    mate.Communicate(TaskType.Foraging.ToString());
                }
            }
            
            // The task is always complete after one attempt.
            return true;
        }

        /// <summary>Handles picking up a loose forage item, applying player professions, and adding it to the appropriate inventory.</summary>
        /// <returns>True if the item was successfully picked up; false otherwise.</returns>
        private static bool ExecuteForagePickup(ISquadMate mate, Item item)
        {
            if (item == null || !CanAcceptItem(item))
                return false;

            Item itemToAdd = item.getOne();

            // Apply Botanist profession (iridium quality on forage)
            if (Game1.player.professions.Contains(Farmer.botanist))
            {
                itemToAdd.Quality = 4;
            }

            // Add the primary item. If it fails, something is wrong, so abort.
            if (!TryAddItemToInventory(itemToAdd))
            {
                return false;
            }

            // Apply Gatherer profession (20% chance for double forage)
            if (Game1.player.professions.Contains(Farmer.gatherer) && Game1.random.NextDouble() < 0.20)
            {
                // Try to add a second item. We don't care if this one fails.
                TryAddItemToInventory(itemToAdd.getOne());
            }

            // Grant the player foraging experience
            Game1.player.gainExperience(2, 7); // 2 = Foraging, 7 = XP amount per item

            return true;
        }

        /// <summary>Handles picking up an animal product and adding it to the appropriate inventory.</summary>
        /// <returns>True if the item was successfully picked up; false otherwise.</returns>
        private static bool ExecuteAnimalProductPickup(ISquadMate mate, Item item, GameLocation location)
        {
            if (item == null || !CanAcceptItem(item))
                return false;

            Item itemToAdd = item.getOne();

            // Add the item. If it fails, something is wrong, so abort.
            if (!TryAddItemToInventory(itemToAdd))
            {
                return false;
            }

            // Grant experience based on vanilla behavior:
            // - Inside coops/barns: 5 Farming XP
            // - Outside (farm/elsewhere): 5 Foraging XP
            bool isInsideCoopOrBarn = location is Coop || location is Barn;

            if (isInsideCoopOrBarn)
            {
                Game1.player.gainExperience(0, 5); // 0 = Farming
            }
            else
            {
                Game1.player.gainExperience(2, 5); // 2 = Foraging
            }

            return true;
        }
        #endregion

        #region Fishing Task
        /// <summary>Checks if the player is currently fishing (includes mini-game AND reeling animation).</summary>
        public static bool IsPlayerFishing()
        {
            var player = Game1.player;
            if (player == null) return false; // Handle unit test scenarios

            // Check if player has a fishing rod equipped
            if (player.CurrentTool is not StardewValley.Tools.FishingRod rod)
                return false;

            // Player is fishing if line is cast or fish is hooked
            if (rod.isFishing || rod.hit)
                return true;

            // Also check if the fishing mini-game (BobberBar) is active
            // This covers the case where the mini-game is open but rod.isFishing might be false
            if (Game1.activeClickableMenu != null && Game1.activeClickableMenu.GetType().Name == "BobberBar")
                return true;

            // Check if player is reeling in a fish (animation after mini-game)
            // This keeps fishing tasks alive until after OnPlayerCaughtFish() is called
            if (rod.pullingOutOfWater)
                return true;

            return false;
        }

        /// <summary>Creates a fishing task for an NPC when the player is fishing. Returns null if no valid fishing spot is found.</summary>
        public static SquadTask? CreateFishingTask(NPC npc, ISet<Vector2> claimedInteractionSpots, ISet<Point> claimedTaskTargets, IMonitor monitor)
        {
            var location = npc.currentLocation;
            var locationInfo = new LocationInfoWrapper(location, npc);
            return CreateFishingTask(locationInfo, Game1.player.TilePoint, npc.TilePoint, claimedInteractionSpots, claimedTaskTargets, monitor);
        }

        /// <summary>Creates a fishing task for an NPC (testable overload).</summary>
        public static SquadTask? CreateFishingTask(
            ILocationInfo locationInfo,
            Point playerPosition,
            Point npcPosition,
            ISet<Vector2> claimedInteractionSpots,
            ISet<Point> claimedTaskTargets,
            IMonitor monitor)
        {
            // Find nearby water tiles (sorted by distance for efficiency)
            var waterTiles = FindNearbyWaterTiles(locationInfo, npcPosition, FishingConstants.WaterSearchRadius);

            // Limit how many water tiles we check for performance
            // Most NPCs will find a spot in the first few tiles
            int maxWaterTilesToCheck = Math.Min(waterTiles.Count, 30);

            for (int i = 0; i < maxWaterTilesToCheck; i++)
            {
                var waterTile = waterTiles[i];

                // Skip water tiles that are already being fished by another NPC
                if (claimedTaskTargets.Contains(waterTile))
                    continue;

                // Skip water tiles that aren't valid for fishing
                // In the testable version, we rely on ILocationInfo.IsWaterTile to determine validity
                if (!locationInfo.IsWaterTile(waterTile))
                    continue;

                // Try to find a valid standing position next to this water tile
                Point? standingSpot = FindFishingSpot(locationInfo, waterTile, playerPosition, npcPosition, claimedInteractionSpots, monitor);
                if (standingSpot.HasValue)
                {
                    // Claim the spot atomically to prevent race conditions
                    claimedInteractionSpots.Add(standingSpot.Value.ToVector2());

                    // Found a valid spot! Return immediately to avoid unnecessary searching
                    return new SquadTask(TaskType.Fishing, waterTile, standingSpot.Value);
                }
            }

            // No valid fishing spots found - NPC will not participate in fishing
            return null;
        }

        /// <summary>Finds a valid fishing spot adjacent to or near a water tile, with pathfinding validation.</summary>
        public static Point? FindFishingSpot(ILocationInfo locationInfo, Point waterTile, Point playerPosition,
            Point npcPosition, ISet<Vector2> claimedInteractionSpots, IMonitor monitor)
        {
            // Check tiles adjacent to water and tiles further away (for docks/piers and cliffs)
            // Include both cardinal and diagonal directions for better cliff coverage
            Point[] directions = new[]
            {
                new Point(0, -1),   // North
                new Point(0, 1),    // South
                new Point(-1, 0),   // West
                new Point(1, 0),    // East
                new Point(-1, -1),  // Northwest
                new Point(1, -1),   // Northeast
                new Point(-1, 1),   // Southwest
                new Point(1, 1),    // Southeast
            };

            var candidates = new List<Point>();

            foreach (var dir in directions)
            {
                // Check tile directly adjacent to water
                var adjacentTile = new Point(waterTile.X + dir.X, waterTile.Y + dir.Y);

                if (IsValidFishingSpot(locationInfo, adjacentTile, playerPosition, claimedInteractionSpots, npcPosition))
                {
                    candidates.Add(adjacentTile);
                }

                // Check tiles up to 2 steps away (for docks and diagonal cliffs) if tiles in between are impassable
                for (int distance = 2; distance <= 2; distance++)
                {
                    var distantTile = new Point(waterTile.X + dir.X * distance, waterTile.Y + dir.Y * distance);

                    // Verify at least one tile in between is impassable (cliff, dock, etc.)
                    bool hasImpassableInBetween = false;
                    for (int d = 1; d < distance; d++)
                    {
                        var inBetweenTile = new Point(waterTile.X + dir.X * d, waterTile.Y + dir.Y * d);
                        if (!locationInfo.IsTilePassable(inBetweenTile))
                        {
                            hasImpassableInBetween = true;
                            break;
                        }
                    }

                    if (hasImpassableInBetween && IsValidFishingSpot(locationInfo, distantTile, playerPosition, claimedInteractionSpots, npcPosition))
                    {
                        candidates.Add(distantTile);
                    }
                }
            }

            if (!candidates.Any())
                return null;

            // Sort candidates by distance to NPC for optimal searching
            var sortedCandidates = candidates.OrderBy(p => Vector2.DistanceSquared(p.ToVector2(), npcPosition.ToVector2())).ToList();

            // First pass: Check for unobstructed paths (fast check)
            foreach (var candidateSpot in sortedCandidates)
            {
                if (Pathfinding.AStarPathfinder.IsPathUnobstructed(locationInfo, npcPosition, candidateSpot))
                {
                    // IMPORTANT: Verify the spot is "escapable" - NPC can path back from it
                    if (IsSpotEscapable(locationInfo, candidateSpot, playerPosition, npcPosition, monitor))
                    {
                        return candidateSpot; // Found a valid, escapable spot
                    }
                }
            }

            // Second pass: Try expensive A* pathfinding, but only for top N candidates to reduce lag
            int pathfindingAttempts = 0;
            foreach (var candidateSpot in sortedCandidates)
            {
                if (pathfindingAttempts >= FishingConstants.MaxPathfindingCandidates)
                    break; // Limit expensive pathfinding operations

                var path = Pathfinding.AStarPathfinder.FindPath(locationInfo, npcPosition, candidateSpot, monitor);
                if (path != null && path.Count > 0)
                {
                    // IMPORTANT: Verify the spot is "escapable" - NPC can path back from it
                    if (IsSpotEscapable(locationInfo, candidateSpot, playerPosition, npcPosition, monitor))
                    {
                        return candidateSpot; // Found a valid, escapable spot
                    }
                }

                pathfindingAttempts++;
            }

            // No reachable fishing spots found
            return null;
        }

        /// <summary>Finds a valid fishing spot adjacent to or near a water tile. (Backward-compatible overload)</summary>
        private static Point? FindFishingSpot(GameLocation location, Point waterTile, NPC npc, ISet<Vector2> claimedInteractionSpots, IMonitor monitor)
        {
            var locationInfo = new LocationInfoWrapper(location, npc);
            return FindFishingSpot(locationInfo, waterTile, Game1.player.TilePoint, npc.TilePoint, claimedInteractionSpots, monitor);
        }

        /// <summary>Checks if an NPC can pathfind away from a fishing spot (to avoid getting trapped).</summary>
        public static bool IsSpotEscapable(ILocationInfo locationInfo, Point spot, Point playerPosition,
            Point npcPosition, IMonitor monitor)
        {
            // Quick check: If there's unobstructed path back to player, it's escapable
            if (Pathfinding.AStarPathfinder.IsPathUnobstructed(locationInfo, spot, playerPosition))
            {
                return true;
            }

            // Slower check: Try to find a path from the fishing spot to a nearby passable tile
            // We don't need to path all the way back to player, just verify they can escape
            Point[] nearbyTargets = new[]
            {
                new Point(spot.X + 2, spot.Y),     // East
                new Point(spot.X - 2, spot.Y),     // West
                new Point(spot.X, spot.Y + 2),     // South
                new Point(spot.X, spot.Y - 2),     // North
            };

            foreach (var target in nearbyTargets)
            {
                if (locationInfo.IsTilePassable(target))
                {
                    var path = Pathfinding.AStarPathfinder.FindPath(locationInfo, spot, target, monitor);
                    if (path != null && path.Count > 0)
                    {
                        return true; // Found a valid escape route
                    }
                }
            }

            // Couldn't find any escape route - this spot is a trap
            return false;
        }

        /// <summary>Checks if an NPC can pathfind away from a fishing spot. (Backward-compatible overload)</summary>
        private static bool IsSpotEscapable(GameLocation location, Point spot, NPC npc, IMonitor monitor)
        {
            var locationInfo = new LocationInfoWrapper(location, npc);
            return IsSpotEscapable(locationInfo, spot, Game1.player.TilePoint, npc.TilePoint, monitor);
        }

        /// <summary>Checks if a water tile is valid for fishing (no objects, not blocked by buildings).</summary>
        private static bool IsValidWaterTile(GameLocation location, Point waterTile)
        {
            // Skip water tiles that have objects on them (e.g., water lilies, coral, seaweed)
            if (location.objects.ContainsKey(waterTile.ToVector2()))
                return false;

            // Skip water tiles with Buildings layer tiles (these are often cliffs, bridges, or other obstacles)
            var buildingLayer = location.Map.GetLayer("Buildings");
            if (buildingLayer != null && waterTile.X >= 0 && waterTile.Y >= 0 &&
                waterTile.X < buildingLayer.LayerWidth && waterTile.Y < buildingLayer.LayerHeight)
            {
                if (buildingLayer.Tiles[waterTile.X, waterTile.Y] != null)
                {
                    // Check if this is explicitly marked as fishable water (some bridges/docks allow fishing)
                    string waterProperty = location.doesTileHaveProperty(waterTile.X, waterTile.Y, "Water", "Buildings");
                    if (waterProperty == null || !waterProperty.Equals("T", StringComparison.OrdinalIgnoreCase))
                    {
                        return false; // This is a cliff or non-fishable building tile
                    }
                }
            }

            return true;
        }

        /// <summary>Checks if a tile is a valid fishing spot.</summary>
        public static bool IsValidFishingSpot(ILocationInfo locationInfo, Point tile, Point playerPosition,
            ISet<Vector2> claimedInteractionSpots, Point npcPosition)
        {
            // Skip if already claimed by another NPC
            if (claimedInteractionSpots.Contains(tile.ToVector2()))
                return false;

            // Skip if player is on this tile
            if (playerPosition == tile)
                return false;

            // Ensure NPCs maintain distance from the player to avoid crowding
            float distanceToPlayer = Vector2.Distance(tile.ToVector2(), playerPosition.ToVector2());
            if (distanceToPlayer < FishingConstants.MinFishingDistance)
                return false;

            // Ensure NPCs maintain distance from other fishing NPCs to avoid crowding
            foreach (var claimedSpot in claimedInteractionSpots)
            {
                float distanceToOtherNpc = Vector2.Distance(tile.ToVector2(), claimedSpot);
                if (distanceToOtherNpc < FishingConstants.MinFishingDistance)
                    return false;
            }

            // Check if tile is passable
            if (!locationInfo.IsTilePassable(tile))
                return false;

            return true;
        }

        /// <summary>Checks if a tile is a valid fishing spot. (Backward-compatible overload)</summary>
        private static bool IsValidFishingSpot(GameLocation location, Point tile, ISet<Vector2> claimedInteractionSpots, NPC npc)
        {
            var locationInfo = new LocationInfoWrapper(location, npc);
            return IsValidFishingSpot(locationInfo, tile, Game1.player.TilePoint, claimedInteractionSpots, npc.TilePoint);
        }

        /// <summary>Executes the fishing task - handles animation.</summary>
        public static bool ExecuteFishingTask(ISquadMate mate)
        {
            var npc = mate.Npc;

            // Halt the NPC to stop any walking animation
            npc.Halt();

            // If we have a water tile target, face it
            if (mate.Task?.Tile != null)
            {
                var waterWorldPosition = mate.Task.Tile.ToVector2() * 64f + new Vector2(32f, 32f);
                FacePosition(npc, waterWorldPosition);
            }

            // Force apply fishing pose animation for continuous task
            // This handles frame animation and flipping consistently each tick
            _spriteManager?.ForceApplyTaskAnimation(npc, "Fishing");

            // Fishing task never "completes" - it continues as long as player is fishing
            return false;
        }

        /// <summary>Calculates the fishing catch chance based on squad size.</summary>
        /// <param name="squadSize">The total number of recruited squad members</param>
        /// <returns>Catch chance as a value between 0.1 (10%) and 0.5 (50%)</returns>
        public static double CalculateFishingCatchChance(int squadSize)
        {
            // 50% with 1 NPC, decreasing linearly to 10% with 10+ NPCs
            if (squadSize <= 1)
                return 0.5;
            if (squadSize >= 10)
                return 0.1;

            // Linear interpolation: 50% - ((squadSize - 1) * 40% / 9)
            return 0.5 - ((squadSize - 1) * 0.4 / 9.0);
        }

        /// <summary>Calculates the dialogue probability multiplier based on squad size.</summary>
        /// <param name="squadSize">The total number of recruited squad members</param>
        /// <returns>Probability multiplier as a value between 0.3 (30%) and 1.0 (100%)</returns>
        public static double CalculateDialogueProbabilityMultiplier(int squadSize)
        {
            // 100% with 1 NPC, decreasing linearly to 30% with 10+ NPCs
            if (squadSize <= 1)
                return 1.0;
            if (squadSize >= 10)
                return 0.3;

            // Linear interpolation: 1.0 - ((squadSize - 1) * 0.7 / 9)
            return 1.0 - ((squadSize - 1) * 0.7 / 9.0);
        }

        /// <summary>Attempts to catch a fish for an NPC based on the fishing location's loot table.</summary>
        /// <param name="mate">The squad member attempting to catch a fish</param>
        /// <param name="squadSize">The total number of recruited squad members</param>
        public static void TryNpcCatchFish(ISquadMate mate, int squadSize)
        {
            // Calculate catch chance based on squad size:
            // 50% with 1 NPC, decreasing linearly to 10% with 10+ NPCs
            double catchChance = CalculateFishingCatchChance(squadSize);

            if (Game1.random.NextDouble() >= catchChance)
                return;

            var npc = mate.Npc;
            var location = npc.currentLocation;

            // Find the nearest water tile to the NPC
            var waterTiles = FindNearbyWaterTiles(npc);
            if (!waterTiles.Any())
                return;
            Point waterTile = waterTiles.First();

            // Get a fish from the location's loot table
            Item fish = GetFishFromLocation(location, waterTile, npc);
            if (fish != null)
            {
                CatchFishForNpc(mate, fish);
            }
        }

        /// <summary>Finds nearby water tiles to an NPC, sorted by distance.</summary>
        public static List<Point> FindNearbyWaterTiles(ILocationInfo locationInfo, Point npcPosition, int searchRadius)
        {
            var waterTiles = new List<Point>();

            // Search in expanding rings around the NPC
            for (int radius = 1; radius <= searchRadius; radius++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        // Only check tiles on the edge of the current radius
                        if (Math.Abs(x) != radius && Math.Abs(y) != radius)
                            continue;

                        var tile = new Point(npcPosition.X + x, npcPosition.Y + y);

                        // Check if this tile is water
                        if (locationInfo.IsWaterTile(tile))
                        {
                            waterTiles.Add(tile);
                        }
                    }
                }
            }

            // Sort by distance to NPC for optimal searching
            return waterTiles.OrderBy(t => Vector2.DistanceSquared(t.ToVector2(), npcPosition.ToVector2())).ToList();
        }

        /// <summary>Finds nearby water tiles to an NPC, sorted by distance. (Backward-compatible overload)</summary>
        private static List<Point> FindNearbyWaterTiles(NPC npc)
        {
            var locationInfo = new LocationInfoWrapper(npc.currentLocation, npc);
            return FindNearbyWaterTiles(locationInfo, npc.TilePoint, FishingConstants.WaterSearchRadius);
        }

        /// <summary>Checks if a fish ID represents a legendary fish.</summary>
        private static bool IsLegendaryFish(int fishId)
        {
            // Base game legendary fish
            if (fishId == 159 || fishId == 160 || fishId == 163 || fishId == 775 || fishId == 682)
                return true;

            // Extended Family legendary fish (from 1.6 update)
            if (fishId >= 898 && fishId <= 902)
                return true;

            return false;
        }

        /// <summary>Gets a random fish from the location's fishing loot table.</summary>
        private static Item GetFishFromLocation(GameLocation location, Point waterTile, NPC npc)
        {
            var player = Game1.player;

            // Use the game's getFish method to determine what can be caught
            // This respects season, weather, time of day, fishing level, etc.
            try
            {
                Item fish = null;
                int maxAttempts = 10; // Prevent infinite loops in case location only has legendary fish
                int attempts = 0;

                // Keep trying until we get a non-legendary fish or hit max attempts
                while (attempts < maxAttempts)
                {
                    // Get the fish that can be caught at this location
                    // The getFish method returns the fish Item based on many factors
                    fish = location.getFish(
                        millisecondsAfterNibble: 0f,
                        bait: null,
                        waterDepth: FishingConstants.WaterDepth,
                        who: player,
                        baitPotency: 0.0,
                        bobberTile: waterTile.ToVector2()
                    );

                    if (fish == null)
                        return null; // No fish available in this location

                    // If it's not a legendary fish, we can use it
                    if (!IsLegendaryFish(fish.ParentSheetIndex))
                        break;

                    // It was legendary, try again
                    attempts++;
                }

                // If we exhausted all attempts and still have a legendary fish, return null
                if (fish != null && IsLegendaryFish(fish.ParentSheetIndex))
                    return null;

                // Apply quality based on fishing level
                int quality = DetermineFishQuality(player.FishingLevel);
                fish.Quality = quality;

                return fish;
            }
            catch
            {
                // If anything goes wrong, return null
                return null;
            }
        }

        /// <summary>Determines fish quality based on fishing level.</summary>
        public static int DetermineFishQuality(int fishingLevel)
        {
            // Use similar logic to the game's quality determination
            double qualityRoll = Game1.random.NextDouble();

            // Quality chances increase with fishing level
            double silverChance = Math.Min(fishingLevel / FishingConstants.SilverChanceDivisor, FishingConstants.SilverChanceMax);
            double goldChance = Math.Min(fishingLevel / FishingConstants.GoldChanceDivisor, FishingConstants.GoldChanceMax);
            double iridiumChance = Math.Min(fishingLevel / FishingConstants.IridiumChanceDivisor, FishingConstants.IridiumChanceMax);

            if (qualityRoll < iridiumChance)
                return 4; // Iridium
            if (qualityRoll < goldChance)
                return 2; // Gold
            if (qualityRoll < silverChance)
                return 1; // Silver

            return 0; // Normal
        }

        /// <summary>Gives an NPC a caught fish and shows the fish icon above their head.</summary>
        private static void CatchFishForNpc(ISquadMate mate, Item fish)
        {
            var npc = mate.Npc;

            // Add the fish to the appropriate inventory
            Item fishCopy = fish.getOne();
            if (!TryAddItemToInventory(fishCopy))
            {
                // Inventory full, don't give fish
                return;
            }

            // Play sound and show fish icon
            Game1.playSound("fishSlap");
            ShowFishIconAboveNpc(npc, fish);

            // Optional "caught" dialogue
            if (Game1.random.Next(2) == 0)
            {
                mate.Communicate("Fishing_Caught");
            }

            // Set a brief cooldown
            mate.ActionCooldown = FishingConstants.CatchCooldown;
        }

        /// <summary>Displays a fish icon above the NPC's head that floats upward.</summary>
        private static void ShowFishIconAboveNpc(NPC npc, Item fish)
        {
            var location = npc.currentLocation;
            if (location == null)
                return;

            // Get the fish sprite from the object sprite sheet
            int fishIndex = fish.ParentSheetIndex;
            Rectangle sourceRect = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, fishIndex, 16, 16);

            // Position above the NPC's head
            Vector2 position = npc.Position + new Vector2(16f, -96f);

            // Create a temporary animated sprite that floats up and fades
            var fishIcon = new TemporaryAnimatedSprite(
                textureName: Game1.objectSpriteSheet.Name,
                sourceRect: sourceRect,
                animationInterval: FishingConstants.FishIconAnimationInterval,
                animationLength: 1,
                numberOfLoops: 0,
                position: position,
                flicker: false,
                flipped: false,
                layerDepth: (npc.GetBoundingBox().Bottom + 128) / 10000f,
                alphaFade: FishingConstants.FishIconAlphaFade,
                color: Color.White,
                scale: FishingConstants.FishIconScale,
                scaleChange: 0f,
                rotation: 0f,
                rotationChange: 0f
            )
            {
                motion = new Vector2(0f, FishingConstants.FishIconMotionY),
                yStopCoordinate = (int)(position.Y + FishingConstants.FishIconStopOffset)
            };

            location.temporarySprites.Add(fishIcon);
        }

        /// <summary>Draws fishing rod and bobber sprites on NPC to mimic fishing action.</summary>
        public static void AnimateFishing(NPC npc, Point waterTile)
        {
            var location = npc.currentLocation;
            if (location == null)
                return;

            // Calculate bobbing animation (same as bobber animation for consistency)
            float bobOffset = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / FishingConstants.RodBobbingPeriod) * FishingConstants.BobbingAmplitude;

            // Get bobber style from player's fishing rod (vanilla behavior)
            int bobberStyle = 0;
            if (Game1.player.CurrentTool is StardewValley.Tools.FishingRod rod)
            {
                bobberStyle = rod.getBobberStyle(Game1.player);
            }

            // Calculate bobber position (with desynchronized bobbing animation)
            Vector2 bobberPos = CalculateBobberPosition(npc, waterTile);
            Rectangle bobberSourceRect = Game1.getSourceRectForStandardTileSheet(Game1.bobbersTexture, bobberStyle, 16, 32);
            bobberSourceRect.Height = 16;
            bobberSourceRect.Y += 16;
            float bobberLayerDepth = bobberPos.Y / 10000f;

            // Add the bobber as a temporary sprite
            location.temporarySprites.Add(new TemporaryAnimatedSprite(
                textureName: Game1.bobbersTexture.Name,
                sourceRect: bobberSourceRect,
                animationInterval: FishingConstants.RodAnimationInterval,
                animationLength: 1,
                numberOfLoops: 0,
                position: bobberPos - new Vector2(8f * FishingConstants.RodScale, 8f * FishingConstants.RodScale), // Adjust for origin offset
                flicker: false,
                flipped: (npc.FacingDirection == 1), // Flip horizontally when facing right
                layerDepth: bobberLayerDepth,
                alphaFade: 0f,
                color: Color.White,
                scale: FishingConstants.RodScale,
                scaleChange: 0f,
                rotation: 0f,
                rotationChange: 0f
            ));

            // Draw fishing rod sprite based on NPC facing direction (skip for facing up)
            if (npc.FacingDirection == 0)
                return; // Up/Back - no rod sprite needed, line and bobber are enough

            // Using sprites from tools.png tilesheet
            Rectangle sourceRect;
            Vector2 positionOffset;
            float layerDepth;
            bool flip = false;

            switch (npc.FacingDirection)
            {
                case 0: // Up/Back - no rod sprite needed, line and bobber are enough
                    return;

                case 1: // Right - 32x32 sprite at (0, 288)
                    sourceRect = new Rectangle(256, 304, 32, 32);
                    positionOffset = new Vector2(-24f, -56f + bobOffset); // Position relative to NPC with bobbing
                    layerDepth = (npc.GetBoundingBox().Bottom + 1) / 10000f;
                    break;

                case 2: // Down/Front - 16x32 sprite at (208, 352)
                    sourceRect = new Rectangle(256, 352, 16, 32);
                    positionOffset = new Vector2(0f, -40f + bobOffset); // Add bobbing animation
                    layerDepth = (npc.GetBoundingBox().Bottom + 1) / 10000f;
                    break;

                case 3: // Left - mirrored 32x32 sprite at (0, 288)
                    sourceRect = new Rectangle(256, 304, 32, 32);
                    positionOffset = new Vector2(-32f, -56f + bobOffset); // Add bobbing animation
                    layerDepth = (npc.GetBoundingBox().Bottom + 1) / 10000f;
                    flip = true;
                    break;

                default:
                    return;
            }

            // Add the fishing rod as a temporary sprite overlay on the NPC
            location.temporarySprites.Add(new TemporaryAnimatedSprite(
                textureName: Game1.toolSpriteSheetName,
                sourceRect: sourceRect,
                animationInterval: FishingConstants.RodAnimationInterval,
                animationLength: 1,
                numberOfLoops: 0,
                position: npc.Position + positionOffset,
                flicker: false,
                flipped: flip,
                layerDepth: layerDepth,
                alphaFade: 0f,
                color: Color.White,
                scale: FishingConstants.RodScale,
                scaleChange: 0f,
                rotation: 0f,
                rotationChange: 0f
            ));
        }

        /// <summary>Gets the position of the fishing rod tip based on NPC facing direction using vanilla offsets.</summary>
        public static Vector2 GetRodTipPosition(NPC npc)
        {
            // Use vanilla fishing rod tip offsets
            // Note: NPCs don't have armOffset, so we use Position directly
            Vector2 npcPosition = npc.Position;

            // Calculate bobbing animation to match the fishing rod sprite
            float bobOffset = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / FishingConstants.RodBobbingPeriod) * FishingConstants.BobbingAmplitude;

            switch (npc.FacingDirection)
            {
                case 0: // Up
                    return npcPosition + new Vector2(28f, -32f + bobOffset);
                case 1: // Right
                    return npcPosition + new Vector2(100f, -16f + bobOffset);
                case 2: // Down
                    return npcPosition + new Vector2(28f, 70f + bobOffset);
                case 3: // Left
                    return npcPosition + new Vector2(-32f, -16f + bobOffset);
                default:
                    return npcPosition;
            }
        }

        /// <summary>Calculates the bobber position for a fishing NPC.</summary>
        public static Vector2 CalculateBobberPosition(NPC npc, Point waterTile)
        {
            // Offset the bobber one tile in the direction the NPC is facing
            int offsetX = 0;
            int offsetY = 0;
            switch (npc.FacingDirection)
            {
                case 0: // Up
                    offsetY = -1;
                    break;
                case 1: // Right
                    offsetX = 1;
                    break;
                case 2: // Down
                    offsetY = 1;
                    break;
                case 3: // Left
                    offsetX = -1;
                    break;
            }

            // Calculate bobber position - center of target tile
            float centerX = (waterTile.X + offsetX) * 64f + 32f;
            float centerY = (waterTile.Y + offsetY) * 64f + 32f;

            // Add bobbing animation (desynchronized from rod for more realistic look)
            // Using different period and phase offset to create independent movement
            float bobOffset = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / FishingConstants.BobberBobbingPeriod + FishingConstants.BobberBobbingPhaseOffset) * FishingConstants.BobbingAmplitude;

            return new Vector2(centerX, centerY + bobOffset);
        }

        /// <summary>Draws a curved fishing line from rod tip to bobber using vanilla technique.</summary>
        public static void DrawFishingLine(SpriteBatch spriteBatch, Vector2 rodTip, Vector2 bobber, float layerDepth)
        {
            // Adjust bobber position to point to center-top instead of center
            // Bobber is drawn with origin (8, 8) at 4x scale = 32 pixels from top
            Vector2 bobberTopCenter = new Vector2(bobber.X, bobber.Y + FishingConstants.BobberTopCenterOffset);

            // Convert to screen coordinates
            Vector2 v1 = Game1.GlobalToLocal(Game1.viewport, rodTip);
            Vector2 localBobber = Game1.GlobalToLocal(Game1.viewport, bobberTopCenter);

            // Calculate Bezier curve control points using vanilla formula
            Vector2 v2 = new Vector2(
                v1.X + (localBobber.X - v1.X) / 3f,
                v1.Y + (localBobber.Y - v1.Y) * 2f / 3f
            );
            Vector2 v3 = new Vector2(
                v1.X + (localBobber.X - v1.X) * 2f / 3f,
                v1.Y + (localBobber.Y - v1.Y) * 6f / 5f // Using fishing state (6/5 for active fishing)
            );
            Vector2 v4 = localBobber;

            // Get the white pixel texture for drawing lines
            Texture2D whitePixel = Game1.staminaRect;

            // Draw curved line segments
            Vector2 lastPosition = v1;
            for (float t = 0f; t < 1f; t += 0.025f)
            {
                Vector2 current = Utility.GetCurvePoint(t, v1, v2, v3, v4);

                // Calculate line segment
                Vector2 diff = current - lastPosition;
                float length = diff.Length();
                float angle = (float)Math.Atan2(diff.Y, diff.X);

                // Draw line segment with layer depth control (50% transparency for subtle appearance)
                spriteBatch.Draw(
                    whitePixel,
                    lastPosition,
                    null,
                    Color.White * 0.5f,
                    angle,
                    Vector2.Zero,
                    new Vector2(length, 1f),
                    SpriteEffects.None,
                    layerDepth
                );

                lastPosition = current;
            }
        }

        /// <summary>Renders fishing bobbers and lines for all NPCs with fishing tasks in the squad.</summary>
        public static void RenderFishing(SpriteBatch spriteBatch, SquadManager squadManager)
        {
            if (!Context.IsWorldReady || squadManager.Count == 0)
                return;

            foreach (var mate in squadManager.Members)
            {
                RenderFishingForNpc(spriteBatch, mate);
            }
        }

        /// <summary>Renders fishing line for an NPC with a fishing task. (Bobber is now rendered via TemporaryAnimatedSprite)</summary>
        private static void RenderFishingForNpc(SpriteBatch spriteBatch, ISquadMate mate)
        {
            var npc = mate.Npc;

            // Only render for NPCs with fishing tasks who are at their fishing spot
            if (mate.Task?.Type != TaskType.Fishing || npc.TilePoint != mate.Task.InteractionTile)
                return;

            // Skip fishing line when facing up (the Harmony patch handles that to draw behind the NPC)
            if (npc.FacingDirection == 0)
                return;

            // Get bobber position for fishing line endpoint
            var waterTile = mate.Task.Tile;
            Vector2 bobberPos = CalculateBobberPosition(npc, waterTile);

            // Calculate rod tip position based on NPC facing direction
            Vector2 rodTipPosition = GetRodTipPosition(npc);

            // Draw the fishing line from rod tip to bobber
            DrawFishingLine(spriteBatch, rodTipPosition, bobberPos, FishingConstants.FishingLineLayerDepth);
        }
        #endregion

        #region Petting Task
        /// <summary>Checks if the player is currently petting animals.</summary>
        public static bool IsPlayerPetting()
        {
            var player = Game1.player;
            if (player == null) return false; // Handle unit test scenarios

            // Check if player has petted an animal within the last 3 seconds (180 ticks at 60 FPS)
            // This works with MimickingTaskTimer (10 seconds) to provide smooth task management:
            // - IsPlayerPetting() returns true for 3 seconds after petting
            // - MimickingTaskTimer gives NPCs 10 seconds grace period to complete their tasks
            return (Game1.ticks - Patches.HarmonyPatches.GetLastPlayerPettingTick()) < 180;
        }

        /// <summary>Finds an unpetted animal (farm animal or player's pet) for an NPC to pet.</summary>
        public static (Point? target, Point? interactionPoint) FindPettableAnimal(
            NPC npc,
            IMonitor monitor,
            ISet<Vector2> claimedInteractionSpots,
            ISet<Point> claimedTaskTargets)
        {
            // Backward-compatible wrapper that uses the testable version
            var location = npc.currentLocation;
            var playerTile = Game1.player.Tile.ToPoint();
            int searchRadius = 15;

            return FindPettableAnimal(
                new LocationInfoWrapper(location, npc),
                playerTile,
                npc.TilePoint,
                searchRadius,
                claimedTaskTargets);
        }

        /// <summary>
        /// Testable version of FindPettableAnimal that uses ILocationInfo abstraction.
        /// Finds an unpetted animal (farm animal or player's pet) for an NPC to pet.
        /// </summary>
        public static (Point? target, Point? interactionPoint) FindPettableAnimal(
            ILocationInfo locationInfo,
            Point searchCenter,
            Point npcPosition,
            int searchRadius,
            ISet<Point> claimedTaskTargets)
        {
            var candidates = new List<Point>();

            // Find unpetted farm animals
            foreach (var (animal, tile) in locationInfo.GetUnpettedFarmAnimals(searchCenter, searchRadius))
            {
                if (!claimedTaskTargets.Contains(tile))
                {
                    candidates.Add(tile);
                }
            }

            // Check for player's pet (Cat/Dog)
            var petInfo = locationInfo.GetUnpettedPlayerPet(searchCenter, searchRadius);
            if (petInfo.HasValue && !claimedTaskTargets.Contains(petInfo.Value.tile))
            {
                candidates.Add(petInfo.Value.tile);
            }

            if (!candidates.Any())
                return (null, null);

            // Sort by distance to NPC (closest first)
            var sortedCandidates = candidates
                .OrderBy(tile => Vector2.DistanceSquared(tile.ToVector2(), npcPosition.ToVector2()));

            // Find first reachable animal
            foreach (var animalTile in sortedCandidates)
            {
                // Find standing position next to animal
                Point? interactionSpot = Pathfinding.AStarPathfinder.FindClosestPassableNeighbor(
                    locationInfo, animalTile, npcPosition);

                if (interactionSpot.HasValue)
                {
                    return (animalTile, interactionSpot.Value);
                }
            }

            return (null, null);
        }

        /// <summary>Executes the petting task - pets the animal and completes the task.</summary>
        public static bool ExecutePettingTask(ISquadMate mate, Point tile)
        {
            NPC npc = mate.Npc;
            var location = npc.currentLocation;
            var tileVector = tile.ToVector2();

            // Find the animal at this tile
            Character targetAnimal = null;

            // Check farm animals
            var farm = Game1.getFarm();
            if (farm != null)
            {
                targetAnimal = farm.getAllFarmAnimals()
                    .FirstOrDefault(a => a.TilePoint == tile);
            }

            // Check player's pet if not found
            if (targetAnimal == null)
            {
                var home = Utility.getHomeOfFarmer(Game1.player);
                if (home != null)
                {
                    targetAnimal = home.characters.OfType<StardewValley.Characters.Pet>()
                        .FirstOrDefault(p => p.TilePoint == tile);
                }
            }

            // Animal not found or already petted
            if (targetAnimal == null)
            {
                return true; // Task complete (animal moved or doesn't exist)
            }

            // Check if it's a farm animal
            if (targetAnimal is FarmAnimal farmAnimal)
            {
                if (farmAnimal.wasPet.Value)
                {
                    return true; // Already petted
                }

                // Save player state to prevent side effects from the pet() method
                var playerPosition = Game1.player.Position;
                var playerFacingDirection = Game1.player.FacingDirection;
                var playerIsMoving = Game1.player.isMoving();

                Patches.HarmonyPatches.BeginIgnorePetting();
                try
                {
                    // Use the game's built-in pet method (manual mode for full friendship bonus)
                    // This properly sets wasPet.Value = true and gives full friendship
                    farmAnimal.pet(Game1.player, is_auto_pet: false);
                }
                finally
                {
                    Patches.HarmonyPatches.EndIgnorePetting();
                }

                // Restore player state so they aren't affected by the NPC's petting action
                Game1.player.Position = playerPosition;
                Game1.player.FacingDirection = playerFacingDirection;
                if (playerIsMoving)
                {
                    Game1.player.setMoving((byte)playerFacingDirection);
                }
            }
            // Check if it's a pet
            else if (targetAnimal is StardewValley.Characters.Pet pet)
            {
                // Check if already petted today using the pet's tracking system
                if (pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out var lastDay)
                    && lastDay == Game1.Date.TotalDays)
                {
                    return true; // Already petted
                }

                Patches.HarmonyPatches.BeginIgnorePetting();
                try
                {
                    // Use the game's built-in checkAction method to pet
                    // This handles friendship increase and sets lastPetDay
                    pet.checkAction(Game1.player, location);

                    // Ensure emote and sound play (checkAction already calls these, but we ensure they happen)
                    // The pet's playContentSound() method is species-specific (cat, dog, etc.)
                    pet.doEmote(20); // Heart emote
                    pet.playContentSound(); // Species-specific sound
                }
                finally
                {
                    Patches.HarmonyPatches.EndIgnorePetting();
                }
            }
            else
            {
                return true; // Unknown animal type
            }

            // Animate NPC
            var targetWorldPosition = tileVector * 64f + new Vector2(32f, 32f);
            FacePosition(npc, targetWorldPosition);

            _spriteManager?.ApplyTaskAnimation(npc, "Petting", 400);
            npc.shake(400);

            mate.ActionCooldown = 48;

            // Optional dialogue (approximately 14% chance - 1 in 7)
            if (Game1.random.Next(7) == 0)
            {
                mate.Communicate(TaskType.Petting.ToString());
            }

            // Task complete after one pet
            return true;
        }

        #endregion

        #region Sitting Task
        /// <summary>Checks if the player is currently sitting on furniture or a bench.</summary>
        public static bool IsPlayerSitting()
        {
            var player = Game1.player;
            if (player == null) return false; // Handle unit test scenarios

            // Player is sitting if they are on furniture or the isSitting flag is set
            return player.sittingFurniture != null || player.isSitting.Value;
        }

        /// <summary>
        /// Gets the player's current seat position if they are sitting.
        /// Returns null if player is not sitting.
        /// </summary>
        private static Vector2? GetPlayerSeatPosition()
        {
            var player = Game1.player;
            if (player == null) return null;

            // If player is on furniture, get the furniture's seat position
            if (player.sittingFurniture != null)
            {
                // For furniture, the player's tile position is their seat position
                return player.Tile;
            }

            // If player is sitting (e.g., on a map seat), use their position
            if (player.isSitting.Value)
            {
                return player.Tile;
            }

            return null;
        }

        /// <summary>
        /// Finds a nearby sittable furniture or map seat for an NPC.
        /// NPCs prioritize seats using weighted scoring: 60% distance to player, 40% distance to NPC.
        /// This encourages gathering around the player while avoiding excessive NPC travel.
        /// Returns the furniture tile and the specific seat position (with fractional offsets) the NPC will sit on.
        /// </summary>
        public static (Point? furnitureTile, Vector2? seatPosition) FindSittingSpot(
            ILocationInfo locationInfo,
            Point playerPosition,
            Point npcPosition,
            int searchRadius,
            ISet<Vector2> claimedInteractionSpots)
        {
            // Get player's current seat position if they're sitting
            Vector2? playerSeatPosition = GetPlayerSeatPosition();

            var candidates = new List<(Point furnitureTile, Vector2 seatPos, float weightedScore)>();

            // Find all sittable furniture (GetSittableFurniture now returns individual seat positions)
            foreach (var (seatPosition, furnitureTile, direction) in locationInfo.GetSittableFurniture(playerPosition, searchRadius))
            {
                // Skip if this is the player's seat (more robust check using actual seat position)
                if (playerSeatPosition.HasValue && Vector2.Distance(seatPosition, playerSeatPosition.Value) < 0.1f)
                {
                    continue;
                }

                // Also skip if player position matches (fallback check)
                if (seatPosition.ToPoint() == playerPosition)
                {
                    continue;
                }

                // Skip if this seat position is already claimed by another NPC
                if (claimedInteractionSpots.Contains(seatPosition))
                {
                    continue;
                }

                // Calculate distances to both player and NPC
                float distanceToPlayer = Vector2.DistanceSquared(seatPosition, playerPosition.ToVector2());
                float distanceToNpc = Vector2.DistanceSquared(seatPosition, npcPosition.ToVector2());
                // Weighted score: 60% player proximity, 40% NPC proximity
                float weightedScore = (distanceToPlayer * 0.6f) + (distanceToNpc * 0.4f);
                candidates.Add((furnitureTile, seatPosition, weightedScore));
            }

            // Find all map seats
            foreach (var (seatPosition, direction) in locationInfo.GetMapSeats(playerPosition, searchRadius))
            {
                // Skip if this is the player's seat (more robust check using actual seat position)
                if (playerSeatPosition.HasValue && Vector2.Distance(seatPosition, playerSeatPosition.Value) < 0.1f)
                {
                    continue;
                }

                // Also skip if player position matches (fallback check)
                if (seatPosition.ToPoint() == playerPosition)
                {
                    continue;
                }

                // Skip if already claimed
                if (claimedInteractionSpots.Contains(seatPosition))
                {
                    continue;
                }

                // Calculate distances to both player and NPC
                float distanceToPlayer = Vector2.DistanceSquared(seatPosition, playerPosition.ToVector2());
                float distanceToNpc = Vector2.DistanceSquared(seatPosition, npcPosition.ToVector2());
                // Weighted score: 60% player proximity, 40% NPC proximity
                float weightedScore = (distanceToPlayer * 0.6f) + (distanceToNpc * 0.4f);
                // For map seats, furniture tile and seat position are the same
                candidates.Add((seatPosition.ToPoint(), seatPosition, weightedScore));
            }

            // No seats found
            if (!candidates.Any())
                return (null, null);

            // Sort by weighted score (lowest first) - NPCs prefer seats close to both player (60%) and themselves (40%)
            var sortedCandidates = candidates.OrderBy(c => c.weightedScore);

            // Find first reachable seat
            foreach (var (furnitureTile, seatPos, _) in sortedCandidates)
            {
                // For sitting, the NPC needs to path to an adjacent tile first, then "sit" on the furniture
                // Find a passable neighbor to path to
                Point? adjacentTile = Pathfinding.AStarPathfinder.FindClosestPassableNeighbor(
                    locationInfo, seatPos.ToPoint(), npcPosition, claimedInteractionSpots);

                if (adjacentTile.HasValue)
                {
                    // Return furniture tile as target, and seat position as where they'll actually sit
                    return (furnitureTile, seatPos);
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Executes the sitting task - makes NPC sit on furniture.
        /// Stops immediately when player stands up (no mimicking timer).
        /// </summary>
        public static bool ExecuteSittingTask(ISquadMate mate, Point furnitureTile)
        {
            NPC npc = mate.Npc;
            var location = npc.currentLocation;

            // The actual seat position is stored in SeatPosition with fractional offsets
            // Fall back to InteractionTile.ToVector2() if SeatPosition is not set (backwards compatibility)
            Vector2 seatPosition = mate.Task.SeatPosition ?? mate.Task.InteractionTile.ToVector2();

            // Check if player is still sitting - if not, stop sitting and complete task
            if (!IsPlayerSitting())
            {
                // Only call StopSitting if the task is still a Sitting task
                if (mate.Task != null && mate.Task.Type == TaskType.Sitting)
                {
                    StopSitting(mate, seatPosition);
                    _followerManager.ClearMateTaskAndReset(mate);
                }
                return true; // Task complete
            }

            // Get sitting direction from the furniture tile
            var locationInfo = new LocationInfoWrapper(location, npc);
            int? sittingDirection = locationInfo.GetSittingDirection(furnitureTile);

            // If furniture doesn't exist anymore, task complete
            if (!sittingDirection.HasValue)
            {
                return true;
            }

            // Stop NPC movement without clearing animation
            // Halt() clears the animation, so we manually stop movement instead
            npc.xVelocity = 0f;
            npc.yVelocity = 0f;

            // Face the sitting direction (do this BEFORE applying animation to avoid clearing it)
            npc.faceDirection(sittingDirection.Value);

            // Try to apply custom sitting sprite sheet first
            // Falls back to ForceApplyTaskAnimation if no custom sitting sprite exists
            bool usedCustomSittingSprite = _spriteManager?.TryApplySittingSpriteSheet(npc, mate, sittingDirection.Value) ?? false;
            if (!usedCustomSittingSprite)
            {
                // Force apply sitting sprite using manual frame management
                // This handles frame animation and flipping for continuous tasks
                _spriteManager?.ForceApplyTaskAnimation(npc, "Sitting");
            }

            // Position NPC at the exact same pixel position as the seat
            Vector2 sittingPosition = seatPosition * 64f;

            // Adjust position for pets (32x32 sprites) to center them on the tile
            // NPCs are 16 pixels wide (centered at tile + 24px)
            // Pets are 32 pixels wide (should be centered at tile + 16px)
            // Adjustment: -8 pixels on X axis
            if (npc is Pet)
            {
                sittingPosition.X -= 31f;
            }

            npc.Position = sittingPosition;

            // Register NPC with the furniture so it renders correctly (split base/front layers)
            // Check for Furniture first
            var furniture = location.furniture?.FirstOrDefault(f => f.TileLocation == furnitureTile.ToVector2());
            if (furniture != null && furniture.GetSeatCapacity() > 0)
            {
                AddSittingNpc(furniture, npc);
            }
            else
            {
                // Check for MapSeat
                var mapSeats = location.mapSeats;
                if (mapSeats != null)
                {
                    foreach (var mapSeat in mapSeats)
                    {
                        var seatPositions = mapSeat.GetSeatPositions();
                        if (seatPositions != null && seatPositions.Any(pos => pos.ToPoint() == seatPosition.ToPoint()))
                        {
                            AddSittingNpc(mapSeat, npc);
                            break;
                        }
                    }
                }
            }

            // Sitting task never completes - it continues until cleared by timer/distance check
            return false;
        }

        /// <summary>
        /// Teleports an NPC from a furniture tile to an adjacent passable tile when they stop sitting.
        /// This prevents NPCs from getting stuck on impassable furniture tiles (Buildings layer).
        /// </summary>
        /// <param name="mate">The squad member to move off the furniture.</param>
        /// <param name="seatPosition">The furniture/seat tile position (with fractional offsets) they are currently on.</param>
        public static void StopSitting(ISquadMate mate, Vector2 seatPosition)
        {
            var npc = mate.Npc;
            var location = npc.currentLocation;

            // Restore original texture if we applied a custom sitting sprite sheet
            _spriteManager?.RestoreOriginalTexture(npc, mate);

            // Unregister NPC from furniture so it stops rendering with split layers
            // Check all furniture for this NPC
            if (location.furniture != null)
            {
                foreach (var furniture in location.furniture)
                {
                    RemoveSittingNpc(furniture, npc);
                }
            }

            // Check all map seats for this NPC
            var mapSeats = location.mapSeats;
            if (mapSeats != null)
            {
                foreach (var mapSeat in mapSeats)
                {
                    RemoveSittingNpc(mapSeat, npc);
                }
            }

            // Find an adjacent passable tile to teleport to
            var locationInfo = new LocationInfoWrapper(location, npc);
            var adjacentTile = Pathfinding.AStarPathfinder.FindClosestPassableNeighbor(
                locationInfo, seatPosition.ToPoint(), npc.TilePoint);

            if (adjacentTile.HasValue)
            {
                // Teleport NPC to the adjacent passable tile
                Vector2 newPosition = adjacentTile.Value.ToVector2() * 64f;
                npc.Position = newPosition;
            }

            // Stop the sitting animation so it doesn't persist
            npc.Sprite.StopAnimation();

            // Reset flip state (in case it was flipped during sitting)
            npc.flip = false;

            // Clear the sitting animation flag so it can be reapplied next time
            npc.modData.Remove("TSS.SittingAnimationApplied");
        }
        #endregion
    }
}
