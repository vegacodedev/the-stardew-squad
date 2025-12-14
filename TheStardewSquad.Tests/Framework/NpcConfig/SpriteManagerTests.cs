using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Abstractions.Data;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.NpcConfig.Models;
using Xunit;

namespace TheStardewSquad.Tests.Framework.NpcConfig
{
    /// <summary>
    /// Tests for SpriteManager - manages sprite animations for NPCs during tasks.
    /// Implements first-match-wins logic for conditional sprite arrays.
    ///
    /// Based on specifications in:
    /// - [CP] TheStardewSquad.ContentAddons/docs/npcConfig.example.json
    /// - [CP] TheStardewSquad.ContentAddons/docs/NpcConfig Reference.md
    /// </summary>
    public class SpriteManagerTests
    {
        private (
            SpriteManager manager,
            Mock<NpcConfigManager> mockConfigManager,
            Mock<IMonitor> mockMonitor,
            Mock<IGameStateChecker> mockGameStateChecker,
            Mock<IGameContext> mockGameContext
        ) CreateTestContext()
        {
            var mockConfigManager = new Mock<NpcConfigManager>(
                Mock.Of<INpcConfigDataProvider>(),
                Mock.Of<IMonitor>()
            );
            var mockMonitor = new Mock<IMonitor>();
            var mockGameStateChecker = new Mock<IGameStateChecker>();
            var mockGameContext = new Mock<IGameContext>();

            var manager = new SpriteManager(
                mockConfigManager.Object,
                mockMonitor.Object,
                mockGameStateChecker.Object,
                mockGameContext.Object
            );

            return (manager, mockConfigManager, mockMonitor, mockGameStateChecker, mockGameContext);
        }

        private NPC CreateMockNpc(string name = "TestNpc")
        {
            // Create real NPC object instead of mocking - Character.Name is not virtual
            return new NPC { Name = name };
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldInitializeAllDependencies()
        {
            // Arrange & Act
            var (manager, _, _, _, _) = CreateTestContext();

            // Assert
            manager.Should().NotBeNull();
        }

        #endregion

        #region GetTaskSpriteConfig - Basic Tests

        [Fact]
        public void GetTaskSpriteConfig_ShouldReturnNull_WhenNoConfigDefined()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("TestNpc");

            var genericConfig = new NpcConfigData(); // No Sprites
            var npcConfig = new NpcConfigData();

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Attacking");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetTaskSpriteConfig_ShouldReturnNpcSpecific_WhenFound()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Abigail");

            var npcSpriteConfig = new JObject
            {
                ["FramesByDirection"] = new JObject
                {
                    ["Down"] = new JArray { 16, 17 },
                    ["Right"] = new JArray { 20, 21 }
                },
                ["FrameDuration"] = 300,
                ["Loop"] = true
            };

            var npcConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig
                {
                    Attacking = npcSpriteConfig
                }
            };

            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Attacking");

            // Assert
            result.Should().NotBeNull();
            result.FrameDuration.Should().Be(300);
            result.Loop.Should().BeTrue();
            result.FramesByDirection.Should().ContainKey("Down");
            result.FramesByDirection["Down"].Should().BeEquivalentTo(new[] { 16, 17 });
        }

        [Fact]
        public void GetTaskSpriteConfig_ShouldFallbackToGeneric_WhenNpcSpecificNotFound()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("TestNpc");

            var genericSpriteConfig = new JObject
            {
                ["FramesByDirection"] = new JObject
                {
                    ["Down"] = new JArray { 1, 0 },
                    ["Right"] = new JArray { 5, 4 }
                },
                ["FrameDuration"] = 400,
                ["Loop"] = false
            };

            var genericConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig
                {
                    Mining = genericSpriteConfig
                }
            };

            var npcConfig = new NpcConfigData(); // No NPC-specific sprites

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Mining");

            // Assert
            result.Should().NotBeNull();
            result.FrameDuration.Should().Be(400);
            result.Loop.Should().BeFalse();
            result.FramesByDirection["Down"].Should().BeEquivalentTo(new[] { 1, 0 });
        }

        [Fact]
        public void GetTaskSpriteConfig_ShouldPreferNpcSpecific_OverGeneric()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Abigail");

            var npcSpriteConfig = new JObject
            {
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 99, 100 } },
                ["FrameDuration"] = 250
            };

            var genericSpriteConfig = new JObject
            {
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 1, 0 } },
                ["FrameDuration"] = 400
            };

            var npcConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig { Fishing = npcSpriteConfig }
            };

            var genericConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig { Fishing = genericSpriteConfig }
            };

            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);
            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Fishing");

            // Assert - Should use NPC-specific (250ms), not Generic (400ms)
            result.Should().NotBeNull();
            result.FrameDuration.Should().Be(250);
            result.FramesByDirection["Down"].Should().BeEquivalentTo(new[] { 99, 100 });
        }

        #endregion

        #region GetTaskSpriteConfig - Task Type Tests

        [Theory]
        [InlineData("Attacking")]
        [InlineData("Mining")]
        [InlineData("Fishing")]
        [InlineData("Watering")]
        [InlineData("Lumbering")]
        [InlineData("Harvesting")]
        [InlineData("Foraging")]
        [InlineData("Idle")]
        [InlineData("Sitting")]
        [InlineData("Petting")]
        public void GetTaskSpriteConfig_ShouldHandleAllTaskTypes(string taskType)
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("TestNpc");

            var spriteConfig = new JObject
            {
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 1, 0 } },
                ["FrameDuration"] = 400
            };

            var genericConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig()
            };

            // Use reflection to set the property based on taskType
            var property = typeof(SpriteConfig).GetProperty(taskType);
            property?.SetValue(genericConfig.Sprites, spriteConfig);

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(new NpcConfigData());

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, taskType);

            // Assert
            result.Should().NotBeNull($"task type '{taskType}' should be supported");
            result.FrameDuration.Should().Be(400);
        }

        [Fact]
        public void GetTaskSpriteConfig_ShouldReturnNull_ForUnknownTaskType()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("TestNpc");

            var genericConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig()
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(new NpcConfigData());

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "InvalidTaskType");

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetTaskSpriteConfig - First-Match-Wins Tests

        [Fact]
        public void GetTaskSpriteConfig_ShouldReturnFirstMatch_FromConditionalArray()
        {
            // Arrange
            var (manager, mockConfigManager, _, mockGameStateChecker, mockGameContext) = CreateTestContext();
            var mockNpc = CreateMockNpc("Abigail");

            var firstEntry = new JObject
            {
                ["Condition"] = "PLAYER_COMBAT_LEVEL Current 5",
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 20, 21 } },
                ["FrameDuration"] = 300
            };

            var secondEntry = new JObject
            {
                ["Condition"] = "PLAYER_FARMING_LEVEL Current 3",
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 10, 11 } },
                ["FrameDuration"] = 500
            };

            var spriteArray = new JArray { firstEntry, secondEntry };

            var npcConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig
                {
                    Attacking = spriteArray
                }
            };

            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // First condition matches - should return immediately
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_COMBAT_LEVEL Current 5",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(true);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Attacking");

            // Assert - First match wins, even if second condition might also match
            result.Should().NotBeNull();
            result.FrameDuration.Should().Be(300);
            result.FramesByDirection["Down"].Should().BeEquivalentTo(new[] { 20, 21 });

            // Verify second condition was never evaluated (first-match-wins)
            mockGameStateChecker.Verify(g => g.CheckConditions(
                "PLAYER_FARMING_LEVEL Current 3",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()), Times.Never);
        }

        [Fact]
        public void GetTaskSpriteConfig_ShouldContinueToNextEntry_WhenFirstConditionFails()
        {
            // Arrange
            var (manager, mockConfigManager, _, mockGameStateChecker, mockGameContext) = CreateTestContext();
            var mockNpc = CreateMockNpc("TestNpc");

            var firstEntry = new JObject
            {
                ["Condition"] = "PLAYER_COMBAT_LEVEL Current 10",
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 30, 31 } },
                ["FrameDuration"] = 200
            };

            var secondEntry = new JObject
            {
                ["Condition"] = "PLAYER_FARMING_LEVEL Current 5",
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 40, 41 } },
                ["FrameDuration"] = 600
            };

            var spriteArray = new JArray { firstEntry, secondEntry };

            var npcConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig
                {
                    Watering = spriteArray
                }
            };

            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // First condition fails
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_COMBAT_LEVEL Current 10",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(false);

            // Second condition matches
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_FARMING_LEVEL Current 5",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(true);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Watering");

            // Assert
            result.Should().NotBeNull();
            result.FrameDuration.Should().Be(600);
            result.FramesByDirection["Down"].Should().BeEquivalentTo(new[] { 40, 41 });
        }

        [Fact]
        public void GetTaskSpriteConfig_ShouldReturnUnconditionalEntry_WhenNoConditionMatches()
        {
            // Arrange
            var (manager, mockConfigManager, _, mockGameStateChecker, mockGameContext) = CreateTestContext();
            var mockNpc = CreateMockNpc("TestNpc");

            var conditionalEntry = new JObject
            {
                ["Condition"] = "PLAYER_COMBAT_LEVEL Current 10",
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 50, 51 } },
                ["FrameDuration"] = 100
            };

            var fallbackEntry = new JObject
            {
                // No Condition = fallback
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 1, 0 } },
                ["FrameDuration"] = 400
            };

            var spriteArray = new JArray { conditionalEntry, fallbackEntry };

            var npcConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig
                {
                    Mining = spriteArray
                }
            };

            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Conditional entry fails
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_COMBAT_LEVEL Current 10",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(false);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Mining");

            // Assert - Should use fallback (no condition)
            result.Should().NotBeNull();
            result.FrameDuration.Should().Be(400);
            result.FramesByDirection["Down"].Should().BeEquivalentTo(new[] { 1, 0 });
        }

        [Fact]
        public void GetTaskSpriteConfig_ShouldReplaceCurrentNpcPlaceholder()
        {
            // Arrange
            var (manager, mockConfigManager, _, mockGameStateChecker, mockGameContext) = CreateTestContext();
            var mockNpc = CreateMockNpc("Penny");

            var conditionalEntry = new JObject
            {
                ["Condition"] = "PLAYER_NPC_RELATIONSHIP Current {CurrentNpc} Married",
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 60, 61 } },
                ["FrameDuration"] = 350
            };

            var spriteArray = new JArray { conditionalEntry };

            var npcConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig
                {
                    Idle = spriteArray
                }
            };

            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Verify that {CurrentNpc} gets replaced with "Penny"
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_NPC_RELATIONSHIP Current Penny Married",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(true);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Idle");

            // Assert - Should use spouse sprite because {CurrentNpc} was replaced with "Penny"
            result.Should().NotBeNull();
            result.FrameDuration.Should().Be(350);

            // Verify the condition was checked with "Penny" substituted
            mockGameStateChecker.Verify(g => g.CheckConditions(
                "PLAYER_NPC_RELATIONSHIP Current Penny Married",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()), Times.Once);
        }

        #endregion

        #region GetFramesForDirection Tests

        [Theory]
        [InlineData(0, "Up")]
        [InlineData(1, "Right")]
        [InlineData(2, "Down")]
        [InlineData(3, "Left")]
        public void GetFramesForDirection_ShouldReturnCorrectFrames_ForValidDirection(int facingDirection, string expectedKey)
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                FramesByDirection = new Dictionary<string, List<object>>
                {
                    { "Up", new List<object> { 9, 8 } },
                    { "Right", new List<object> { 5, 4 } },
                    { "Down", new List<object> { 1, 0 } },
                    { "Left", new List<object> { 13, 12 } }
                }
            };

            // Act
            var result = manager.GetFramesForDirection(config, facingDirection);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(config.FramesByDirection[expectedKey]);
        }

        [Fact]
        public void GetFramesForDirection_ShouldFallbackToDown_WhenDirectionNotFound()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                FramesByDirection = new Dictionary<string, List<object>>
                {
                    { "Down", new List<object> { 1, 0 } }
                    // Only Down defined
                }
            };

            // Act - Request Up direction (not defined)
            var result = manager.GetFramesForDirection(config, 0);

            // Assert - Should fallback to Down
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new[] { 1, 0 });
        }

        [Fact]
        public void GetFramesForDirection_ShouldReturnNull_WhenConfigIsNull()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            // Act
            var result = manager.GetFramesForDirection(null, 2);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetFramesForDirection_ShouldReturnNull_WhenFramesByDirectionIsNull()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                FramesByDirection = null
            };

            // Act
            var result = manager.GetFramesForDirection(config, 2);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetFramesForDirection_ShouldReturnNull_WhenNoDirectionsMatch()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                FramesByDirection = new Dictionary<string, List<object>>
                {
                    { "Up", new List<object> { 9, 8 } },
                    { "Right", new List<object> { 5, 4 } }
                    // No Down or other fallback
                }
            };

            // Act - Request Down direction (not defined, and Down fallback doesn't exist)
            var result = manager.GetFramesForDirection(config, 2);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetFramesForDirection_ShouldFallbackToDown_ForInvalidFacingDirection()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                FramesByDirection = new Dictionary<string, List<object>>
                {
                    { "Down", new List<object> { 1, 0 } },
                    { "Up", new List<object> { 9, 8 } }
                }
            };

            // Act - Invalid facing direction (99)
            var result = manager.GetFramesForDirection(config, 99);

            // Assert - Should fallback to Down
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(new[] { 1, 0 });
        }

        #endregion

        #region HasExtensionSheet Tests

        [Fact]
        public void HasExtensionSheet_ShouldReturnTrue_WhenExtensionSheetIsDefined()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                ExtensionSheet = "Characters\\Abigail_Attack"
            };

            // Act
            var result = manager.HasExtensionSheet(config);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasExtensionSheet_ShouldReturnFalse_WhenExtensionSheetIsNull()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                ExtensionSheet = null
            };

            // Act
            var result = manager.HasExtensionSheet(config);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasExtensionSheet_ShouldReturnFalse_WhenExtensionSheetIsEmpty()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                ExtensionSheet = ""
            };

            // Act
            var result = manager.HasExtensionSheet(config);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasExtensionSheet_ShouldReturnFalse_WhenExtensionSheetIsWhitespace()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            var config = new SpriteAnimationConfig
            {
                ExtensionSheet = "   "
            };

            // Act
            var result = manager.HasExtensionSheet(config);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasExtensionSheet_ShouldReturnFalse_WhenConfigIsNull()
        {
            // Arrange
            var (manager, _, _, _, _) = CreateTestContext();

            // Act
            var result = manager.HasExtensionSheet(null);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void GetTaskSpriteConfig_IntegrationTest_WithComplexConfiguration()
        {
            // Arrange
            var (manager, mockConfigManager, _, mockGameStateChecker, mockGameContext) = CreateTestContext();
            var mockNpc = CreateMockNpc("Abigail");

            // Complex configuration with multiple conditions
            var conditionalEntry1 = new JObject
            {
                ["Condition"] = "PLAYER_HAS_MOD abigail.combat.pack",
                ["FramesByDirection"] = new JObject
                {
                    ["Down"] = new JArray { 100, 101 },
                    ["Right"] = new JArray { 104, 105 }
                },
                ["ExtensionSheet"] = "Characters\\Abigail_Combat",
                ["FrameDuration"] = 250,
                ["Loop"] = true
            };

            var conditionalEntry2 = new JObject
            {
                ["Condition"] = "PLAYER_COMBAT_LEVEL Current 5",
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 20, 21 } },
                ["FrameDuration"] = 300
            };

            var fallbackEntry = new JObject
            {
                ["FramesByDirection"] = new JObject { ["Down"] = new JArray { 1, 0 } },
                ["FrameDuration"] = 400
            };

            var spriteArray = new JArray { conditionalEntry1, conditionalEntry2, fallbackEntry };

            var npcConfig = new NpcConfigData
            {
                Sprites = new SpriteConfig
                {
                    Attacking = spriteArray
                }
            };

            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // First condition fails, second condition matches
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_HAS_MOD abigail.combat.pack",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(false);

            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_COMBAT_LEVEL Current 5",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(true);

            // Act
            var result = manager.GetTaskSpriteConfig(mockNpc, "Attacking");

            // Assert - Should use second entry
            result.Should().NotBeNull();
            result.FrameDuration.Should().Be(300);
            result.FramesByDirection["Down"].Should().BeEquivalentTo(new[] { 20, 21 });
            result.ExtensionSheet.Should().BeNullOrEmpty();
        }

        #endregion
    }
}
