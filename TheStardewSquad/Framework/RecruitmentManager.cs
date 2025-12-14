using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Pathfinding;
using TheStardewSquad.Framework.Behaviors;
using System.Linq;
using StardewValley.Characters;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Abstractions.Character;

namespace TheStardewSquad.Framework
{
    public class RecruitmentManager
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly SquadManager _squadManager;
        private readonly WaitingNpcsManager _waitingNpcsManager;
        private readonly FormationManager _formationManager;
        private readonly ISquadMateStateHelper _stateHelper;
        private FollowerManager _followerManager;

        public RecruitmentManager(IModHelper helper, IMonitor monitor, SquadManager squadManager, WaitingNpcsManager waitingNpcsManager, FormationManager formationManager, ISquadMateStateHelper stateHelper)
        {
            this._helper = helper;
            this._monitor = monitor;
            this._squadManager = squadManager;
            this._waitingNpcsManager = waitingNpcsManager;
            this._formationManager = formationManager;
            this._stateHelper = stateHelper;
        }

        public void SetFollowerManager(FollowerManager followerManager)
        {
            this._followerManager = followerManager;
        }

        public void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            this.DismissAll(useFade: false);
            this.DismissAllWaiting();
        }

        /// <summary>Dismisses all waiting NPCs (called at day end).</summary>
        public void DismissAllWaiting()
        {
            if (this._waitingNpcsManager.Count == 0)
                return;

            // Iterate a snapshot to avoid modifying the collection during enumeration
            foreach (var mate in this._waitingNpcsManager.WaitingMembers.ToList())
            {
                mate.HandleDismissal(isSilent: true, DismissalWarpBehavior.GoHome);
                this._waitingNpcsManager.Remove(mate.Npc);
            }

            this._monitor.Log("All waiting NPCs have been dismissed at day end.", LogLevel.Info);
        }

        /// <summary>Dismisses all squad mates.</summary>
        public void DismissAll(bool useFade = true, DismissalWarpBehavior npcWarp = DismissalWarpBehavior.GoHome, DismissalWarpBehavior petWarp = DismissalWarpBehavior.GoHome)
        {
            if (this._squadManager.Count == 0)
                return;

            void perform()
            {
                // Iterate a snapshot to avoid modifying the collection during enumeration.
                foreach (var mate in this._squadManager.Members.ToList())
                {
                    var warp = (mate.Npc is Pet) ? petWarp : npcWarp;
                    this.Dismiss(mate, isSilent: true, warpBehavior: warp);
                }
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
        public void Recruit(ISquadMate mate)
        {
            var config = this._helper.ReadConfig<ModConfig>();
            if (this._squadManager.Count >= config.MaxSquadSize)
            {
                return;
            }

            if (this._squadManager.IsRecruited(mate.Npc))
                return;

            // Check if NPC is currently waiting - if so, resume them from waiting state
            // Use the existing ISquadMate from waiting list to preserve state (e.g., IsInPool)
            if (this._waitingNpcsManager.IsWaiting(mate.Npc))
            {
                // Get the existing mate from waiting list to preserve state
                var waitingMate = this._waitingNpcsManager.GetWaitingMember(mate.Npc);
                if (waitingMate != null)
                {
                    mate = waitingMate; // Use the existing instance with preserved state
                }

                // Clear waiting task
                mate.Task = null;

                // Clear waiting facing direction
                mate.WaitingFacingDirection = null;

                // Remove from waiting list
                this._waitingNpcsManager.Remove(mate.Npc);

                // Show resume message instead of recruit message
                Game1.showGlobalMessage(this._helper.Translation.Get("management.resumedFromWaiting", new { name = mate.Name }));
            }
            else
            {
                mate.Communicate(DialogueKeys.Recruit);
            }

            _stateHelper.PrepareForRecruitment(mate.Npc);

            this._formationManager.AssignSlot(mate);
            this._squadManager.Add(mate);

            this._monitor.Log($"{mate.Name} has been added to the team.", LogLevel.Info);
        }

        /// <summary>Initiates the dismissal process for a squad mate.</summary>
        public virtual void Dismiss(ISquadMate mate, bool isSilent = false, DismissalWarpBehavior warpBehavior = DismissalWarpBehavior.GoHome)
        {
            if (!this._squadManager.IsRecruited(mate.Npc))
                return;

            this._formationManager.ReleaseSlot(mate);

            mate.HandleDismissal(isSilent, warpBehavior);

            if (isSilent)
            {
                this._monitor.Log($"{mate.Name} has been removed from the team (silently).", LogLevel.Info);
            }
            else
            {
                this._monitor.Log($"{mate.Name} has been removed from the team.", LogLevel.Info);
            }
        }

        /// <summary>Sets a squad mate to waiting state - removes from squad but keeps control at current location.</summary>
        public void SetWaiting(ISquadMate mate)
        {
            if (!this._squadManager.IsRecruited(mate.Npc))
                return;

            // For pets, use RoamHere dismissal behavior instead of adding to waiting list
            if (mate.Npc is Pet)
            {
                this._formationManager.ReleaseSlot(mate);
                mate.HandleDismissal(isSilent: false, DismissalWarpBehavior.RoamHere);
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

            // Show message to player
            Game1.showGlobalMessage(this._helper.Translation.Get("management.waiting", new { name = mate.Name }));
            this._monitor.Log($"{mate.Name} is now waiting.", LogLevel.Info);
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
