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
        public bool IsPlayerFishing() => TaskManager.IsPlayerFishing();

        public bool IsPlayerWatering() => TaskManager.IsPlayerWatering();

        public bool IsPlayerMining() => TaskManager.IsPlayerMining();

        public bool IsPlayerLumbering() => TaskManager.IsPlayerLumbering();

        public bool IsPlayerHarvesting() => TaskManager.IsPlayerHarvesting();

        public bool IsPlayerPetting() => TaskManager.IsPlayerPetting();

        public bool IsPlayerInCombat() => TaskManager.IsPlayerInCombat();

        public bool IsPlayerSitting() => TaskManager.IsPlayerSitting();

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
