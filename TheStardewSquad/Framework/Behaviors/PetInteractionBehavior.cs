using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Character;
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
        private readonly SquadMemberPrompt _memberPrompt;

        public PetInteractionBehavior(IModHelper helper, RecruitmentManager recruitmentManager, SquadManager squadManager, InteractionManager interactionManager, ISquadMateStateHelper stateHelper, SquadMemberPrompt memberPrompt)
        {
            this._helper = helper;
            this._recruitmentManager = recruitmentManager;
            this._squadManager = squadManager;
            this._interactionManager = interactionManager;
            this._stateHelper = stateHelper;
            this._memberPrompt = memberPrompt;
        }

        public void HandleRecruitment(ISquadMate mate, Farmer player)
        {
            var npc = mate.Npc;

            _memberPrompt.PromptForRecruitment(mate, player, () =>
            {
                this._recruitmentManager.Recruit(mate, player);
                var message = _helper.Translation.Get("recruitment.petRecruited", new { name = npc.Name });
                Game1.showGlobalMessage(message);
            });
        }

        public void HandleManagement(ISquadMate mate)
        {
            _memberPrompt.PromptForManagement(mate, action =>
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
        }

        public void HandleDismissal(ISquadMate mate, bool isSilent, DismissalWarpBehavior warpBehavior, bool suppressVisual = false)
        {
            var npc = mate.Npc;

            // Remove from SquadManager synchronously so RecruitmentManager.Dismiss's subsequent
            // BroadcastSnapshot captures the post-remove state, and so the warpToFarmHouse
            // Harmony block (which gates on recruited status) doesn't reject the warp inside
            // the fade callback below.
            this._squadManager.Remove(npc);

            Game1.afterFadeFunction onDismiss = () =>
            {
                _stateHelper.PrepareForDismissal(npc);

                if (npc is Pet pet)
                {
                    if (warpBehavior == DismissalWarpBehavior.GoHome)
                    {
                        bool sendToHouse = Game1.isRaining || Game1.isLightning || Game1.timeOfDay >= 2000;
                        if (sendToHouse)
                        {
                            // Route to the pet's actual owner (resolved via Pet.homeLocationName)
                            // so a farmhand-owned pet warps to their cabin, not the local screen's
                            // farmhouse.
                            pet.warpToFarmHouse(PetOwnerResolver.ResolveOwner(pet));
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

            if (isSilent || suppressVisual || warpBehavior == DismissalWarpBehavior.RoamHere)
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
