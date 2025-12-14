using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using System.Collections.Generic;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Abstractions.UI;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.UI;

namespace TheStardewSquad.Framework.Behaviors
{
    public class PetInteractionBehavior : IInteractionBehavior
    {
        private readonly IModHelper _helper;
        private readonly RecruitmentManager _recruitmentManager;
        private readonly SquadManager _squadManager;
        private readonly InteractionManager _interactionManager;
        private readonly ISquadMateStateHelper _stateHelper;
        private readonly IUIService _uiService;

        public PetInteractionBehavior(IModHelper helper, RecruitmentManager recruitmentManager, SquadManager squadManager, InteractionManager interactionManager, ISquadMateStateHelper stateHelper, IUIService uiService)
        {
            this._helper = helper;
            this._recruitmentManager = recruitmentManager;
            this._squadManager = squadManager;
            this._interactionManager = interactionManager;
            this._stateHelper = stateHelper;
            this._uiService = uiService;
        }

        public void HandleRecruitment(ISquadMate mate, Farmer player)
        {
            var npc = mate.Npc;

            var menu = new SquadMemberMenu(_helper, mate, false, (action) =>
            {
                if (action == "recruit")
                {
                    this._recruitmentManager.Recruit(mate);
                    var message = _helper.Translation.Get("recruitment.petRecruited", new { name = npc.Name });
                    Game1.showGlobalMessage(message);
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
                else if (action == "dismiss")
                {
                    // For pets, dismiss immediately sends them home
                    this._recruitmentManager.Dismiss(mate, isSilent: false, DismissalWarpBehavior.GoHome);
                }
                else if (action == "dismissAll")
                {
                    // Immediately dismiss all squad members
                    this._recruitmentManager.DismissAll(useFade: true, npcWarp: DismissalWarpBehavior.GoHome, petWarp: DismissalWarpBehavior.GoHome);
                }
                else if (action == "wait")
                {
                    // For pets, "wait" uses the roam here behavior (handled in RecruitmentManager.SetWaiting)
                    this._recruitmentManager.SetWaiting(mate);
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

                // Remove the pet BEFORE warping it away. This is due to the fact that we block warpToFarmHouse with Harmony as long as it is recruited.
                this._squadManager.Remove(npc);

                if (npc is Pet pet)
                {
                    if (warpBehavior == DismissalWarpBehavior.GoHome)
                    {
                        bool sendToHouse = Game1.isRaining || Game1.isLightning || Game1.timeOfDay >= 2000;
                        if (sendToHouse)
                        {
                            pet.warpToFarmHouse(Game1.player);
                        }
                        else
                        {
                            pet.WarpToPetBowl();
                        }
                    }
                    else if (warpBehavior == DismissalWarpBehavior.RoamHere)
                    {
                        pet.CurrentBehavior = Pet.behavior_Walk;
                    }
                }

                if (!isSilent)
                {
                    var message = _helper.Translation.Get("recruitment.petDismissed", new { name = mate.Name });
                    Game1.showGlobalMessage(message);
                }
            };

            if (isSilent || warpBehavior == DismissalWarpBehavior.RoamHere)
            {
                onDismiss();
            }
            else
            {
                Game1.globalFadeToBlack(onDismiss);
            }
        }
    }
}
