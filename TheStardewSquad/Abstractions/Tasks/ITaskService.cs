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
        /// <summary>Checks if the given farmer is currently fishing.</summary>
        bool IsFarmerFishing(Farmer who);

        /// <summary>Checks if the given farmer is currently sitting on furniture or a bench.</summary>
        bool IsFarmerSitting(Farmer who);

        /// <summary>Animates an NPC fishing at a specific tile.</summary>
        void AnimateFishing(NPC npc, Point targetTile);

        /// <summary>Makes an NPC face a specific position.</summary>
        void FacePosition(NPC npc, Vector2 position);

        /// <summary>Attempts to make an NPC catch a fish.</summary>
        void TryNpcCatchFish(ISquadMate mate, int squadSize);
    }
}
