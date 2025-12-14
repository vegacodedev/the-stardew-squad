using FluentAssertions;
using Microsoft.Xna.Framework;
using TheStardewSquad.Framework;
using Xunit;

namespace TheStardewSquad.Tests.Framework
{
    /// <summary>
    /// Tests for TaskManager petting task logic using the ILocationInfo wrapper pattern.
    /// These tests verify animal and pet finding without requiring real game objects.
    /// </summary>
    public class TaskManagerPettingTests
    {
        #region Basic FindPettableAnimal Tests

        [Fact]
        public void FindPettableAnimal_ShouldReturnNull_WhenNoAnimalsExist()
        {
            // Arrange
            var location = new MockLocationInfo();
            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("no animals or pets exist");
            interactionPoint.Should().BeNull();
        }

        [Fact]
        public void FindPettableAnimal_ShouldFindSingleFarmAnimal()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "farm animal exists in search area");
            interactionPoint.Should().NotBeNull("should find adjacent passable neighbor");
        }

        [Fact]
        public void FindPettableAnimal_ShouldFindPlayerPet()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedPlayerPet(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "player pet exists in search area");
            interactionPoint.Should().NotBeNull("should find adjacent passable neighbor");
        }

        #endregion

        #region Mixed Animal Types

        [Fact]
        public void FindPettableAnimal_ShouldFindClosestAnimal_WhenMultipleExist()
        {
            // Arrange - Multiple animals at different distances from NPC
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(15, 15)  // Distance from (10,8) = sqrt(74) ≈ 8.60
                .WithUnpettedFarmAnimal(12, 12)  // Distance from (10,8) = sqrt(20) ≈ 4.47
                .WithUnpettedPlayerPet(11, 9)    // Distance from (10,8) = sqrt(2)  ≈ 1.41 (closest!)
                .WithPassableRegion(9, 8, 8, 8);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 8); // NPC is south of search center
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 9), "should select target closest to NPC (pet)");
        }

        [Fact]
        public void FindPettableAnimal_ShouldPreferFarmAnimal_WhenPetInaccessible()
        {
            // Arrange
            var location = new MockLocationInfo(defaultPassable: false)
                .WithUnpettedPlayerPet(5, 5)        // Closer but inaccessible (no passable neighbors)
                .WithUnpettedFarmAnimal(12, 12)     // Farther but accessible
                .WithPassableRegion(11, 11, 3, 3);  // Makes farm animal area passable, not pet

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should find accessible farm animal over inaccessible pet");
        }

        #endregion

        #region Claimed Tasks

        [Fact]
        public void FindPettableAnimal_ShouldExcludeClaimedFarmAnimal()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(12, 12)
                .WithUnpettedFarmAnimal(13, 13)
                .WithPassableRegion(11, 11, 4, 4);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point> { new Point(12, 12) }; // Claim first animal

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(13, 13), "should find unclaimed farm animal");
        }

        [Fact]
        public void FindPettableAnimal_ShouldExcludeClaimedPet()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedPlayerPet(12, 12)
                .WithUnpettedFarmAnimal(13, 13)
                .WithPassableRegion(11, 11, 4, 4);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point> { new Point(12, 12) }; // Claim pet

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(13, 13), "should find unclaimed farm animal");
        }

        [Fact]
        public void FindPettableAnimal_ShouldReturnNull_WhenAllAnimalsClaimed()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(12, 12)
                .WithUnpettedPlayerPet(13, 13)
                .WithPassableRegion(11, 11, 4, 4);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point> { new Point(12, 12), new Point(13, 13) };

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("all animals are claimed");
            interactionPoint.Should().BeNull();
        }

        #endregion

        #region Distance and Radius

        [Fact]
        public void FindPettableAnimal_ShouldSortByDistanceToNpc()
        {
            // Arrange - Multiple animals at different distances from NPC
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(15, 15)  // Distance from (10,8) = sqrt(74) ≈ 8.60
                .WithUnpettedFarmAnimal(12, 12)  // Distance from (10,8) = sqrt(20) ≈ 4.47
                .WithUnpettedFarmAnimal(11, 9)   // Distance from (10,8) = sqrt(2)  ≈ 1.41 (closest!)
                .WithPassableRegion(9, 8, 8, 8);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 8); // NPC is south of search center
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 9), "should select animal closest to NPC");
        }

        [Fact]
        public void FindPettableAnimal_ShouldRespectSearchRadius_ForFarmAnimals()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(12, 12)  // Within radius (distance ≈ 2.83)
                .WithUnpettedFarmAnimal(20, 20)  // Outside radius
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5; // Only first animal is within radius
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should only find animals within search radius");
        }

        [Fact]
        public void FindPettableAnimal_ShouldRespectSearchRadius_ForPet()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedPlayerPet(12, 12)  // Within radius
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5; // Pet is within radius
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should find pet within search radius");
        }

        [Fact]
        public void FindPettableAnimal_ShouldReturnNull_WhenPetOutsideRadius()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedPlayerPet(20, 20)  // Outside radius
                .WithPassableRegion(19, 19, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5; // Pet is outside radius
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("pet is outside search radius");
            interactionPoint.Should().BeNull();
        }

        #endregion

        #region Interaction Points

        [Fact]
        public void FindPettableAnimal_ShouldFindAdjacentInteractionPoint()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(11, 12); // NPC is west of animal
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
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
        public void FindPettableAnimal_ShouldSkipInaccessibleAnimals()
        {
            // Arrange - Two animals, one accessible, one not
            var location = new MockLocationInfo(defaultPassable: false)
                .WithUnpettedFarmAnimal(12, 12) // Inaccessible (no passable neighbors)
                .WithUnpettedFarmAnimal(15, 15) // Accessible
                .WithPassableRegion(14, 14, 3, 3); // Makes second animal area passable

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(15, 15), "should find accessible animal");
        }

        [Fact]
        public void FindPettableAnimal_ShouldReturnNull_WhenAllAnimalsInaccessible()
        {
            // Arrange - Animals exist but have no passable neighbors
            var location = new MockLocationInfo(defaultPassable: false)
                .WithUnpettedFarmAnimal(12, 12)
                .WithUnpettedPlayerPet(13, 13);
            // No passable tiles

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().BeNull("all animals are inaccessible");
            interactionPoint.Should().BeNull();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void FindPettableAnimal_ShouldHandleEmptyClaimedSet()
        {
            // Arrange
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>(); // Empty set

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().NotBeNull("should work with empty claimed set");
        }

        [Fact]
        public void FindPettableAnimal_ShouldHandleMixedAccessibility()
        {
            // Arrange - Mix of accessible and inaccessible animals
            var location = new MockLocationInfo(defaultPassable: false)
                .WithUnpettedFarmAnimal(11, 11) // Accessible (closest)
                .WithUnpettedFarmAnimal(5, 5)   // Outside radius
                .WithUnpettedPlayerPet(12, 12)  // Accessible but farther
                .WithPassableRegion(10, 10, 4, 4); // Makes (11,11) and (12,12) passable

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 5; // (5,5) is outside this radius
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(11, 11), "should find closest accessible animal within radius");
        }

        [Fact]
        public void FindPettableAnimal_ShouldHandleOnlyPetAvailable()
        {
            // Arrange - Only pet, no farm animals
            var location = new MockLocationInfo()
                .WithUnpettedPlayerPet(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should find pet when no farm animals exist");
        }

        [Fact]
        public void FindPettableAnimal_ShouldHandleOnlyFarmAnimalsAvailable()
        {
            // Arrange - Only farm animals, no pet
            var location = new MockLocationInfo()
                .WithUnpettedFarmAnimal(12, 12)
                .WithPassableRegion(11, 11, 3, 3);

            var searchCenter = new Point(10, 10);
            var npcPosition = new Point(10, 10);
            var searchRadius = 15;
            var claimedTasks = new HashSet<Point>();

            // Act
            var (target, interactionPoint) = TaskManager.FindPettableAnimal(
                location, searchCenter, npcPosition, searchRadius, claimedTasks);

            // Assert
            target.Should().Be(new Point(12, 12), "should find farm animal when no pet exists");
        }

        #endregion
    }
}
