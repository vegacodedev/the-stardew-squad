using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Pathfinding;
using TheStardewSquad.Framework.Behaviors;
using System.Linq;
using StardewValley.Characters;
using TheStardewSquad.Framework.Multiplayer;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.Wrappers;

namespace TheStardewSquad.Framework
{
    public class RecruitmentManager
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly ModConfig _config;
        private readonly SquadManager _squadManager;
        private readonly WaitingNpcsManager _waitingNpcsManager;
        private readonly FormationManager _formationManager;
        private readonly SquadMateStateHelper _stateHelper;
        private FollowerManager _followerManager;
        private MessageDispatcher? _dispatcher;

        public RecruitmentManager(IModHelper helper, IMonitor monitor, ModConfig config, SquadManager squadManager, WaitingNpcsManager waitingNpcsManager, FormationManager formationManager, SquadMateStateHelper stateHelper)
        {
            this._helper = helper;
            this._monitor = monitor;
            this._config = config;
            this._squadManager = squadManager;
            this._waitingNpcsManager = waitingNpcsManager;
            this._formationManager = formationManager;
            this._stateHelper = stateHelper;
        }

        public void SetFollowerManager(FollowerManager followerManager)
        {
            this._followerManager = followerManager;
        }

        public void AttachDispatcher(MessageDispatcher dispatcher)
        {
            this._dispatcher = dispatcher;
        }

        public void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            this.DismissAll(useFade: false);
            this.DismissAllWaiting();
        }

        /// <summary>Dismisses all waiting NPCs (called at day end).</summary>
        public void DismissAllWaiting()
        {
            if (Context.IsMultiplayer && !Context.IsMainPlayer) return;
            if (this._waitingNpcsManager.Count == 0)
                return;

            // Iterate a snapshot to avoid modifying the collection during enumeration
            foreach (var mate in this._waitingNpcsManager.WaitingMembers.ToList())
            {
                mate.HandleDismissal(isSilent: true, DismissalWarpBehavior.GoHome);
                this._waitingNpcsManager.Remove(mate.Npc);
            }

            this._monitor.Log("All waiting NPCs have been dismissed at day end.", LogLevel.Info);

            // Single snapshot at the end so farmhands' local waiting state clears too.
            this._dispatcher?.BroadcastSnapshot();
        }

        /// <summary>Dismisses all squad mates. <paramref name="requesterId"/> filters by recruiter
        /// (UI button path); null means global (day-end cleanup).</summary>
        public void DismissAll(bool useFade = true, long? requesterId = null, DismissalWarpBehavior npcWarp = DismissalWarpBehavior.GoHome, DismissalWarpBehavior petWarp = DismissalWarpBehavior.GoHome)
        {
            // Farmhand forwards to host; show the fade locally first so the dismissing screen
            // sees feedback while the host processes the request.
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                long localId = Game1.player.UniqueMultiplayerID;
                if (!this._squadManager.Members.Any(m => m.RecruiterUniqueId == localId))
                    return;

                if (useFade)
                {
                    Game1.globalFadeToBlack(() =>
                    {
                        Game1.globalFadeToClear();
                        Game1.showGlobalMessage(this._helper.Translation.Get("recruitment.dismiss.allDone"));
                    });
                }
                this._dispatcher?.SendDismissAllRequest();
                return;
            }

            void perform()
            {
                // Iterate a snapshot to avoid modifying the collection during enumeration.
                // Suppress per-mate broadcasts; we'll send one snapshot at the end of the loop.
                var members = requesterId.HasValue
                    ? this._squadManager.Members.Where(m => m.RecruiterUniqueId == requesterId.Value).ToList()
                    : this._squadManager.Members.ToList();

                if (members.Count == 0)
                    return;

                foreach (var mate in members)
                {
                    var warp = (mate.Npc is Pet) ? petWarp : npcWarp;
                    this.Dismiss(mate, isSilent: true, warpBehavior: warp, broadcast: false, requesterId: requesterId);
                }

                // Single snapshot covering every dismissal in this batch.
                this._dispatcher?.BroadcastSnapshot();
            }

            if (useFade)
            {
                Game1.globalFadeToBlack(() =>
                {
                    perform();
                    Game1.globalFadeToClear();
                    Game1.showGlobalMessage(this._helper.Translation.Get("recruitment.dismiss.allDone"));
                });
            }
            else
            {
                perform();
            }
        }

        /// <summary>Finalizes the recruitment of a squad mate by setting their initial state and adding them to the manager.</summary>
        /// <param name="mate">The squad mate to recruit.</param>
        /// <param name="recruiter">The farmer doing the recruiting; their <see cref="Farmer.UniqueMultiplayerID"/> is stamped onto the NPC's modData so the link survives save/reload and syncs across peers.</param>
        /// <param name="isSilent">If true, suppresses the recruit dialogue and the "resumed from waiting"
        /// global message. Used by R2.8 auto-unpark when a recruiter reconnects.</param>
        public void Recruit(ISquadMate mate, Farmer recruiter, bool isSilent = false)
        {
            // MP routing: farmhand forwards to the host for authoritative processing.
            // Host's broadcast SquadSnapshot will sync local state, and the recruit
            // dialogue plays on the farmhand's screen when the host's RecruitResult
            // arrives with success (see MessageDispatcher.HandleRecruitResult). The
            // farmhand-local cap pre-check below ensures the squadFull error fires
            // immediately on the farmhand's screen and that the farmhand's local
            // MaxSquadSize is what governs their cap (passed in the request).
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                long localId = Game1.player.UniqueMultiplayerID;
                int localCount = this._squadManager.Members.Count(m => m.RecruiterUniqueId == localId);
                if (localCount >= this._config.MaxSquadSize && !this._waitingNpcsManager.IsWaiting(mate.Npc))
                {
                    if (!isSilent)
                    {
                        Game1.addHUDMessage(new HUDMessage(
                            this._helper.Translation.Get("recruitment.squadFull"),
                            HUDMessage.error_type));
                    }
                    return;
                }

                this._dispatcher?.SendRecruitRequest(
                    mate.Npc.Name,
                    mate.Npc.currentLocation?.NameOrUniqueName ?? string.Empty,
                    this._config.MaxSquadSize);
                return;
            }

            long recruiterId = recruiter?.UniqueMultiplayerID ?? Game1.MasterPlayer?.UniqueMultiplayerID ?? 0L;

            // Per-recruiter cap check using the LOCAL config (host-self recruit path only).
            // Farmhand recruits route via MessageDispatcher.OnRecruitRequest, which uses the
            // farmhand's MaxSquadSize from the message (per-farmer caps).
            int recruiterCount = this._squadManager.Members.Count(m => m.RecruiterUniqueId == recruiterId);
            if (recruiterCount >= this._config.MaxSquadSize)
            {
                Game1.addHUDMessage(new HUDMessage(
                    this._helper.Translation.Get("recruitment.squadFull"),
                    HUDMessage.error_type));
                return;
            }

            if (this._squadManager.IsRecruited(mate.Npc))
                return;

            // Stamp recruiter onto the NPC's modData. Auto-syncs to peers and persists in save.
            mate.Npc.modData[SquadMate.RecruiterIdKey] = recruiterId.ToString();
            mate.Npc.modData[SquadMate.SchemaVersionKey] = SquadMate.CurrentSchemaVersion;

            // Check if NPC is currently waiting - if so, resume them from waiting state
            // Use the existing ISquadMate from waiting list to preserve state (e.g., IsInPool)
            if (this._waitingNpcsManager.IsWaiting(mate.Npc))
            {
                // Get the existing mate from waiting list to preserve state
                var waitingMate = this._waitingNpcsManager.GetWaitingMember(mate.Npc);
                if (waitingMate != null)
                {
                    mate = waitingMate; // Use the existing instance with preserved state
                    // Re-stamp recruiter on the resumed mate's NPC (in case it differs from waiting-time recruiter)
                    mate.Npc.modData[SquadMate.RecruiterIdKey] = recruiterId.ToString();
                    mate.Npc.modData[SquadMate.SchemaVersionKey] = SquadMate.CurrentSchemaVersion;
                }

                // Clear waiting task
                mate.Task = null;

                // Clear waiting facing direction
                mate.WaitingFacingDirection = null;

                // Remove from waiting list
                this._waitingNpcsManager.Remove(mate.Npc);

                // Show resume message instead of recruit message
                if (!isSilent)
                    Game1.showGlobalMessage(this._helper.Translation.Get("management.resumedFromWaiting", new { name = mate.Name }));
            }
            else if (!isSilent && recruiter?.UniqueMultiplayerID == Game1.player.UniqueMultiplayerID)
            {
                mate.Communicate(DialogueKeys.Recruit);
            }

            _stateHelper.PrepareForRecruitment(mate.Npc);

            this._formationManager.AssignSlot(mate);
            this._squadManager.Add(mate);

            this._monitor.Log($"{mate.Name} has been added to the team.", LogLevel.Info);

            // Sync membership change to all peers (no-op in SP).
            this._dispatcher?.BroadcastSnapshot();
        }

        /// <summary>Initiates the dismissal process for a squad mate.</summary>
        /// <param name="broadcast">If true (default), syncs the membership change to peers via
        /// <see cref="MessageDispatcher.BroadcastSnapshot"/>. Bulk operations like
        /// <see cref="DismissAll"/> pass false and broadcast once at the end to avoid N
        /// snapshots in a row.</param>
        public virtual void Dismiss(ISquadMate mate, bool isSilent = false, DismissalWarpBehavior warpBehavior = DismissalWarpBehavior.GoHome, bool broadcast = true, long? requesterId = null)
        {
            // MP routing: farmhand forwards to the host. Host's broadcast SquadSnapshot
            // syncs local state. Fire optimistic visual locally so the dismissing farmhand
            // sees the fade + dialogue on their own screen (mirrors the recruit-side pattern);
            // the host suppresses the visual when proxying via the suppressVisual flag below.
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                if (!isSilent)
                {
                    bool isPet = mate.Npc is StardewValley.Characters.Pet;
                    Game1.globalFadeToBlack(() =>
                    {
                        // Pet dismiss HUD: fire optimistically on the farmhand's screen.
                        // The host's PetInteractionBehavior.HandleDismissal suppresses its
                        // own local showGlobalMessage when proxying (suppressVisual=true)
                        // because Game1.showGlobalMessage isn't peer-propagated.
                        if (isPet)
                            Game1.showGlobalMessage(this._helper.Translation.Get("recruitment.petDismissed", new { name = mate.Name }));
                        mate.Communicate(DialogueKeys.Dismiss);
                        Game1.globalFadeToClear();
                    });
                }
                this._dispatcher?.SendDismissRequest(mate.Npc.Name);
                return;
            }

            if (!this._squadManager.IsRecruited(mate.Npc))
                return;

            this._formationManager.ReleaseSlot(mate);

            // When the host is proxying a farmhand's DismissRequest, run cleanup synchronously
            // without the fade/dialogue (those play on the farmhand's screen optimistically).
            bool suppressVisual = requesterId.HasValue && requesterId.Value != Game1.player.UniqueMultiplayerID;
            mate.HandleDismissal(isSilent, warpBehavior, suppressVisual);

            if (isSilent)
            {
                this._monitor.Log($"{mate.Name} has been removed from the team (silently).", LogLevel.Info);
            }
            else
            {
                this._monitor.Log($"{mate.Name} has been removed from the team.", LogLevel.Info);
            }

            // Sync membership change to all peers (no-op in SP). Skipped during bulk ops.
            if (broadcast)
                this._dispatcher?.BroadcastSnapshot();
        }

        /// <summary>Sets a squad mate to waiting state - removes from squad but keeps control at current location.</summary>
        /// <param name="isSilent">If true, suppresses the global "X is now waiting" message and the
        /// pet roam-here notification. Used by auto-park when a recruiter goes offline,
        /// and by <see cref="MessageDispatcher.OnWaitRequest"/> when the host is proxying a
        /// farmhand's WaitRequest (the HUD is shown on the farmhand via WaitResult instead).</param>
        public void SetWaiting(ISquadMate mate, bool isSilent = false)
        {
            // MP routing: farmhand forwards to the host. Mirrors the Recruit/Dismiss
            // pattern. The host's broadcast SquadSnapshot syncs squad/waiting membership;
            // the farmhand's HandleWaitResult shows the HUD message.
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                this._dispatcher?.SendWaitRequest(mate.Npc.Name);
                return;
            }
            if (!this._squadManager.IsRecruited(mate.Npc))
                return;

            // For pets, use RoamHere dismissal behavior instead of adding to waiting list
            if (mate.Npc is Pet)
            {
                this._formationManager.ReleaseSlot(mate);
                mate.HandleDismissal(isSilent, DismissalWarpBehavior.RoamHere);
                if (!isSilent)
                    this._monitor.Log($"{mate.Name} is now roaming here.", LogLevel.Info);
                return;
            }

            // For NPCs: Clear any existing tasks and state first
            this._followerManager.ClearMateTaskAndReset(mate);

            // Store the NPC's current facing direction so they don't turn toward the player
            mate.WaitingFacingDirection = mate.Npc.FacingDirection;

            // Release formation slot
            this._formationManager.ReleaseSlot(mate);

            // Remove from squad and add to waiting list
            this._squadManager.Remove(mate.Npc);
            this._waitingNpcsManager.Add(mate);

            if (!isSilent)
            {
                Game1.showGlobalMessage(this._helper.Translation.Get("management.waiting", new { name = mate.Name }));
                this._monitor.Log($"{mate.Name} is now waiting.", LogLevel.Info);
            }

            // Sync membership change to all peers (no-op in SP).
            this._dispatcher?.BroadcastSnapshot();
        }

        public (string, Point) GetSpouseDismissalTarget(NPC npc)
        {
            // Each player has their own farmhouse/cabin; resolve via the spouse-farmer.
            Farmer spouseFarmer = npc.getSpouse();
            FarmHouse spouseHome = spouseFarmer != null
                ? Utility.getHomeOfFarmer(spouseFarmer)
                : null;

            if (spouseHome == null)
            {
                return GetTargetLocationForNow(npc);
            }

            string homeName = spouseFarmer.homeLocation.Value;

            if (Game1.timeOfDay >= 2200)
            {
                Point bedSpot = spouseHome.getSpouseBedSpot(npc.Name);
                if (bedSpot.X != -1000)
                {
                    npc.playSleepingAnimation();
                    return (homeName, bedSpot);
                }
            }

            Point kitchenSpot = spouseHome.getKitchenStandingSpot();
            if (spouseHome.isTileOnMap(kitchenSpot.X, kitchenSpot.Y))
            {
                return (homeName, kitchenSpot);
            }

            return GetTargetLocationForNow(npc);
        }
        
        public (string, Point) GetTargetLocationForNow(NPC npc)
        {
            SchedulePathDescription scheduleEntry = GetCurrentScheduleEntryFor(npc);

            if (scheduleEntry != null)
                return (scheduleEntry.targetLocationName, scheduleEntry.targetTile);

            return (npc.DefaultMap, (npc.DefaultPosition / 64).ToPoint());
        }

        public SchedulePathDescription GetCurrentScheduleEntryFor(NPC npc)
        {
            var schedule = npc.Schedule;
            if (schedule == null)
            {
                // Vanilla NPC.checkSchedule (the per-tick schedule loader) is host-only, so
                // farmhand NPC instances often have a null Schedule mid-day. TryLoadSchedule
                // is public and parses the schedule data (only the dayScheduleName.Value
                // netfield writes inside it are IsMasterGame-gated, which is fine - those
                // are syncable from host anyway). Force-load so dismissal-time animation
                // replay can find the current entry on every peer.
                npc.TryLoadSchedule();
                schedule = npc.Schedule;
                if (schedule == null)
                    return null;
            }

            SchedulePathDescription lastValidEntry = null;
            int lastValidTime = -1;

            foreach (var entry in schedule)
            {
                if (entry.Key <= Game1.timeOfDay)
                {
                    if (entry.Key > lastValidTime)
                    {
                        lastValidTime = entry.Key;
                        lastValidEntry = entry.Value;
                    }
                }
            }

            return lastValidEntry;
        }

        /// <summary>
        /// Plays the end-of-route schedule animation/behavior for an NPC at a known schedule
        /// entry. Sets <c>endOfRouteMessage</c> and runs vanilla's <c>startRouteBehavior</c>
        /// + <c>reallyDoAnimationAtEndOfScheduleRoute</c> via reflection. Used by both the
        /// host's dismissal flow (after warping the NPC to her schedule destination) and
        /// the farmhand's <see cref="MessageDispatcher.ApplySnapshot"/> dismiss branch
        /// (Sprite state isn't netfielded, so each peer must replay the animation locally
        /// on its own NPC instances). Caller is responsible for the warp itself.
        /// </summary>
        public void PlayScheduleEntryAnimation(NPC npc, SchedulePathDescription scheduleEntry)
        {
            if (scheduleEntry == null) return;

            // endOfRouteMessage is netfielded; host-side writes propagate to peers. Strip
            // surrounding quotes because vanilla's message-only schedule parse (NPC.cs:5677)
            // doesn't, and _PushTemporaryDialogue treats the value as a content asset path.
            if (!string.IsNullOrEmpty(scheduleEntry.endOfRouteMessage))
                npc.endOfRouteMessage.Value = scheduleEntry.endOfRouteMessage.Replace("\"", "");

            string behavior = scheduleEntry.endOfRouteBehavior;
            if (string.IsNullOrEmpty(behavior))
                return;

            // SQUARE WALKING - e.g. Elliott's "square_4_5" pacing by the sea.
            // Vanilla NPC.startRouteBehavior parses "square_W_H[_facingPref]" and calls
            // walkInSquare(...), but only when Game1.IsMasterGame == true (vanilla NPC.cs
            // line 4491). On a farmhand the gate fails silently, isWalkingInSquare stays
            // false, and the NPC just stands still. Replicate the parse + walkInSquare here
            // because walkInSquare itself isn't IsMasterGame-gated.
            if (behavior.Contains("square_"))
            {
                string[] parts = behavior.Split('_');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int sw) && int.TryParse(parts[2], out int sh))
                {
                    npc.walkInSquare(sw, sh, 6000); // 6000 = vanilla's hardcoded squarePauseOffset (NPC.cs:4493)
                    npc.squareMovementFacingPreference = (parts.Length >= 4 && int.TryParse(parts[3], out int fp)) ? fp : -1;
                }
                return; // Square-walking behaviors don't pair with an end-of-route animation.
            }

            // ANIMATION BEHAVIORS (manning_shop, abigail_videogames, sleep, etc.). Vanilla's
            // per-peer animation chain (NPC.update at vanilla NPC.cs line 3202+) is driven by
            // the endOfRouteBehaviorName NetString and the doingEndOfRouteAnimation NetBool;
            // when those change, each peer's update auto-loads behavior data and plays the
            // animation locally. We don't go through vanilla's path-completion handoff
            // (getRouteEndBehaviorFunction sets the netfield + calls loadEndOfRouteBehavior),
            // so we set up the missing pieces ourselves:
            //   1. Set endOfRouteBehaviorName.Value on host so the netfield change syncs to
            //      farmhands (drives vanilla auto-detection on the netcoll-bound instance).
            //   2. Call loadEndOfRouteBehavior on EVERY peer so loadedEndOfRouteBehavior is
            //      populated before reallyDoAnimationAtEndOfScheduleRoute reads it at line 4399.
            //   3. Call startRouteBehavior + reallyDoAnimation on EVERY peer so the animation
            //      also plays on local-spawn duplicates that aren't netcoll-bound (per
            //      pattern-mp-npc-duplication.md). Vanilla's auto-trigger may also fire on the
            //      netcoll-bound instance — idempotent since it gates on currentlyDoingEnd*.
            if (Context.IsMainPlayer)
                npc.endOfRouteBehaviorName.Value = behavior;

            this._helper.Reflection.GetMethod(npc, "loadEndOfRouteBehavior").Invoke(behavior);
            this._helper.Reflection.GetMethod(npc, "startRouteBehavior").Invoke(behavior);

            try
            {
                this._helper.Reflection.GetMethod(npc, "reallyDoAnimationAtEndOfScheduleRoute").Invoke();
            }
            catch (System.Exception ex)
            {
                // Animation method expects pathfinding state that doesn't exist after teleport
                // This is safe to ignore - startRouteBehavior already set up the behavior
                this._monitor.Log($"Ignored animation error for {npc.Name} at dismissal (expected after teleport): {ex.Message}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Convenience wrapper that resolves the current schedule entry and plays its
        /// end-of-route animation in one call. Used by farmhand-side replay where the
        /// caller doesn't already have the entry in hand.
        /// </summary>
        public void PlayCurrentScheduleAnimation(NPC npc)
        {
            var entry = GetCurrentScheduleEntryFor(npc);
            if (entry != null)
                PlayScheduleEntryAnimation(npc, entry);
        }
    }
}
