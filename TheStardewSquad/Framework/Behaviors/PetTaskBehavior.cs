using StardewValley.Monsters;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Behaviors
{
    internal class PetTaskBehavior : ITaskBehavior
    {
        public bool CanPerformTask(ISquadMate mate, TaskType type)
        {
            // Pets can attack and sit.
            return type == TaskType.Attacking || type == TaskType.Sitting;
        }

        public bool ExecuteTask(ISquadMate mate)
        {
            switch (mate.Task.Type)
            {
                case TaskType.Attacking:
                    if (mate.Task.TargetCharacter is Monster m && m.Health > 0)
                    {
                        return TaskManager.ExecutePetAttackingTask(mate, m);
                    }
                    return true;
                case TaskType.Sitting:
                    return TaskManager.ExecuteSittingTask(mate, mate.Task.Tile);
                default:
                    // Unknown task type - mark as complete
                    return true;
            }
        }
    }
}