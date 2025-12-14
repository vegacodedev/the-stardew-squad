using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Tasks
{
    /// <summary>
    /// Centralized manager for task priority order.
    /// Ensures consistent task priorities across Autonomous and Mimicking modes.
    /// </summary>
    public static class TaskPriorityManager
    {
        /// <summary>
        /// Defines the priority order for regular tasks (lower index = higher priority).
        /// Note: Attacking is handled separately as a high-priority override.
        /// </summary>
        private static readonly TaskType[] PriorityOrder = new[]
        {
            TaskType.Harvesting,  // Priority #1 - Harvest ready crops first
            TaskType.Lumbering,   // Priority #2 - Chop trees and clear debris
            TaskType.Watering,    // Priority #3 - Water crops
            TaskType.Petting,     // Priority #4 - Pet animals
            TaskType.Foraging,    // Priority #5 - Gather forage items
            TaskType.Mining,      // Priority #6 - Break rocks
            TaskType.Fishing,     // Priority #7 - Fish
            TaskType.Sitting      // Priority #8 - Sit on furniture (lowest priority)
        };

        /// <summary>
        /// Gets all tasks in priority order (highest priority first).
        /// Excludes Attacking and Following as they are handled separately.
        /// </summary>
        /// <returns>Array of TaskType in priority order</returns>
        public static TaskType[] GetTasksInPriorityOrder()
        {
            return PriorityOrder;
        }

        /// <summary>
        /// Gets the priority value for a specific task type.
        /// Lower values indicate higher priority.
        /// </summary>
        /// <param name="taskType">The task type to check</param>
        /// <returns>Priority index (0 = highest priority), or -1 if not in regular priority list</returns>
        public static int GetPriority(TaskType taskType)
        {
            for (int i = 0; i < PriorityOrder.Length; i++)
            {
                if (PriorityOrder[i] == taskType)
                    return i;
            }
            return -1; // Not in priority list (e.g., Attacking, Following)
        }

        /// <summary>
        /// Checks if a task type is available in the given mode configuration.
        /// </summary>
        /// <param name="taskType">The task type to check</param>
        /// <param name="mode">The task mode (Disabled, Mimicking, Autonomous)</param>
        /// <returns>True if the task is available in this mode</returns>
        public static bool IsTaskAvailableInMode(TaskType taskType, TaskMode mode)
        {
            // All tasks disabled in Disabled mode
            if (mode == TaskMode.Disabled)
                return false;

            // Fishing and Sitting are mimicking-only (no autonomous mode)
            if (taskType == TaskType.Fishing || taskType == TaskType.Sitting)
                return mode == TaskMode.Mimicking;

            // All other tasks work in both modes
            return true;
        }

        /// <summary>
        /// Gets the configured task mode for a specific task type from the mod config.
        /// </summary>
        /// <param name="config">The mod configuration</param>
        /// <param name="taskType">The task type to query</param>
        /// <returns>The configured TaskMode for the given task type</returns>
        public static TaskMode GetTaskMode(ModConfig config, TaskType taskType)
        {
            return taskType switch
            {
                TaskType.Harvesting => config.HarvestingMode,
                TaskType.Lumbering => config.LumberingMode,
                TaskType.Watering => config.WateringMode,
                TaskType.Petting => config.PettingMode,
                TaskType.Foraging => config.ForagingMode,
                TaskType.Mining => config.MiningMode,
                TaskType.Fishing => config.FishingMode,
                TaskType.Attacking => config.AttackingMode,
                TaskType.Sitting => config.EnableSitting ? TaskMode.Mimicking : TaskMode.Disabled,
                _ => TaskMode.Disabled
            };
        }
    }
}
