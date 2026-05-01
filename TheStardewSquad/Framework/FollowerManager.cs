using TheStardewSquad.Pathfinding;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewModdingAPI.Utilities;
using TheStardewSquad.Framework.Squad;
using StardewValley.Characters;
using TheStardewSquad.Framework.Multiplayer;
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
        private SpriteManager? _spriteManager;
        private readonly HashSet<Vector2> _claimedInteractionSpots = new();
        private readonly HashSet<Point> _claimedTaskTargets = new();

        // Tick interval constants for update loop timing
        private const int SlowTickInterval = 15;
        private const int FastTickInterval = 2;

        /// <summary>
        /// Per-direction saddle anchor for the Riding task.
        /// World-pixel position (relative to player.Position) where the NPC sprite's AnchorPixel will land.
        /// Key = Character.FacingDirection (0=Up, 1=Right, 2=Down, 3=Left).
        /// </summary>
        private static readonly Dictionary<int, Vector2> RidingSaddleAnchorByDirection = new()
        {
            { 0, new Vector2(50f,  24f) }, // Up
            { 1, new Vector2(24f, 16f) }, // Right
            { 2, new Vector2(50f, -4f) }, // Down
            { 3, new Vector2(88f, 16f) }, // Left
        };

        private const int MimickingTaskDurationTicks = 40; // 40 slow ticks = 10 seconds (40 * 15 frames / 60 FPS)

        private bool _wasInFestival = false;
        // Per-screen cutscene detection so each peer (and each split-screen player) tracks its
        // own cutscene state. The host warps its own mates directly when its local cutscene ends;
        // a farmhand fires a CutsceneEnded notify message which the host receives and re-warps
        // the farmhand's mates only.
        private readonly PerScreen<bool> _wasInCutscene = new(() => false);
        private bool _wasPlayerFishing = false;
        // Per-farmer riding state. In MP each online farmer can mount independently and
        // their own recruited mate rides with them.
        private readonly Dictionary<long, bool> _wasFarmerRiding = new();
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
            ITaskService taskService)
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
        }

        /// <summary>Sets the SpriteManager dependency (called after construction due to initialization order).</summary>
        public void SetSpriteManager(SpriteManager spriteManager)
        {
            this._spriteManager = spriteManager;
        }

        private MessageDispatcher? _dispatcher;

        public void AttachDispatcher(MessageDispatcher dispatcher)
        {
            this._dispatcher = dispatcher;
        }

        private RecruitmentManager? _recruitment;

        public void SetRecruitmentManager(RecruitmentManager recruitment)
        {
            this._recruitment = recruitment;
        }

        // Wall-clock timestamp per auto-parked recruiter id.
        private readonly Dictionary<long, DateTime> _autoParkedAt = new();

        // Per-screen smoothed riding anchor per recruiter. In MP the NPC's Position netfield
        // can lag the rider's Position by 1-2 ticks on remote screens, producing a visible gap
        // between the riding NPC and the horse. We compensate by computing the draw offset
        // against the recruiter's CURRENT position (rather than the lagged npc.Position) and
        // exponentially smoothing across frames to absorb any per-frame jitter.
        // Keyed by recruiter UniqueMultiplayerID so multiple riders smooth independently.
        private readonly PerScreen<Dictionary<long, Vector2>> _smoothedRiderAnchor =
            new(() => new Dictionary<long, Vector2>());

        /// <summary>
        /// Resolves the recruiter for a mate, falling back to <see cref="Game1.MasterPlayer"/>
        /// (or local <see cref="Game1.player"/> if even that's null in tests) when the recruiter
        /// is offline.
        /// </summary>
        private static Farmer ResolveRecruiterOrFallback(ISquadMate mate)
        {
            if (mate.TryGetRecruiter(out var rec))
                return rec;
            return Game1.MasterPlayer ?? Game1.player;
        }

        public void ResetStateForNewSession()
        {
            this._claimedInteractionSpots.Clear();
            this._claimedTaskTargets.Clear();
            foreach (var mate in this._squadManager.Members)
            {
                mate.ClaimedInteractionSpot = null;
                if (mate.IsRidingWithPlayer)
                {
                    mate.Npc.HideShadow = false;
                    mate.IsRidingWithPlayer = false;
                }
            }
            this._formationManager.Reset();
            this._lastFriendshipGainHour = -1;
            this._wasFarmerRiding.Clear();
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

        /// <summary>
        /// Auto-parks mates whose recruiter just disconnected, auto-resumes mates whose
        /// recruiter just reconnected, and auto-dismisses mates whose recruiter has been
        /// offline longer than <see cref="ModConfig.ParkTimeoutMinutes"/>. Reuses the existing
        /// Waiting feature: <see cref="RecruitmentManager.SetWaiting"/> handles parking and
        /// <see cref="RecruitmentManager.Recruit"/>'s waiting-resume branch handles unparking.
        /// </summary>
        private void HandleOfflineRecruiters()
        {
            if (Context.IsMultiplayer && !Context.IsMainPlayer) return;
            if (!Context.IsMultiplayer) return; // SP: nobody can be offline.
            if (this._recruitment == null) return; // Safety: not yet wired (shouldn't happen post-Entry).

            // Pass A: park mates whose recruiter is currently offline.
            foreach (var mate in this._squadManager.Members.ToList())
            {
                if (mate.TryGetRecruiter(out _)) continue; // recruiter online — skip

                if (this._config.WarpHomeOnDisconnect)
                {
                    this._recruitment.Dismiss(mate, isSilent: true, DismissalWarpBehavior.GoHome);
                    continue;
                }

                long rid = mate.RecruiterUniqueId;
                if (!this._autoParkedAt.ContainsKey(rid))
                    this._autoParkedAt[rid] = DateTime.UtcNow;
                this._recruitment.SetWaiting(mate, isSilent: true);
            }

            // Pass B: resume mates whose recruiter just came back online.
            foreach (var mate in this._waitingNpcsManager.WaitingMembers.ToList())
            {
                if (!this._autoParkedAt.ContainsKey(mate.RecruiterUniqueId)) continue; // not auto-parked
                if (!mate.TryGetRecruiter(out var rec)) continue; // still offline

                this._recruitment.Recruit(mate, rec, isSilent: true);
                this._autoParkedAt.Remove(mate.RecruiterUniqueId);
            }

            // Pass C: auto-dismiss mates whose recruiter has been offline beyond the timeout.
            if (this._config.ParkTimeoutMinutes <= 0) return;
            var now = DateTime.UtcNow;
            foreach (var (rid, parkedAt) in this._autoParkedAt.ToList())
            {
                if ((now - parkedAt).TotalMinutes < this._config.ParkTimeoutMinutes) continue;

                foreach (var mate in this._waitingNpcsManager.WaitingMembers
                    .Where(m => m.RecruiterUniqueId == rid).ToList())
                {
                    this._recruitment.Dismiss(mate, isSilent: true, DismissalWarpBehavior.GoHome);
                }
                this._autoParkedAt.Remove(rid);
            }
        }

        /// <summary>
        /// Authoritative entry point for "a farmer just performed a mimickable task".
        /// Sets the mimicking timer on the farmer's own squad mates that are co-located
        /// with them. Runs on the host only — farmhand-side callers must route through
        /// <see cref="MessageDispatcher.SendMimickingRequest"/> instead.
        /// </summary>
        public void OnFarmerAction(Farmer who, TaskType type)
        {
            if (Context.IsMultiplayer && !Context.IsMainPlayer) return;
            if (!_config.TasksEnabled) return;
            if (!IsMimickingTask(type)) return;
            if (who == null) return;

            foreach (var mate in _squadManager.Members)
            {
                if (mate.RecruiterUniqueId != who.UniqueMultiplayerID) continue;
                if (mate.Npc.currentLocation != who.currentLocation) continue;

                mate.MimickingTaskTimer = MimickingTaskDurationTicks;
                mate.MimickingTaskType = type;
            }
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
                TaskType.Shearing => _taskService.IsPlayerShearing(),
                TaskType.Milking => _taskService.IsPlayerMilking(),
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

            // Check all task types that could be in Mimicking mode.
            // Polling reads local-screen state (Game1.player), so in MP only the host's tool
            // actions are detected here - farmhand tool/state actions don't reach the host's
            // polling. Event-based tasks (Petting/Harvesting/Shearing/Milking) ARE fully
            // MP-aware via Harmony postfixes calling OnFarmerAction directly. TODO: Make it fully
            // MP-compatible in the next pass, right now I can't do this cleanly
            TaskType[] mimickableTaskTypes = new[]
            {
                TaskType.Watering,
                TaskType.Lumbering,
                TaskType.Mining,
                TaskType.Harvesting,
                TaskType.Petting,
                TaskType.Shearing,
                TaskType.Milking,
                TaskType.Attacking,
                TaskType.Fishing,
                TaskType.Sitting
            };

            foreach (var taskType in mimickableTaskTypes)
            {
                if (IsMimickingTask(taskType) && IsPlayerPerformingTask(taskType))
                {
                    // Route through OnFarmerAction so timers are scoped to the local player's
                    // mates only (recruiter-filtered). Without this, all mates regardless of
                    // recruiter would mimic when ANY farmer triggers polling.
                    OnFarmerAction(Game1.player, taskType);
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

        /// <summary>Called by Harmony patch when a farmer catches a fish.</summary>
        /// <param name="who">The farmer who caught the fish (may be a remote farmhand in MP).</param>
        public void OnPlayerCaughtFish(Farmer who)
        {
            if (who.CurrentTool is not StardewValley.Tools.FishingRod)
                return;

            // Give nearby squad members of THIS recruiter a chance to catch fish too
            // (matches fishing spot search radius). In MP another farmhand's fish catch
            // doesn't reward this recruiter's mates.
            int squadSize = _squadManager.Count;
            foreach (var mate in _squadManager.Members)
            {
                if (mate.RecruiterUniqueId != who.UniqueMultiplayerID)
                    continue;

                // Only reward NPCs who have fishing tasks and are nearby
                if (mate.Task?.Type == TaskType.Fishing &&
                    Vector2.Distance(mate.Npc.Tile, who.Tile) < 15f &&
                    !mate.IsOnCooldown())
                {
                    _taskService.TryNpcCatchFish(mate, squadSize);
                }
            }

            // After rewards, clear ALL fishing tasks for this recruiter's mates
            // (even for NPCs who didn't get rewards). This ensures fishing stops when
            // the recruiter catches.
            foreach (var mate in _squadManager.Members)
            {
                if (mate.RecruiterUniqueId != who.UniqueMultiplayerID)
                    continue;

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
            if (!_gameStateService.IsWorldReady) return;
            if (this._squadManager.Count == 0 && this._waitingNpcsManager.Count == 0) return;

            // Per-screen cutscene-end detection. Runs above the host-only guard so farmhand
            // peers can also detect when their own local cutscene ended; they notify the host
            // via CutsceneEnded so the host can re-warp their mates. Each split-screen player
            // tracks its own state via PerScreen<bool>.
            bool isInCutsceneNow = _gameStateService.IsEventUp;
            bool wasInCutscene = this._wasInCutscene.Value;
            if (wasInCutscene && !isInCutsceneNow)
            {
                var localPlayer = Game1.player;
                if (Context.IsMainPlayer)
                {
                    WarpSquadToFarmer(localPlayer);
                }
                else
                {
                    this._dispatcher?.SendCutsceneEnded(localPlayer.UniqueMultiplayerID);
                }
            }
            this._wasInCutscene.Value = isInCutsceneNow;

            // Host-only authority: in MP, only the main player runs squad AI mutations.
            if (Context.IsMultiplayer && !Context.IsMainPlayer) return;

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

                // Park mates whose recruiter disconnected, resume on reconnect, auto-dismiss
                // after ParkTimeoutMinutes. Does nothing in SP.
                HandleOfflineRecruiters();

                UpdateMimickingTimers();

                // Detect when player stops fishing - clear immediately
                // Works correctly now because IsPlayerFishing() returns true during reeling
                if (_wasPlayerFishing && !isPlayerFishing)
                {
                    OnPlayerStoppedFishing();
                }
                _wasPlayerFishing = isPlayerFishing;
            }

            // Handle riding state transitions and identify the riding member
            var ridingMate = UpdateRidingState();

            var members = this._squadManager.Members.ToList();
            for (int i = 0; i < members.Count; i++)
            {
                var mate = members[i];

                // If this member is riding on the horse, update riding logic instead of normal following
                if (mate == ridingMate)
                {
                    UpdateRidingMember(mate);
                    continue;
                }

                bool isFastTick = this._updateCounter % FastTickInterval == 0;
                bool isSlowTick = (this._updateCounter + i) % SlowTickInterval == 0;

                // If player is not free but is fishing, only update fishing-related logic
                if (!_gameStateService.IsPlayerFree && isPlayerFishing)
                {
                    UpdateFishingOnly(mate, isFastTick);
                }
                else
                {
                    UpdateSquadMember(mate, ResolveRecruiterOrFallback(mate), isFastTick, isSlowTick);
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

        /// <summary>
        /// Re-warps the given recruiter's mates to their location after a cutscene/event ends.
        /// Called locally on the host when the host's cutscene ends, and by the dispatcher's
        /// <see cref="MessageDispatcher.OnCutsceneEnded"/> handler when a farmhand reports its
        /// own cutscene end via <see cref="CutsceneEnded"/>. Host-only; farmhands route via
        /// the notify message.
        /// </summary>
        public void WarpSquadToFarmer(Farmer recruiter)
        {
            if (Context.IsMultiplayer && !Context.IsMainPlayer) return;
            if (recruiter == null) return;

            foreach (var mate in this._squadManager.Members)
            {
                if (mate.RecruiterUniqueId != recruiter.UniqueMultiplayerID) continue;

                var npc = mate.Npc;
                _warpService.WarpCharacter(npc, recruiter.currentLocation.NameOrUniqueName, recruiter.TilePoint);
                mate.Path.Clear();
                ClearMateTask(mate);
                mate.IsCatchingUp = false;
                mate.StuckCounter = 0;
                mate.CurrentMoveDirection = -1;
                mate.IsInPool = false;
                mate.LastTilePoint = null;
                if (mate.IsRidingWithPlayer)
                {
                    mate.IsRidingWithPlayer = false;
                    npc.HideShadow = false;
                }
                npc.drawOffset = Vector2.Zero;
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

            // Recover if an external caller (e.g., another mod's Harmony patch) cleared
            // IsAnimating without going through Halt(), leaving the sentinel cooldown stranded.
            if (!mate.IsAnimating && mate.ActionCooldown > 500)
            {
                mate.Halt(); // This stops the animation and resets the IsAnimating flag.
                mate.ActionCooldown = 0;
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
                GenerateAndFollowPath(mate, player.TilePoint, isSlowTick, player);
                return;
            }

            // High-priority attacking task check (checked on fast ticks, can interrupt any task)
            if (isFastTick)
            {
                if (_config.TasksEnabled)
                {
                    var locationInfo = new LocationInfoWrapper(npc.currentLocation, npc);
                    var attackingTask = _unifiedTaskManager.FindAttackingTask(mate, locationInfo, player.TilePoint, npc.TilePoint, this._claimedInteractionSpots);
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
                        SquadTask newTask = _unifiedTaskManager.FindUnifiedTask(mate, locationInfo, player.TilePoint, npc.TilePoint, this._claimedTaskTargets, this._claimedInteractionSpots);
                        if (newTask != null)
                        {
                            AssignTaskToMate(mate, newTask);
                        }
                    }
                }
            }

            if (mate.HasTask())
            {
                HandleTaskExecution(mate, isSlowTick, player);
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

        private void HandleTaskExecution(ISquadMate mate, bool isSlowTick, Farmer player)
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
                GenerateAndFollowPath(mate, targetInteractionTile, isSlowTick, player);
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

                    // Only face the recruiter if we haven't just cleared a task (prevents brief turn glitch)
                    if (mate.FramesSinceTaskCleared > 15)
                    {
                        _taskService.FacePosition(mate.Npc, player.getStandingPosition());
                    }
                }
            }
            else
            {
                // If we are far from our spot, generate a path to it.
                GenerateAndFollowPath(mate, idealTile, isSlowTick, player);
            }
        }

        private void GenerateAndFollowPath(ISquadMate mate, Point targetTile, bool isSlowTick, Farmer player)
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
                        // fall back to pathing directly to the recruiter to collapse the formation.
                        pathableTarget = player.TilePoint;
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
                _warpService.WarpCharacter(npc, npc.currentLocation.NameOrUniqueName, player.TilePoint);
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

            if (npc.currentLocation != player.currentLocation)
            {
                _warpService.WarpCharacter(npc, player.currentLocation.NameOrUniqueName, player.TilePoint);
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
                int taskSpeed = (int)player.getMovementSpeed() + 1;
                npc.speed = Math.Max(2, taskSpeed);
            }
            else
            {
                float distanceToTarget = Vector2.Distance(npc.Tile, player.Tile);
                float targetSpeed = player.getMovementSpeed();
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
                // Friendship gain credits the recruiter so a farmhand's mate raises that
                // farmhand's friendship.
                var who = mate.TryGetRecruiter(out var rec) ? rec : (Game1.MasterPlayer ?? Game1.player);
                if (mate.Npc is Pet pet)
                {
                    pet.friendshipTowardFarmer.Value = Math.Min(1000, pet.friendshipTowardFarmer.Value + pointsToAdd);
                }
                else if (mate.Npc.isVillager())
                {
                    who.changeFriendship(pointsToAdd, mate.Npc);
                }
            }
            
            this._lastFriendshipGainHour = currentHour;
        }

        #region Horse Riding

        /// <summary>
        /// Updates riding state transitions (mount/dismount) for each online farmer and
        /// identifies the squad member riding behind a farmer (if any). In MP each online
        /// farmer can independently mount; the first eligible mate of THAT farmer rides
        /// with them.
        /// </summary>
        /// <returns>
        /// The squad member currently riding with the LOCAL screen's player, or null. The
        /// local-screen filter ensures the per-screen riding-update path stays compatible
        /// with the existing single-rider drawing/animation logic; remote farmers' riding
        /// mates are still updated server-side via the foreach below.
        /// </returns>
        private ISquadMate? UpdateRidingState()
        {
            if (!_config.EnableRiding)
                return null;

            // Walk every online farmer. In SP this loops once over Game1.player. In MP each
            // farmer's mount state is independently tracked so two farmhands can each ride
            // with their own recruited mate at the same time.
            foreach (var farmer in Game1.getOnlineFarmers())
            {
                long farmerId = farmer.UniqueMultiplayerID;
                bool isFarmerRiding = farmer.mount != null;
                _wasFarmerRiding.TryGetValue(farmerId, out bool wasFarmerRiding);

                if (isFarmerRiding && !wasFarmerRiding)
                {
                    // This farmer just mounted — assign their first available recruited mate to ride.
                    var rideMate = _squadManager.Members
                        .FirstOrDefault(m => m.RecruiterUniqueId == farmerId && !m.IsRidingWithPlayer);
                    if (rideMate != null)
                    {
                        ClearMateTaskAndReset(rideMate);
                        rideMate.IsRidingWithPlayer = true;
                        rideMate.Npc.HideShadow = true;

                        // Apply sitting sprite (same as Sitting feature)
                        _spriteManager?.TryApplySittingSpriteSheet(rideMate.Npc, rideMate, rideMate.Npc.FacingDirection);
                    }
                }
                else if (!isFarmerRiding && wasFarmerRiding)
                {
                    // This farmer just dismounted — release any of their riding mates.
                    foreach (var mate in _squadManager.Members)
                    {
                        if (mate.RecruiterUniqueId == farmerId && mate.IsRidingWithPlayer)
                        {
                            mate.IsRidingWithPlayer = false;
                            mate.Npc.drawOffset = Vector2.Zero;
                            mate.Npc.HideShadow = false;
                            _spriteManager?.RestoreOriginalTexture(mate.Npc, mate);
                            mate.Npc.Sprite.StopAnimation();
                            mate.Npc.flip = false;
                            mate.Halt();
                        }
                    }
                }

                _wasFarmerRiding[farmerId] = isFarmerRiding;
            }

            // Return only the LOCAL-screen player's riding mate so the existing per-tick
            // riding update path drives at most one mate per screen. Remote farmers' riding
            // mates have their position synced via the netfields once the host writes Position.
            var localId = Game1.player?.UniqueMultiplayerID ?? 0L;
            return _squadManager.Members.FirstOrDefault(m =>
                m.IsRidingWithPlayer && m.RecruiterUniqueId == localId);
        }

        /// <summary>
        /// Updates a squad member that is riding on the horse behind the player.
        /// Aligns the NPC to the player's visual riding position using drawOffset,
        /// applies sitting sprite, and syncs the horse wobble animation.
        /// </summary>
        private void UpdateRidingMember(ISquadMate mate)
        {
            var npc = mate.Npc;
            // Anchor to the recruiter (the farmer this NPC is riding behind), not the local
            // screen's player. In MP a farmhand's recruited mate rides behind the farmhand;
            // their mount.Position is the most current position via netfield sync.
            if (!mate.TryGetRecruiter(out var rider))
                return;

            SquadMateStateHelper.MaintainControl(npc);

            // Ensure the NPC is in the same location as the rider
            if (npc.currentLocation != rider.currentLocation)
            {
                _warpService.WarpCharacter(npc, rider.currentLocation.NameOrUniqueName, rider.TilePoint);
            }

            // +Y offset layers the NPC in front of the rider for Up/Left/Right.
            // For Down, HarmonyPatches.AdjustSittingNpcDepth overrides depth to slot the NPC behind the rider.
            // Use mount.Position when available — Horse.SyncPositionToRider keeps it current
            // and the netfield replication is more reliable than reading rider.Position directly
            // for remote farmhands.
            var anchorPos = rider.mount?.Position ?? rider.Position;
            npc.Position = new Vector2(anchorPos.X, anchorPos.Y + 8f);

            npc.faceDirection(rider.FacingDirection);

            bool usedCustomSprite = _spriteManager?.TryApplySittingSpriteSheet(npc, mate, rider.FacingDirection) ?? false;
            if (!usedCustomSprite)
            {
                _spriteManager?.ForceApplyTaskAnimation(npc, "Sitting");
            }
        }

        /// <summary>
        /// Computes the riding draw offset for a squad mate. Invoked once per NPC.draw call.
        /// </summary>
        internal Vector2 ComputeRidingDrawOffset(ISquadMate mate)
        {
            var npc = mate.Npc;
            // Resolve the rider this mate is riding behind. Falls through to the local
            // player only if the recruiter is offline (rare for an actively-riding mate).
            var player = mate.TryGetRecruiter(out var rec) ? rec : Game1.player;
            if (player?.mount == null) return Vector2.Zero;

            // Wobble: sync to the horse's sprite animation index (same formula as Farmer.showRiding).
            float wobble = 0f;
            if (player.isMoving())
            {
                wobble = player.mount.Sprite.currentAnimationIndex switch
                {
                    1 => -4f,
                    2 => -4f,
                    4 => 4f,
                    5 => 4f,
                    _ => 0f
                };
            }

            const float depthOffset = 8f;

            Vector2 saddleAnchor = RidingSaddleAnchorByDirection.TryGetValue(player.FacingDirection, out var a)
                ? a
                : RidingSaddleAnchorByDirection[2];

            int spriteWidth = npc.Sprite?.SpriteWidth > 0 ? npc.Sprite.SpriteWidth : 16;
            int spriteHeight = npc.Sprite?.SpriteHeight > 0 ? npc.Sprite.SpriteHeight : 32;
            var ridingCfg = _spriteManager?.GetTaskSpriteConfig(npc, "Sitting");
            float anchorX = ridingCfg?.AnchorPixel?.X ?? (spriteWidth / 2f);
            float anchorY = ridingCfg?.AnchorPixel?.Y ?? spriteHeight;

            float pivotBaseX = spriteWidth * 4f / 2f;
            float pivotBaseY = npc.GetBoundingBox().Height / 2f;

            // Compensation for mods that alter the horse's render-centering width
            // (e.g., Horse Overhaul's ThinHorse)
            int horseSpriteW = player.mount.Sprite?.SpriteWidth ?? 32;
            int horseWidth = player.mount.GetSpriteWidthForPositioning();
            float xDelta = 2 * (horseWidth - horseSpriteW);

            Vector2 baseOffset = new Vector2(
                saddleAnchor.X - pivotBaseX - (anchorX - spriteWidth / 2f) * 4f + xDelta,
                saddleAnchor.Y - depthOffset - wobble - pivotBaseY - (anchorY - spriteHeight * 3f / 4f) * 4f
            );

            // MP smoothing: on remote screens npc.Position can trail the rider's Position by
            // 1-2 ticks, leaving the NPC visibly behind the horse. Compute the offset against
            // the rider's *current* anchor and exponentially smooth (Lerp 0.5 ≈ ~2-frame
            // half-life) per screen, per recruiter. On the host this collapses to a no-op
            // because npc.Position == rider.mount.Position + (0,8) already.
            Vector2 currentRiderAnchor = (player.mount?.Position ?? player.Position) + new Vector2(0f, 8f);
            var perScreenDict = _smoothedRiderAnchor.Value;
            Vector2 lastAnchor = perScreenDict.TryGetValue(player.UniqueMultiplayerID, out var lp)
                ? lp
                : currentRiderAnchor;
            Vector2 smoothedAnchor = Vector2.Lerp(lastAnchor, currentRiderAnchor, 0.5f);
            perScreenDict[player.UniqueMultiplayerID] = smoothedAnchor;

            Vector2 lagCorrection = smoothedAnchor - npc.Position;

            return baseOffset + lagCorrection;
        }

        #endregion
    }
}
