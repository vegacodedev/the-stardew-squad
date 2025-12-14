using FluentAssertions;
using Microsoft.Xna.Framework;
using TheStardewSquad.Framework;
using Xunit;

namespace TheStardewSquad.Tests.Framework
{
    /// <summary>
    /// Tests for TaskManager foraging task logic using the ILocationInfo wrapper pattern.
    /// These tests verify forage item and berry bush finding without requiring real game objects.
    /// </summary>
    public class TaskManagerForagingTests
    {
        #region Basic FindForageableTarget Tests

        [Fact]
        public void FindForageableTarget_ShouldReturnNull_WhenNoForageablesExist()
        {
            // Arrange
            var location = new MockLocationInfo();
            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("no forageable items or bushes exist");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindForageableTarget_ShouldFindSingleForageItem()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithForageableItem(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "forage item exists in search area");
            interactionPoint.Should().NotBeNull("should find adjacent passable neighbor");
        }

        [Fact]
        public void FindForageableTarget_ShouldFindBerryBush()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableBush(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "bushes are always collected when Foraging task is active");
            interactionPoint.Should().NotBeNull();
        }

        #endregion

        #region Mixed Forageable Types

        [Fact]
        public void FindForageableTarget_ShouldFindClosestTarget_WhenBothForageAndBushExist()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithForageableItem(12, 12)  // Distance from (10,8) = sqrt(20) ≈ 4.47
                .WithHarvestableBush(11, 9)  // Distance from (10,8) = sqrt(2)  ≈ 1.41 (closest!)
                .WithPassableRegion(9, 8, 4, 4);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 8); // NPC is south of search center
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 9), "should select the target closest to NPC (bush)");
        }

        [Fact]
        public void FindForageableTarget_ShouldPreferForage_WhenOnlyForageAccessible()
        {
            // Arrange
            var location = new MockLocationInfo(defaultPassable: false)
                .WithForageableItem(11, 11)
                .WithHarvestableBush(12, 12) // Inaccessible (no passable neighbors)
                .WithPassableRegion(10, 10, 3, 3); // Makes forage item area passable

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 11), "should find accessible forage item over inaccessible bush");
        }

        #endregion

        #region Claimed Tasks

        [Fact]
        public void FindForageableTarget_ShouldExcludeClaimedForageItem()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithForageableItem(12, 12)
                .WithForageableItem(13, 13)
                .WithPassableRegion(11, 11, 4, 4);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point> { new Point(12, 12) }; // Claim first item

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(13, 13), "should find unclaimed forage item");
        }

        [Fact]
        public void FindForageableTarget_ShouldExcludeClaimedBush()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableBush(12, 12)
                .WithHarvestableBush(13, 13)
                .WithPassableRegion(11, 11, 4, 4);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point> { new Point(12, 12) }; // Claim first bush

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(13, 13), "should find unclaimed bush");
        }

        [Fact]
        public void FindForageableTarget_ShouldReturnNull_WhenAllTargetsClaimed()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithForageableItem(12, 12)
                .WithHarvestableBush(13, 13)
                .WithPassableRegion(11, 11, 4, 4);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point> { new Point(12, 12), new Point(13, 13) };

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("all targets are claimed");
            interactionPoint.Should().BeNull();
        }

        #endregion

        #region Distance and Radius

        [Fact]
        public void FindForageableTarget_ShouldSortByDistanceToNpc()
        {
            // Arrange - Multiple targets at different distances from NPC
            var location = new MockLocationInfo()
                .WithForageableItem(15, 15)  // Distance from (10,8) = sqrt(74) ≈ 8.60
                .WithForageableItem(12, 12)  // Distance from (10,8) = sqrt(20) ≈ 4.47
                .WithForageableItem(11, 9)   // Distance from (10,8) = sqrt(2)  ≈ 1.41 (closest!)
                .WithPassableRegion(9, 8, 8, 8);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 8); // NPC is south of search center
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 9), "should select target closest to NPC");
        }

        [Fact]
        public void FindForageableTarget_ShouldRespectSearchRadius_ForForageItems()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithForageableItem(12, 12)  // Within radius
                .WithForageableItem(25, 25)  // Outside radius
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5; // Only first item is within radius
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should only find items within search radius");
        }

        [Fact]
        public void FindForageableTarget_ShouldRespectSearchRadius_ForBushes()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithHarvestableBush(12, 12)  // Within radius
                .WithHarvestableBush(25, 25)  // Outside radius
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5; // Only first bush is within radius
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should only find bushes within search radius");
        }

        #endregion

        #region Interaction Points

        [Fact]
        public void FindForageableTarget_ShouldFindAdjacentInteractionPoint()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithForageableItem(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(11, 12); // NPC is west of forage item
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12));
            interactionPoint.Should().NotBeNull("should find adjacent passable neighbor");

            // Verify interaction point is actually adjacent (Manhattan distance == 1)
            var distance = Math.Abs(interactionPoint!.Value.X - target!.Value.X) +
                          Math.Abs(interactionPoint.Value.Y - target.Value.Y);
            distance.Should().Be(1, "interaction point should be exactly one tile adjacent");
        }

        [Fact]
        public void FindForageableTarget_ShouldSkipInaccessibleTargets()
        {
            // Arrange - Two targets, one accessible, one not
            var location = new MockLocationInfo(defaultPassable: false)
                .WithForageableItem(12, 12) // Inaccessible (no passable neighbors)
                .WithForageableItem(15, 15) // Accessible
                .WithPassableRegion(14, 14, 3, 3); // Makes second item area passable

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(15, 15), "should find accessible target");
        }

        [Fact]
        public void FindForageableTarget_ShouldReturnNull_WhenAllTargetsInaccessible()
        {
            // Arrange - Targets exist but have no passable neighbors
            var location = new MockLocationInfo(defaultPassable: false)
                .WithForageableItem(12, 12)
                .WithHarvestableBush(13, 13);
            // No passable tiles

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("all targets are inaccessible");
            interactionPoint.Should().BeNull();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void FindForageableTarget_ShouldHandleEmptyClaimedSet()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithForageableItem(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>(); // Empty set

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().NotBeNull("should work with empty claimed set");
        }

        [Fact]
        public void FindForageableTarget_ShouldHandleMixedAccessibility()
        {
            // Arrange - Mix of accessible and inaccessible targets
            var location = new MockLocationInfo(defaultPassable: false)
                .WithForageableItem(5, 5) // Outside search radius (distance ~7.07 > 5)
                .WithHarvestableBush(11, 11) // Accessible and closest
                .WithForageableItem(12, 12) // Accessible but farther
                .WithPassableRegion(10, 10, 4, 4); // Makes area around (11,11) and (12,12) passable

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5; // (5,5) is outside this radius
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 11), "should find closest accessible target and exclude out-of-range items");
        }

        #endregion

        #region Animal Product Tests

        [Fact]
        public void FindForageableTarget_ShouldFindAnimalProduct()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithAnimalProduct(12, 12) // Egg, wool, truffle, etc.
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "animal products should be collected by foraging task");
            interactionPoint.Should().NotBeNull("should find adjacent passable neighbor");
        }

        [Fact]
        public void FindForageableTarget_ShouldFindClosestTarget_WhenForageAndAnimalProductExist()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithForageableItem(15, 15)  // Distance from (10,8) = sqrt(74) ≈ 8.60
                .WithAnimalProduct(11, 9)    // Distance from (10,8) = sqrt(2)  ≈ 1.41 (closest!)
                .WithPassableRegion(9, 8, 8, 8);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 8); // NPC is south of search center
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 9), "should select the target closest to NPC (animal product)");
        }

        [Fact]
        public void FindForageableTarget_ShouldHandleMixedForageAnimalProductsAndBushes()
        {
            // Arrange - Multiple types at different distances
            var location = new MockLocationInfo()
                .WithForageableItem(15, 15)   // Distance from (10,8) = sqrt(74) ≈ 8.60
                .WithAnimalProduct(12, 12)    // Distance from (10,8) = sqrt(20) ≈ 4.47
                .WithHarvestableBush(11, 9)   // Distance from (10,8) = sqrt(2)  ≈ 1.41 (closest!)
                .WithPassableRegion(9, 8, 8, 8);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 8);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 9), "should select closest target regardless of type");
        }

        [Fact]
        public void FindForageableTarget_ShouldExcludeClaimedAnimalProduct()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithAnimalProduct(12, 12)
                .WithAnimalProduct(13, 13)
                .WithPassableRegion(11, 11, 4, 4);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point> { new Point(12, 12) }; // Claim first animal product

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(13, 13), "should find unclaimed animal product");
        }

        [Fact]
        public void FindForageableTarget_ShouldRespectSearchRadius_ForAnimalProducts()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithAnimalProduct(12, 12)  // Within radius
                .WithAnimalProduct(25, 25)  // Outside radius
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5; // Only first animal product is within radius
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should only find animal products within search radius");
        }

        [Fact]
        public void FindForageableTarget_ShouldFindMultipleAnimalProducts()
        {
            // Arrange - Multiple animal products at different distances
            var location = new MockLocationInfo()
                .WithAnimalProduct(15, 15)  // Distance from (10,10) = sqrt(50) ≈ 7.07
                .WithAnimalProduct(12, 12)  // Distance from (10,10) = sqrt(8)  ≈ 2.83 (closest!)
                .WithAnimalProduct(14, 11)  // Distance from (10,10) = sqrt(17) ≈ 4.12
                .WithPassableRegion(9, 9, 8, 8);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should select closest animal product");
        }

        [Fact]
        public void FindForageableTarget_ShouldPreferAnimalProduct_WhenOnlyAnimalProductAccessible()
        {
            // Arrange
            var location = new MockLocationInfo(defaultPassable: false)
                .WithAnimalProduct(11, 11)      // Accessible
                .WithForageableItem(12, 12)     // Inaccessible (no passable neighbors)
                .WithPassableRegion(10, 10, 3, 3); // Makes animal product area passable

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 10;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindForageableTarget(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 11), "should find accessible animal product over inaccessible forage");
        }

        #endregion
    }
}
