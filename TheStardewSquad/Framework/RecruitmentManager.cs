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
using TheStardewSquad.Abstractions.Character;

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
        private readonly ISquadMateStateHelper _stateHelper;
        private FollowerManager _followerManager;
        private MessageDispatcher? _dispatcher;

        public RecruitmentManager(IModHelper helper, IMonitor monitor, ModConfig config, SquadManager squadManager, WaitingNpcsManager waitingNpcsManager, FormationManager formationManager, ISquadMateStateHelper stateHelper)
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

        /// <summary>Dismisses all squad mates.</summary>
        public void DismissAll(bool useFade = true, DismissalWarpBehavior npcWarp = DismissalWarpBehavior.GoHome, DismissalWarpBehavior petWarp = DismissalWarpBehavior.GoHome)
        {
            if (Context.IsMultiplayer && !Context.IsMainPlayer) return;
            if (this._squadManager.Count == 0)
                return;

            void perform()
            {
                // Iterate a snapshot to avoid modifying the collection during enumeration.
                // Suppress per-mate broadcasts; we'll send one snapshot at the end of the loop.
                foreach (var mate in this._squadManager.Members.ToList())
                {
                    var warp = (mate.Npc is Pet) ? petWarp : npcWarp;
                    this.Dismiss(mate, isSilent: true, warpBehavior: warp, broadcast: false);
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
                    Game1.globalFadeToBlack(() =>
                    {
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
        /// pet roam-here notification. Used by R2.8 auto-park when a recruiter goes offline.</param>
        public void SetWaiting(ISquadMate mate, bool isSilent = false)
        {
            if (Context.IsMultiplayer && !Context.IsMainPlayer) return;
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
            const string farmHouseName = "FarmHouse";
            var farmHouse = Game1.getLocationFromName(farmHouseName) as FarmHouse;

            if (farmHouse == null)
            {
                var (map, tile) = GetTargetLocationForNow(npc);
                return (map, tile);
            }

            if (Game1.timeOfDay >= 2200)
            {
                Point bedSpot = farmHouse.getSpouseBedSpot(npc.Name);
                if (bedSpot.X != -1000)
                {
                    npc.playSleepingAnimation();
                    return (farmHouseName, bedSpot);
                }
            }

            Point kitchenSpot = farmHouse.getKitchenStandingSpot();
            if (farmHouse.isTileOnMap(kitchenSpot.X, kitchenSpot.Y))
            {
                return (farmHouseName, kitchenSpot);
            }

            var (defaultMap, defaultTile) = GetTargetLocationForNow(npc);
            return (defaultMap, defaultTile);
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
                return null;

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
    }
}
