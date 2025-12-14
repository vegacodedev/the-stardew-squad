using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Pathfinding;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.UI;

namespace TheStardewSquad.Framework.Behaviors
{
    internal class NpcInteractionBehavior : IInteractionBehavior
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly RecruitmentManager _recruitmentManager;
        private readonly SquadManager _squadManager;
        private readonly InteractionManager _interactionManager;
        private readonly BehaviorManager _behaviorManager;
        private readonly ISquadMateStateHelper _stateHelper;
        private readonly DialogueManager _dialogueManager;

        public NpcInteractionBehavior(IModHelper helper, IMonitor monitor, RecruitmentManager recruitmentManager, SquadManager squadManager, InteractionManager interactionManager, BehaviorManager behaviorManager, ISquadMateStateHelper stateHelper, DialogueManager dialogueManager)
        {
            this._helper = helper;
            this._monitor = monitor;
            this._recruitmentManager = recruitmentManager;
            this._squadManager = squadManager;
            this._interactionManager = interactionManager;
            this._behaviorManager = behaviorManager;
            this._stateHelper = stateHelper;
            this._dialogueManager = dialogueManager;
        }

        public void HandleRecruitment(ISquadMate mate, Farmer player)
        {
            var npc = mate.Npc;

            var menu = new SquadMemberMenu(_helper, mate, false, (action) =>
            {
                if (action == "recruit")
                {
                    // Check recruitment condition using BehaviorManager
                    if (!this._behaviorManager.CanRecruit(npc))
                    {
                        // Get custom refusal dialogue key or use default
                        string refusalKey = this._behaviorManager.GetRecruitmentRefusalDialogueKey(npc);
                        string dialogue = !string.IsNullOrEmpty(refusalKey)
                            ? refusalKey
                            : _dialogueManager.GetDialogue(npc, DialogueKeys.RecruitmentRefusal);

                        Game1.DrawDialogue(new StardewValley.Dialogue(npc, null, dialogue));
                        return;
                    }

                    // Check friendship requirement
                    ModConfig config = _helper.ReadConfig<ModConfig>();
                    if (npc.isMarried() || player.getFriendshipHeartLevelForNPC(npc.Name) >= config.FriendshipRequirement)
                    {
                        this._recruitmentManager.Recruit(mate);
                    }
                    else
                    {
                        mate.Communicate(DialogueKeys.FriendshipTooLow);
                    }
                }
            });

            Game1.activeClickableMenu = menu;
        }

        public void HandleManagement(ISquadMate mate)
        {
            var npc = mate.Npc;

            var menu = new SquadMemberMenu(_helper, mate, true, (action) =>
            {
                if (action == "inventory")
                {
                    this._interactionManager.ShowSquadInventory();
                }
                else if (action == "wait")
                {
                    // Set the squad member to waiting state
                    this._recruitmentManager.SetWaiting(mate);
                }
                else if (action == "dismiss")
                {
                    // Immediately dismiss this squad member
                    this._recruitmentManager.Dismiss(mate);
                }
                else if (action == "dismissAll")
                {
                    // Immediately dismiss all squad members
                    this._recruitmentManager.DismissAll(useFade: true);
                }
            });

            Game1.activeClickableMenu = menu;
        }

        public void HandleDismissal(ISquadMate mate, bool isSilent, DismissalWarpBehavior warpBehavior)
        {
            var npc = mate.Npc;
            
            Game1.afterFadeFunction onDismiss = () =>
            {
                _stateHelper.PrepareForDismissal(npc);
                this._squadManager.Remove(npc);

                SchedulePathDescription scheduleEntry = _recruitmentManager.GetCurrentScheduleEntryFor(npc);

                if (scheduleEntry != null)
                {
                    WarpToScheduleEntry(npc, scheduleEntry);
                }
                else if (npc.isMarried())
                {
                    var (targetMap, targetTile) = _recruitmentManager.GetSpouseDismissalTarget(npc);
                    Game1.warpCharacter(npc, targetMap, targetTile);
                }
                else
                {
                    var (defaultMap, defaultTile) = _recruitmentManager.GetTargetLocationForNow(npc);
                    Game1.warpCharacter(npc, defaultMap, defaultTile);
                }

                if (!isSilent)
                {
                    mate.Communicate(DialogueKeys.Dismiss);
                    Game1.globalFadeToClear();
                }
            };

            if (isSilent)
            {
                onDismiss();
            }
            else
            {
                Game1.globalFadeToBlack(onDismiss);
            }
        }

        private void WarpToScheduleEntry(NPC npc, SchedulePathDescription scheduleEntry)
        {
            Game1.warpCharacter(npc, scheduleEntry.targetLocationName, scheduleEntry.targetTile);
            npc.faceDirection(scheduleEntry.facingDirection);
            npc.endOfRouteMessage.Value = scheduleEntry.endOfRouteMessage;

            if (!string.IsNullOrEmpty(scheduleEntry.endOfRouteBehavior))
            {
                this._helper.Reflection.GetMethod(npc, "startRouteBehavior").Invoke(scheduleEntry.endOfRouteBehavior);

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
        }
    }
}
