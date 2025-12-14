using FluentAssertions;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using TheStardewSquad.Framework;
using Xunit;

namespace TheStardewSquad.Tests.Framework
{
    /// <summary>
    /// Tests for Mining task finding logic in TaskManager.
    /// Uses MockLocationInfo to simulate different mining scenarios without requiring a real GameLocation.
    /// </summary>
    public class TaskManagerMiningTests
    {
        #region Basic Rock Finding

        [Fact]
        public void FindMinableRock_WithSingleRegularStone_FindsStone()
        {
            // Arrange: Single regular stone with basic pickaxe
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0); // Basic pickaxe

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
            // Interaction spot should be adjacent to target
            var distance = Math.Abs(interaction!.Value.X - target.Value.X) + Math.Abs(interaction.Value.Y - target.Value.Y);
            distance.Should().Be(1);
        }

        [Fact]
        public void FindMinableRock_WithNoRocks_ReturnsNull()
        {
            // Arrange: No rocks in location
            var locationInfo = new MockLocationInfo()
                .WithPassableRegion(5, 5, 10, 10)
                .WithPlayerAt(10, 10)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 10);
            var npcPosition = new Point(10, 11);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithMultipleRocks_FindsClosestToNPC()
        {
            // Arrange: Multiple rocks at different distances from NPC
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 10) // 4 tiles from NPC
                .WithRegularStone(10, 15) // 9 tiles from NPC
                .WithPassableRegion(8, 5, 5, 12)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6); // Closer to (10,10)
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert - should find the closer rock
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindMinableRock_WithReadyForHarvestRock_SkipsRock()
        {
            // Arrange: Rock that is ready for harvest should be skipped
            var locationInfo = new MockLocationInfo()
                .WithReadyRock(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithMixedRocksIncludingReady_FindsValidRock()
        {
            // Arrange: Mix of ready and not-ready rocks
            var locationInfo = new MockLocationInfo()
                .WithReadyRock(10, 10)      // Should skip
                .WithRegularStone(10, 12)   // Should find this
                .WithPassableRegion(9, 9, 3, 5)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 12));
            interaction.Should().NotBeNull();
        }

        #endregion

        #region Pickaxe Level Requirements

        [Fact]
        public void FindMinableRock_WithMysticStone_RequiresGoldPickaxe()
        {
            // Arrange: Mystic Stone (46) requires pickaxe level >= 3
            var locationInfo = new MockLocationInfo()
                .WithMysticStone(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(2); // Steel pickaxe (not enough)

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert - cannot mine Mystic Stone with Steel pickaxe
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithMysticStoneAndGoldPickaxe_FindsStone()
        {
            // Arrange: Mystic Stone with Gold pickaxe (level 3)
            var locationInfo = new MockLocationInfo()
                .WithMysticStone(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(3); // Gold pickaxe

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindMinableRock_WithIridiumNode_RequiresSteelPickaxe()
        {
            // Arrange: Iridium Node (765) requires pickaxe level >= 2
            var locationInfo = new MockLocationInfo()
                .WithIridiumNode(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(1); // Copper pickaxe (not enough)

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert - cannot mine Iridium Node with Copper pickaxe
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithIridiumNodeAndSteelPickaxe_FindsNode()
        {
            // Arrange: Iridium Node with Steel pickaxe (level 2)
            var locationInfo = new MockLocationInfo()
                .WithIridiumNode(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(2); // Steel pickaxe

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindMinableRock_WithGemNode_RequiresCopperPickaxe()
        {
            // Arrange: Gem Node (2) requires pickaxe level >= 1
            var locationInfo = new MockLocationInfo()
                .WithGemNode(10, 10, gemType: 2)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0); // Basic pickaxe (not enough)

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert - cannot mine Gem Node with Basic pickaxe
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithGemNodeAndCopperPickaxe_FindsNode()
        {
            // Arrange: Gem Node with Copper pickaxe (level 1)
            var locationInfo = new MockLocationInfo()
                .WithGemNode(10, 10, gemType: 2)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(1); // Copper pickaxe

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindMinableRock_WithRegularStone_WorksWithAnyPickaxe()
        {
            // Arrange: Regular stone works with basic pickaxe
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0); // Basic pickaxe

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindMinableRock_WithMixedRockTypes_OnlyFindsBreakableOnes()
        {
            // Arrange: Mix of rocks with Steel pickaxe
            // Should find: regular stone, gem node, iridium node
            // Should skip: mystic stone (needs Gold)
            var locationInfo = new MockLocationInfo()
                .WithMysticStone(10, 10)      // Too hard (needs level 3)
                .WithIridiumNode(10, 12)      // Breakable (needs level 2)
                .WithGemNode(10, 14, gemType: 2) // Breakable (needs level 1)
                .WithRegularStone(10, 16)     // Breakable (needs level 0)
                .WithPassableRegion(9, 9, 3, 9)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(2); // Steel pickaxe

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert - should find the closest breakable rock (Iridium Node at 10, 12)
            target.Should().Be(new Point(10, 12));
            interaction.Should().NotBeNull();
        }

        #endregion

        #region Search Radius

        [Fact]
        public void FindMinableRock_WithRockAtEdgeOfRadius_FindsRock()
        {
            // Arrange: Rock at exactly 14 tiles from player (within radius of 15)
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 19) // 14 tiles from player at (10, 5)
                .WithPassableRegion(9, 5, 3, 16)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().NotBeNull();
            interaction.Should().NotBeNull();
        }

        [Fact]
        public void FindMinableRock_WithRockOutsideRadius_DoesNotFindRock()
        {
            // Arrange: Rock at 20 tiles from player (outside radius of 15)
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 25) // 20 tiles from player at (10, 5)
                .WithPassableRegion(9, 5, 3, 22)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_SearchesFromPlayerPosition_NotNPCPosition()
        {
            // Arrange: Rock close to player but NPC is far away
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 8) // 3 tiles from player
                .WithPassableRegion(9, 7, 3, 3)
                .WithPassableRegion(9, 29, 3, 3) // NPC area (far away)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 30); // 25 tiles from player (but rock is within 15 from player)
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert - should find rock (searched from player position, not NPC)
            target.Should().Be(new Point(10, 8));
        }

        #endregion

        #region Pathfinding Integration

        [Fact]
        public void FindMinableRock_WithInaccessibleRock_ReturnsNull()
        {
            // Arrange: Rock surrounded by impassable tiles
            var locationInfo = new MockLocationInfo(defaultPassable: false)
                .WithRegularStone(10, 10)
                .WithPassableTile(10, 6) // Only NPC position is passable
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithClaimedInteractionSpots_SkipsClaimedRock()
        {
            // Arrange: Rock with all interaction spots claimed
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>
            {
                new Vector2(9, 10),  // Left
                new Vector2(11, 10), // Right
                new Vector2(10, 9),  // Up
                new Vector2(10, 11)  // Down
            };

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithPartiallyClaimedSpots_FindsUnclaimedSpot()
        {
            // Arrange: Rock with some interaction spots claimed
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 10)
                .WithPassableRegion(9, 9, 3, 3)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>
            {
                new Vector2(9, 10),  // Left claimed
                new Vector2(11, 10)  // Right claimed
                // Up (10, 9) and Down (10, 11) are still available
            };

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 10));
            interaction.Should().NotBeNull();
            // Should find one of the unclaimed spots
            (interaction.Value == new Point(10, 9) || interaction.Value == new Point(10, 11)).Should().BeTrue();
        }

        [Fact]
        public void FindMinableRock_WithValidPath_ReturnsTargetAndInteraction()
        {
            // Arrange: Clear path from NPC to rock
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 15)
                .WithPassableRegion(8, 5, 5, 12)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().Be(new Point(10, 15));
            interaction.Should().NotBeNull();
            // Interaction spot should be adjacent to target
            var distance = Math.Abs(interaction!.Value.X - target.Value.X) + Math.Abs(interaction.Value.Y - target.Value.Y);
            distance.Should().Be(1);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void FindMinableRock_WithEmptyLocation_ReturnsNull()
        {
            // Arrange: No rocks at all
            var locationInfo = new MockLocationInfo()
                .WithPassableRegion(5, 5, 10, 10)
                .WithPlayerAt(10, 10)
                .SetPlayerPickaxeLevel(4); // Iridium pickaxe

            var playerPosition = new Point(10, 10);
            var npcPosition = new Point(10, 11);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithAllRocksTooHard_ReturnsNull()
        {
            // Arrange: All rocks require higher pickaxe level
            var locationInfo = new MockLocationInfo()
                .WithMysticStone(10, 10)      // Needs level 3
                .WithMysticStone(10, 12)      // Needs level 3
                .WithIridiumNode(10, 14)      // Needs level 2
                .WithPassableRegion(9, 9, 3, 7)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0); // Basic pickaxe

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            var claimedSpots = new HashSet<Vector2>();

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        [Fact]
        public void FindMinableRock_WithMultipleRocksButAllSpotsClaimed_ReturnsNull()
        {
            // Arrange: Multiple rocks but all their interaction spots are claimed
            var locationInfo = new MockLocationInfo()
                .WithRegularStone(10, 10)
                .WithRegularStone(10, 12)
                .WithPassableRegion(9, 9, 3, 5)
                .WithPlayerAt(10, 5)
                .SetPlayerPickaxeLevel(0);

            var playerPosition = new Point(10, 5);
            var npcPosition = new Point(10, 6);
            // Claim all spots around both rocks
            var claimedSpots = new HashSet<Vector2>
            {
                // Around (10, 10)
                new Vector2(9, 10), new Vector2(11, 10), new Vector2(10, 9), new Vector2(10, 11),
                // Around (10, 12)
                new Vector2(9, 12), new Vector2(11, 12), new Vector2(10, 11), new Vector2(10, 13)
            };

            // Act
            var (target, interaction) = TaskManager.FindMinableRock(
                locationInfo, playerPosition, npcPosition, 15, claimedSpots, null!);

            // Assert
            target.Should().BeNull();
            interaction.Should().BeNull();
        }

        #endregion
    }
}
