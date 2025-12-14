using FluentAssertions;
using Microsoft.Xna.Framework;
using TheStardewSquad.Framework;
using Xunit;

namespace TheStardewSquad.Tests.Framework
{
    /// <summary>
    /// Tests for TaskManager harvesting task logic using the ILocationInfo wrapper pattern.
    /// These tests verify crop finding and trellis handling without requiring real game objects.
    /// </summary>
    public class TaskManagerHarvestingTests
    {
        #region FindHarvestableCrop Tests

        [Fact]
        public void FindHarvestableCrop_ShouldReturnNull_WhenNoCropsExist()
        {
            // Arrange
            var location = new MockLocationInfo();
            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("no harvestable crops exist in the search area");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindHarvestableCrop_ShouldFindSingleCrop()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(12, 12); // Within radius from search center

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().NotBeNull("a harvestable crop exists in the search area");
            target.Should().Be(new Point(12, 12));
            interactionPoint.Should().Be(new Point(12, 12), "non-trellis crops can be harvested from the same tile");
        }

        [Fact]
        public void FindHarvestableCrop_ShouldExcludeClaimedCrops()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(12, 12)
                .WithHarvestableCrop(13, 13);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point> { new Point(12, 12) }; // Claim the closer crop

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(13, 13), "should find the unclaimed crop");
        }

        [Fact]
        public void FindHarvestableCrop_ShouldReturnNull_WhenAllCropsAreClaimed()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(12, 12)
                .WithHarvestableCrop(13, 13);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point> { new Point(12, 12), new Point(13, 13) };

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("all crops are already claimed");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindHarvestableCrop_ShouldFindClosestCropToNpc()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(12, 12)  // Distance from (10,8) = sqrt(8)  ≈ 2.83
                .WithHarvestableCrop(15, 15)  // Distance from (10,8) = sqrt(74) ≈ 8.60
                .WithHarvestableCrop(11, 9);  // Distance from (10,8) = sqrt(2)  ≈ 1.41 (closest!)

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 8); // NPC is south of search center
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 9), "should select the crop closest to the NPC");
        }

        [Fact]
        public void FindHarvestableCrop_ShouldRespectSearchRadius()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(12, 12)  // Within radius 5
                .WithHarvestableCrop(20, 20); // Outside radius 5

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should only find crops within the search radius");
        }

        #endregion

        #region Trellis Crop Tests

        [Fact]
        public void FindHarvestableCrop_ShouldHandleTrellisCrop_WhenAdjacentTilesPassable()
        {
            // Arrange - Trellis at (12,12) with passable neighbors
            var location = new MockLocationInfo()
                .WithHarvestableCrop(12, 12, isTrellis: true)
                .WithPassableRegion(11, 11, 3, 3); // Make area around trellis passable

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 12); // NPC is west of the trellis
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should find the trellis crop");
            interactionPoint.Should().NotBeNull("trellis crops need an adjacent interaction point");
            interactionPoint.Should().NotBe(new Point(12, 12), "trellis interaction point should be adjacent, not on the crop itself");
        }

        [Fact]
        public void FindHarvestableCrop_ShouldSkipTrellis_WhenNoAdjacentPassableTiles()
        {
            // Arrange - Trellis completely surrounded by impassable tiles
            var location = new MockLocationInfo(defaultPassable: false)
                .WithHarvestableCrop(12, 12, isTrellis: true); // No passable neighbors

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("trellis with no adjacent passable tiles cannot be harvested");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindHarvestableCrop_ShouldPreferNonTrellisOverInaccessibleTrellis()
        {
            // Arrange
            var location = new MockLocationInfo(defaultPassable: false)
                .WithHarvestableCrop(12, 12, isTrellis: true)  // Inaccessible trellis
                .WithHarvestableCrop(13, 13, isTrellis: false) // Accessible normal crop
                .WithPassableTile(13, 13); // Only the normal crop is passable

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(13, 13), "should find the accessible normal crop instead of inaccessible trellis");
        }

        #endregion

        #region CheckForTrellis Tests

        [Fact]
        public void CheckForTrellis_ShouldReturnSameTileForNonTrellis()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(10, 10, isTrellis: false)
                .WithPassableRegion(9, 9, 3, 3);

            var targetTile = new Point(10, 10);
            var npcPosition = new Point(9, 9);

            // Act
            var (target, interactionPoint) = TaskManager.CheckForTrellis(location, targetTile, npcPosition);

            // Assert
            target.Should().Be(new Point(10, 10));
            interactionPoint.Should().Be(new Point(10, 10), "non-trellis crops use the same tile for interaction");
        }

        [Fact]
        public void CheckForTrellis_ShouldReturnAdjacentTileForTrellis()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(10, 10, isTrellis: true)
                .WithPassableRegion(9, 9, 3, 3);

            var targetTile = new Point(10, 10);
            var npcPosition = new Point(9, 10); // NPC is west of trellis

            // Act
            var (target, interactionPoint) = TaskManager.CheckForTrellis(location, targetTile, npcPosition);

            // Assert
            target.Should().Be(new Point(10, 10), "target should remain the trellis tile");
            interactionPoint.Should().NotBeNull();
            interactionPoint.Should().NotBe(new Point(10, 10), "interaction point should be adjacent to trellis");

            // Verify interaction point is actually adjacent (Manhattan distance == 1)
            var distance = Math.Abs(interactionPoint!.Value.X - targetTile.X) + Math.Abs(interactionPoint.Value.Y - targetTile.Y);
            distance.Should().Be(1, "interaction point should be exactly one tile adjacent");
        }

        [Fact]
        public void CheckForTrellis_ShouldReturnNullInteractionPoint_WhenNoPassableNeighbors()
        {
            // Arrange
            var location = new MockLocationInfo(defaultPassable: false)
                .WithHarvestableCrop(10, 10, isTrellis: true); // No passable neighbors

            var targetTile = new Point(10, 10);
            var npcPosition = new Point(9, 9);

            // Act
            var (target, interactionPoint) = TaskManager.CheckForTrellis(location, targetTile, npcPosition);

            // Assert
            target.Should().Be(new Point(10, 10));
            interactionPoint.Should().BeNull("trellis with no passable neighbors cannot be accessed");
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void FindHarvestableCrop_ShouldHandleEmptyClaimedSet()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(12, 12);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>(); // Empty set

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().NotBeNull("should work with empty claimed tasks set");
        }

        [Fact]
        public void FindHarvestableCrop_ShouldHandleZeroRadius()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(10, 10); // Exactly at search center

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 0; // Only search the center tile
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(10, 10), "should find crop at search center with radius 0");
        }

        [Fact]
        public void FindHarvestableCrop_ShouldHandleMultipleCropsAtSameDistance()
        {
            // Arrange - Two crops equidistant from NPC
            var location = new MockLocationInfo()
                .WithHarvestableCrop(11, 10) // Distance = 1 (east)
                .WithHarvestableCrop(10, 11); // Distance = 1 (south)

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().NotBeNull("should find one of the equidistant crops");
            // Either crop is valid - we don't specify which one should be chosen
            var validTargets = new[] { new Point(11, 10), new Point(10, 11) };
            validTargets.Should().Contain(target!.Value);
        }

        #endregion

        #region Beehouse Flower Protection Tests

        [Fact]
        public void FindHarvestableCrop_ShouldFilterFlowerNearBeehouse_WhenProtectionEnabled()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithFlowerCrop(12, 12) // Flower within 5 tiles of beehouse
                .WithBeehouse(10, 10);  // Beehouse at search center

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks, protectBeehouseFlowers: 5);

            // Assert
            target.Should().BeNull("flower within 5 tiles of beehouse should be filtered out when protection is enabled");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindHarvestableCrop_ShouldFindFlowerFarFromBeehouse_WhenProtectionEnabled()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithFlowerCrop(20, 20) // Flower more than 5 tiles from beehouse
                .WithBeehouse(10, 10);  // Beehouse

            var searchCenter = new Point(20, 20);
            var npcPosition = new Point(20, 20);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks, protectBeehouseFlowers: 5);

            // Assert
            target.Should().Be(new Point(20, 20), "flower more than 5 tiles from beehouse should not be filtered");
            interactionPoint.Should().Be(new Point(20, 20));
        }

        [Fact]
        public void FindHarvestableCrop_ShouldFindNonFlowerNearBeehouse_WhenProtectionEnabled()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableCrop(12, 12) // Non-flower crop within 5 tiles of beehouse
                .WithBeehouse(10, 10);       // Beehouse

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks, protectBeehouseFlowers: 5);

            // Assert
            target.Should().Be(new Point(12, 12), "non-flower crops should not be filtered even near beehouses");
            interactionPoint.Should().Be(new Point(12, 12));
        }

        [Fact]
        public void FindHarvestableCrop_ShouldFindFlowerNearBeehouse_WhenProtectionDisabled()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithFlowerCrop(12, 12) // Flower within 5 tiles of beehouse
                .WithBeehouse(10, 10);  // Beehouse

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks, protectBeehouseFlowers: 0);

            // Assert
            target.Should().Be(new Point(12, 12), "flower should be found when protection is disabled");
            interactionPoint.Should().Be(new Point(12, 12));
        }

        [Fact]
        public void FindHarvestableCrop_ShouldPreferNonFlowerOverFlower_WhenBothAvailable()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithFlowerCrop(11, 11)      // Flower closer to NPC but near beehouse
                .WithHarvestableCrop(13, 13) // Non-flower crop slightly farther
                .WithBeehouse(10, 10);       // Beehouse

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindHarvestableCrop(
                location, searchCenter, npcPosition, searchRadius, claimedTasks, protectBeehouseFlowers: 5);

            // Assert
            target.Should().Be(new Point(13, 13), "should find non-flower crop when flower is filtered out");
            interactionPoint.Should().Be(new Point(13, 13));
        }

        #endregion
    }
}
