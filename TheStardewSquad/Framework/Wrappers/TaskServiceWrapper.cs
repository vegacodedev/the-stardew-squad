using Microsoft.Xna.Framework;
using StardewValley;
using TheStardewSquad.Abstractions.Tasks;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of ITaskService that calls TaskManager static methods.
    /// </summary>
    public class TaskServiceWrapper : ITaskService
    {
        public bool IsFarmerFishing(Farmer who) => TaskManager.IsFarmerFishing(who);

        public bool IsFarmerSitting(Farmer who) => TaskManager.IsFarmerSitting(who);

        public void AnimateFishing(NPC npc, Point targetTile)
        {
            TaskManager.AnimateFishing(npc, targetTile);
        }

        public void FacePosition(NPC npc, Vector2 position)
        {
            TaskManager.FacePosition(npc, position);
        }

        public void TryNpcCatchFish(ISquadMate mate, int squadSize)
        {
            TaskManager.TryNpcCatchFish(mate, squadSize);
        }
    }
}
