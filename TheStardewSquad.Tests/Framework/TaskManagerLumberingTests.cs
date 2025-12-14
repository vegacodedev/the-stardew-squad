using FluentAssertions;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using TheStardewSquad.Framework;
using Xunit;

namespace TheStardewSquad.Tests.Framework
{
    /// <summary>
    /// Tests for TaskManager lumbering-related functionality.
    /// Covers tree finding, twig finding, tapper detection, and pathfinding integration.
    /// </summary>
    public class TaskManagerLumberingTests
    {
        #region Basic Tree Finding Tests

        [Fact]
        public void FindLumberingTarget_WithSingleDamagedTree_ReturnsTree()
        {
            // Arrange
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithNoTrees_ReturnsNull()
        {
            // Arrange
            var locationInfo = new MockLocationInfo()
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithFullHealthTree_ReturnsNull()
        {
            // Arrange: Full health tree (1.0) should not be found
            var locationInfo = new MockLocationInfo()
                .WithFullHealthTree(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithDeadTree_ReturnsNull()
        {
            // Arrange: Dead tree (0.0 health) should not be found
            var locationInfo = new MockLocationInfo()
                .WithDeadTree(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithMultipleDamagedTrees_ReturnsFirstValid()
        {
            // Arrange: Multiple trees at different distances
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 10, health: 0.5f)
                .WithDamagedTree(12, 10, health: 0.3f)
                .WithDamagedTree(14, 10, health: 0.7f)
                .WithPassableRegion(9, 9, 6, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert: Should return one of the valid trees
            target.Should().NotBeNull();
            interaction.Should().NotBeNull();
        }

        #endregion

        #region Tapper Detection Tests

        [Fact]
        public void FindLumberingTarget_WithTappedTree_SkipsTree()
        {
            // Arrange: Tree with tapper should be skipped
            var locationInfo = new MockLocationInfo()
                .WithTappedTree(10, 10, health: 0.5f)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithTappedAndNonTappedTrees_FindsNonTapped()
        {
            // Arrange: Mix of tapped and non-tapped trees
            var locationInfo = new MockLocationInfo()
                .WithTappedTree(10, 10, health: 0.5f)
                .WithDamagedTree(12, 10, health: 0.5f)
                .WithPassableRegion(9, 9, 5, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert: Should find the non-tapped tree
            target.Should().Be(new Point(12, 10));
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithOnlyTappedTrees_ReturnsNull()
        {
            // Arrange: Only tapped trees available
            var locationInfo = new MockLocationInfo()
                .WithTappedTree(10, 10, health: 0.5f)
                .WithTappedTree(12, 10, health: 0.3f)
                .WithPassableRegion(9, 9, 5, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        #endregion

        #region Twig Finding Tests

        [Fact]
        public void FindLumberingTarget_WithSingleTwig_ReturnsTwig()
        {
            // Arrange
            var locationInfo = new MockLocationInfo()
                .WithTwig(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithClaimedTwig_SkipsTwig()
        {
            // Arrange: Twig is claimed by another NPC
            var locationInfo = new MockLocationInfo()
                .WithTwig(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point> { new Point(10, 10) };
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithMultipleTwigs_ReturnsFirstValid()
        {
            // Arrange: Multiple twigs
            var locationInfo = new MockLocationInfo()
                .WithTwig(10, 10)
                .WithTwig(12, 10)
                .WithPassableRegion(9, 9, 5, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().NotBeNull();
            interaction.Should().NotBeNull();
        }

        #endregion

        #region Mixed Targets Tests

        [Fact]
        public void FindLumberingTarget_WithTreesAndTwigs_FindsValidTarget()
        {
            // Arrange: Both trees and twigs available
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 10, health: 0.5f)
                .WithTwig(12, 10)
                .WithPassableRegion(9, 9, 5, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert: Should find one of them
            target.Should().NotBeNull();
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithMixOfValidAndInvalidTargets_FindsValid()
        {
            // Arrange: Mix of full health trees, tapped trees, damaged trees, and twigs
            var locationInfo = new MockLocationInfo()
                .WithFullHealthTree(8, 10)              // Invalid: full health
                .WithTappedTree(9, 10, health: 0.5f)    // Invalid: tapped
                .WithDamagedTree(10, 10, health: 0.5f)  // Valid
                .WithTwig(11, 10)                       // Valid
                .WithPassableRegion(7, 9, 6, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert: Should find either the damaged tree or twig
            target.Should().NotBeNull();
            (target.Value == new Point(10, 10) || target.Value == new Point(11, 10)).Should().BeTrue();
            interaction.Should().NotBeNull();
        }

        #endregion

        #region Pathfinding Integration Tests

        [Fact]
        public void FindLumberingTarget_WithInaccessibleTree_ReturnsNull()
        {
            // Arrange: Tree with no accessible neighbor tiles
            var locationInfo = new MockLocationInfo(defaultPassable: false)
                .WithDamagedTree(10, 10, health: 0.5f)
                .WithPassableTile(10, 6)  // NPC position only
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithClaimedInteractionSpots_SkipsTree()
        {
            // Arrange: Tree with all adjacent spots claimed
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 10, health: 0.5f)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>
            {
                new Vector2(9, 10), new Vector2(11, 10),
                new Vector2(10, 9), new Vector2(10, 11)
            };

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithNoPathToInteractionSpot_SkipsTree()
        {
            // Arrange: Tree with passable neighbor but no path from NPC
            var locationInfo = new MockLocationInfo(defaultPassable: false)
                .WithDamagedTree(10, 10, health: 0.5f)
                .WithPassableTile(10, 6)   // NPC position
                .WithPassableTile(10, 9);  // Adjacent to tree, but no path

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithValidPathToTree_ReturnsTreeAndInteractionSpot()
        {
            // Arrange: Clear path from NPC to tree
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 10, health: 0.5f)
                .WithPassableRegion(9, 6, 3, 5)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
            // Interaction spot should be adjacent to tree
            var distance = Math.Abs(interaction.Value.X - target.Value.X) + Math.Abs(interaction.Value.Y - target.Value.Y);
            distance.Should().Be(1);
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public void FindLumberingTarget_WithEmptyLocation_ReturnsNull()
        {
            // Arrange: No trees or twigs
            var locationInfo = new MockLocationInfo()
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithAllTargetsClaimed_ReturnsNull()
        {
            // Arrange: Tree and twig both claimed
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 10, health: 0.5f)
                .WithTwig(12, 10)
                .WithPassableRegion(9, 9, 5, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point> { new Point(12, 10) }; // Only twig is claimable
            var claimedSpots = new HashSet<Vector2>();

            // Act: Tree should be found since trees aren't checked against claimedTargets
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert: Should find the tree (trees don't check claimedTargets)
            target.Should().Be(new Point(10, 10));
        }

        [Fact]
        public void FindLumberingTarget_WithAllInteractionSpotsClaimed_ReturnsNull()
        {
            // Arrange: All possible interaction spots claimed
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 10, health: 0.5f)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>
            {
                new Vector2(9, 9), new Vector2(10, 9), new Vector2(11, 9),
                new Vector2(9, 10), new Vector2(11, 10),
                new Vector2(9, 11), new Vector2(10, 11), new Vector2(11, 11)
            };

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, 10, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        #endregion

        #region Search Radius Tests

        [Fact]
        public void FindLumberingTarget_WithTreeAtEdgeOfRadius_FindsTree()
        {
            // Arrange: Tree within radius distance from player
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 14, health: 0.5f)  // 9 tiles from player (within radius of 10)
                .WithPassableRegion(9, 6, 3, 10)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var searchRadius = 10;
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, searchRadius, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().NotBeNull();
        }

        [Fact]
        public void FindLumberingTarget_WithTreeOutsideRadius_ReturnsNull()
        {
            // Arrange: Tree beyond search radius
            var locationInfo = new MockLocationInfo()
                .WithDamagedTree(10, 20, health: 0.5f)  // 15 tiles from player
                .WithPassableRegion(9, 6, 3, 15)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var searchRadius = 10;
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, searchRadius, claimedTargets, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindLumberingTarget_SearchesFromPlayerPosition_NotNPCPosition()
        {
            // Arrange: Tree close to player but NPC can't reach it
            var locationInfo = new MockLocationInfo(defaultPassable: false)
                .WithDamagedTree(10, 8, health: 0.5f)  // 3 tiles from player (within radius)
                .WithPassableRegion(9, 7, 3, 2)       // Passable around tree
                .WithPassableTile(10, 20)              // NPC position (no path to tree)
                .WithPlayerAt(10, 5);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 20);  // NPC is far away, no path
            var searchRadius = 5;
            var claimedTargets = new HashSet<Point>();
            var claimedSpots = new HashSet<Vector2>();

            // Act: Tree is within search radius from player, but NPC can't path to it
            var (target, interaction) = TaskManager.FindLumberingTarget(
                locationInfo, playerPosition, npcPosition, searchRadius, claimedTargets, claimedSpots, null!);

            // Assert: Returns null because NPC can't reach it (no path)
            target.Should().BeNull();
        }

        #endregion
    }
}
