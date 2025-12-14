using FluentAssertions;
using Microsoft.Xna.Framework;
using TheStardewSquad.Framework;
using Xunit;

namespace TheStardewSquad.Tests.Framework
{
    /// <summary>
    /// Tests for TaskManager watering task logic using the ILocationInfo wrapper pattern.
    /// These tests verify dry soil finding and trellis handling without requiring real game objects.
    /// </summary>
    public class TaskManagerWateringTests
    {
        #region FindWaterableTile Tests

        [Fact]
        public void FindWaterableTile_ShouldReturnNull_WhenNotFarmOrGreenhouse()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(false)
                .WithDryHoeDirt(10, 10);

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("watering only works in Farm or Greenhouse");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindWaterableTile_ShouldReturnNull_WhenNoDryHoeDirtExists()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true);
            // No dry HoeDirt added

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("no dry HoeDirt exists in the search area");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindWaterableTile_ShouldFindSingleDryTile()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(12, 12); // Within radius from NPC

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().NotBeNull("dry HoeDirt exists in search area");
            target.Should().Be(new Point(12, 12));
            interactionPoint.Should().Be(new Point(12, 12), "non-trellis crops can be watered from the same tile");
        }

        [Fact]
        public void FindWaterableTile_ShouldExcludeClaimedTiles()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(11, 11)
                .WithDryHoeDirt(12, 12);

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point> { new Point(11, 11) }; // Claim first tile

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should find the unclaimed dry tile");
        }

        [Fact]
        public void FindWaterableTile_ShouldReturnNull_WhenAllTilesAreClaimed()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(11, 11)
                .WithDryHoeDirt(12, 12);

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point> { new Point(11, 11), new Point(12, 12) };

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("all dry tiles are already claimed");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindWaterableTile_ShouldReturnFirstFound_NotClosest()
        {
            // Arrange - Watering uses nested loop order, not distance sorting
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(10, 11) // (0, +1) from NPC - will be found first by loop
                .WithDryHoeDirt(11, 10); // (+1, 0) from NPC - found later

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            // Loop order: for x in -5..5, for y in -5..5
            // So x=0, y=+1 (tile 10,11) is found before x=+1, y=0 (tile 11,10)
            target.Should().Be(new Point(10, 11), "should return first tile found by loop order");
        }

        [Fact]
        public void FindWaterableTile_ShouldRespectSearchRadius()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(12, 12)  // Within radius 5
                .WithDryHoeDirt(20, 20); // Outside radius 5

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should only find tiles within search radius");
        }

        [Fact]
        public void FindWaterableTile_ShouldSearchAroundNpcPosition_NotPlayer()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(15, 15); // Within radius of NPC, not player

            var npcPosition = new Point(15, 15);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(15, 15), "should search around NPC position");
        }

        #endregion

        #region Trellis Watering Tests

        [Fact]
        public void FindWaterableTile_ShouldHandleDryTrellisCrop_WhenAdjacentTilesPassable()
        {
            // Arrange - Dry trellis at (12,12) with passable neighbors
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(12, 12, isTrellis: true)
                .WithPassableRegion(11, 11, 3, 3); // Make area around trellis passable

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should find the dry trellis");
            interactionPoint.Should().NotBeNull("trellis crops need an adjacent interaction point");
            interactionPoint.Should().NotBe(new Point(12, 12), "trellis interaction point should be adjacent");
        }

        [Fact]
        public void FindWaterableTile_ShouldSkipDryTrellis_WhenNoAdjacentPassableTiles()
        {
            // Arrange - Dry trellis completely surrounded by impassable tiles
            var location = new MockLocationInfo(defaultPassable: false)
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(12, 12, isTrellis: true); // No passable neighbors

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("trellis with no adjacent passable tiles cannot be watered");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindWaterableTile_ShouldPreferNonTrellisOverInaccessibleTrellis()
        {
            // Arrange
            var location = new MockLocationInfo(defaultPassable: false)
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(11, 11, isTrellis: true)  // Inaccessible trellis
                .WithDryHoeDirt(13, 13, isTrellis: false) // Accessible normal tile
                .WithPassableTile(13, 13); // Only the normal tile is passable

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(13, 13), "should find the accessible normal tile");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void FindWaterableTile_ShouldHandleEmptyClaimedSet()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(12, 12);

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>(); // Empty set

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().NotBeNull("should work with empty claimed tasks set");
        }

        [Fact]
        public void FindWaterableTile_ShouldHandleZeroRadius()
        {
            // Arrange
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(10, 10); // Exactly at NPC position

            var npcPosition = new Point(10, 10);
            var searchRadius = 0; // Only search the NPC's tile
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(10, 10), "should find tile at NPC position with radius 0");
        }

        [Fact]
        public void FindWaterableTile_ShouldFindTileInGreenhouse()
        {
            // Arrange - Greenhouse should work like Farm
            var location = new MockLocationInfo()
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(12, 12);

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().NotBeNull("watering should work in greenhouse");
        }

        [Fact]
        public void FindWaterableTile_ShouldReturnNull_WhenMultipleDryTilesButAllTrellisCropsInaccessible()
        {
            // Arrange - Multiple dry tiles but all are inaccessible trellis
            var location = new MockLocationInfo(defaultPassable: false)
                .SetIsFarmOrGreenhouse(true)
                .WithDryHoeDirt(11, 11, isTrellis: true)
                .WithDryHoeDirt(12, 12, isTrellis: true)
                .WithDryHoeDirt(13, 13, isTrellis: true);
            // No passable neighbors for any trellis

            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindWaterableTile(
                location, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("all dry tiles are inaccessible trellis crops");
            interactionPoint.Should().BeNull();
        }

        #endregion
    }
}
