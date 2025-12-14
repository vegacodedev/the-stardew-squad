using Microsoft.Xna.Framework;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Tests.Helpers;

/// <summary>
/// Builder classes for creating test data with fluent API.
/// Provides a clean way to construct complex test objects.
/// </summary>
public static class TestDataBuilders
{
    /// <summary>
    /// Builder for creating Vector2 positions easily in tests.
    /// </summary>
    public class PositionBuilder
    {
        private float _x = 0;
        private float _y = 0;

        public PositionBuilder At(float x, float y)
        {
            _x = x;
            _y = y;
            return this;
        }

        public PositionBuilder AtOrigin()
        {
            _x = 0;
            _y = 0;
            return this;
        }

        public Vector2 Build() => new Vector2(_x, _y);
    }

    /// <summary>
    /// Builder for creating test TaskType configurations.
    /// </summary>
    public class TaskTypeBuilder
    {
        private TaskType _taskType = TaskType.Watering;
        private TaskMode _mode = TaskMode.Disabled;

        public TaskTypeBuilder WithTask(TaskType taskType)
        {
            _taskType = taskType;
            return this;
        }

        public TaskTypeBuilder InMode(TaskMode mode)
        {
            _mode = mode;
            return this;
        }

        public TaskTypeBuilder AsWatering() => WithTask(TaskType.Watering);
        public TaskTypeBuilder AsHarvesting() => WithTask(TaskType.Harvesting);
        public TaskTypeBuilder AsAttacking() => WithTask(TaskType.Attacking);
        public TaskTypeBuilder AsMining() => WithTask(TaskType.Mining);
        public TaskTypeBuilder AsLumbering() => WithTask(TaskType.Lumbering);
        public TaskTypeBuilder AsForaging() => WithTask(TaskType.Foraging);
        public TaskTypeBuilder AsFishing() => WithTask(TaskType.Fishing);
        public TaskTypeBuilder AsPetting() => WithTask(TaskType.Petting);

        public TaskTypeBuilder Autonomous() => InMode(TaskMode.Autonomous);
        public TaskTypeBuilder Mimicking() => InMode(TaskMode.Mimicking);
        public TaskTypeBuilder Disabled() => InMode(TaskMode.Disabled);

        public (TaskType taskType, TaskMode mode) Build() => (_taskType, _mode);
    }

    public static PositionBuilder Position() => new PositionBuilder();
    public static TaskTypeBuilder Task() => new TaskTypeBuilder();
}
