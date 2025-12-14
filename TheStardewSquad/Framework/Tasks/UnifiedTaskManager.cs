using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Pathfinding;
using TheStardewSquad.Abstractions.Location;

namespace TheStardewSquad.Framework.Tasks
{
    /// <summary>
    /// Unified task manager that respects priority order across both Mimicking and Autonomous modes.
    /// For each task in priority order, checks BOTH Mimicking and Autonomous conditions before moving to next priority level.
    /// </summary>
    public class UnifiedTaskManager
    {
        private readonly ModConfig _config;
        private readonly IMonitor _monitor;

        public UnifiedTaskManager(ModConfig config, IMonitor monitor)
        {
            this._config = config;
            this._monitor = monitor;
        }

        /// <summary>
        /// Finds a high-priority attacking task that can override any current task.
        /// This is checked on fast ticks and can interrupt any other task.
        /// </summary>
        public SquadTask FindAttackingTask(
            ISquadMate mate,
            ILocationInfo locationInfo,
            Point playerPosition,
            Point npcPosition,
            ISet<Vector2> claimedInteractionSpots)
        {
            if (mate.IsOnCooldown())
                return null;

            // Get attacking mode
            TaskMode attackingMode = TaskPriorityManager.GetTaskMode(_config, TaskType.Attacking);

            // Skip if disabled or mate can't perform it
            if (attackingMode == TaskMode.Disabled || !mate.CanPerformTask(TaskType.Attacking))
                return null;

            // If in mimicking mode, only attack when the timer is active for Attacking
            // UpdateMimickingTimers() handles setting the timer when player is in combat
            if (attackingMode == TaskMode.Mimicking &&
                (mate.MimickingTaskType != TaskType.Attacking || mate.MimickingTaskTimer <= 0))
                return null;

            // Find hostile monster
            Monster monster = TaskManager.FindHostileMonster(locationInfo, playerPosition, npcPosition, 8);
            if (monster != null)
            {
                // Get the set of spots claimed by *other* NPCs to avoid collisions
                var otherClaimedSpots = new HashSet<Vector2>(claimedInteractionSpots);
                if (mate.ClaimedInteractionSpot.HasValue)
                {
                    otherClaimedSpots.Remove(mate.ClaimedInteractionSpot.Value);
                }

                var interactionTile = AStarPathfinder.FindClosestPassableNeighbor(locationInfo, monster.TilePoint, npcPosition, otherClaimedSpots);
                if (interactionTile.HasValue)
                {
                    return new SquadTask(TaskType.Attacking, monster.TilePoint, interactionTile.Value, monster);
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the best available task using unified priority order.
        /// For each task in priority order, checks BOTH Mimicking and Autonomous conditions before moving to next priority.
        /// </summary>
        public SquadTask FindUnifiedTask(
            ISquadMate mate,
            ILocationInfo locationInfo,
            Point playerPosition,
            Point npcPosition,
            ISet<Point> claimedTaskTargets,
            ISet<Vector2> claimedInteractionSpots)
        {
            // Get the set of spots claimed by *other* NPCs
            var otherClaimedSpots = new HashSet<Vector2>(claimedInteractionSpots);
            if (mate.ClaimedInteractionSpot.HasValue)
            {
                otherClaimedSpots.Remove(mate.ClaimedInteractionSpot.Value);
            }

            // Check tasks in priority order - for each task, check BOTH mimicking and autonomous
            foreach (var taskType in TaskPriorityManager.GetTasksInPriorityOrder())
            {
                // Skip attacking - it's handled separately as an override
                if (taskType == TaskType.Attacking)
                    continue;

                // Get the task mode for this task type
                TaskMode taskMode = TaskPriorityManager.GetTaskMode(_config, taskType);

                // Skip if disabled or mate can't perform this task
                if (taskMode == TaskMode.Disabled || !mate.CanPerformTask(taskType))
                    continue;

                // Check Mimicking mode first (higher priority within same task)
                if (taskMode == TaskMode.Mimicking)
                {
                    // Find mimicking tasks ONLY when the timer is active for this specific task type
                    // UpdateMimickingTimers() handles setting the timer when player performs the task
                    bool timerActiveForThisTask = mate.MimickingTaskTimer > 0 && mate.MimickingTaskType == taskType;

                    if (timerActiveForThisTask)
                    {
                        SquadTask task = TryFindTaskByType(taskType, locationInfo, playerPosition, npcPosition, claimedTaskTargets, otherClaimedSpots);
                        if (task != null)
                            return task;
                    }
                }
                // Check Autonomous mode
                else if (taskMode == TaskMode.Autonomous)
                {
                    SquadTask task = TryFindTaskByType(taskType, locationInfo, playerPosition, npcPosition, claimedTaskTargets, otherClaimedSpots);
                    if (task != null)
                        return task;
                }
            }

            return null;
        }

        /// <summary>Attempts to find a task of the specified type for the given NPC.</summary>
        private SquadTask TryFindTaskByType(
            TaskType taskType,
            ILocationInfo locationInfo,
            Point playerPosition,
            Point npcPosition,
            ISet<Point> claimedTaskTargets,
            ISet<Vector2> otherClaimedSpots)
        {
            const int searchRadius = 8;
            const int sittingSearchRadius = 5;

            switch (taskType)
            {
                case TaskType.Harvesting:
                    var (harvestTarget, harvestInteractionPoint) = TaskManager.FindHarvestableCrop(
                        locationInfo, playerPosition, npcPosition, searchRadius, claimedTaskTargets,
                        _config.ProtectBeehouseFlowers);
                    if (harvestTarget.HasValue && harvestInteractionPoint.HasValue)
                        return new SquadTask(TaskType.Harvesting, harvestTarget.Value, harvestInteractionPoint.Value);
                    break;

                case TaskType.Lumbering:
                    var (lumberTarget, lumberInteractionPoint) = TaskManager.FindLumberingTarget(
                        locationInfo, playerPosition, npcPosition, searchRadius, claimedTaskTargets, otherClaimedSpots, _monitor, TaskManager.LumberingTargetType.Both);
                    if (lumberTarget.HasValue && lumberInteractionPoint.HasValue)
                        return new SquadTask(TaskType.Lumbering, lumberTarget.Value, lumberInteractionPoint.Value);
                    break;

                case TaskType.Watering:
                    var (waterTarget, waterInteractionPoint) = TaskManager.FindWaterableTile(
                        locationInfo, npcPosition, searchRadius, claimedTaskTargets);
                    if (waterTarget.HasValue && waterInteractionPoint.HasValue)
                        return new SquadTask(TaskType.Watering, waterTarget.Value, waterInteractionPoint.Value);
                    break;

                case TaskType.Petting:
                    var (pettingTarget, pettingInteractionPoint) = TaskManager.FindPettableAnimal(
                        locationInfo, playerPosition, npcPosition, searchRadius, claimedTaskTargets);
                    if (pettingTarget.HasValue && pettingInteractionPoint.HasValue)
                        return new SquadTask(TaskType.Petting, pettingTarget.Value, pettingInteractionPoint.Value);
                    break;

                case TaskType.Foraging:
                    var (forageTarget, forageInteractionPoint) = TaskManager.FindForageableTarget(
                        locationInfo, playerPosition, npcPosition, searchRadius, claimedTaskTargets);
                    if (forageTarget.HasValue && forageInteractionPoint.HasValue)
                        return new SquadTask(TaskType.Foraging, forageTarget.Value, forageInteractionPoint.Value);
                    break;

                case TaskType.Mining:
                    var (rockTarget, rockInteractionPoint) = TaskManager.FindMinableRock(
                        locationInfo, playerPosition, npcPosition, searchRadius, otherClaimedSpots, _monitor);
                    if (rockTarget.HasValue && rockInteractionPoint.HasValue)
                        return new SquadTask(TaskType.Mining, rockTarget.Value, rockInteractionPoint.Value);
                    break;

                case TaskType.Fishing:
                    var fishingTask = TaskManager.CreateFishingTask(
                        locationInfo, playerPosition, npcPosition, otherClaimedSpots, claimedTaskTargets, _monitor);
                    if (fishingTask != null)
                        return fishingTask;
                    break;

                case TaskType.Sitting:
                    var (sittingFurnitureTile, sittingSeatPosition) = TaskManager.FindSittingSpot(
                        locationInfo, playerPosition, npcPosition, sittingSearchRadius, otherClaimedSpots);
                    if (sittingFurnitureTile.HasValue && sittingSeatPosition.HasValue)
                        return new SquadTask(TaskType.Sitting, sittingFurnitureTile.Value, sittingSeatPosition.Value.ToPoint(), seatPosition: sittingSeatPosition.Value);
                    break;
            }

            return null;
        }
    }
}
