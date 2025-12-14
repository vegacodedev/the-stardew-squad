using TheStardewSquad.Pathfinding;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using TheStardewSquad.Framework.Squad;
using StardewValley.Characters;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Behaviors;
using TheStardewSquad.Framework.Gathering;
using TheStardewSquad.Framework.Tasks;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Abstractions.Tasks;
using TheStardewSquad.Framework.Wrappers;

namespace TheStardewSquad.Framework
{
    public class FollowerManager
    {
        private readonly IMonitor _monitor;
        private readonly SquadManager _squadManager;
        private readonly WaitingNpcsManager _waitingNpcsManager;
        private readonly ModConfig _config;
        private readonly DebrisCollector _debrisCollector;
        private readonly UnifiedTaskManager _unifiedTaskManager;
        private readonly FormationManager _formationManager;
        private readonly BehaviorManager _behaviorManager;
        private readonly IGameStateService _gameStateService;
        private readonly IWarpService _warpService;
        private readonly IRandomService _randomService;
        private readonly ITaskService _taskService;
        private readonly IPlayerService _playerService;
        private readonly HashSet<Vector2> _claimedInteractionSpots = new();
        private readonly HashSet<Point> _claimedTaskTargets = new();

        // Tick interval constants for update loop timing
        private const int SlowTickInterval = 15;
        private const int FastTickInterval = 2;
        private const int MimickingTaskDurationTicks = 40; // 40 slow ticks = 10 seconds (40 * 15 frames / 60 FPS)

        private bool _wasInFestival = false;
        private bool _wasInCutscene = false;
        private bool _wasPlayerFishing = false;
        private int _updateCounter = 0;
        private int _lastFriendshipGainHour = -1;

        public FollowerManager(
            IMonitor monitor,
            SquadManager squadManager,
            WaitingNpcsManager waitingNpcsManager,
            ModConfig config,
            DebrisCollector debrisCollector,
            UnifiedTaskManager unifiedTaskManager,
            FormationManager formationManager,
            BehaviorManager behaviorManager,
            IGameStateService gameStateService,
            IWarpService warpService,
            IRandomService randomService,
            ITaskService taskService,
            IPlayerService playerService)
        {
            this._monitor = monitor;
            this._squadManager = squadManager;
            this._waitingNpcsManager = waitingNpcsManager;
            this._config = config;
            this._debrisCollector = debrisCollector;
            this._unifiedTaskManager = unifiedTaskManager;
            this._formationManager = formationManager;
            this._behaviorManager = behaviorManager;
            this._gameStateService = gameStateService;
            this._warpService = warpService;
            this._randomService = randomService;
            this._taskService = taskService;
            this._playerService = playerService;
        }

        public void ResetStateForNewSession()
        {
            this._claimedInteractionSpots.Clear();
            this._claimedTaskTargets.Clear();
            foreach (var mate in this._squadManager.Members)
            {
                mate.ClaimedInteractionSpot = null;
            }
            this._formationManager.Reset();
            this._lastFriendshipGainHour = -1;
        }

        private void ClearMateTask(ISquadMate mate)
        {
            if (mate.Task != null) this._claimedTaskTargets.Remove(mate.Task.Tile);
            if (mate.ClaimedInteractionSpot.HasValue)
            {
                this._claimedInteractionSpots.Remove(mate.ClaimedInteractionSpot.Value);
                mate.ClaimedInteractionSpot = null;
            }
            mate.Task = null;
            mate.FramesSinceTaskCleared = 0; // Start counting frames since task cleared
            mate.LastMonsterTile = null; // Reset monster tracking when task is cleared
        }

        /// <summary>Clears a mate's task and resets their movement state.</summary>
        public void ClearMateTaskAndReset(ISquadMate mate)
        {
            ClearMateTask(mate);
            mate.Path.Clear();
            mate.StuckCounter = 0;
            mate.CurrentMoveDirection = -1;
            mate.MimickingTaskTimer = 0;
            mate.MimickingTaskType = null;
            mate.Halt();

            // Reset flip state in case it was set during tasks (Sitting, Fishing, etc.)
            mate.Npc.flip = false;
        }

        /// <summary>Checks if a task type is configured in Mimicking mode.</summary>
        private bool IsMimickingTask(TaskType taskType)
        {
            TaskMode mode = TaskPriorityManager.GetTaskMode(_config, taskType);
            return mode == TaskMode.Mimicking;
        }

        /// <summary>Checks if the player is currently performing a specific task.</summary>
        private bool IsPlayerPerformingTask(TaskType taskType)
        {
            return taskType switch
            {
                TaskType.Fishing => _taskService.IsPlayerFishing(),
                TaskType.Watering => _taskService.IsPlayerWatering(),
                TaskType.Mining => _taskService.IsPlayerMining(),
                TaskType.Lumbering => _taskService.IsPlayerLumbering(),
                TaskType.Harvesting => _taskService.IsPlayerHarvesting(),
                TaskType.Petting => _taskService.IsPlayerPetting(),
                TaskType.Attacking => _taskService.IsPlayerInCombat(),
                TaskType.Sitting => _taskService.IsPlayerSitting(),
                _ => false
            };
        }

        /// <summary>
        /// Manages the entire mimicking timer system for all squad members.
        /// Called on slow ticks to detect player actions and manage timer lifecycle.
        /// </summary>
        private void UpdateMimickingTimers()
        {
            // If tasks are disabled, clear all mimicking timers and task types
            if (!_config.TasksEnabled)
            {
                foreach (var mate in _squadManager.Members)
                {
                    mate.MimickingTaskTimer = 0;
                    mate.MimickingTaskType = null;
                }
                return;
            }

            // Check all task types that could be in Mimicking mode
            // Note: Fishing is excluded - it has special handling via _wasPlayerFishing
            TaskType[] mimickableTaskTypes = new[]
            {
                TaskType.Watering,
                TaskType.Lumbering,
                TaskType.Mining,
                TaskType.Harvesting,
                TaskType.Petting,
                TaskType.Attacking,
                TaskType.Fishing,
                TaskType.Sitting
            };

            // Check if player is performing any mimicking task and reset timer
            foreach (var taskType in mimickableTaskTypes)
            {
                if (IsMimickingTask(taskType) && IsPlayerPerformingTask(taskType))
                {
                    // Player is performing a mimicking task - reset timer and update task type
                    // The ignore flags prevent NPC actions from triggering this, so it's safe to reset continuously
                    foreach (var mate in _squadManager.Members)
                    {
                        mate.MimickingTaskTimer = MimickingTaskDurationTicks;
                        mate.MimickingTaskType = taskType;
                    }
                    break; // Only process one task type per update
                }
            }

            // ALWAYS decrement timers and clear expired tasks (regardless of what player is doing)
            foreach (var mate in _squadManager.Members)
            {
                // Decrement timer
                if (mate.MimickingTaskTimer > 0)
                {
                    mate.MimickingTaskTimer--;
                }

                // Clear mimicking tasks if timer expired
                if (mate.HasTask() && IsMimickingTask(mate.Task.Type))
                {
                    // Only clear if the timer is for THIS specific task type and has expired
                    if (mate.MimickingTaskType == mate.Task.Type && mate.MimickingTaskTimer <= 0)
                    {
                        ClearMateTaskAndReset(mate);
                    }
                }

                // Clear the task type when timer expires (even if no task is assigned)
                if (mate.MimickingTaskTimer <= 0)
                {
                    mate.MimickingTaskType = null;
                }
            }
        }

        /// <summary>Called by Harmony patch when the player catches a fish.</summary>
        public void OnPlayerCaughtFish()
        {
            if (_playerService.CurrentTool is not StardewValley.Tools.FishingRod)
                return;

            // Give nearby squad members a chance to catch fish too (matches fishing spot search radius)
            int squadSize = _squadManager.Count;
            foreach (var mate in _squadManager.Members)
            {
                // Only reward NPCs who have fishing tasks and are nearby
                if (mate.Task?.Type == TaskType.Fishing &&
                    Vector2.Distance(mate.Npc.Tile, _playerService.Tile) < 15f &&
                    !mate.IsOnCooldown())
                {
                    _taskService.TryNpcCatchFish(mate, squadSize);
                }
            }

            // After rewards, clear ALL fishing tasks (even for NPCs who didn't get rewards)
            // This ensures fishing stops when player catches
            foreach (var mate in _squadManager.Members)
            {
                if (mate.Task?.Type == TaskType.Fishing)
                {
                    ClearMateTaskAndReset(mate);
                }
            }
        }

        /// <summary>Called when player stops fishing without catching (cancel/walk away).</summary>
        private void OnPlayerStoppedFishing()
        {
            foreach (var mate in _squadManager.Members)
            {
                if (mate.Task?.Type == TaskType.Fishing)
                {
                    ClearMateTaskAndReset(mate);
                }
            }
        }

        public void OnUpdateTicked(object? sender, EventArgs e)
        {
            if (!_gameStateService.IsWorldReady || this._squadManager.Count == 0) return;

            // If a cutscene just ended, warp the squad to the player's new position.
            if (this._wasInCutscene && !_gameStateService.IsEventUp)
            {
                WarpSquadToPlayer();
            }
            this._wasInCutscene = _gameStateService.IsEventUp;

            this._debrisCollector.Update(this._squadManager.Members);

            if (HandleFestivalState()) return;

            this.HandleFriendshipGain();

            // Check if player is fishing - if so, we need to keep updating fishing animations even during the mini-game menu
            bool isPlayerFishing = _config.FishingMode != TaskMode.Disabled && _taskService.IsPlayerFishing();

            if (!_gameStateService.IsPlayerFree || !_gameStateService.IsGameActive)
            {
                // If player is not fishing, halt all squad members and return
                if (!isPlayerFishing)
                {
                    foreach (var mate in this._squadManager.Members)
                    {
                        mate.Halt();
                    }
                    return;
                }
                // Otherwise, continue to update fishing animations even during mini-game
            }

            this._updateCounter++;

            // Update mimicking timers once per cycle (not per mate) to avoid multiple resets
            bool isGlobalSlowTick = this._updateCounter % SlowTickInterval == 0;
            if (isGlobalSlowTick)
            {
                // Track player sitting state on slow ticks (timer is set when player sits down)
                Patches.HarmonyPatches.UpdatePlayerSittingState();

                UpdateMimickingTimers();

                // Detect when player stops fishing - clear immediately
                // Works correctly now because IsPlayerFishing() returns true during reeling
                if (_wasPlayerFishing && !isPlayerFishing)
                {
                    OnPlayerStoppedFishing();
                }
                _wasPlayerFishing = isPlayerFishing;
            }

            var members = this._squadManager.Members.ToList();
            for (int i = 0; i < members.Count; i++)
            {
                var mate = members[i];

                bool isFastTick = this._updateCounter % FastTickInterval == 0;
                bool isSlowTick = (this._updateCounter + i) % SlowTickInterval == 0;

                // If player is not free but is fishing, only update fishing-related logic
                if (!_gameStateService.IsPlayerFree && isPlayerFishing)
                {
                    UpdateFishingOnly(mate, isFastTick);
                }
                else
                {
                    UpdateSquadMember(mate, _playerService.Player, isFastTick, isSlowTick);
                }
            }

            // Update waiting NPCs (keep them stationary and maintain control)
            foreach (var waitingMate in this._waitingNpcsManager.WaitingMembers.ToList())
            {
                UpdateWaitingNpc(waitingMate);
            }
        }

        /// <summary>
        /// Updates a waiting NPC to keep them stationary at their wait position.
        /// </summary>
        private void UpdateWaitingNpc(ISquadMate mate)
        {
            var npc = mate.Npc;

            // Maintain control over the NPC
            SquadMateStateHelper.MaintainControl(npc);

            // Keep NPC halted at their wait position
            npc.Halt();

            // Lock the NPC's facing direction so they don't turn toward the player
            if (mate.WaitingFacingDirection.HasValue)
            {
                npc.FacingDirection = mate.WaitingFacingDirection.Value;
            }
        }

        /// <summary>
        /// Updates only fishing animations for a squad member during the fishing mini-game.
        /// This is called when Context.IsPlayerFree is false but the player is fishing.
        /// </summary>
        private void UpdateFishingOnly(ISquadMate mate, bool isFastTick)
        {
            var npc = mate.Npc;

            // Only animate if the NPC has a fishing task and is at their fishing spot
            if (mate.Task?.Type == TaskType.Fishing && npc.TilePoint == mate.Task.InteractionTile && isFastTick)
            {
                _taskService.AnimateFishing(npc, mate.Task.Tile);
            }
        }

        private void WarpSquadToPlayer()
        {
            foreach (var mate in this._squadManager.Members)
            {
                var npc = mate.Npc;
                _warpService.WarpCharacter(npc, _playerService.CurrentLocation.Name, _playerService.TilePoint);
                mate.Path.Clear();
                ClearMateTask(mate);
                mate.IsCatchingUp = false;
                mate.StuckCounter = 0;
                mate.CurrentMoveDirection = -1;
                mate.IsInPool = false;
                mate.LastTilePoint = null;
                mate.Halt();
            }
        }
        
        private void UpdateSquadMember(ISquadMate mate, Farmer player, bool isFastTick, bool isSlowTick)
        {
            var npc = mate.Npc;
            float distanceToPlayer = Vector2.Distance(npc.Tile, player.Tile);

            // If tasks are disabled and mate has an automatic task, clear it (but keep manual tasks)
            if (!_config.TasksEnabled && mate.HasTask() && !mate.Task.IsManual)
            {
                ClearMateTaskAndReset(mate);
            }

            // If an idle animation is playing, check if the player has moved too far away.
            if (mate.IsAnimating && distanceToPlayer > 2.5f)
            {
                mate.Halt(); // This stops the animation and resets the IsAnimating flag.
                mate.ActionCooldown = 0; // Reset the cooldown to allow other actions.

                // Manually reset the sprite to a standard idle frame, since Halt() can leave it on a weird frame.
                npc.Sprite.CurrentFrame = BehaviorManager.GetIdleFrame(npc.FacingDirection);
            }

            if (mate.IsOnCooldown())
            {
                // DecrementCooldown() returns true on the exact tick the cooldown finishes.
                // This is the perfect moment to call Halt() to reset the NPC's animation
                // and state *after* the task's animation has finished playing.
                if (mate.DecrementCooldown())
                {
                    mate.Halt();
                }
                // By returning here, we ensure the NPC does nothing else while on cooldown,
                // allowing the animation to play out without being interrupted by new commands.
                return;
            }

            // Increment frames since task cleared (capped at 20 to prevent overflow)
            if (mate.FramesSinceTaskCleared < 20)
            {
                mate.FramesSinceTaskCleared++;
            }

            SquadMateStateHelper.MaintainControl(npc);
            HandleLocationAndSpeed(mate, player);

            // The catch-up mechanic should only trigger if the squad member has a task.
            // This prevents followers in a long "conga line" from constantly trying to catch up
            // when they are simply following the person in front of them.
            if (!mate.IsCatchingUp && mate.HasTask() && distanceToPlayer > 15f)
            {
                mate.IsCatchingUp = true;
                ClearMateTask(mate);
                mate.Path.Clear();
                mate.CurrentMoveDirection = -1;
                mate.Halt();
            }
            else if (mate.IsCatchingUp && distanceToPlayer < 7f)
            {
                mate.IsCatchingUp = false;
                mate.Halt();
            }

            if (mate.IsCatchingUp)
            {
                GenerateAndFollowPath(mate, player.TilePoint, isSlowTick);
                return;
            }

            // High-priority attacking task check (checked on fast ticks, can interrupt any task)
            if (isFastTick)
            {
                if (_config.TasksEnabled)
                {
                    var locationInfo = new LocationInfoWrapper(npc.currentLocation, npc);
                    var attackingTask = _unifiedTaskManager.FindAttackingTask(mate, locationInfo, _playerService.TilePoint, npc.TilePoint, this._claimedInteractionSpots);
                    if (attackingTask != null)
                    {
                        // Only assign if not already attacking the same monster
                        // This prevents animation glitching from constant task reassignment (30 FPS)
                        // Path updates when monster moves are handled in HandleTaskExecution via monsterMoved check
                        bool isAlreadyAttackingSameMonster =
                            mate.Task?.Type == TaskType.Attacking &&
                            mate.Task?.TargetCharacter == attackingTask.TargetCharacter;

                        if (!isAlreadyAttackingSameMonster)
                        {
                            AssignTaskToMate(mate, attackingTask);
                        }
                    }
                }

                // Special case: Fishing animation (only when at fishing spot)
                if (mate.Task?.Type == TaskType.Fishing && npc.TilePoint == mate.Task.InteractionTile)
                {
                    _taskService.AnimateFishing(npc, mate.Task.Tile);
                    if (Game1.random.Next(600) == 0)
                    {
                        mate.Communicate("Fishing_Waiting");
                    }
                }

                // Special case: Sitting animation (keep NPC stationary on furniture tile)
                if (mate.Task?.Type == TaskType.Sitting)
                {
                    npc.Halt();
                    npc.Sprite.StopAnimation();

                    // Force NPC to stay on the seat position (with fractional tile offsets preserved)
                    // Use SeatPosition if available (preserves Y-offsets from MapSeat/Furniture), else fall back to InteractionTile
                    Vector2 seatTilePosition = mate.Task.SeatPosition ?? mate.Task.InteractionTile.ToVector2();
                    Vector2 sittingPosition = seatTilePosition * 64f;
                    if (Vector2.Distance(npc.Position, sittingPosition) > 5f)
                    {
                        npc.Position = sittingPosition; // Snap back to sitting position if drifted
                    }
                }
            }

            // Unified task system (checked on slow ticks, respects priority order across all modes)
            if (isSlowTick)
            {
                // Find new task if none assigned (switch on completion)
                if (!mate.HasTask())
                {
                    if (_config.TasksEnabled)
                    {
                        var locationInfo = new LocationInfoWrapper(npc.currentLocation, npc);
                        SquadTask newTask = _unifiedTaskManager.FindUnifiedTask(mate, locationInfo, _playerService.TilePoint, npc.TilePoint, this._claimedTaskTargets, this._claimedInteractionSpots);
                        if (newTask != null)
                        {
                            AssignTaskToMate(mate, newTask);
                        }
                    }
                }
            }

            if (mate.HasTask())
            {
                HandleTaskExecution(mate, isSlowTick);
            }
            else
            {
                HandleFollowing(mate, player, isSlowTick);
            }
            
            // --- IDLE BEHAVIORS ---
            if (isSlowTick && !mate.IsOnCooldown())
            {
                bool isStandingStill = !mate.HasTask() && mate.Path.Count == 0 && mate.CurrentMoveDirection == -1 && !mate.Npc.isMoving();
                if (isStandingStill)
                {
                    mate.IdleTicks++;

                    // 180 ticks = 3 seconds at 60 FPS.
                    if (this._config.EnableIdleAnimations && mate.IdleTicks > 180 && _randomService.NextDouble() < 1.0 / 40.0)
                    {
                        var animationSpec = _behaviorManager.GetRandomIdleAnimation(mate.Npc);
                        if (animationSpec != null)
                        {
                            _behaviorManager.PlayIdleAnimation(mate, animationSpec);
                            return;
                        }
                    }
                }
                else
                {
                    mate.IdleTicks = 0;
                }

                if (!mate.HasTask())
                {
                    if (_randomService.NextDouble() < 1.0 / 720.0)
                    {
                        mate.Communicate(DialogueKeys.Idle);
                    }
                }
            }
        }

        /// <summary>Assigns a new task to a squad mate, clearing any previous task and resetting their state.</summary>
        private void AssignTaskToMate(ISquadMate mate, SquadTask newTask)
        {
            mate.Halt();
            ClearMateTask(mate);

            mate.Task = newTask;
            mate.FramesSinceTaskCleared = 999; // Large value = not recently cleared
            if (newTask.Type != TaskType.Attacking)
            {
                this._claimedTaskTargets.Add(newTask.Tile);
            }
            // For sitting tasks, claim the actual seat position (Vector2 with fractional offsets)
            // For other tasks, claim the interaction tile converted to Vector2
            if (newTask.Type == TaskType.Sitting && newTask.SeatPosition.HasValue)
            {
                mate.ClaimedInteractionSpot = newTask.SeatPosition.Value;
                this._claimedInteractionSpots.Add(newTask.SeatPosition.Value);
            }
            else
            {
                mate.ClaimedInteractionSpot = newTask.InteractionTile.ToVector2();
                this._claimedInteractionSpots.Add(newTask.InteractionTile.ToVector2());
            }

            mate.Path.Clear();
            mate.ActionCooldown = 0;
            mate.CurrentMoveDirection = -1;

            // Note: MimickingTaskTimer is now managed centrally in UpdateMimickingTimers()
            // based on player actions, not when assigning tasks
        }

        private void HandleTaskExecution(ISquadMate mate, bool isSlowTick)
        {
            Point npcTile = mate.Npc.TilePoint;
            Point targetInteractionTile;

            if (mate.Task.TargetCharacter is Monster monster && monster.Health > 0)
            {
                // Validate monster is still targetable before continuing to path towards it
                // This prevents NPCs from wasting time walking to invalid targets
                if (!TaskManager.IsMonsterTargetable(monster, mate.Npc.currentLocation))
                {
                    ClearMateTask(mate);
                    return;
                }

                // Track monster position to detect movement
                Point currentMonsterTile = monster.TilePoint;
                bool monsterMoved = mate.LastMonsterTile.HasValue && mate.LastMonsterTile.Value != currentMonsterTile;

                // Only recalculate ideal spot on slow ticks, when monster moves, or when no spot is claimed yet
                // This prevents excessive pathfinding updates (60 FPS → 4 FPS) and fixes animation glitching
                if (isSlowTick || monsterMoved || !mate.ClaimedInteractionSpot.HasValue)
                {
                    mate.LastMonsterTile = currentMonsterTile;

                    // Convert Vector2 claimed spots to Point for pathfinder (attacking uses whole tiles)
                    var otherClaimedSpots = new HashSet<Vector2>(this._claimedInteractionSpots);
                    if (mate.ClaimedInteractionSpot.HasValue)
                    {
                        otherClaimedSpots.Remove(mate.ClaimedInteractionSpot.Value);
                    }

                    // IMPORTANT: Pass validateReachability=true for attacking tasks to prevent chasing unreachable monsters
                    // This validates that the neighbor tile is pathfinding-reachable, not just passable
                    var idealSpot = AStarPathfinder.FindClosestPassableNeighbor(mate.Npc.currentLocation, monster.TilePoint, mate.Npc, otherClaimedSpots, validateReachability: true, monitor: this._monitor);
                    if (!idealSpot.HasValue)
                    {
                        // No reachable adjacent tiles - monster is unreachable (e.g., flew over wall)
                        ClearMateTask(mate);
                        return;
                    }
                    targetInteractionTile = idealSpot.Value;

                    // If the ideal spot has changed, we need to update our claim and force a path recalculation.
                    Vector2 idealSpotVector = targetInteractionTile.ToVector2();
                    if (mate.ClaimedInteractionSpot != idealSpotVector)
                    {
                        if (mate.ClaimedInteractionSpot.HasValue)
                        {
                            this._claimedInteractionSpots.Remove(mate.ClaimedInteractionSpot.Value);
                        }
                        mate.ClaimedInteractionSpot = idealSpotVector;
                        this._claimedInteractionSpots.Add(idealSpotVector);

                        // Force a path recalculation to the new, better spot.
                        // This prevents the NPC from following an obsolete path into a wall or the monster itself.
                        mate.Path.Clear();
                        mate.CurrentMoveDirection = -1;
                    }
                }
                else
                {
                    // Reuse existing claimed spot when monster hasn't moved and it's not a slow tick
                    // This maintains smooth pathfinding without constant recalculation
                    targetInteractionTile = mate.ClaimedInteractionSpot.Value.ToPoint();
                }
            }
            else
            {
                targetInteractionTile = mate.Task.InteractionTile;
            }

            // For sitting tasks with fractional coordinates, use position-based check instead of tile-based
            // This handles MapSeats at positions like (46, 78.5) where tile comparison fails
            bool isAtInteractionSpot;
            if (mate.Task.Type == TaskType.Sitting && mate.Task.SeatPosition.HasValue)
            {
                // Check if NPC's position is close to the seat position (within 32 pixels / 0.5 tiles)
                Vector2 seatPixelPosition = mate.Task.SeatPosition.Value * 64f;
                float distanceToSeat = Vector2.Distance(mate.Npc.Position, seatPixelPosition);
                isAtInteractionSpot = distanceToSeat < 32f;
            }
            else
            {
                // Standard tile-based check for other tasks
                isAtInteractionSpot = npcTile == targetInteractionTile;
            }

            if (isAtInteractionSpot)
            {
                bool taskCompleted = mate.ExecuteTask();
                if (taskCompleted)
                {
                    ClearMateTask(mate);
                    mate.CurrentMoveDirection = -1;
                }
            }
            else
            {
                GenerateAndFollowPath(mate, targetInteractionTile, isSlowTick);
            }
        }

        private void HandleFollowing(ISquadMate mate, Farmer player, bool isSlowTick)
        {
            // Try to get the ideal formation tile for this mate.
            if (!this._formationManager.TryGetTargetTile(mate, player, out Point idealTile))
            {
                // No slot assigned, something is wrong. Halt to be safe.
                mate.Halt();
                return;
            }
            
            // Check distance to the ideal target tile.
            float distanceToTarget = Vector2.Distance(mate.Npc.Tile, idealTile.ToVector2());
            
            // If we're already very close to our spot (and player isn't moving), just stop and face the player.
            if (distanceToTarget < 2.5f)
            {
                // Check if we're too close to other squad members before halting
                bool tooCloseToOthers = false;
                foreach (var otherMate in this._squadManager.Members)
                {
                    if (otherMate == mate)
                        continue;

                    float distanceToOther = Vector2.Distance(idealTile.ToVector2(), otherMate.Npc.Tile);
                    if (distanceToOther < 0.5f)
                    {
                        tooCloseToOthers = true;
                        break;
                    }
                }

                // Only halt if we're not too close to others
                if (!tooCloseToOthers)
                {
                    mate.Path.Clear();
                    mate.CurrentMoveDirection = -1;
                    mate.Halt();

                    // Only face the player if we haven't just cleared a task (prevents brief turn glitch)
                    if (mate.FramesSinceTaskCleared > 15)
                    {
                        _taskService.FacePosition(mate.Npc, _playerService.StandingPosition);
                    }
                }
            }
            else
            {
                // If we are far from our spot, generate a path to it.
                GenerateAndFollowPath(mate, idealTile, isSlowTick);
            }
        }

        private void GenerateAndFollowPath(ISquadMate mate, Point targetTile, bool isSlowTick)
        {
            var npc = mate.Npc;
            Point pathableTarget = targetTile;

            if (isSlowTick || mate.Path == null || mate.Path.Count == 0)
            {
                if (!AStarPathfinder.IsTilePassableForFollower(npc.currentLocation, pathableTarget, npc))
                {
                    Point? bestNeighbor = AStarPathfinder.FindClosestPassableNeighbor(npc.currentLocation, pathableTarget, npc);
                    if (bestNeighbor.HasValue)
                        pathableTarget = bestNeighbor.Value;
                    else
                    {
                        // If the target tile and all its neighbors are impassable (e.g., tight corridor),
                        // fall back to pathing directly to the player to collapse the formation.
                        pathableTarget = _playerService.TilePoint;
                    }
                }

                Stack<Point> path = null;
                if (AStarPathfinder.IsPathUnobstructed(npc.currentLocation, npc.TilePoint, pathableTarget, npc))
                {
                    path = new Stack<Point>();
                    path.Push(pathableTarget);
                }
                else
                {
                    path = AStarPathfinder.FindPath(npc.currentLocation, npc.TilePoint, pathableTarget, npc, this._monitor);
                    if (path != null && path.Count > 0 && path.Peek() == npc.TilePoint)
                    {
                        path.Pop();
                    }
                }
                
                if (path != null && path.Count > 0)
                {
                    mate.Path = path;
                    mate.StuckCounter = 0;
                    mate.CurrentMoveDirection = -1;
                }
                else
                {
                    mate.StuckCounter++;
                }
            }

            if (mate.StuckCounter > 20)
            {
                _warpService.WarpCharacter(npc, npc.currentLocation.Name, _playerService.TilePoint);
                mate.Path.Clear();
                ClearMateTask(mate);
                mate.StuckCounter = 0;
                mate.CurrentMoveDirection = -1;
                mate.IsInPool = false;
                mate.LastTilePoint = null;
                return;
            }

            if (mate.Path != null && mate.Path.Count > 0)
            {
                ExecutePathMovement(mate);
            }
        }

        private void ExecutePathMovement(ISquadMate mate)
        {
            var npc = mate.Npc;

            Point targetNode = mate.Path.Peek();
            if (mate.Path.Count > 1)
            {
                var pathAsList = mate.Path.ToList();
                for (int i = pathAsList.Count - 2; i >= 0; i--)
                {
                    // Check if there's a clear line of sight to this node
                    if (AStarPathfinder.IsPathUnobstructed(npc.currentLocation, npc.TilePoint, pathAsList[i], npc))
                    {
                        // IMPORTANT: Also verify all tiles along the direct path are passable
                        // This prevents NPCs from trying to move through water, walls, etc.
                        if (AStarPathfinder.IsDirectPathFullyPassable(npc.currentLocation, npc.TilePoint, pathAsList[i], npc))
                        {
                            targetNode = pathAsList[i];
                        }
                        else
                        {
                            break; // Can't skip to this node - direct path has impassable tiles
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            Vector2 targetPosition = (Utility.PointToVector2(targetNode) * 64f) + new Vector2(32, 32);

            if (mate.CurrentMoveDirection == -1)
            {
                Vector2 directionVector = targetPosition - npc.getStandingPosition();
                if (Math.Abs(directionVector.X) > Math.Abs(directionVector.Y))
                {
                    mate.CurrentMoveDirection = directionVector.X > 0 ? 1 : 3;
                }
                else
                {
                    mate.CurrentMoveDirection = directionVector.Y > 0 ? 2 : 0;
                }
            }

            npc.faceDirection(mate.CurrentMoveDirection);
            Vector2 velocity = Utility.getVelocityTowardPoint(npc.getStandingPosition(), targetPosition, npc.speed);
            npc.Position += velocity;
            npc.animateInFacingDirection(_gameStateService.CurrentGameTime);

            if (Vector2.Distance(npc.getStandingPosition(), targetPosition) <= npc.speed)
            {
                while (mate.Path.Count > 0 && mate.Path.Peek() != targetNode)
                {
                    mate.Path.Pop();
                }
                if (mate.Path.Count > 0)
                {
                    mate.Path.Pop();
                }

                mate.CurrentMoveDirection = -1;
                if (mate.Path.Count == 0)
                {
                    npc.Halt();
                }
            }
        }

        private void HandleLocationAndSpeed(ISquadMate mate, Farmer player)
        {
            var npc = mate.Npc;

            if (npc.currentLocation != _playerService.CurrentLocation)
            {
                _warpService.WarpCharacter(npc, _playerService.CurrentLocation.Name, _playerService.TilePoint);
                mate.Path.Clear();
                ClearMateTask(mate);
                mate.StuckCounter = 0;
                mate.CurrentMoveDirection = -1;
                mate.IsInPool = false;
                mate.LastTilePoint = null;
                return;
            }

            if (mate.HasTask() || mate.IsCatchingUp)
            {
                int taskSpeed = (int)_playerService.MovementSpeed + 1;
                npc.speed = Math.Max(2, taskSpeed);
            }
            else
            {
                float distanceToTarget = Vector2.Distance(npc.Tile, _playerService.Tile);
                float targetSpeed = _playerService.MovementSpeed;
                int baseNpcSpeed = Math.Max(1, (int)targetSpeed);

                if (distanceToTarget > 5f)
                    npc.speed = baseNpcSpeed + 2;
                else if (distanceToTarget < 2.8f)
                    npc.speed = Math.Max(1, baseNpcSpeed - 1);
                else
                    npc.speed = baseNpcSpeed;
            }
        }

        private bool HandleFestivalState()
        {
            bool isInFestival = _gameStateService.IsFestival();
            if (isInFestival && !this._wasInFestival)
            {
                this._wasInFestival = true;
                if (this._squadManager.Count > 0)
                {
                    foreach (var mate in this._squadManager.Members.ToList())
                    {
                        mate.HandleDismissal(isSilent: true, DismissalWarpBehavior.GoHome);
                    }
                }

                // Also dismiss waiting NPCs during festivals
                if (this._waitingNpcsManager.Count > 0)
                {
                    foreach (var mate in this._waitingNpcsManager.WaitingMembers.ToList())
                    {
                        mate.HandleDismissal(isSilent: true, DismissalWarpBehavior.GoHome);
                        this._waitingNpcsManager.Remove(mate.Npc);
                    }
                }
                return true;
            }

            if (!isInFestival && this._wasInFestival)
            {
                this._wasInFestival = false;
            }

            return isInFestival;
        }

        private void HandleFriendshipGain()
        {
            if (this._config.FriendshipPointsPerHour <= 0)
                return;

            int currentHour = _gameStateService.TimeOfDay / 100;

            if (this._lastFriendshipGainHour == -1)
            {
                this._lastFriendshipGainHour = currentHour;
                return;
            }

            if (currentHour == this._lastFriendshipGainHour)
                return;

            int hoursPassed = currentHour - this._lastFriendshipGainHour;

            int pointsToAdd = hoursPassed * this._config.FriendshipPointsPerHour;
            if (pointsToAdd <= 0)
                return;

            foreach (var mate in this._squadManager.Members)
            {
                if (mate.Npc is Pet pet)
                {
                    pet.friendshipTowardFarmer.Value = Math.Min(1000, pet.friendshipTowardFarmer.Value + pointsToAdd);
                }
                else if (mate.Npc.isVillager())
                {
                    _playerService.ChangeFriendship(pointsToAdd, mate.Npc);
                }
            }
            
            this._lastFriendshipGainHour = currentHour;
        }
    }
}
