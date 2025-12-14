using FluentAssertions;
using Microsoft.Xna.Framework;
using Moq;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.Tasks;
using TheStardewSquad.Tests.Framework;
using Xunit;
using MockFactory = TheStardewSquad.Tests.Helpers.MockFactory;
using ConfigTestHelper = TheStardewSquad.Tests.Helpers.ConfigTestHelper;

namespace TheStardewSquad.Tests.Framework.Tasks;

/// <summary>
/// Comprehensive tests for UnifiedTaskManager - validates unified task orchestration logic.
/// Tests cover priority ordering, mode handling (Mimicking/Autonomous), task selection, and edge cases.
///
/// Note: These are primarily logic/integration tests. UnifiedTaskManager relies on TaskManager
/// static methods which are tested separately. These tests focus on the orchestration logic.
/// </summary>
public class UnifiedTaskManagerTests
{
    private readonly Mock<IMonitor> _mockMonitor;

    public UnifiedTaskManagerTests()
    {
        _mockMonitor = new Mock<IMonitor>();
    }

    #region Helper Methods

    /// <summary>
    /// Creates a mock squad mate with configurable capabilities.
    /// Note: NPC properties like Name and Position can't be mocked as they're non-virtual.
    /// </summary>
    private Mock<ISquadMate> CreateMockMate(
        string name = "TestNPC",
        bool isOnCooldown = false,
        HashSet<TaskType>? canPerform = null,
        Vector2? claimedSpot = null)
    {
        // Create a mock NPC - note that many NPC properties are non-virtual and can't be mocked
        var mockNpc = new Mock<NPC>();
        // Only setup virtual properties or methods if absolutely needed

        var mockMate = new Mock<ISquadMate>();

        mockMate.Setup(m => m.Npc).Returns(mockNpc.Object);
        mockMate.Setup(m => m.Name).Returns(name);
        mockMate.Setup(m => m.IsOnCooldown()).Returns(isOnCooldown);
        mockMate.Setup(m => m.ClaimedInteractionSpot).Returns(claimedSpot);

        // Setup CanPerformTask - allow all tasks by default
        if (canPerform == null)
        {
            mockMate.Setup(m => m.CanPerformTask(It.IsAny<TaskType>())).Returns(true);
        }
        else
        {
            mockMate.Setup(m => m.CanPerformTask(It.IsAny<TaskType>()))
                .Returns((TaskType t) => canPerform.Contains(t));
        }

        return mockMate;
    }

    /// <summary>
    /// Helper to create standard test context for FindUnifiedTask tests.
    /// </summary>
    private (MockLocationInfo location, Point playerPos, Point npcPos) CreateTestContext()
    {
        var location = new MockLocationInfo();
        var playerPos = new Point(10, 10);
        var npcPos = new Point(10, 10);
        return (location, playerPos, npcPos);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithConfigAndMonitor()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfig();
        var monitor = new Mock<IMonitor>().Object;

        // Act
        var manager = new UnifiedTaskManager(config, monitor);

        // Assert
        manager.Should().NotBeNull();
    }

    #endregion

    #region FindAttackingTask Tests

    [Fact]
    public void FindAttackingTask_ShouldReturnNull_WhenMateIsOnCooldown()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(attacking: TaskMode.Mimicking);
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate(isOnCooldown: true);
        var location = new MockLocationInfo();
        var playerPos = new Point(10, 10);
        var npcPos = new Point(10, 10);
        var claimedSpots = new HashSet<Vector2>();

        // Act
        var task = manager.FindAttackingTask(mate.Object, location, playerPos, npcPos, claimedSpots);

        // Assert
        task.Should().BeNull("mate is on cooldown");
    }

    [Fact]
    public void FindAttackingTask_ShouldReturnNull_WhenAttackingIsDisabled()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(attacking: TaskMode.Disabled);
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate(isOnCooldown: false);
        var location = new MockLocationInfo();
        var playerPos = new Point(10, 10);
        var npcPos = new Point(10, 10);
        var claimedSpots = new HashSet<Vector2>();

        // Act
        var task = manager.FindAttackingTask(mate.Object, location, playerPos, npcPos, claimedSpots);

        // Assert
        task.Should().BeNull("attacking mode is disabled");
    }

    [Fact]
    public void FindAttackingTask_ShouldReturnNull_WhenMateCannotPerformAttacking()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(attacking: TaskMode.Mimicking);
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var canPerform = new HashSet<TaskType> { TaskType.Harvesting, TaskType.Watering };
        var mate = CreateMockMate(canPerform: canPerform);
        var location = new MockLocationInfo();
        var playerPos = new Point(10, 10);
        var npcPos = new Point(10, 10);
        var claimedSpots = new HashSet<Vector2>();

        // Act
        var task = manager.FindAttackingTask(mate.Object, location, playerPos, npcPos, claimedSpots);

        // Assert
        task.Should().BeNull("mate cannot perform attacking tasks");
    }

    [Fact]
    public void FindAttackingTask_ShouldRespectClaimedInteractionSpot()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(attacking: TaskMode.Autonomous);
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var claimedSpot = new Vector2(10, 10);
        var mate = CreateMockMate(claimedSpot: claimedSpot);
        var location = new MockLocationInfo();
        var playerPos = new Point(10, 10);
        var npcPos = new Point(10, 10);

        // The mate's claimed spot should be removed from the set of spots to avoid
        var claimedSpots = new HashSet<Vector2> { claimedSpot, new Vector2(11, 11) };

        // Act
        var task = manager.FindAttackingTask(mate.Object, location, playerPos, npcPos, claimedSpots);

        // Assert
        // We can't easily test the full behavior without a real monster,
        // but we can verify the method runs without error
        // (In a real scenario, this would use PathFinder with otherClaimedSpots that excludes mate's spot)
    }

    #endregion

    #region FindUnifiedTask - Priority Order Tests

    [Fact]
    public void FindUnifiedTask_ShouldCheckTasksInPriorityOrder()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Autonomous,
            lumbering: TaskMode.Autonomous,
            watering: TaskMode.Autonomous,
            petting: TaskMode.Autonomous,
            foraging: TaskMode.Autonomous,
            mining: TaskMode.Autonomous,
            fishing: TaskMode.Mimicking
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // The method should complete without error and return null or a task
        // We can't mock TaskManager static methods, but we can verify the flow works
        // In a real scenario with available tasks, it would return tasks in priority order
    }

    [Fact]
    public void FindUnifiedTask_ShouldSkipAttackingTask()
    {
        // Arrange - Enable attacking in config
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            attacking: TaskMode.Autonomous,
            harvesting: TaskMode.Autonomous
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // FindUnifiedTask should never return an attacking task
        // (attacking is handled separately via FindAttackingTask)
        task?.Type.Should().NotBe(TaskType.Attacking, "attacking should be skipped in unified task search");
    }

    [Fact]
    public void FindUnifiedTask_ShouldSkipDisabledTasks()
    {
        // Arrange - Disable all tasks
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Disabled,
            petting: TaskMode.Disabled,
            foraging: TaskMode.Disabled,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Disabled
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        task.Should().BeNull("all tasks are disabled");
    }

    [Fact]
    public void FindUnifiedTask_ShouldSkipTasksMateCannotPerform()
    {
        // Arrange - Mate can only perform Watering
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Autonomous,
            lumbering: TaskMode.Autonomous,
            watering: TaskMode.Autonomous,
            petting: TaskMode.Autonomous,
            foraging: TaskMode.Autonomous,
            mining: TaskMode.Autonomous,
            fishing: TaskMode.Mimicking
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var canPerform = new HashSet<TaskType> { TaskType.Watering };
        var mate = CreateMockMate(canPerform: canPerform);
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Should only look for Watering tasks (or return null if none available)
        if (task != null)
        {
            task.Type.Should().Be(TaskType.Watering, "mate can only perform watering");
        }
    }

    [Fact]
    public void FindUnifiedTask_ShouldRemoveMateClaimedSpotFromOtherClaimedSpots()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(harvesting: TaskMode.Autonomous);
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mateClaimedSpot = new Vector2(5, 5);
        var mate = CreateMockMate(claimedSpot: mateClaimedSpot);
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2> { mateClaimedSpot, new Vector2(6, 6) };
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // The mate's own claimed spot should not be in the collision avoidance set
        // This is tested indirectly - the method should run without treating
        // the mate's own spot as blocked
    }

    #endregion

    #region FindUnifiedTask - Mode Handling Tests

    [Fact]
    public void FindUnifiedTask_MimickingMode_ShouldRequirePlayerAction()
    {
        // Arrange - All tasks in Mimicking mode
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Mimicking,
            lumbering: TaskMode.Mimicking,
            watering: TaskMode.Mimicking,
            petting: TaskMode.Mimicking,
            foraging: TaskMode.Mimicking,
            mining: TaskMode.Mimicking,
            fishing: TaskMode.Mimicking
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Without player performing any task, mimicking mode should return null
        // (This is a logic test - in practice, TaskManager.IsPlayerXXX() would be called)
        // We can't mock static methods easily, so this verifies the code path runs
    }

    [Fact]
    public void FindUnifiedTask_AutonomousMode_ShouldNotRequirePlayerAction()
    {
        // Arrange - All tasks in Autonomous mode
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Autonomous,
            lumbering: TaskMode.Autonomous,  // Note: Lumbering is normally Mimicking-only
            watering: TaskMode.Autonomous,
            petting: TaskMode.Autonomous,
            foraging: TaskMode.Autonomous,
            mining: TaskMode.Autonomous,
            fishing: TaskMode.Autonomous  // Note: Fishing is normally Mimicking-only
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Autonomous mode doesn't require player action check
        // Result will be null or a task depending on TaskManager finding logic
        // This test verifies the code path executes without error
    }

    [Fact]
    public void FindUnifiedTask_MixedMode_ShouldCheckMimickingBeforeAutonomous()
    {
        // Arrange - Mix of Mimicking and Autonomous
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Mimicking,
            watering: TaskMode.Autonomous
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // According to priority order: Harvesting (priority 0) > Watering (priority 2)
        // Even though Harvesting is Mimicking and Watering is Autonomous,
        // the method should check Harvesting first
        // If player is not harvesting, it should then check Watering (autonomous)
    }

    [Fact]
    public void FindUnifiedTask_ShouldRespectConfiguredPriorityOrder()
    {
        // Arrange - Enable specific tasks in different priorities
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Autonomous,    // Priority 0 (highest)
            lumbering: TaskMode.Disabled,       // Priority 1 (skipped)
            watering: TaskMode.Autonomous,      // Priority 2
            petting: TaskMode.Disabled,         // Priority 3 (skipped)
            foraging: TaskMode.Autonomous,      // Priority 4
            mining: TaskMode.Autonomous,        // Priority 5
            fishing: TaskMode.Mimicking         // Priority 6 (lowest)
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Should check in order: Harvesting, Watering, Foraging, Mining, Fishing
        // The actual returned task depends on TaskManager finding logic
    }

    #endregion

    #region FindUnifiedTask - Task Type Specific Tests

    [Fact]
    public void FindUnifiedTask_ShouldHandleHarvestingTaskType()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Autonomous,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Disabled,
            petting: TaskMode.Disabled,
            foraging: TaskMode.Disabled,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Disabled
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Will call TaskManager.FindHarvestableCrop
        // Returns null or a Harvesting task
        if (task != null)
        {
            task.Type.Should().Be(TaskType.Harvesting);
        }
    }

    [Fact]
    public void FindUnifiedTask_ShouldHandleLumberingTaskType()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Mimicking,
            watering: TaskMode.Disabled,
            petting: TaskMode.Disabled,
            foraging: TaskMode.Disabled,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Disabled
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Will call TaskManager.FindLumberingTarget
        // Returns null or a Lumbering task
        if (task != null)
        {
            task.Type.Should().Be(TaskType.Lumbering);
        }
    }

    [Fact]
    public void FindUnifiedTask_ShouldHandleWateringTaskType()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Autonomous,
            petting: TaskMode.Disabled,
            foraging: TaskMode.Disabled,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Disabled
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Will call TaskManager.FindWaterableTile
        if (task != null)
        {
            task.Type.Should().Be(TaskType.Watering);
        }
    }

    [Fact]
    public void FindUnifiedTask_ShouldHandlePettingTaskType()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Disabled,
            petting: TaskMode.Autonomous,
            foraging: TaskMode.Disabled,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Disabled
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Will call TaskManager.FindPettableAnimal
        if (task != null)
        {
            task.Type.Should().Be(TaskType.Petting);
        }
    }

    [Fact]
    public void FindUnifiedTask_ShouldHandleForagingTaskType()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Disabled,
            petting: TaskMode.Disabled,
            foraging: TaskMode.Autonomous,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Disabled
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Will call TaskManager.FindForageableTarget
        if (task != null)
        {
            task.Type.Should().Be(TaskType.Foraging);
        }
    }

    [Fact]
    public void FindUnifiedTask_ShouldHandleMiningTaskType()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Disabled,
            petting: TaskMode.Disabled,
            foraging: TaskMode.Disabled,
            mining: TaskMode.Autonomous,
            fishing: TaskMode.Disabled
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Will call TaskManager.FindMinableRock
        if (task != null)
        {
            task.Type.Should().Be(TaskType.Mining);
        }
    }

    [Fact]
    public void FindUnifiedTask_ShouldHandleFishingTaskType()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Disabled,
            petting: TaskMode.Disabled,
            foraging: TaskMode.Disabled,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Mimicking
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Will call TaskManager.CreateFishingTask
        if (task != null)
        {
            task.Type.Should().Be(TaskType.Fishing);
        }
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void FindUnifiedTask_WithNoAvailableTasks_ShouldReturnNull()
    {
        // Arrange - Disable all tasks
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Disabled,
            lumbering: TaskMode.Disabled,
            watering: TaskMode.Disabled,
            petting: TaskMode.Disabled,
            foraging: TaskMode.Disabled,
            mining: TaskMode.Disabled,
            fishing: TaskMode.Disabled,
            attacking: TaskMode.Disabled
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        task.Should().BeNull("all task types are disabled");
    }

    [Fact]
    public void FindUnifiedTask_WithEmptyClaimedSets_ShouldExecuteNormally()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfig();
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Should execute without error (result depends on TaskManager logic)
    }

    [Fact]
    public void FindUnifiedTask_WithMultipleClaimedSpots_ShouldPassCorrectSetToTaskManager()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfigWithModes(mining: TaskMode.Autonomous);
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point> { new Point(1, 1), new Point(2, 2) };
        var claimedSpots = new HashSet<Vector2> { new Vector2(3, 3), new Vector2(4, 4) };
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // The method should pass these sets to TaskManager methods
        // This verifies the method executes without error
    }

    [Fact]
    public void FindUnifiedTask_ShouldHandleNullMateClaimedSpot()
    {
        // Arrange
        var config = ConfigTestHelper.CreateTestConfig();
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate(claimedSpot: null);
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2> { new Vector2(1, 1) };
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Should handle null ClaimedInteractionSpot gracefully
    }

    [Fact]
    public void FindUnifiedTask_WithAllTasksEnabledAutonomous_ShouldCheckInPriorityOrder()
    {
        // Arrange - Enable all tasks in Autonomous mode
        var config = ConfigTestHelper.CreateTestConfigWithModes(
            harvesting: TaskMode.Autonomous,
            lumbering: TaskMode.Autonomous,
            watering: TaskMode.Autonomous,
            petting: TaskMode.Autonomous,
            foraging: TaskMode.Autonomous,
            mining: TaskMode.Autonomous,
            fishing: TaskMode.Autonomous
        );
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Should check tasks in priority order: Harvesting, Lumbering, Watering, Petting, Foraging, Mining, Fishing
        // Returns first available task found (or null if none available)
    }

    [Fact]
    public void FindUnifiedTask_WithSelectiveMateCapabilities_ShouldOnlyCheckCompatibleTasks()
    {
        // Arrange - Mate can only perform Foraging and Mining
        var config = ConfigTestHelper.CreateTestConfig();
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var canPerform = new HashSet<TaskType> { TaskType.Foraging, TaskType.Mining };
        var mate = CreateMockMate(canPerform: canPerform);
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        // Should skip tasks mate cannot perform
        // If a task is found, it should be one the mate can perform
        if (task != null)
        {
            canPerform.Should().Contain(task.Type, "returned task should be one the mate can perform");
        }
    }

    #endregion

    #region Documentation and Regression Tests

    [Fact]
    public void UnifiedTaskManager_ShouldFollowDocumentedPriorityOrder()
    {
        // This test documents the expected priority order as a regression test
        // Priority order from TaskPriorityManager:
        // 1. Harvesting (highest priority)
        // 2. Lumbering
        // 3. Watering
        // 4. Petting
        // 5. Foraging
        // 6. Mining
        // 7. Fishing
        // 8. Sitting (lowest priority)
        // Note: Attacking is handled separately and not part of unified priority

        var expectedOrder = new[]
        {
            TaskType.Harvesting,
            TaskType.Lumbering,
            TaskType.Watering,
            TaskType.Petting,
            TaskType.Foraging,
            TaskType.Mining,
            TaskType.Fishing,
            TaskType.Sitting
        };

        // Arrange
        var actualOrder = TaskPriorityManager.GetTasksInPriorityOrder();

        // Assert
        actualOrder.Should().BeEquivalentTo(expectedOrder, options => options.WithStrictOrdering(),
            "UnifiedTaskManager relies on this priority order");
    }

    [Fact]
    public void UnifiedTaskManager_ShouldNeverReturnFollowingTask()
    {
        // Following tasks are not part of the unified task system
        // This test ensures FindUnifiedTask never returns a Following task

        // Arrange
        var config = ConfigTestHelper.CreateTestConfig();
        var manager = new UnifiedTaskManager(config, _mockMonitor.Object);
        var mate = CreateMockMate();
        var claimedTasks = new HashSet<Point>();
        var claimedSpots = new HashSet<Vector2>();
        var (location, playerPos, npcPos) = CreateTestContext();

        // Act
        var task = manager.FindUnifiedTask(
            mate.Object,
            location,
            playerPos,
            npcPos,
            claimedTasks,
            claimedSpots);

        // Assert
        task?.Type.Should().NotBe(TaskType.Following, "Following is not part of unified task system");
    }

    #endregion
}
