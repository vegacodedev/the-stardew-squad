using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Behaviors
{
    public interface ITaskBehavior
    {
        bool CanPerformTask(ISquadMate mate, TaskType type);
        bool ExecuteTask(ISquadMate mate);
    }
}