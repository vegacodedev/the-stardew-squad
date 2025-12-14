using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Abstractions.UI;
using TheStardewSquad.Framework.Behaviors;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Wrappers;

namespace TheStardewSquad.Framework.Squad
{
    public class SquadMateFactory
    {
        // NPC Behaviors
        private readonly ITaskBehavior _npcTaskBehavior;
        private readonly ICommunicationBehavior _npcCommunicationBehavior;
        private readonly IInteractionBehavior _npcInteractionBehavior;

        // Pet Behaviors
        private readonly ITaskBehavior _petTaskBehavior;
        private readonly ICommunicationBehavior _petCommunicationBehavior;
        private readonly IInteractionBehavior _petInteractionBehavior;

        public SquadMateFactory(IModHelper helper, RecruitmentManager recruitmentManager, SquadManager squadManager, ModConfig config, InteractionManager interactionManager, BehaviorManager behaviorManager, ISquadMateStateHelper stateHelper, DialogueManager dialogueManager, IMonitor monitor)
        {
            // Create UI service for interaction behaviors
            IUIService uiService = new UIServiceWrapper(helper);

            // Instantiate NPC behaviors
            this._npcTaskBehavior = new NpcTaskBehavior(behaviorManager);
            this._npcCommunicationBehavior = new NpcCommunicationBehavior(config, dialogueManager, squadManager, monitor);
            this._npcInteractionBehavior = new NpcInteractionBehavior(helper, monitor, recruitmentManager, squadManager, interactionManager, behaviorManager, stateHelper, dialogueManager);

            // Instantiate Pet behaviors
            this._petTaskBehavior = new PetTaskBehavior();
            this._petCommunicationBehavior = new PetCommunicationBehavior(config);
            this._petInteractionBehavior = new PetInteractionBehavior(helper, recruitmentManager, squadManager, interactionManager, stateHelper, uiService);
        }

        /// <summary>Creates a new squad mate instance for a given character.</summary>
        public ISquadMate Create(NPC npc)
        {
            if (npc is Pet)
            {
                // Create a SquadMate with pet-specific behaviors
                return new SquadMate(npc, _petTaskBehavior, _petInteractionBehavior, _petCommunicationBehavior);
            }
            
            // Default to creating a SquadMate with standard NPC behaviors
            return new SquadMate(npc, _npcTaskBehavior, _npcInteractionBehavior, _npcCommunicationBehavior);
        }
    }
}
