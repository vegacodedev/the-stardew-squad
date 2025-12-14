using System.Linq;
using StardewValley;
using StardewValley.Monsters;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Behaviors
{
    internal class NpcTaskBehavior : ITaskBehavior
    {
        private readonly BehaviorManager _behaviorManager;

        public NpcTaskBehavior(BehaviorManager behaviorManager)
        {
            this._behaviorManager = behaviorManager;
        }

        public bool CanPerformTask(ISquadMate mate, TaskType type)
        {
            var allowedTasksString = this._behaviorManager.GetAllowedTasks(mate.Npc);
            var allowedTasks = allowedTasksString.Split(',').Select(t => t.Trim()).ToList();
            return allowedTasks.Contains(type.ToString());
        }

        public bool ExecuteTask(ISquadMate mate)
        {
            switch (mate.Task.Type)
            {
                case TaskType.Attacking:
                    if (mate.Task.TargetCharacter is Monster m && m.Health > 0)
                        return TaskManager.ExecuteAttackingTask(mate, m);
                    return true;
                case TaskType.Harvesting:
                    return TaskManager.ExecuteHarvestingTask(mate, mate.Task.Tile);
                case TaskType.Lumbering:
                    return TaskManager.ExecuteLumberingTask(mate, mate.Task.Tile);
                case TaskType.Watering:
                    return TaskManager.ExecuteWateringTask(mate, mate.Task.Tile);
                case TaskType.Foraging:
                    return TaskManager.ExecuteForagingTask(mate, mate.Task.Tile);
                case TaskType.Mining:
                    return TaskManager.ExecuteMiningTask(mate, mate.Task.Tile);
                case TaskType.Fishing:
                    return TaskManager.ExecuteFishingTask(mate);
                case TaskType.Petting:
                    return TaskManager.ExecutePettingTask(mate, mate.Task.Tile);
                case TaskType.Sitting:
                    return TaskManager.ExecuteSittingTask(mate, mate.Task.Tile);
                default:
                    return true;
            }
        }
    }
}
