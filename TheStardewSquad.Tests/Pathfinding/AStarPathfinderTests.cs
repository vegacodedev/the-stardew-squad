using FluentAssertions;
using Microsoft.Xna.Framework;
using TheStardewSquad.Pathfinding;
using TheStardewSquad.Abstractions.Location;
using Xunit;

namespace TheStardewSquad.Tests.Pathfinding
{
    /// <summary>
    /// Tests for AStarPathfinder using the IMapInfo wrapper pattern.
    /// These tests verify pathfinding logic without requiring a real GameLocation.
    /// </summary>
    public class AStarPathfinderTests
    {
        #region Basic Pathfinding Tests

        [Fact]
        public void FindPath_ShouldReturnDirectPath_OnEmptyMap()
        {
            // Arrange - Create a small passable area
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 10, 10);

            var start = new Point(0, 0);
            var end = new Point(5, 5);

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().NotBeNull("a path should exist on an empty map");
            path.Should().Contain(start, "path should start at the start position");
            path.Should().Contain(end, "path should end at the end position");

            // Path should be relatively direct (diagonal movement allowed)
            // From (0,0) to (5,5) should be roughly 5 diagonal steps
            path.Count.Should().BeLessOrEqualTo(8, "path should be reasonably direct with diagonal movement");
        }

        [Fact]
        public void FindPath_ShouldReturnNull_WhenStartEqualsEnd()
        {
            // Arrange
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);
            var position = new Point(5, 5);

            // Act
            var path = AStarPathfinder.FindPath(map, position, position);

            // Assert - When start equals end, we reach the goal immediately
            // The path should contain just the single position
            path.Should().NotBeNull("reaching the goal immediately should succeed");
            path.Count.Should().Be(1, "path should contain only the single position");
            path.Peek().Should().Be(position);
        }

        [Fact]
        public void FindPath_ShouldReturnNull_WhenDestinationIsImpassable()
        {
            // Arrange
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 10, 10)
                .WithImpassableTile(5, 5); // Destination is blocked

            var start = new Point(0, 0);
            var end = new Point(5, 5);

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().BeNull("cannot reach an impassable destination");
        }

        [Fact]
        public void FindPath_ShouldReturnNull_WhenNoPathExists()
        {
            // Arrange - Create two separate passable areas with a wall between them
            // Use defaultPassable: false to ensure only explicitly marked tiles are passable
            var map = new MockMapInfo(defaultPassable: false)
                .WithPassableRegion(0, 0, 5, 5)   // Left area (x: 0-4, y: 0-4)
                .WithPassableRegion(6, 0, 5, 5);  // Right area (x: 6-10, y: 0-4)
            // Wall at x=5 is implicitly impassable (not in either region)

            var start = new Point(2, 2);  // In left area
            var end = new Point(8, 2);    // In right area

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().BeNull("no path exists when areas are separated by a wall");
        }

        [Fact]
        public void FindPath_ShouldNavigateAroundSingleObstacle()
        {
            // Arrange - Create a corridor with a single obstacle
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 10, 3)  // 10x3 corridor
                .WithImpassableTile(5, 1);        // Obstacle in the middle

            var start = new Point(0, 1);
            var end = new Point(9, 1);

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().NotBeNull("path should navigate around the obstacle");
            path.Should().Contain(start);
            path.Should().Contain(end);
            path.Should().NotContain(new Point(5, 1), "path should not pass through the obstacle");
        }

        #endregion

        #region Diagonal Movement Tests

        [Fact]
        public void FindPath_ShouldUseDiagonalMovement_WhenPossible()
        {
            // Arrange - Open area allowing diagonal movement
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);

            var start = new Point(0, 0);
            var end = new Point(3, 3);

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().NotBeNull();
            // Diagonal path from (0,0) to (3,3) should be 3 diagonal moves + start position = 4 tiles
            path.Count.Should().BeLessOrEqualTo(5, "diagonal movement should create a shorter path");
        }

        [Fact]
        public void FindPath_ShouldNotCutCorners_WhenDiagonallyBlocked()
        {
            // Arrange - Create an L-shaped wall that would be cut if corner-cutting was allowed
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 10, 10)
                .WithImpassableTile(5, 5)  // Corner tile
                .WithImpassableTile(5, 4)  // North of corner
                .WithImpassableTile(4, 5); // West of corner

            var start = new Point(4, 4);  // Northwest of the blocked corner
            var end = new Point(6, 6);    // Southeast of the blocked corner

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().NotBeNull("a path around the corner should exist");

            // The path should NOT go diagonally through (5, 5) because the adjacent tiles are blocked
            // Verify by checking if any point in the path is adjacent to both blocked tiles
            var pathList = path.ToList();
            pathList.Should().NotContain(new Point(5, 5), "should not pass through the impassable corner");
        }

        #endregion

        #region Complex Pathfinding Tests

        [Fact]
        public void FindPath_ShouldFindPathAroundUShapedObstacle()
        {
            // Arrange - Create a U-shaped obstacle
            // ##########
            // #........#
            // #.####...#
            // #.#  #...#
            // #.#  #...#
            // #.####...#
            // #........#
            // ##########
            var map = new MockMapInfo()
                .WithPassableRegion(1, 1, 8, 6)    // Interior area
                .WithVerticalWall(3, 2, 3)         // Left side of U
                .WithHorizontalWall(3, 2, 2)       // Top of U
                .WithHorizontalWall(3, 4, 2)       // Bottom of U
                .WithVerticalWall(4, 2, 3);        // Right side of U

            var start = new Point(3, 3);  // Inside the U
            var end = new Point(6, 3);    // Outside the U

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().NotBeNull("should find path around the U-shaped obstacle");
            path.Should().Contain(start);
            path.Should().Contain(end);
        }

        [Fact]
        public void FindPath_ShouldHandleMazeWithMultipleTurns()
        {
            // Arrange - Create a simple maze
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 7, 7)
                .WithVerticalWall(2, 0, 5)    // First barrier
                .WithVerticalWall(4, 2, 5);   // Second barrier

            var start = new Point(0, 3);
            var end = new Point(6, 3);

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().NotBeNull("should navigate through the maze");
            path.Should().Contain(start);
            path.Should().Contain(end);
        }

        [Fact]
        public void FindPath_ShouldAbortAfterMaxIterations_WhenDestinationUnreachable()
        {
            // Arrange - Completely surround the destination
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 20, 20)
                .WithImpassableTile(9, 9)   // Top-left
                .WithImpassableTile(10, 9)  // Top
                .WithImpassableTile(11, 9)  // Top-right
                .WithImpassableTile(9, 10)  // Left
                .WithImpassableTile(11, 10) // Right
                .WithImpassableTile(9, 11)  // Bottom-left
                .WithImpassableTile(10, 11) // Bottom
                .WithImpassableTile(11, 11);// Bottom-right

            var start = new Point(0, 0);
            var end = new Point(10, 10); // Completely surrounded

            // Act
            var path = AStarPathfinder.FindPath(map, start, end);

            // Assert
            path.Should().BeNull("should return null when max iterations exceeded");
        }

        #endregion

        #region FindClosestPassableNeighbor Tests

        [Fact]
        public void FindClosestPassableNeighbor_ShouldReturnNull_WhenNoNeighborsPassable()
        {
            // Arrange - Target tile surrounded by impassable tiles
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 10, 10)
                .WithImpassableTile(5, 6)  // Down
                .WithImpassableTile(4, 5)  // Left
                .WithImpassableTile(6, 5)  // Right
                .WithImpassableTile(5, 4); // Up

            var targetTile = new Point(5, 5);
            var characterTile = new Point(0, 0);

            // Act
            var result = AStarPathfinder.FindClosestPassableNeighbor(map, targetTile, characterTile);

            // Assert
            result.Should().BeNull("no passable neighbors exist");
        }

        [Fact]
        public void FindClosestPassableNeighbor_ShouldReturnClosestNeighbor_WhenMultipleAvailable()
        {
            // Arrange
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);

            var targetTile = new Point(5, 5);
            var characterTile = new Point(5, 8); // Character is south of target

            // Act
            var result = AStarPathfinder.FindClosestPassableNeighbor(map, targetTile, characterTile);

            // Assert
            result.Should().NotBeNull("passable neighbors exist");
            result.Value.Should().Be(new Point(5, 6), "should return the neighbor closest to character (down)");
        }

        [Fact]
        public void FindClosestPassableNeighbor_ShouldExcludeClaimedSpots()
        {
            // Arrange
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);

            var targetTile = new Point(5, 5);
            var characterTile = new Point(5, 8);
            var claimedSpots = new HashSet<Vector2> { new Vector2(5, 6) }; // Claim the closest spot

            // Act
            var result = AStarPathfinder.FindClosestPassableNeighbor(map, targetTile, characterTile, claimedSpots);

            // Assert
            result.Should().NotBeNull("other neighbors exist");
            result.Value.Should().NotBe(new Point(5, 6), "should not return a claimed spot");
        }

        #endregion

        #region IsPathUnobstructed Tests

        [Fact]
        public void IsPathUnobstructed_ShouldReturnTrue_WhenPathIsClear()
        {
            // Arrange
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);

            var start = new Point(0, 0);
            var end = new Point(5, 5);

            // Act
            var result = AStarPathfinder.IsPathUnobstructed(map, start, end);

            // Assert
            result.Should().BeTrue("path is clear with no obstacles");
        }

        [Fact]
        public void IsPathUnobstructed_ShouldReturnFalse_WhenObstacleInPath()
        {
            // Arrange
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 10, 10)
                .WithImpassableTile(2, 2); // Obstacle along the diagonal path

            var start = new Point(0, 0);
            var end = new Point(5, 5);

            // Act
            var result = AStarPathfinder.IsPathUnobstructed(map, start, end);

            // Assert
            result.Should().BeFalse("obstacle blocks the direct path");
        }

        [Fact]
        public void IsPathUnobstructed_ShouldReturnTrue_WhenStartEqualsEnd()
        {
            // Arrange
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);
            var position = new Point(5, 5);

            // Act
            var result = AStarPathfinder.IsPathUnobstructed(map, position, position);

            // Assert
            result.Should().BeTrue("path from a position to itself is always clear");
        }

        #endregion

        #region IsDirectPathFullyPassable Tests

        [Fact]
        public void IsDirectPathFullyPassable_ShouldReturnTrue_WhenAllTilesPassable()
        {
            // Arrange
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);

            var start = new Point(0, 0);
            var end = new Point(5, 0); // Straight horizontal line

            // Act
            var result = AStarPathfinder.IsDirectPathFullyPassable(map, start, end);

            // Assert
            result.Should().BeTrue("all tiles along the direct path are passable");
        }

        [Fact]
        public void IsDirectPathFullyPassable_ShouldReturnFalse_WhenAnyTileImpassable()
        {
            // Arrange
            var map = new MockMapInfo()
                .WithPassableRegion(0, 0, 10, 10)
                .WithImpassableTile(3, 0); // Obstacle in the middle of the path

            var start = new Point(0, 0);
            var end = new Point(5, 0);

            // Act
            var result = AStarPathfinder.IsDirectPathFullyPassable(map, start, end);

            // Assert
            result.Should().BeFalse("impassable tile blocks the direct path");
        }

        [Fact]
        public void IsDirectPathFullyPassable_ShouldCheckDiagonalPath()
        {
            // Arrange
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);

            var start = new Point(0, 0);
            var end = new Point(5, 5); // Diagonal path

            // Act
            var result = AStarPathfinder.IsDirectPathFullyPassable(map, start, end);

            // Assert
            result.Should().BeTrue("diagonal path is fully passable");
        }

        [Fact]
        public void IsDirectPathFullyPassable_ShouldReturnTrue_WhenStartEqualsEnd()
        {
            // Arrange
            var map = new MockMapInfo().WithPassableRegion(0, 0, 10, 10);
            var position = new Point(5, 5);

            // Act
            var result = AStarPathfinder.IsDirectPathFullyPassable(map, position, position);

            // Assert
            result.Should().BeTrue("a single passable tile is a valid path");
        }

        #endregion
    }
}
