using FluentAssertions;
using Microsoft.Xna.Framework;
using Moq;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Tests.Helpers;
using Xunit;

namespace TheStardewSquad.Tests.Framework.Squad;

/// <summary>
/// Tests for FormationManager using the IFarmerInfo wrapper pattern.
/// These tests demonstrate how the wrapper pattern enables testing of
/// previously untestable code without requiring real game objects.
/// </summary>
public class FormationManagerWrapperTests
{
    #region Formation Position Tests with IFarmerInfo

    [Fact]
    public void TryGetTargetTile_ShouldReturnFalse_ForUnassignedMate()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC", slotIndex: -1);
        var mockFarmer = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: 2);

        // Act
        var result = manager.TryGetTargetTile(mockMate.Object, mockFarmer.Object, out Point targetTile);

        // Assert
        result.Should().BeFalse("unassigned mates should not have a target tile");
        targetTile.Should().Be(default(Point));
    }

    [Fact]
    public void TryGetTargetTile_ShouldCalculatePositionBehindPlayer_WhenFacingDown()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC", slotIndex: 0);
        var mockFarmer = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: 2);

        // Act
        var result = manager.TryGetTargetTile(mockMate.Object, mockFarmer.Object, out Point targetTile);

        // Assert
        result.Should().BeTrue("assigned mate should have a target tile");
        targetTile.X.Should().Be(10, "X should be same as player when directly behind");
        targetTile.Y.Should().Be(11, "Y should be one tile behind player (south)");
    }

    [Fact]
    public void TryGetTargetTile_ShouldRotateFormation180Degrees_WhenFacingUp()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC", slotIndex: 0);
        var mockFarmer = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: 0);

        // Act
        var result = manager.TryGetTargetTile(mockMate.Object, mockFarmer.Object, out Point targetTile);

        // Assert
        result.Should().BeTrue();
        // First slot offset is (0, 1). When facing up, rotate 180°: (0, 1) → (0, -1)
        targetTile.X.Should().Be(10, "X should be same as player");
        targetTile.Y.Should().Be(9, "Y should be one tile behind player (north)");
    }

    [Fact]
    public void TryGetTargetTile_ShouldRotate90DegreesClockwise_WhenFacingRight()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC", slotIndex: 0);
        var mockFarmer = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: 1);

        // Act
        var result = manager.TryGetTargetTile(mockMate.Object, mockFarmer.Object, out Point targetTile);

        // Assert
        result.Should().BeTrue();
        // First slot offset is (0, 1). When facing right, rotate 90° CW: (0, 1) → (1, 0)
        targetTile.X.Should().Be(11, "X should be one tile behind player (west)");
        targetTile.Y.Should().Be(10, "Y should be same as player");
    }

    [Fact]
    public void TryGetTargetTile_ShouldRotate90DegreesCounterClockwise_WhenFacingLeft()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC", slotIndex: 0);
        var mockFarmer = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: 3);

        // Act
        var result = manager.TryGetTargetTile(mockMate.Object, mockFarmer.Object, out Point targetTile);

        // Assert
        result.Should().BeTrue();
        // First slot offset is (0, 1). When facing left, rotate 90° CCW: (0, 1) → (-1, 0)
        targetTile.X.Should().Be(9, "X should be one tile behind player (east)");
        targetTile.Y.Should().Be(10, "Y should be same as player");
    }

    [Theory]
    [InlineData(0, 10, 9)]   // Facing Up → behind is north
    [InlineData(1, 11, 10)]  // Facing Right → behind is west
    [InlineData(2, 10, 11)]  // Facing Down → behind is south
    [InlineData(3, 9, 10)]   // Facing Left → behind is east
    public void TryGetTargetTile_ShouldRotateCorrectly_ForAllFacingDirections(
        int facingDirection,
        int expectedX,
        int expectedY)
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC", slotIndex: 0);
        var mockFarmer = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: facingDirection);

        // Act
        var result = manager.TryGetTargetTile(mockMate.Object, mockFarmer.Object, out Point targetTile);

        // Assert
        result.Should().BeTrue();
        targetTile.Should().Be(new Point(expectedX, expectedY),
            $"formation should position mate behind player when facing direction {facingDirection}");
    }

    [Fact]
    public void TryGetTargetTile_ShouldPositionMultipleSquadMembers_InCorrectFormation()
    {
        // Arrange
        var manager = new FormationManager();
        // Formation slot offsets:
        // Slot 0: (0, 1) - directly behind
        // Slot 1: (-1, 1) - behind and to the left
        // Slot 2: (1, 1) - behind and to the right
        var mate1 = CreateMockSquadMate("NPC1", slotIndex: 0);
        var mate2 = CreateMockSquadMate("NPC2", slotIndex: 1);
        var mate3 = CreateMockSquadMate("NPC3", slotIndex: 2);

        var mockFarmer = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: 2); // Facing down

        // Act
        manager.TryGetTargetTile(mate1.Object, mockFarmer.Object, out Point tile1);
        manager.TryGetTargetTile(mate2.Object, mockFarmer.Object, out Point tile2);
        manager.TryGetTargetTile(mate3.Object, mockFarmer.Object, out Point tile3);

        // Assert - When facing down, formation spreads behind player
        //     [NPC2] [NPC1] [NPC3]
        //         Player↓
        tile1.Should().Be(new Point(10, 11), "NPC1 should be directly behind");
        tile2.Should().Be(new Point(9, 11), "NPC2 should be behind and left");
        tile3.Should().Be(new Point(11, 11), "NPC3 should be behind and right");
    }

    [Fact]
    public void TryGetTargetTile_ShouldMaintainRelativeFormationShape_WhenPlayerMoves()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC", slotIndex: 0);

        var position1 = CreateMockFarmerInfo(tileX: 5, tileY: 5, facing: 2);
        var position2 = CreateMockFarmerInfo(tileX: 20, tileY: 15, facing: 2);

        // Act
        manager.TryGetTargetTile(mockMate.Object, position1.Object, out Point tile1);
        manager.TryGetTargetTile(mockMate.Object, position2.Object, out Point tile2);

        // Assert - Formation offset should be consistent
        var offset1 = new Point(tile1.X - 5, tile1.Y - 5);
        var offset2 = new Point(tile2.X - 20, tile2.Y - 15);

        offset1.Should().Be(offset2, "formation offset should remain constant as player moves");
        offset1.Should().Be(new Point(0, 1), "offset should be (0, 1) when facing down");
    }

    [Fact]
    public void TryGetTargetTile_ShouldRotateFormation_WhenPlayerChangesFacingDirection()
    {
        // Arrange
        var manager = new FormationManager();
        var mockMate = CreateMockSquadMate("TestNPC", slotIndex: 0);

        // Player at same position but different facing directions
        var facingDown = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: 2);
        var facingUp = CreateMockFarmerInfo(tileX: 10, tileY: 10, facing: 0);

        // Act
        manager.TryGetTargetTile(mockMate.Object, facingDown.Object, out Point tileDown);
        manager.TryGetTargetTile(mockMate.Object, facingUp.Object, out Point tileUp);

        // Assert - Formation should be on opposite sides
        tileDown.Y.Should().BeGreaterThan(10, "when facing down, formation is south");
        tileUp.Y.Should().BeLessThan(10, "when facing up, formation is north");

        var distanceDown = Math.Abs(tileDown.Y - 10);
        var distanceUp = Math.Abs(tileUp.Y - 10);
        distanceDown.Should().Be(distanceUp, "formation should be same distance from player");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock IFarmerInfo for testing.
    /// This is much easier than trying to mock or create a real Farmer!
    /// </summary>
    private Mock<IFarmerInfo> CreateMockFarmerInfo(int tileX, int tileY, int facing)
    {
        var mock = new Mock<IFarmerInfo>();
        mock.Setup(f => f.TilePoint).Returns(new Point(tileX, tileY));
        mock.Setup(f => f.FacingDirection).Returns(facing);
        return mock;
    }

    /// <summary>
    /// Creates a mock squad mate with a specific formation slot.
    /// </summary>
    private Mock<ISquadMate> CreateMockSquadMate(string name, int slotIndex)
    {
        var mock = new Mock<ISquadMate>();
        mock.Setup(m => m.Name).Returns(name);
        mock.Setup(m => m.FormationSlotIndex).Returns(slotIndex);
        return mock;
    }

    #endregion
}
