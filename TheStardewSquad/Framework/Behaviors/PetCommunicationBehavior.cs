using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Behaviors
{
    internal class PetCommunicationBehavior : ICommunicationBehavior
    {
        private readonly ModConfig _config;

        public PetCommunicationBehavior(ModConfig config)
        {
            this._config = config;
        }

        public void Communicate(ISquadMate mate, string dialogueKey)
        {
            // If ambient communication is disabled, do nothing.
            if (!_config.EnableCommunication)
                return;

            string? sound = null;
            var pet = mate.Npc as Pet;

            if (pet is null) return;

            if (pet.petType.ToString() == "Cat")
            {
                sound = "cat";
            }
            else if (pet.petType.ToString() == "Dog")
            {
                sound = "dog_bark";
            }

            if (!Game1.options.muteAnimalSounds && sound != null)
            {
                if (dialogueKey == TaskType.Attacking.ToString())
                {
                    mate.Npc.currentLocation.playSound(sound, mate.Npc.Tile);
                }
            }
        }
    }
}
