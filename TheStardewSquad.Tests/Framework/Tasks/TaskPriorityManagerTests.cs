using FluentAssertions;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.Tasks;
using TheStardewSquad.Tests.Helpers;
using Xunit;

namespace TheStardewSquad.Tests.Framework.Tasks;

/// <summary>
/// Unit tests for TaskPriorityManager - validates task priority logic.
/// These tests ensure consistent task ordering and availability across different modes.
/// </summary>
public class TaskPriorityManagerTests
{
    #region GetTasksInPriorityOrder Tests

    [Fact]
    public void GetTasksInPriorityOrder_ShouldReturnEightTasks()
    {
        // Act
        var tasks = TaskPriorityManager.GetTasksInPriorityOrder();

        // Assert
        tasks.Should().HaveCount(8, "there are 8 regular priority tasks (excluding Attacking and Following)");
    }

    [Fact]
    public void GetTasksInPriorityOrder_ShouldHaveHarvestingAsHighestPriority()
    {
        // Act
        var tasks = TaskPriorityManager.GetTasksInPriorityOrder();

        // Assert
        tasks[0].Should().Be(TaskType.Harvesting, "harvesting ready crops should be the highest priority");
    }

    [Fact]
    public void GetTasksInPriorityOrder_ShouldHaveSittingAsLowestPriority()
    {
        // Act
        var tasks = TaskPriorityManager.GetTasksInPriorityOrder();

        // Assert
        tasks[^1].Should().Be(TaskType.Sitting, "sitting should be the lowest priority task");
    }

    [Fact]
    public void GetTasksInPriorityOrder_ShouldFollowExpectedPrioritySequence()
    {
        // Act
        var tasks = TaskPriorityManager.GetTasksInPriorityOrder();

        // Assert - Verify the complete priority order
        tasks.Should().BeEquivalentTo(new[]
        {
            TaskType.Harvesting,  // #1
            TaskType.Lumbering,   // #2
            TaskType.Watering,    // #3
            TaskType.Petting,     // #4
            TaskType.Foraging,    // #5
            TaskType.Mining,      // #6
            TaskType.Fishing,     // #7
            TaskType.Sitting      // #8
        }, options => options.WithStrictOrdering(), "the priority order should match the documented design");
    }

    [Fact]
    public void GetTasksInPriorityOrder_ShouldNotIncludeAttacking()
    {
        // Act
        var tasks = TaskPriorityManager.GetTasksInPriorityOrder();

        // Assert
        tasks.Should().NotContain(TaskType.Attacking, "attacking is handled separately as a high-priority override");
    }

    [Fact]
    public void GetTasksInPriorityOrder_ShouldNotIncludeFollowing()
    {
        // Act
        var tasks = TaskPriorityManager.GetTasksInPriorityOrder();

        // Assert
        tasks.Should().NotContain(TaskType.Following, "following is handled separately and not part of priority system");
    }

    #endregion

    #region GetPriority Tests

    [Theory]
    [InlineData(TaskType.Harvesting, 0)]
    [InlineData(TaskType.Lumbering, 1)]
    [InlineData(TaskType.Watering, 2)]
    [InlineData(TaskType.Petting, 3)]
    [InlineData(TaskType.Foraging, 4)]
    [InlineData(TaskType.Mining, 5)]
    [InlineData(TaskType.Fishing, 6)]
    [InlineData(TaskType.Sitting, 7)]
    public void GetPriority_ShouldReturnCorrectPriorityIndex(TaskType taskType, int expectedPriority)
    {
        // Act
        var priority = TaskPriorityManager.GetPriority(taskType);

        // Assert
        priority.Should().Be(expectedPriority, $"{taskType} should have priority index {expectedPriority}");
    }

    [Theory]
    [InlineData(TaskType.Attacking)]
    [InlineData(TaskType.Following)]
    public void GetPriority_ShouldReturnMinusOneForNonPriorityTasks(TaskType taskType)
    {
        // Act
        var priority = TaskPriorityManager.GetPriority(taskType);

        // Assert
        priority.Should().Be(-1, $"{taskType} is not in the regular priority list");
    }

    [Fact]
    public void GetPriority_HigherPriorityTasksShouldHaveLowerIndexes()
    {
        // Arrange - Harvesting should be higher priority than Fishing
        var harvestingPriority = TaskPriorityManager.GetPriority(TaskType.Harvesting);
        var fishingPriority = TaskPriorityManager.GetPriority(TaskType.Fishing);

        // Assert
        harvestingPriority.Should().BeLessThan(fishingPriority,
            "harvesting (higher priority) should have a lower index than fishing (lower priority)");
    }

    #endregion

    #region IsTaskAvailableInMode Tests

    [Theory]
    [InlineData(TaskType.Harvesting)]
    [InlineData(TaskType.Watering)]
    [InlineData(TaskType.Mining)]
    [InlineData(TaskType.Attacking)]
    [InlineData(TaskType.Foraging)]
    [InlineData(TaskType.Petting)]
    [InlineData(TaskType.Fishing)]
    [InlineData(TaskType.Lumbering)]
    public void IsTaskAvailableInMode_AllTasksShouldBeUnavailableWhenDisabled(TaskType taskType)
    {
        // Act
        var isAvailable = TaskPriorityManager.IsTaskAvailableInMode(taskType, TaskMode.Disabled);

        // Assert
        isAvailable.Should().BeFalse($"{taskType} should not be available in Disabled mode");
    }

    [Theory]
    [InlineData(TaskType.Fishing)]
    public void IsTaskAvailableInMode_FishingShouldBeOnlyMimicking(TaskType taskType)
    {
        // Act
        var availableInMimicking = TaskPriorityManager.IsTaskAvailableInMode(taskType, TaskMode.Mimicking);
        var availableInAutonomous = TaskPriorityManager.IsTaskAvailableInMode(taskType, TaskMode.Autonomous);

        // Assert
        availableInMimicking.Should().BeTrue($"{taskType} should be available in Mimicking mode");
        availableInAutonomous.Should().BeFalse($"{taskType} should NOT be available in Autonomous mode");
    }

    [Theory]
    [InlineData(TaskType.Harvesting)]
    [InlineData(TaskType.Watering)]
    [InlineData(TaskType.Mining)]
    [InlineData(TaskType.Attacking)]
    [InlineData(TaskType.Foraging)]
    [InlineData(TaskType.Petting)]
    [InlineData(TaskType.Lumbering)]
    public void IsTaskAvailableInMode_OtherTasksShouldBeAvailableInBothModes(TaskType taskType)
    {
        // Act
        var availableInMimicking = TaskPriorityManager.IsTaskAvailableInMode(taskType, TaskMode.Mimicking);
        var availableInAutonomous = TaskPriorityManager.IsTaskAvailableInMode(taskType, TaskMode.Autonomous);

        // Assert
        availableInMimicking.Should().BeTrue($"{taskType} should be available in Mimicking mode");
        availableInAutonomous.Should().BeTrue($"{taskType} should be available in Autonomous mode");
    }

    #endregion

    #region GetTaskMode Tests

    [Fact]
    public void GetTaskMode_ShouldReturnCorrectModeForAllTaskTypes()
    {
        // Arrange - Create a config with known values
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Autonomous,
            lumbering: TaskMode.Mimicking,
            watering: TaskMode.Disabled,
            petting: TaskMode.Autonomous,
            foraging: TaskMode.Mimicking,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Mimicking,
            attacking: TaskMode.Autonomous
        );

        // Act & Assert
        TaskPriorityManager.GetTaskMode(config, TaskType.Harvesting).Should().Be(TaskMode.Autonomous);
        TaskPriorityManager.GetTaskMode(config, TaskType.Lumbering).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Watering).Should().Be(TaskMode.Disabled);
        TaskPriorityManager.GetTaskMode(config, TaskType.Petting).Should().Be(TaskMode.Autonomous);
        TaskPriorityManager.GetTaskMode(config, TaskType.Foraging).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Mining).Should().Be(TaskMode.Disabled);
        TaskPriorityManager.GetTaskMode(config, TaskType.Fishing).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Attacking).Should().Be(TaskMode.Autonomous);
    }

    [Fact]
    public void GetTaskMode_ShouldReturnDisabledForUnknownTaskType()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfig();
        var unknownTaskType = (TaskType)999; // Invalid task type

        // Act
        var mode = TaskPriorityManager.GetTaskMode(config, unknownTaskType);

        // Assert
        mode.Should().Be(TaskMode.Disabled, "unknown task types should default to Disabled");
    }

    [Fact]
    public void GetTaskMode_ShouldReturnDisabledForFollowingTask()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfig();

        // Act
        var mode = TaskPriorityManager.GetTaskMode(config, TaskType.Following);

        // Assert
        mode.Should().Be(TaskMode.Disabled, "Following task has no configuration setting");
    }

    [Fact]
    public void GetTaskMode_ShouldRespectDefaultConfigValues()
    {
        // Arrange - Use default ModConfig
        var config = ConfigTestHelper.CreateTestConfig();

        // Act & Assert - Verify defaults match expected values
        TaskPriorityManager.GetTaskMode(config, TaskType.Watering).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Lumbering).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Mining).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Attacking).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Harvesting).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Foraging).Should().Be(TaskMode.Autonomous);
        TaskPriorityManager.GetTaskMode(config, TaskType.Fishing).Should().Be(TaskMode.Mimicking);
        TaskPriorityManager.GetTaskMode(config, TaskType.Petting).Should().Be(TaskMode.Mimicking);
    }

    #endregion

    #region Integration Tests - Priority + Availability

    [Fact]
    public void PrioritySystem_ShouldProcessTasksInCorrectOrder()
    {
        // Arrange - Get all tasks in priority order
        var tasks = TaskPriorityManager.GetTasksInPriorityOrder();
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Autonomous,
            lumbering: TaskMode.Mimicking,
            watering: TaskMode.Autonomous,
            petting: TaskMode.Autonomous,
            foraging: TaskMode.Autonomous,
            mining: TaskMode.Autonomous,
            fishing: TaskMode.Mimicking
        );

        // Act - Get available tasks in autonomous mode
        var availableTasks = tasks
            .Where(t => TaskPriorityManager.IsTaskAvailableInMode(
                t, TaskPriorityManager.GetTaskMode(config, t)))
            .ToList();

        // Assert - Verify Harvesting is still first among available tasks
        availableTasks.First().Should().Be(TaskType.Harvesting,
            "even after filtering, harvesting should remain the highest priority available task");
    }

    [Fact]
    public void PrioritySystem_ShouldFilterOutDisabledTasks()
    {
        // Arrange
        var tasks = TaskPriorityManager.GetTasksInPriorityOrder();
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Autonomous,
            foraging: TaskMode.Autonomous,
            mining: TaskMode.Autonomous,
            fishing: TaskMode.Mimicking,
            petting: TaskMode.Autonomous
        );

        // Act - Get only enabled tasks
        var enabledTasks = tasks
            .Where(t => TaskPriorityManager.GetTaskMode(config, t) != TaskMode.Disabled)
            .ToList();

        // Assert
        enabledTasks.Should().NotContain(TaskType.Harvesting);
        enabledTasks.Should().NotContain(TaskType.Lumbering);
        enabledTasks.Should().Contain(TaskType.Watering);
    }

    #endregion
}
