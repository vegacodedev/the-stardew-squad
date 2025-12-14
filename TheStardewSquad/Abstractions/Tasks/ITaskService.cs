using Microsoft.Xna.Framework;
using StardewValley;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Abstractions.Tasks
{
    /// <summary>
    /// Abstracts TaskManager static methods for task detection and execution.
    /// </summary>
    public interface ITaskService
    {
        /// <summary>Checks if the player is currently fishing.</summary>
        bool IsPlayerFishing();

        /// <summary>Checks if the player is currently watering crops.</summary>
        bool IsPlayerWatering();

        /// <summary>Checks if the player is currently mining.</summary>
        bool IsPlayerMining();

        /// <summary>Checks if the player is currently chopping wood.</summary>
        bool IsPlayerLumbering();

        /// <summary>Checks if the player is currently harvesting crops.</summary>
        bool IsPlayerHarvesting();

        /// <summary>Checks if the player is currently petting animals.</summary>
        bool IsPlayerPetting();

        /// <summary>Checks if the player is currently in combat.</summary>
        bool IsPlayerInCombat();

        /// <summary>Checks if the player is currently sitting on furniture or a bench.</summary>
        bool IsPlayerSitting();

        /// <summary>Animates an NPC fishing at a specific tile.</summary>
        void AnimateFishing(NPC npc, Point targetTile);

        /// <summary>Makes an NPC face a specific position.</summary>
        void FacePosition(NPC npc, Vector2 position);

        /// <summary>Attempts to make an NPC catch a fish.</summary>
        void TryNpcCatchFish(ISquadMate mate, int squadSize);
    }
}
