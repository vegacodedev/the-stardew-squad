using FluentAssertions;
using Microsoft.Xna.Framework;
using Moq;
using StardewValley;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Tests.Helpers;
using Xunit;

namespace TheStardewSquad.Tests.Framework.Squad;

/// <summary>
/// Unit tests for FormationManager - validates squad formation logic.
/// Demonstrates how to test classes that use interfaces with Moq.
/// </summary>
public class FormationManagerTests
{
    #region Slot Assignment Tests

    [Fact]
    public void AssignSlot_ShouldAssignFirstSlotToFirstMate()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC");

        // Act
        manager.AssignSlot(mockMate.Object);

        // Assert
        mockMate.VerifySet(m => m.FormationSlotIndex = 0, Times.Once,
            "the first squad mate should be assigned to slot 0");
    }

    [Fact]
    public void AssignSlot_ShouldAssignSequentialSlotsToMultipleMates()
    {
        // Arrange
        var manager = new FormationManager();
        var mate1 = CreateMockSquadMate("NPC1");
        var mate2 = CreateMockSquadMate("NPC2");
        var mate3 = CreateMockSquadMate("NPC3");

        // Act
        manager.AssignSlot(mate1.Object);
        manager.AssignSlot(mate2.Object);
        manager.AssignSlot(mate3.Object);

        // Assert
        mate1.VerifySet(m => m.FormationSlotIndex = 0, Times.Once);
        mate2.VerifySet(m => m.FormationSlotIndex = 1, Times.Once);
        mate3.VerifySet(m => m.FormationSlotIndex = 2, Times.Once);
    }

    [Fact]
    public void AssignSlot_ShouldNotReassignIfMateAlreadyHasSlot()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC");

        // Act - Assign twice
        manager.AssignSlot(mockMate.Object);
        manager.AssignSlot(mockMate.Object);

        // Assert - Should only be assigned once
        mockMate.VerifySet(m => m.FormationSlotIndex = It.IsAny<int>(), Times.Once,
            "attempting to assign a slot to an already-assigned mate should be ignored");
    }

    #endregion

    #region Slot Release Tests

    [Fact]
    public void ReleaseSlot_ShouldFreeUpSlotForReassignment()
    {
        // Arrange
        var manager = new FormationManager();
        var mate1 = CreateMockSquadMate("NPC1");
        var mate2 = CreateMockSquadMate("NPC2");

        manager.AssignSlot(mate1.Object);

        // Act - Release first mate's slot
        manager.ReleaseSlot(mate1.Object);

        // Assign second mate (should get slot 0 since it's now free)
        manager.AssignSlot(mate2.Object);

        // Assert
        mate1.VerifySet(m => m.FormationSlotIndex = -1, Times.Once,
            "released mate should have slot index set to -1");
        mate2.VerifySet(m => m.FormationSlotIndex = 0, Times.Once,
            "newly assigned mate should get the freed slot 0");
    }

    [Fact]
    public void ReleaseSlot_ShouldHandleReleasingUnassignedMate()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC");

        // Act - Release without assigning first
        manager.ReleaseSlot(mockMate.Object);

        // Assert - Should not throw or cause issues
        mockMate.VerifySet(m => m.FormationSlotIndex = It.IsAny<int>(), Times.Never,
            "releasing a mate that was never assigned should not change slot index");
    }

    #endregion

    #region Formation Position Tests

    // NOTE: TryGetTargetTile tests are commented out because they require mocking Farmer.TilePoint
    // and Farmer.FacingDirection, which are non-virtual properties and cannot be mocked with Moq.
    // These would require integration tests with actual game objects or a custom Farmer wrapper class.
    //
    // This demonstrates an important limitation in unit testing SMAPI mods:
    // Many game classes were not designed for testability and cannot be easily mocked.
    // For comprehensive testing of formation positioning, consider:
    // 1. Integration tests with real game objects
    // 2. Creating wrapper interfaces for game classes
    // 3. Manual/exploratory testing in-game

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ShouldClearAllSlotAssignments()
    {
        // Arrange
        var manager = new FormationManager();
        var mate1 = CreateMockSquadMate("NPC1");
        var mate2 = CreateMockSquadMate("NPC2");

        manager.AssignSlot(mate1.Object);
        manager.AssignSlot(mate2.Object);

        // Act
        manager.Reset();

        // Now assign a new mate - should get slot 0 again
        var mate3 = CreateMockSquadMate("NPC3");
        manager.AssignSlot(mate3.Object);

        // Assert
        mate3.VerifySet(m => m.FormationSlotIndex = 0, Times.Once,
            "after reset, the first assigned mate should get slot 0");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock squad mate for testing.
    /// </summary>
    private Mock<ISquadMate> CreateMockSquadMate(string name)
    {
        var mock = new Mock<ISquadMate>();
        mock.Setup(m => m.Name).Returns(name);
        mock.SetupProperty(m => m.FormationSlotIndex, -1); // Default to unassigned
        return mock;
    }

    #endregion
}
