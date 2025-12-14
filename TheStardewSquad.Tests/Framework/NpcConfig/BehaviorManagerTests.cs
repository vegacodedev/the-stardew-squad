using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Abstractions.Data;
using TheStardewSquad.Abstractions.Utilities;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.NpcConfig.Models;
using TheStardewSquad.Framework.Squad;
using Xunit;

namespace TheStardewSquad.Tests.Framework.NpcConfig
{
    /// <summary>
    /// Tests for BehaviorManager - manages idle animations, task restrictions, and recruitment.
    /// Tests pool-all-matches for idle animations and first-match-wins for allowed tasks.
    ///
    /// Based on specifications in:
    /// - [CP] TheStardewSquad.ContentAddons/docs/npcConfig.example.json
    /// - [CP] TheStardewSquad.ContentAddons/docs/NpcConfig Reference.md
    /// </summary>
    public class BehaviorManagerTests
    {
        private (
            BehaviorManager manager,
            Mock<NpcConfigManager> mockConfigManager,
            Mock<INpcConfigDataProvider> mockDataProvider,
            Mock<IMonitor> mockMonitor,
            Mock<IRandomSelector> mockRandomSelector,
            Mock<IGameStateChecker> mockGameStateChecker,
            Mock<IGameContext> mockGameContext,
            Mock<INpcSpriteService> mockNpcSpriteService,
            Mock<INpcDialogueService> mockNpcDialogueService
        ) CreateTestContext()
        {
            var mockConfigManager = new Mock<NpcConfigManager>(
                Mock.Of<INpcConfigDataProvider>(),
                Mock.Of<IMonitor>()
            );
            var mockDataProvider = new Mock<INpcConfigDataProvider>();
            var mockMonitor = new Mock<IMonitor>();
            var mockRandomSelector = new Mock<IRandomSelector>();
            var mockGameStateChecker = new Mock<IGameStateChecker>();
            var mockGameContext = new Mock<IGameContext>();
            var mockNpcSpriteService = new Mock<INpcSpriteService>();
            var mockNpcDialogueService = new Mock<INpcDialogueService>();

            var manager = new BehaviorManager(
                mockConfigManager.Object,
                mockDataProvider.Object,
                mockMonitor.Object,
                mockRandomSelector.Object,
                mockGameStateChecker.Object,
                mockGameContext.Object,
                mockNpcSpriteService.Object,
                mockNpcDialogueService.Object
            );

            return (manager, mockConfigManager, mockDataProvider, mockMonitor, mockRandomSelector,
                mockGameStateChecker, mockGameContext, mockNpcSpriteService, mockNpcDialogueService);
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
            var (manager, _, _, _, _, _, _, _, _) = CreateTestContext();

            // Assert
            manager.Should().NotBeNull();
        }

        #endregion

        #region Idle Animations - Pool-All-Matches Tests

        [Fact]
        public void GetRandomIdleAnimation_ShouldReturnNull_WhenNoAnimationsDefined()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("TestNpc");

            var genericConfig = new NpcConfigData();
            var npcConfig = new NpcConfigData();

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetRandomIdleAnimation(mockNpc);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetRandomIdleAnimation_ShouldReturnAnimation_FromSimpleStringFormat()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, mockRandomSelector, _, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Abigail");

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object>
                    {
                        "abigail_read"
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            var expectedSpec = new IdleAnimationSpec { Id = "abigail_read", Loop = true };
            mockRandomSelector.Setup(r => r.ChooseFrom(It.IsAny<List<IdleAnimationSpec>>()))
                .Returns(expectedSpec);

            // Act
            var result = manager.GetRandomIdleAnimation(mockNpc);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be("abigail_read");
            result.Loop.Should().BeTrue();
        }

        [Fact]
        public void GetRandomIdleAnimation_ShouldReturnAnimation_FromObjectFormat()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, mockRandomSelector, _, _, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Shane");

            var animationSpec = new JObject
            {
                ["Id"] = "shane_drink",
                ["Loop"] = false
            };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { animationSpec }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            var expectedSpec = new IdleAnimationSpec { Id = "shane_drink", Loop = false };
            mockRandomSelector.Setup(r => r.ChooseFrom(It.IsAny<List<IdleAnimationSpec>>()))
                .Returns(expectedSpec);

            // Act
            var result = manager.GetRandomIdleAnimation(mockNpc);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be("shane_drink");
            result.Loop.Should().BeFalse();
        }

        [Fact]
        public void GetRandomIdleAnimation_ShouldPoolAllMatchingConditionalAnimations()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, mockRandomSelector, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Penny");

            // Both conditions will match
            var conditionalEntry1 = new JObject
            {
                ["Condition"] = "PLAYER_HAS_MAIL Current pennyHome",
                ["Animations"] = new JArray { "penny_read", "penny_teach" }
            };

            var conditionalEntry2 = new JObject
            {
                ["Condition"] = "SEASON Spring",
                ["Animations"] = new JArray { "penny_garden" }
            };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object>
                    {
                        conditionalEntry1,
                        conditionalEntry2
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Both conditions match
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_HAS_MAIL Current pennyHome",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(true);

            mockGameStateChecker.Setup(g => g.CheckConditions(
                "SEASON Spring",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(true);

            List<IdleAnimationSpec> capturedPool = null;
            mockRandomSelector.Setup(r => r.ChooseFrom(It.IsAny<IList<IdleAnimationSpec>>()))
                .Callback<IList<IdleAnimationSpec>>(pool => capturedPool = new List<IdleAnimationSpec>(pool))
                .Returns(new IdleAnimationSpec { Id = "penny_read", Loop = true });

            // Act
            var result = manager.GetRandomIdleAnimation(mockNpc);

            // Assert - All 3 animations from both matching conditions should be pooled
            capturedPool.Should().NotBeNull();
            capturedPool.Should().HaveCount(3);
            capturedPool.Select(a => a.Id).Should().Contain(new[] { "penny_read", "penny_teach", "penny_garden" });
        }

        [Fact]
        public void GetRandomIdleAnimation_ShouldExcludeNonMatchingConditions()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, mockRandomSelector, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Emily");

            var matchingEntry = new JObject
            {
                ["Condition"] = "SEASON Summer",
                ["Animations"] = new JArray { "emily_dance" }
            };

            var nonMatchingEntry = new JObject
            {
                ["Condition"] = "SEASON Winter",
                ["Animations"] = new JArray { "emily_sew" }
            };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object>
                    {
                        matchingEntry,
                        nonMatchingEntry
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Only Summer condition matches
            mockGameStateChecker.Setup(g => g.CheckConditions("SEASON Summer", It.IsAny<GameLocation>(), It.IsAny<Farmer>()))
                .Returns(true);
            mockGameStateChecker.Setup(g => g.CheckConditions("SEASON Winter", It.IsAny<GameLocation>(), It.IsAny<Farmer>()))
                .Returns(false);

            List<IdleAnimationSpec> capturedPool = null;
            mockRandomSelector.Setup(r => r.ChooseFrom(It.IsAny<IList<IdleAnimationSpec>>()))
                .Callback<IList<IdleAnimationSpec>>(pool => capturedPool = new List<IdleAnimationSpec>(pool))
                .Returns(new IdleAnimationSpec { Id = "emily_dance", Loop = true });

            // Act
            var result = manager.GetRandomIdleAnimation(mockNpc);

            // Assert - Only matching condition's animations should be pooled
            capturedPool.Should().NotBeNull();
            capturedPool.Should().HaveCount(1);
            capturedPool.Select(a => a.Id).Should().Contain("emily_dance");
            capturedPool.Select(a => a.Id).Should().NotContain("emily_sew");
        }

        [Fact]
        public void GetRandomIdleAnimation_ShouldIncludeUnconditionalEntriesInPool()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, mockRandomSelector, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Harvey");

            var unconditionalEntry = new JObject
            {
                ["Animations"] = new JArray { "harvey_clipboard" } // No Condition = always included
            };

            var conditionalEntry = new JObject
            {
                ["Condition"] = "SEASON Fall",
                ["Animations"] = new JArray { "harvey_examine" }
            };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object>
                    {
                        "harvey_coffee", // Simple string - always included
                        unconditionalEntry,
                        conditionalEntry
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            mockGameStateChecker.Setup(g => g.CheckConditions("SEASON Fall", It.IsAny<GameLocation>(), It.IsAny<Farmer>()))
                .Returns(true);

            List<IdleAnimationSpec> capturedPool = null;
            mockRandomSelector.Setup(r => r.ChooseFrom(It.IsAny<IList<IdleAnimationSpec>>()))
                .Callback<IList<IdleAnimationSpec>>(pool => capturedPool = new List<IdleAnimationSpec>(pool))
                .Returns(new IdleAnimationSpec { Id = "harvey_coffee", Loop = true });

            // Act
            var result = manager.GetRandomIdleAnimation(mockNpc);

            // Assert - All animations should be pooled (2 unconditional + 1 conditional match)
            capturedPool.Should().NotBeNull();
            capturedPool.Should().HaveCount(3);
            capturedPool.Select(a => a.Id).Should().Contain(new[] { "harvey_coffee", "harvey_clipboard", "harvey_examine" });
        }

        #endregion

        #region PlayIdleAnimation Tests

        [Fact]
        public void PlayIdleAnimation_ShouldSetInfiniteCooldown_ForLoopingAnimations()
        {
            // Arrange
            var (manager, _, mockDataProvider, _, _, _, _, mockNpcSpriteService, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Abigail");
            var mockMate = new Mock<ISquadMate>();
            mockMate.Setup(m => m.Npc).Returns(mockNpc);

            var animationSpec = new IdleAnimationSpec { Id = "abigail_read", Loop = true };

            var animationDescriptions = new Dictionary<string, string>
            {
                { "abigail_read", "16 17 18/19 20 21/" }
            };

            mockDataProvider.Setup(d => d.LoadAnimationDescriptions()).Returns(animationDescriptions);
            mockNpcSpriteService.Setup(s => s.GetFacingDirection(It.IsAny<NPC>())).Returns(2); // Down

            // Act
            manager.PlayIdleAnimation(mockMate.Object, animationSpec);

            // Assert
            mockMate.VerifySet(m => m.IsAnimating = true, Times.Once);
            mockMate.VerifySet(m => m.ActionCooldown = int.MaxValue, Times.Once); // Infinite cooldown
        }

        [Fact]
        public void PlayIdleAnimation_ShouldCalculateCooldown_ForNonLoopingAnimations()
        {
            // Arrange
            var (manager, _, mockDataProvider, _, _, _, _, mockNpcSpriteService, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Shane");
            var mockMate = new Mock<ISquadMate>();
            mockMate.Setup(m => m.Npc).Returns(mockNpc);

            var animationSpec = new IdleAnimationSpec { Id = "shane_drink", Loop = false };

            // Animation with 3 frames at 200ms each = 600ms total
            // Expected cooldown: (600 / 16.666) + 2 = ~38 ticks
            var animationDescriptions = new Dictionary<string, string>
            {
                { "shane_drink", "10 11/12/13" }
            };

            mockDataProvider.Setup(d => d.LoadAnimationDescriptions()).Returns(animationDescriptions);
            mockNpcSpriteService.Setup(s => s.GetFacingDirection(It.IsAny<NPC>())).Returns(2);

            int capturedCooldown = 0;
            mockMate.SetupSet(m => m.ActionCooldown = It.IsAny<int>())
                .Callback<int>(value => capturedCooldown = value);

            // Act
            manager.PlayIdleAnimation(mockMate.Object, animationSpec);

            // Assert
            mockMate.VerifySet(m => m.IsAnimating = true, Times.Once);
            capturedCooldown.Should().BeGreaterThan(0);
            capturedCooldown.Should().BeLessThan(int.MaxValue); // Not infinite
        }

        [Fact]
        public void PlayIdleAnimation_ShouldLogWarning_WhenAnimationNotFound()
        {
            // Arrange
            var (manager, _, mockDataProvider, mockMonitor, _, _, _, mockNpcSpriteService, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("TestNpc");
            var mockMate = new Mock<ISquadMate>();
            mockMate.Setup(m => m.Npc).Returns(mockNpc);

            var animationSpec = new IdleAnimationSpec { Id = "unknown_animation", Loop = true };

            mockDataProvider.Setup(d => d.LoadAnimationDescriptions()).Returns(new Dictionary<string, string>());

            // Act
            manager.PlayIdleAnimation(mockMate.Object, animationSpec);

            // Assert
            mockMonitor.Verify(m => m.Log(
                It.Is<string>(s => s.Contains("Could not find animation 'unknown_animation'")),
                LogLevel.Warn), Times.Once);
        }

        #endregion

        #region GetIdleFrame Tests

        [Fact]
        public void GetIdleFrame_ShouldReturnCorrectFrame_ForAllDirections()
        {
            // Arrange & Act & Assert
            BehaviorManager.GetIdleFrame(0).Should().Be(8);  // Up
            BehaviorManager.GetIdleFrame(1).Should().Be(4);  // Right
            BehaviorManager.GetIdleFrame(2).Should().Be(0);  // Down
            BehaviorManager.GetIdleFrame(3).Should().Be(12); // Left
        }

        [Fact]
        public void GetIdleFrame_ShouldReturnDown_ForInvalidDirection()
        {
            // Arrange & Act & Assert
            BehaviorManager.GetIdleFrame(-1).Should().Be(0); // Invalid -> Down
            BehaviorManager.GetIdleFrame(99).Should().Be(0); // Invalid -> Down
        }

        #endregion

        #region Allowed Tasks - First-Match-Wins Tests

        [Fact]
        public void GetAllowedTasks_ShouldReturnDefaultTasks_WhenNoConfigDefined()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData(); // No Behavior defined

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("TestNpc");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetAllowedTasks(mockNpc.Object);

            // Assert
            result.Should().Be("Watering, Lumbering, Mining, Attacking, Harvesting, Foraging, Fishing, Petting, Sitting");
        }

        [Fact]
        public void GetAllowedTasks_ShouldReturnSimpleString_WhenNoConditions()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    AllowedTasks = "Watering, Harvesting"
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Maru");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetAllowedTasks(mockNpc.Object);

            // Assert
            result.Should().Be("Watering, Harvesting");
        }

        [Fact]
        public void GetAllowedTasks_ShouldReturnFirstMatch_FromPriorityArray()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();

            var firstEntry = new JObject
            {
                ["Condition"] = "PLAYER_COMBAT_LEVEL Current 5",
                ["Tasks"] = "Attacking, Mining"
            };

            var secondEntry = new JObject
            {
                ["Condition"] = "PLAYER_FARMING_LEVEL Current 3",
                ["Tasks"] = "Watering, Harvesting"
            };

            var tasksArray = new JArray { firstEntry, secondEntry };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    AllowedTasks = tasksArray
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Abigail");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // First condition matches - should return immediately
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_COMBAT_LEVEL Current 5",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(true);

            // Act
            var result = manager.GetAllowedTasks(mockNpc.Object);

            // Assert - First match wins, even if second condition might also match
            result.Should().Be("Attacking, Mining");

            // Verify second condition was never evaluated (first-match-wins)
            mockGameStateChecker.Verify(g => g.CheckConditions(
                "PLAYER_FARMING_LEVEL Current 3",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()), Times.Never);
        }

        [Fact]
        public void GetAllowedTasks_ShouldContinueToNextEntry_WhenFirstConditionFails()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();

            var firstEntry = new JObject
            {
                ["Condition"] = "PLAYER_COMBAT_LEVEL Current 10",
                ["Tasks"] = "Attacking"
            };

            var secondEntry = new JObject
            {
                ["Condition"] = "PLAYER_FARMING_LEVEL Current 5",
                ["Tasks"] = "Watering, Harvesting"
            };

            var tasksArray = new JArray { firstEntry, secondEntry };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    AllowedTasks = tasksArray
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Penny");
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
            var result = manager.GetAllowedTasks(mockNpc.Object);

            // Assert
            result.Should().Be("Watering, Harvesting");
        }

        [Fact]
        public void GetAllowedTasks_ShouldReturnUnconditionalEntry_WhenNoConditionMatches()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();

            var conditionalEntry = new JObject
            {
                ["Condition"] = "PLAYER_COMBAT_LEVEL Current 10",
                ["Tasks"] = "Attacking, Mining"
            };

            var fallbackEntry = new JObject
            {
                ["Tasks"] = "Watering, Petting" // No Condition = fallback
            };

            var tasksArray = new JArray { conditionalEntry, fallbackEntry };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    AllowedTasks = tasksArray
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Harvey");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Conditional entry fails
            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_COMBAT_LEVEL Current 10",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(false);

            // Act
            var result = manager.GetAllowedTasks(mockNpc.Object);

            // Assert - Should use fallback (no condition)
            result.Should().Be("Watering, Petting");
        }

        [Fact]
        public void GetAllowedTasks_ShouldReturnFirstStringEntry_FromArray()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();

            // Array with simple string entry (no conditions)
            var tasksArray = new JArray { "Watering, Harvesting, Foraging" };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    AllowedTasks = tasksArray
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Leah");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetAllowedTasks(mockNpc.Object);

            // Assert
            result.Should().Be("Watering, Harvesting, Foraging");
        }

        #endregion

        #region Recruitment Tests

        [Fact]
        public void CanRecruit_ShouldReturnTrue_WhenNoRecruitmentConfigDefined()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData(); // No Behavior.Recruitment

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("TestNpc");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.CanRecruit(mockNpc.Object);

            // Assert
            result.Should().BeTrue(); // Default: recruitment enabled
        }

        [Fact]
        public void CanRecruit_ShouldReturnFalse_WhenRecruitmentDisabled()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    Recruitment = new RecruitmentConfig
                    {
                        Enabled = false
                    }
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Pierre");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.CanRecruit(mockNpc.Object);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void CanRecruit_ShouldEvaluateCondition_WhenProvided()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    Recruitment = new RecruitmentConfig
                    {
                        Enabled = true,
                        Condition = "PLAYER_HAS_MAIL Current pennyHome"
                    }
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Penny");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_HAS_MAIL Current pennyHome",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(true);

            // Act
            var result = manager.CanRecruit(mockNpc.Object);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void CanRecruit_ShouldReturnFalse_WhenConditionFails()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    Recruitment = new RecruitmentConfig
                    {
                        Enabled = true,
                        Condition = "PLAYER_HAS_MAIL Current ccVault"
                    }
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("JojaMart");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            mockGameStateChecker.Setup(g => g.CheckConditions(
                "PLAYER_HAS_MAIL Current ccVault",
                It.IsAny<GameLocation>(),
                It.IsAny<Farmer>()))
                .Returns(false);

            // Act
            var result = manager.CanRecruit(mockNpc.Object);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void CanRecruit_ShouldReturnTrue_WhenEnabledWithNoCondition()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    Recruitment = new RecruitmentConfig
                    {
                        Enabled = true
                        // No Condition specified
                    }
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Abigail");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.CanRecruit(mockNpc.Object);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void GetRecruitmentRefusalDialogueKey_ShouldReturnKey_WhenConfigured()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    Recruitment = new RecruitmentConfig
                    {
                        Enabled = false,
                        RefusalDialogueKey = "dialogue.recruitment.refusal.pierre.busy"
                    }
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Pierre");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetRecruitmentRefusalDialogueKey(mockNpc.Object);

            // Assert
            result.Should().Be("dialogue.recruitment.refusal.pierre.busy");
        }

        [Fact]
        public void GetRecruitmentRefusalDialogueKey_ShouldReturnNull_WhenNotConfigured()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _, _, _, _) = CreateTestContext();
            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    Recruitment = new RecruitmentConfig
                    {
                        Enabled = true
                        // No RefusalDialogueKey
                    }
                }
            };

            var mockNpc = new Mock<NPC>();
            mockNpc.Setup(n => n.Name).Returns("Abigail");
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            // Act
            var result = manager.GetRecruitmentRefusalDialogueKey(mockNpc.Object);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void GetRandomIdleAnimation_IntegrationTest_WithComplexConfiguration()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, mockRandomSelector, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Abigail");

            // Complex configuration with multiple formats and conditions
            var animationSpec1 = new JObject { ["Id"] = "abigail_flute", ["Loop"] = false };

            var conditionalEntry = new JObject
            {
                ["Condition"] = "SEASON Fall",
                ["Animations"] = new JArray
                {
                    "abigail_read",
                    new JObject { ["Id"] = "abigail_game", ["Loop"] = true }
                }
            };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object>
                    {
                        "abigail_mine", // Simple string
                        animationSpec1,  // Object spec
                        conditionalEntry // Conditional with array
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);

            mockGameStateChecker.Setup(g => g.CheckConditions("SEASON Fall", It.IsAny<GameLocation>(), It.IsAny<Farmer>()))
                .Returns(true);

            List<IdleAnimationSpec> capturedPool = null;
            mockRandomSelector.Setup(r => r.ChooseFrom(It.IsAny<IList<IdleAnimationSpec>>()))
                .Callback<IList<IdleAnimationSpec>>(pool => capturedPool = new List<IdleAnimationSpec>(pool))
                .Returns(new IdleAnimationSpec { Id = "abigail_mine", Loop = true });

            // Act
            var result = manager.GetRandomIdleAnimation(mockNpc);

            // Assert - All 4 animations should be pooled (2 simple + 2 from conditional)
            capturedPool.Should().NotBeNull();
            capturedPool.Should().HaveCount(4);
            capturedPool.Select(a => a.Id).Should().Contain(new[] { "abigail_mine", "abigail_flute", "abigail_read", "abigail_game" });
        }

        [Fact]
        public void GetRandomIdleAnimation_ShouldReplaceCurrentNpcPlaceholder_InConditions()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, mockRandomSelector, mockGameStateChecker, mockGameContext, _, _) = CreateTestContext();
            var mockNpc = CreateMockNpc("Penny");
            Farmer player = null;
            GameLocation location = null;

            // Config with {CurrentNpc} placeholder for spouse-specific animations
            var conditionalEntry = new JObject
            {
                ["Condition"] = "PLAYER_NPC_RELATIONSHIP Current {CurrentNpc} Married",
                ["Animations"] = new JArray { "spouse_idle_1", "spouse_idle_2" }
            };

            var npcConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { conditionalEntry }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.IsAny<NPC>())).Returns(npcConfig);
            mockGameContext.Setup(c => c.Player).Returns(player);
            mockGameContext.Setup(c => c.CurrentLocation).Returns(location);

            // Verify that {CurrentNpc} gets replaced with "Penny"
            mockGameStateChecker.Setup(g => g.CheckConditions("PLAYER_NPC_RELATIONSHIP Current Penny Married", location, player))
                .Returns(true);

            List<IdleAnimationSpec> capturedPool = null;
            mockRandomSelector.Setup(r => r.ChooseFrom(It.IsAny<IList<IdleAnimationSpec>>()))
                .Callback<IList<IdleAnimationSpec>>(pool => capturedPool = new List<IdleAnimationSpec>(pool))
                .Returns(new IdleAnimationSpec { Id = "spouse_idle_1", Loop = true });

            // Act
            var result = manager.GetRandomIdleAnimation(mockNpc);

            // Assert - Should use spouse animations because {CurrentNpc} was replaced with "Penny"
            capturedPool.Should().NotBeNull();
            capturedPool.Should().HaveCount(2);
            capturedPool.Select(a => a.Id).Should().Contain(new[] { "spouse_idle_1", "spouse_idle_2" });

            // Verify the condition was checked with "Penny" substituted
            mockGameStateChecker.Verify(g => g.CheckConditions("PLAYER_NPC_RELATIONSHIP Current Penny Married", location, player), Times.Once);
        }

        #endregion
    }
}
