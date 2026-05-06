using System.Reflection;
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
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Framework.Wrappers;

namespace TheStardewSquad.Framework
{
    public class FollowerManager
    {
        #region Construction & Dependencies

        // Service injections
        private readonly IMonitor _monitor;
        private readonly SquadManager _squadManager;
        private readonly WaitingNpcsManager _waitingNpcsManager;
        private readonly ModConfig _config;
        private readonly DebrisCollector _debrisCollector;
        private readonly UnifiedTaskManager _unifiedTaskManager;
        private readonly FormationManager _formationManager;
        private readonly BehaviorManager _behaviorManager;
        private readonly IRandomService _randomService;
        private SpriteManager? _spriteManager;
        private MessageDispatcher? _dispatcher;
        private RecruitmentManager? _recruitment;

        // Tick-loop counters and intervals
        private const int SlowTickInterval = 15;
        private const int FastTickInterval = 2;
        private const int MimickingTaskDurationTicks = 40; // 40 slow ticks = 10 seconds (40 * 15 frames / 60 FPS)
        private int _updateCounter = 0;
        private int _lastFriendshipGainHour = -1;

        // Per-mate task-claim sets (host-side path planning).
        private readonly HashSet<Vector2> _claimedInteractionSpots = new();
        private readonly HashSet<Point> _claimedTaskTargets = new();

        // Festival latch — `true` while a festival is running so we re-dismiss only on entry.
        private bool _wasInFestival = false;

        // Per-screen cutscene detection so each peer (and each split-screen player) tracks its
        // own cutscene state. The host warps its own mates directly when its local cutscene ends;
        // a farmhand fires a CutsceneEnded notify message which the host receives and re-warps
        // the farmhand's mates only.
        private readonly PerScreen<bool> _wasInCutscene = new(() => false);

        // Per-farmer fishing state, keyed by UniqueMultiplayerID. The host iterates online
        // farmers each slow tick to detect fishing transitions per peer — needed because in MP
        // a farmhand reeling in must clear that recruiter's mates' fishing tasks.
        private readonly Dictionary<long, bool> _wasFarmerFishing = new();

        // Per-farmer riding state. In MP each online farmer can mount independently and
        // their own recruited mate rides with them.
        private readonly Dictionary<long, bool> _wasFarmerRiding = new();

        // Per-mate IsAnimating from the previous host tick, keyed by (NpcName, RecruiterId).
        // Used to detect IsAnimating true→false transitions and broadcast a ClearIdleAnim
        // so peers stop the animation. Host-only; farmhand never populates this.
        private readonly Dictionary<(string, long), bool> _wasAnimating = new();

        // Wall-clock timestamp per auto-parked recruiter id (offline-recruiter handling).
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
        /// Per-direction saddle anchor for the Riding task.
        /// World-pixel position (relative to player.Position) where the NPC sprite's AnchorPixel will land.
        /// Key = Character.FacingDirection (0=Up, 1=Right, 2=Down, 3=Left).
        /// </summary>
        private static readonly Dictionary<int, Vector2> RidingSaddleAnchorByDirection = new()
        {
            { 0, new Vector2(50f,  28f) }, // Up
            { 1, new Vector2(24f, 16f) }, // Right
            { 2, new Vector2(50f, -4f) }, // Down
            { 3, new Vector2(88f, 16f) }, // Left
        };

        public FollowerManager(
            IMonitor monitor,
            SquadManager squadManager,
            WaitingNpcsManager waitingNpcsManager,
            ModConfig config,
            DebrisCollector debrisCollector,
            UnifiedTaskManager unifiedTaskManager,
            FormationManager formationManager,
            BehaviorManager behaviorManager,
            IRandomService randomService)
        {
            this._monitor = monitor;
            this._squadManager = squadManager;
            this._waitingNpcsManager = waitingNpcsManager;
            this._config = config;
            this._debrisCollector = debrisCollector;
            this._unifiedTaskManager = unifiedTaskManager;
            this._formationManager = formationManager;
            this._behaviorManager = behaviorManager;
            this._randomService = randomService;
        }

        /// <summary>Sets the SpriteManager dependency (called after construction due to initialization order).</summary>
        public void SetSpriteManager(SpriteManager spriteManager)
        {
            this._spriteManager = spriteManager;
        }

        public void AttachDispatcher(MessageDispatcher dispatcher)
        {
            this._dispatcher = dispatcher;
        }

        public void SetRecruitmentManager(RecruitmentManager recruitment)
        {
            this._recruitment = recruitment;
        }

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

        #endregion

        #region Multiplayer Coordination
        // MP-specific helpers for state vanilla SDV doesn't netfield (Sprite animations,
        // farmerPassesThrough, controller, behavior cache, etc.). See pattern-mp-npc-duplication
        // and pattern-non-netfield-mp-state in /memory for the design rationale. Each method
        // notes whether it runs host-only, farmhand-only, or both.

        /// <summary>
        /// Returns ALL live NPC instances the player could collide with that share the captured
        /// NPC's name, across all loaded locations including building interiors AND generated
        /// levels. In MP the farmhand process can hold multiple same-name NPC instances:
        /// a. the captured reference from <see cref="MessageDispatcher.ApplySnapshot"/>,
        ///     which can be orphaned by vanilla netcode replacing it on warp/location-load;
        /// b. a vanilla-spawned instance at the NPC's home location (no recruiter modData);
        /// c. the host's recruited instance replicated into the host's current location.
        /// We must apply per-process state maintenance to all of them. Vanilla collision
        /// only checks the instance currently in <c>location.characters</c>, but we don't
        /// know which one that is from any single screen's perspective. Side effect: any
        /// non-recruited modded duplicates sharing a recruit's name will also be marked
        /// walk-through; accepted as the lesser evil.
        /// </summary>
        public static IEnumerable<NPC> ResolveAllLiveNpcs(NPC captured)
        {
            string name = captured.Name;
            var found = new List<NPC>();
            Utility.ForEachLocation(loc =>
            {
                foreach (var c in loc.characters)
                {
                    if (c is NPC npc && npc.Name == name)
                        found.Add(npc);
                }
                return true;
            }, includeInteriors: true, includeGenerated: true);
            return found;
        }

        /// <summary>
        /// Farmhand-only: tick down per-mate animation cooldowns set by
        /// <see cref="MessageDispatcher.OnPlayIdleAnim"/>, and clear the animation when a
        /// non-looping idle finishes.
        /// </summary>
        private void TickFarmhandIdleAnimationCooldowns()
        {
            foreach (var mate in this._squadManager.Members.ToList())
            {
                if (!mate.IsAnimating) continue;
                if (mate.ActionCooldown == int.MaxValue) continue;
                if (!mate.IsOnCooldown()) continue;

                if (mate.DecrementCooldown())
                {
                    mate.Halt();
                    // mate.Halt() clears Sprite.CurrentAnimation on mate.Npc, but per
                    // pattern-mp-npc-duplication mate.Npc may be orphaned; clear all live
                    // same-name instances too so the visible NPC stops animating.
                    foreach (var live in ResolveAllLiveNpcs(mate.Npc))
                    {
                        if (ReferenceEquals(live, mate.Npc)) continue;
                        live.Sprite.CurrentAnimation = null;
                        live.Sprite.StopAnimation();
                    }
                }
            }
        }

        // Vanilla Pet's master-side update calls _OnNewBehavior() whenever the cached
        // _currentBehavior diverges from the netfielded CurrentBehavior. There's no equivalent
        // slave-side trigger, so on a farmhand a behavior change (e.g., dismissed pet → Sleep)
        // never re-arms Sprite.CurrentAnimation and Pet.updateSlaveAnimation early-returns
        // on null animation. We invoke _OnNewBehavior reflectively below to restore the
        // missing slave-side hook for non-recruited pets.
        private static readonly FieldInfo PetCurrentBehaviorField = typeof(Pet).GetField(
            "_currentBehavior", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo PetOnNewBehaviorMethod = typeof(Pet).GetMethod(
            "_OnNewBehavior", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// <summary>
        /// Per-process per-tick frame cycling for sustained tasks (Sitting, Fishing).
        /// ForceApplyTaskAnimation derives the frame from <c>Game1.currentGameTime.TotalGameTime.TotalMilliseconds</c>
        /// which is local-process; each peer drives its own NPC instances.
        /// </summary>
        private void DriveSustainedTaskFrames()
        {
            if (this._spriteManager == null) return;
            foreach (var mate in this._squadManager.Members)
            {
                var type = mate.Task?.Type;
                if (type != TaskType.Sitting && type != TaskType.Fishing) continue;
                foreach (var live in ResolveAllLiveNpcs(mate.Npc))
                    this._spriteManager.ForceApplyTaskAnimation(live, type.ToString()!);
            }
        }

        /// <summary>
        /// Farmhand-only per-tick sprite animation drive for pets.
        /// </summary>
        private void DriveFarmhandPetAnimation(GameTime time)
        {
            // PASS 1: recruited pets — explicit walk/idle drive.
            var recruitedPetNames = new HashSet<string>();
            foreach (var mate in this._squadManager.Members)
            {
                if (mate.Npc is not Pet) continue;
                recruitedPetNames.Add(mate.Npc.Name);

                foreach (var live in ResolveAllLiveNpcs(mate.Npc))
                {
                    if (live is not Pet pet) continue;

                    if (mate.IsAnimating)
                    {
                        if (!pet.isMoving()) continue;
                        mate.IsAnimating = false;
                    }

                    pet.Sprite.CurrentAnimation = null;
                    pet.faceDirection(pet.FacingDirection);
                    if (pet.isMoving())
                    {
                        pet.animateInFacingDirection(time);
                    }
                    else
                    {
                        pet.Sprite.CurrentFrame = BehaviorManager.GetIdleFrame(pet.FacingDirection);
                        pet.Sprite.StopAnimation();
                    }
                }
            }

            if (PetCurrentBehaviorField == null || PetOnNewBehaviorMethod == null) return;

            Utility.ForEachLocation(loc =>
            {
                foreach (var c in loc.characters)
                {
                    if (c is not Pet pet) continue;
                    if (recruitedPetNames.Contains(pet.Name)) continue;

                    string netCurrent = pet.CurrentBehavior;
                    string cached = (string)PetCurrentBehaviorField.GetValue(pet);
                    if (netCurrent != cached)
                    {
                        PetOnNewBehaviorMethod.Invoke(pet, null);
                    }
                }
                return true;
            }, includeInteriors: true, includeGenerated: true);
        }

        /// <summary>
        /// Host-only. Compares current <c>IsAnimating</c> against the snapshot from the
        /// previous tick; broadcasts <c>ClearIdleAnim</c> for each mate that just transitioned
        /// true→false. Then refreshes the snapshot and prunes entries for mates no longer
        /// in the squad. Cheap (one dictionary lookup per mate; no allocs in steady state).
        /// </summary>
        private void DetectAndBroadcastAnimationClears()
        {
            foreach (var mate in this._squadManager.Members)
            {
                var key = (mate.Npc.Name, mate.RecruiterUniqueId);
                bool wasAnim = this._wasAnimating.TryGetValue(key, out var prev) && prev;
                bool isAnim = mate.IsAnimating;
                if (wasAnim && !isAnim)
                {
                    this._dispatcher?.BroadcastClearIdleAnim(mate);
                }
                this._wasAnimating[key] = isAnim;
            }

            // Drop snapshot entries for mates no longer in the squad (dismissed, recruiter
            // disconnected, etc.) so the dictionary doesn't grow unbounded across a session.
            if (this._wasAnimating.Count > this._squadManager.Count)
            {
                var liveKeys = new HashSet<(string, long)>(
                    this._squadManager.Members.Select(m => (m.Npc.Name, m.RecruiterUniqueId)));
                foreach (var k in this._wasAnimating.Keys.ToList())
                {
                    if (!liveKeys.Contains(k)) this._wasAnimating.Remove(k);
                }
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
                Game1.warpCharacter(npc, recruiter.currentLocation.NameOrUniqueName, recruiter.TilePoint);
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

        #endregion

        #region Tick Entry & Update Dispatch
        // Per-tick orchestrator (OnUpdateTicked) plus the per-mate dispatch helpers it routes to.
        // Task-clearing and session-reset utilities live here too because they're called from
        // many of these per-mate paths.

        public void OnUpdateTicked(object? sender, EventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (this._squadManager.Count == 0 && this._waitingNpcsManager.Count == 0) return;

            // Per-screen cutscene-end detection. Runs above the host-only guard so farmhand
            // peers can also detect when their own local cutscene ended; they notify the host
            // via CutsceneEnded so the host can re-warp their mates. Each split-screen player
            // tracks its own state via PerScreen<bool>.
            bool isInCutsceneNow = Game1.eventUp;
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

            // Per-process local-state maintenance. Runs on host AND farmhands because
            // farmerPassesThrough, controller, IsWalkingInSquare are non-netfield local
            // fields (vanilla SDV does not sync them); see SquadMateStateHelper.MaintainControl.
            //
            // We iterate ALL same-name NPC instances across every loaded location, not just
            // mate.Npc, because in MP the farmhand process can hold multiple
            // instances of the same NPC
            foreach (var mate in this._squadManager.Members.ToList())
            {
                foreach (var live in ResolveAllLiveNpcs(mate.Npc))
                {
                    SquadMateStateHelper.MaintainControl(live);
                }
            }
            foreach (var waitingMate in this._waitingNpcsManager.WaitingMembers.ToList())
            {
                foreach (var live in ResolveAllLiveNpcs(waitingMate.Npc))
                {
                    SquadMateStateHelper.MaintainControl(live);
                }
            }

            // Farmhand-only: drive sprite animation for recruited pets. MaintainControl pins
            // pet.CurrentBehavior to "Sleep", which makes vanilla Pet.updateSlaveAnimation
            // early-return on its `CurrentBehavior != "Walk"` gate, so vanilla never animates
            // the pet on the farmhand. We replicate the motion-aware drive vanilla uses for
            // villager mates (which works because the base Character.updateSlaveAnimation has
            // no behavior gate). The host already drives animation via ExecutePathMovement
            // below this gate, so this pass is intentionally farmhand-only.
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                this.TickFarmhandIdleAnimationCooldowns();
                this.DriveFarmhandPetAnimation(Game1.currentGameTime);
            }

            this.HandleFriendshipGain();

            // Per-process per-tick frame drive for sustained tasks (Sitting, Fishing).
            // Sprite.currentFrame and npc.flip aren't netfielded; ForceApplyTaskAnimation
            // computes the frame from elapsed time, which each peer drives locally.
            this.DriveSustainedTaskFrames();

            // Track player sitting state on slow ticks (timer is set when player sits down)
            Patches.HarmonyPatches.UpdatePlayerSittingState();

            // Per-screen riding-state transitions. Sets mate.IsRidingWithPlayer (mod state) and
            // applies the sitting sprite sheet on every process so each peer's NPC.Draw prefix
            // computes the saddle drawOffset and renders the sitting sprite locally. Host-only
            // authoritative writes (HideShadow netfield, ClearMateTaskAndReset, mate.Halt,
            // BroadcastClearTaskAnim) are gated inside.
            this.UpdateRidingState();

            // Host-only authority: in MP, only the main player runs squad AI mutations.
            if (Context.IsMultiplayer && !Context.IsMainPlayer) return;

            this._debrisCollector.Update(this._squadManager.Members);

            if (HandleFestivalState()) return;

            // Per-recruiter busy gate. Host-local UI state (menu open, window unfocused) only
            // gates the HOST's own mates — farmhand-recruited mates must keep updating regardless,
            // otherwise the host opening a menu freezes farmhand gameplay. The actual gate runs
            // per-mate inside the loop below; here we just precompute the host-local flags.
            // Slow-tick globals (mimicking timers, per-farmer action trackers) and the riding-state
            // pass also need to keep firing while the host is busy so farmhand actions still reach
            // their mates.
            bool isHostBusy = !Context.IsPlayerFree || !Game1.game1.IsActive;
            bool isHostFishing = _config.FishingMode != TaskMode.Disabled && TaskManager.IsFarmerFishing(Game1.player);

            this._updateCounter++;

            // Update mimicking timers once per cycle (not per mate) to avoid multiple resets
            bool isGlobalSlowTick = this._updateCounter % SlowTickInterval == 0;
            if (isGlobalSlowTick)
            {
                // Park mates whose recruiter disconnected, resume on reconnect, auto-dismiss
                // after ParkTimeoutMinutes. Does nothing in SP.
                HandleOfflineRecruiters();

                UpdateMimickingTimers();

                // Per-farmer state polling: detects each online farmer's tool use (Watering/
                // Mining/Lumbering/Attacking), fishing transitions, and sitting, dispatching
                // to OnFarmerAction so MP farmhand actions reach the host's mates.
                UpdateFarmerActionTrackers();
            }

            var members = this._squadManager.Members.ToList();
            for (int i = 0; i < members.Count; i++)
            {
                var mate = members[i];

                // Drive Position/facingDirection for any mate currently riding (host's own mate
                // OR a remote farmer's recruited mate). Riding-state transitions are handled
                // above the host-only gate; this branch just keeps the riding mate pinned to
                // its rider's saddle every tick. Host is the authoritative writer for the
                // netfielded position and facing.
                if (mate.IsRidingWithPlayer)
                {
                    UpdateRidingMember(mate);
                    continue;
                }

                bool isFastTick = this._updateCounter % FastTickInterval == 0;
                bool isSlowTick = (this._updateCounter + i) % SlowTickInterval == 0;

                var recruiter = ResolveRecruiterOrFallback(mate);
                bool isHostRecruiter = recruiter.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID;

                // Gate on host's local UI state ONLY for host-recruited mates. Farmhand-recruited
                // mates always update so host's menu/alt-tab doesn't freeze them.
                if (isHostRecruiter && isHostBusy)
                {
                    if (isHostFishing)
                        UpdateFishingOnly(mate, isFastTick);
                    else if (!mate.IsAnimating)
                        mate.Halt();
                    // If an idle animation is mid-play, skip Halt so it isn't cleared.
                    continue;
                }

                UpdateSquadMember(mate, recruiter, isFastTick, isSlowTick);
            }

            // Update waiting NPCs (keep them stationary and maintain control)
            foreach (var waitingMate in this._waitingNpcsManager.WaitingMembers.ToList())
            {
                UpdateWaitingNpc(waitingMate);
            }

            this.DetectAndBroadcastAnimationClears();
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

            // Idle animations must yield to incoming work.
            bool idleBlocksMimicking = mate.IsAnimating && !mate.HasTask() && mate.MimickingTaskTimer > 0;
            if ((mate.IsAnimating && distanceToPlayer > 2.5f) || idleBlocksMimicking)
            {
                mate.Halt();
                mate.ActionCooldown = 0;
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
                    TaskManager.AnimateFishing(npc, mate.Task.Tile);
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

                    if (this._config.EnableIdleAnimations && mate.IdleTicks > 12 && _randomService.NextDouble() < 1.0 / 80.0)
                    {
                        var animationSpec = _behaviorManager.GetRandomIdleAnimation(mate.Npc);
                        if (animationSpec != null)
                        {
                            _behaviorManager.PlayIdleAnimation(mate, animationSpec);
                            // Sprite.CurrentAnimation isn't netfielded; broadcast so peers
                            // replay the same frames on their NPC instance. No-op in SP.
                            this._dispatcher?.BroadcastPlayIdleAnim(mate, animationSpec);
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

        /// <summary>
        /// Updates a waiting NPC to keep them stationary at their wait position.
        /// </summary>
        private void UpdateWaitingNpc(ISquadMate mate)
        {
            var npc = mate.Npc;

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
                TaskManager.AnimateFishing(npc, mate.Task.Tile);
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

            // Sustained tasks need their tiles propagated to peers so the line/seat render
            // paths can compare npc.TilePoint to mate.Task.InteractionTile. SquadEntry
            // carries InteractionTile/TaskTile (v5+); peers rehydrate via ApplySnapshot.
            if (Context.IsMainPlayer
                && (newTask.Type == TaskType.Fishing || newTask.Type == TaskType.Sitting))
            {
                this._dispatcher?.BroadcastSnapshot();
            }

            // Note: MimickingTaskTimer is now managed centrally in UpdateMimickingTimers()
            // based on player actions, not when assigning tasks
        }

        private void ClearMateTask(ISquadMate mate)
        {
            // Broadcast a peer clear only when the cleared task had a long-running visual
            // that peers wouldn't terminate on their own. Sustained tasks (Sitting, Fishing)
            // and texture swaps need an explicit clear.
            bool clearedTaskIsSustained = mate.Task != null
                && (mate.Task.Type == TaskType.Fishing || mate.Task.Type == TaskType.Sitting);
            bool needsClearBroadcast = (clearedTaskIsSustained || !string.IsNullOrEmpty(mate.AppliedTaskTexture))
                && Context.IsMainPlayer;

            // Snapshot peers so they drop the stale Task object too. Without this, peers
            // would keep rendering the line/seat at the old InteractionTile until the
            // next recruit/dismiss-driven snapshot arrives.
            bool needsSnapshot = Context.IsMainPlayer
                && mate.Task != null
                && (mate.Task.Type == TaskType.Fishing || mate.Task.Type == TaskType.Sitting);

            if (mate.Task != null) this._claimedTaskTargets.Remove(mate.Task.Tile);
            if (mate.ClaimedInteractionSpot.HasValue)
            {
                this._claimedInteractionSpots.Remove(mate.ClaimedInteractionSpot.Value);
                mate.ClaimedInteractionSpot = null;
            }
            mate.Task = null;
            mate.FramesSinceTaskCleared = 0; // Start counting frames since task cleared
            mate.LastMonsterTile = null; // Reset monster tracking when task is cleared

            if (needsClearBroadcast) this._dispatcher?.BroadcastClearTaskAnim(mate);
            if (needsSnapshot) this._dispatcher?.BroadcastSnapshot();
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

        #endregion

        #region Following & Pathfinding

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
                        TaskManager.FacePosition(mate.Npc, player.getStandingPosition());
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
                Game1.warpCharacter(npc, npc.currentLocation.NameOrUniqueName, player.TilePoint);
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
            npc.animateInFacingDirection(Game1.currentGameTime);

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
                Game1.warpCharacter(npc, player.currentLocation.NameOrUniqueName, player.TilePoint);
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

        #endregion

        #region Task Execution

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

        #endregion

        #region Mimicking Subsystem
        // Per-farmer detection of mimickable actions (tool use, fishing, sitting, harvest, etc.)
        // and the timer-based decay that drives same-task mimicking on co-located mates. Detection
        // is split between Harmony postfixes (event-based: Pet/FarmAnimal/Crop/Shears/MilkPail) and
        // per-tick state polling here (sustained: Watering/Mining/Lumbering/Attacking/Fishing/Sitting).

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

        /// <summary>
        /// Decays mimicking timers and clears expired mimicking tasks. Detection itself happens
        /// upstream — Harmony postfixes call <see cref="OnFarmerAction"/> for event-based tasks
        /// (Pet/FarmAnimal/Crop/Shears/MilkPail), and <see cref="UpdateFarmerActionTrackers"/>
        /// re-asserts per-farmer state-based tasks (Watering/Mining/Lumbering/Attacking/Fishing/Sitting)
        /// each slow tick. This method is the pure decay half.
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

            foreach (var mate in _squadManager.Members)
            {
                if (mate.MimickingTaskTimer > 0)
                {
                    mate.MimickingTaskTimer--;
                }

                // Clear the active mimicking task when its timer expires.
                if (mate.HasTask() && IsMimickingTask(mate.Task.Type)
                    && mate.MimickingTaskType == mate.Task.Type
                    && mate.MimickingTaskTimer <= 0)
                {
                    ClearMateTaskAndReset(mate);
                }

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
                    TaskManager.TryNpcCatchFish(mate, squadSize);
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

        /// <summary>Called when a farmer stops fishing without catching (cancel/walk away). Clears only that recruiter's fishing-task mates.</summary>
        private void OnFarmerStoppedFishing(Farmer who)
        {
            if (who == null) return;
            foreach (var mate in _squadManager.Members)
            {
                if (mate.RecruiterUniqueId != who.UniqueMultiplayerID) continue;
                if (mate.Task?.Type == TaskType.Fishing)
                {
                    ClearMateTaskAndReset(mate);
                }
            }
        }

        /// <summary>
        /// Per-farmer state polling for tool actions and fishing. Runs host-only each slow tick.
        /// Iterates <see cref="Game1.getOnlineFarmers"/> and re-asserts the mimicking timer for any
        /// farmer actively using a tool (Watering/Mining/Lumbering/Attacking) or fishing. For
        /// fishing, also detects the stop transition to clear that recruiter's fishing-task mates.
        /// Sitting is handled separately in <see cref="UpdateFarmerSittingTracker"/> so the
        /// transition fires once on sit-down.
        /// </summary>
        private void UpdateFarmerActionTrackers()
        {
            if (!_config.TasksEnabled) return;

            foreach (var f in Game1.getOnlineFarmers())
            {
                if (f == null) continue;

                // Tool-active actions: re-assert each tick the swing is in progress so the
                // mimicking timer stays full while the action is sustained. OnFarmerAction
                // is a no-op for non-mimicking modes, so safe to call unconditionally.
                if (f.UsingTool)
                {
                    switch (f.CurrentTool)
                    {
                        case StardewValley.Tools.WateringCan:
                            OnFarmerAction(f, TaskType.Watering);
                            break;
                        case StardewValley.Tools.Pickaxe:
                            OnFarmerAction(f, TaskType.Mining);
                            break;
                        case StardewValley.Tools.Axe:
                            OnFarmerAction(f, TaskType.Lumbering);
                            break;
                        case StardewValley.Tools.MeleeWeapon:
                        case StardewValley.Tools.Slingshot:
                            OnFarmerAction(f, TaskType.Attacking);
                            break;
                    }
                }

                // Fishing has a stop transition (clear that recruiter's fishing tasks when the
                // farmer reels in / cancels). The OnFarmerAction call re-asserts mimicking;
                // the transition handler clears any active fishing task on stop.
                bool isFishingNow = TaskManager.IsFarmerFishing(f);
                if (isFishingNow)
                {
                    OnFarmerAction(f, TaskType.Fishing);
                }
                bool wasFishing = _wasFarmerFishing.GetValueOrDefault(f.UniqueMultiplayerID);
                if (wasFishing && !isFishingNow)
                {
                    OnFarmerStoppedFishing(f);
                }
                _wasFarmerFishing[f.UniqueMultiplayerID] = isFishingNow;

                // Sitting is a sustained state with no clean Harmony hook covering all sit-down
                // entry paths (right-click vs walk-onto vs Furniture.checkForAction). Re-asserting
                // each tick the farmer is sitting catches every sit-down implicitly and keeps the
                // mimicking timer fresh. No stop-transition handler needed — ExecuteSittingTask
                // (TaskManager.cs) clears its own task once IsFarmerSitting(recruiter) goes false.
                if (TaskManager.IsFarmerSitting(f))
                {
                    OnFarmerAction(f, TaskType.Sitting);
                }
            }
        }

        #endregion

        #region Festival & Friendship

        private bool HandleFestivalState()
        {
            bool isInFestival = Game1.isFestival();
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

            int currentHour = Game1.timeOfDay / 100;

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

            long localId = Game1.player.UniqueMultiplayerID;
            bool isHost = !Context.IsMultiplayer || Context.IsMainPlayer;

            foreach (var mate in this._squadManager.Members)
            {
                if (mate.Npc is Pet pet)
                {
                    // Pet.friendshipTowardFarmer is a host-owned netfield (Pet lives in
                    // Game1.locations); only host-side writes replicate. Vanilla pet
                    // friendship has no per-recruiter dimension; one shared value.
                    if (isHost)
                        pet.friendshipTowardFarmer.Value = Math.Min(1000, pet.friendshipTowardFarmer.Value + pointsToAdd);
                }
                else if (mate.Npc.isVillager())
                {
                    // Each peer credits only its own Game1.player. Host writing to a
                    // farmhand's friendshipData replica does not propagate.
                    if (mate.RecruiterUniqueId == localId)
                        Game1.player.changeFriendship(pointsToAdd, mate.Npc);
                }
            }

            this._lastFriendshipGainHour = currentHour;
        }

        #endregion

        #region Horse Riding

        /// <summary>
        /// Updates riding state transitions (mount/dismount) for each online farmer and
        /// identifies the squad member riding behind a farmer (if any). In MP each online
        /// farmer can independently mount; the first eligible mate of THAT farmer rides
        /// with them.
        /// </summary>
        private void UpdateRidingState()
        {
            if (!_config.EnableRiding)
                return;

            // Walk every online farmer. In SP this loops once over Game1.player. In MP each
            // farmer's mount state is independently tracked so two farmhands can each ride
            // with their own recruited mate at the same time. Runs on every process; host-only
            // authoritative writes (HideShadow netfield, ClearMateTaskAndReset, mate.Halt,
            // BroadcastClearTaskAnim) are gated on Context.IsMainPlayer below. The per-screen
            // side effects (mate.IsRidingWithPlayer, sprite sheet swap, drawOffset reset) must
            // run on every process so each peer's NPC.Draw prefix applies the saddle offset
            // and renders the sitting sprite. _wasFarmerRiding is per-instance, so each
            // process tracks transitions independently.
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
                        rideMate.IsRidingWithPlayer = true;

                        // Host-only: task clear, HideShadow netfield, and sprite-sheet swap.
                        // TryApplySittingSpriteSheet broadcasts PlayTaskAnim to peers, whose
                        // OnPlayTaskAnim handler iterates ResolveAllLiveNpcs and applies the
                        // swap to every live duplicate. Calling it directly on farmhand would
                        // target only mate.Npc (potentially the orphan per
                        // pattern-mp-npc-duplication) and pre-set mate.OriginalTexture from
                        // the orphan, which the broadcast handler then refuses to overwrite,
                        // leaving the visible duplicate stuck on the sitting sheet.
                        if (Context.IsMainPlayer)
                        {
                            ClearMateTaskAndReset(rideMate);
                            rideMate.Npc.HideShadow = true;
                            bool usedCustomSprite = _spriteManager?.TryApplySittingSpriteSheet(rideMate.Npc, rideMate, farmer.FacingDirection) ?? false;
                            if (!usedCustomSprite)
                            {
                                _spriteManager?.ForceApplyTaskAnimation(rideMate.Npc, TaskType.Sitting.ToString());
                                _dispatcher?.BroadcastPlayTaskAnim(rideMate, TaskType.Sitting.ToString(), farmer.FacingDirection, null);
                            }
                        }
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

                            // Reset drawOffset on every live duplicate. NPC.Draw_Prefix sets
                            // drawOffset on the visible instance every frame while riding;
                            // once IsRidingWithPlayer flips false the prefix stops touching
                            // it, so the last saddle offset would persist if not cleared
                            // explicitly. mate.Npc may be the orphan (per
                            // pattern-mp-npc-duplication), so iterate across all duplicates.
                            foreach (var live in ResolveAllLiveNpcs(mate.Npc))
                            {
                                live.drawOffset = Vector2.Zero;
                            }

                            // Host-only: HideShadow netfield, sprite restore (broadcasts
                            // ClearTaskAnim to peers; OnClearTaskAnim handler iterates
                            // ResolveAllLiveNpcs and calls RestoreOriginalTexture per
                            // duplicate), and mate state cleanup.
                            if (Context.IsMainPlayer)
                            {
                                mate.Npc.HideShadow = false;
                                _spriteManager?.RestoreOriginalTexture(mate.Npc, mate);
                                mate.Npc.Sprite.StopAnimation();
                                mate.Npc.flip = false;
                                mate.Halt();
                                // Riding-dismount cleanup doesn't go through ClearMateTask; broadcast directly.
                                this._dispatcher?.BroadcastClearTaskAnim(mate);
                            }
                        }
                    }
                }

                _wasFarmerRiding[farmerId] = isFarmerRiding;
            }
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

            // Ensure the NPC is in the same location as the rider
            if (npc.currentLocation != rider.currentLocation)
            {
                Game1.warpCharacter(npc, rider.currentLocation.NameOrUniqueName, rider.TilePoint);
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
        /// Takes the live NPC instance being drawn rather than reading mate.Npc, because on
        /// the farmhand mate.Npc may be the orphan duplicate (per pattern-mp-npc-duplication).
        /// The visible instance's Position is what the host has been writing during riding,
        /// so reading the orphan's Position would feed a stale anchor into lagCorrection and
        /// push the saddle offset far enough to render the NPC offscreen.
        /// </summary>
        internal Vector2 ComputeRidingDrawOffset(NPC npc, ISquadMate mate)
        {
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
