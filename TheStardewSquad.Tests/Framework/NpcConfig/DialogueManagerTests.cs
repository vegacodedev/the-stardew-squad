using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Abstractions.Utilities;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.NpcConfig.Models;
using Xunit;

namespace TheStardewSquad.Tests.Framework.NpcConfig
{
    /// <summary>
    /// Tests for DialogueManager - handles dialogue selection using pool-all-matches logic.
    ///
    /// Pool-all-matches: Dialogue pools ALL matching conditional entries. If multiple conditions
    /// match, all their lines are available for random selection.
    ///
    /// Based on specifications in:
    /// - [CP] TheStardewSquad.ContentAddons/docs/npcConfig.example.json
    /// - [CP] TheStardewSquad.ContentAddons/docs/NpcConfig Reference.md
    /// </summary>
    public class DialogueManagerTests
    {
        private (
            DialogueManager manager,
            Mock<NpcConfigManager> mockConfigManager,
            Mock<IRandomSelector> mockRandomSelector,
            Mock<IGameStateChecker> mockGameStateChecker,
            Mock<INpcDialogueService> mockNpcDialogueService,
            Mock<IGameContext> mockGameContext
        ) CreateTestContext()
        {
            var mockConfigManager = new Mock<NpcConfigManager>(null, null);
            var mockRandomSelector = new Mock<IRandomSelector>();
            var mockGameStateChecker = new Mock<IGameStateChecker>();
            var mockNpcDialogueService = new Mock<INpcDialogueService>();
            var mockGameContext = new Mock<IGameContext>();
            var mockMonitor = new Mock<IMonitor>();

            var manager = new DialogueManager(
                mockConfigManager.Object,
                mockRandomSelector.Object,
                mockGameStateChecker.Object,
                mockNpcDialogueService.Object,
                mockGameContext.Object,
                mockMonitor.Object
            );

            return (manager, mockConfigManager, mockRandomSelector, mockGameStateChecker, mockNpcDialogueService, mockGameContext);
        }

        private NPC CreateMockNPC(string name)
        {
            var npc = new NPC { Name = name };
            return npc;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldInitializeAllDependencies()
        {
            // Arrange & Act
            var (manager, _, _, _, _, _) = CreateTestContext();

            // Assert
            manager.Should().NotBeNull();
        }

        #endregion

        #region GetDialogue - Basic Tests

        [Fact]
        public void GetDialogue_ShouldReturnNull_WhenNoDialogueConfigured()
        {
            // Arrange
            var (manager, mockConfigManager, _, _, _, _) = CreateTestContext();
            var npc = CreateMockNPC("Abigail");

            var genericConfig = new NpcConfigData();
            var npcConfig = new NpcConfigData();

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "Abigail"))).Returns(npcConfig);

            // Act
            var result = manager.GetDialogue(npc, "Recruit");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetDialogue_ShouldReturnSimpleDialogue_FromGeneric()
        {
            // Arrange
            var (manager, mockConfigManager, mockRandomSelector, _, _, _) = CreateTestContext();
            var npc = CreateMockNPC("Abigail");

            var genericConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Recruit = new List<object>
                    {
                        "dialogue.recruit.generic.1",
                        "dialogue.recruit.generic.2"
                    }
                }
            };
            var npcConfig = new NpcConfigData();

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "Abigail"))).Returns(npcConfig);
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<List<string>>()))
                .Returns("dialogue.recruit.generic.1");

            // Act
            var result = manager.GetDialogue(npc, "Recruit");

            // Assert
            result.Should().Be("dialogue.recruit.generic.1");
        }

        [Fact]
        public void GetDialogue_ShouldReturnSimpleDialogue_FromNpcSpecific()
        {
            // Arrange
            var (manager, mockConfigManager, mockRandomSelector, _, _, _) = CreateTestContext();
            var npc = CreateMockNPC("Abigail");

            var genericConfig = new NpcConfigData();
            var npcConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Attacking = new List<object>
                    {
                        "dialogue.attacking.abigail.excited.1",
                        "dialogue.attacking.abigail.gamer.1"
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "Abigail"))).Returns(npcConfig);
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<List<string>>()))
                .Returns("dialogue.attacking.abigail.gamer.1");

            // Act
            var result = manager.GetDialogue(npc, "Attacking");

            // Assert
            result.Should().Be("dialogue.attacking.abigail.gamer.1");
        }

        #endregion

        #region Pool-All-Matches Logic Tests

        [Fact]
        public void GetDialogue_ShouldUseOnlyNpcDialogue_WhenNpcHasDialogue()
        {
            // Arrange
            var (manager, mockConfigManager, mockRandomSelector, _, _, _) = CreateTestContext();
            var npc = CreateMockNPC("Sam");

            var genericConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Idle = new List<object>
                    {
                        "dialogue.idle.generic.1",
                        "dialogue.idle.generic.2"
                    }
                }
            };

            var npcConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Idle = new List<object>
                    {
                        "dialogue.idle.sam.music.1",
                        "dialogue.idle.sam.band.1"
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "Sam"))).Returns(npcConfig);

            List<string> capturedPool = null;
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<IList<string>>()))
                .Callback<IList<string>>(pool => capturedPool = new List<string>(pool))
                .Returns("dialogue.idle.sam.music.1");

            // Act
            var result = manager.GetDialogue(npc, "Idle");

            // Assert - NPC dialogue completely overrides Generic
            capturedPool.Should().NotBeNull();
            capturedPool.Should().NotContain("dialogue.idle.generic.1");
            capturedPool.Should().NotContain("dialogue.idle.generic.2");
            capturedPool.Should().Contain("dialogue.idle.sam.music.1");
            capturedPool.Should().Contain("dialogue.idle.sam.band.1");
            capturedPool.Should().HaveCount(2); // Only NPC dialogue, NOT generic
        }

        [Fact]
        public void GetDialogue_ShouldUseGenericAsFallback_WhenNpcHasNoDialogue()
        {
            // Arrange
            var (manager, mockConfigManager, mockRandomSelector, _, _, _) = CreateTestContext();
            var npc = CreateMockNPC("RandomVillager");

            var genericConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Idle = new List<object>
                    {
                        "dialogue.idle.generic.1",
                        "dialogue.idle.generic.2"
                    }
                }
            };

            // NPC has no Idle dialogue defined
            var npcConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Recruit = new List<object> { "dialogue.recruit.villager.1" }
                    // No Idle dialogue
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "RandomVillager"))).Returns(npcConfig);

            List<string> capturedPool = null;
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<IList<string>>()))
                .Callback<IList<string>>(pool => capturedPool = new List<string>(pool))
                .Returns("dialogue.idle.generic.1");

            // Act
            var result = manager.GetDialogue(npc, "Idle");

            // Assert - Generic is used as fallback
            capturedPool.Should().NotBeNull();
            capturedPool.Should().Contain("dialogue.idle.generic.1");
            capturedPool.Should().Contain("dialogue.idle.generic.2");
            capturedPool.Should().HaveCount(2); // Uses Generic fallback
        }

        [Fact]
        public void GetDialogue_ShouldIncludeMatchingConditionalDialogue()
        {
            // Arrange
            var (manager, mockConfigManager, mockRandomSelector, mockGameStateChecker, _, mockGameContext) = CreateTestContext();
            var npc = CreateMockNPC("Shane");
            // Use null for player/location - they're only passed to mocks for verification
            Farmer player = null;
            GameLocation location = null;

            // Shane has conditional dialogue based on heart level
            var npcConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Idle = new List<object>
                    {
                        // Conditional entry (0-6 hearts) - should match
                        JObject.FromObject(new
                        {
                            Condition = "PLAYER_HEARTS Current Shane 0 6",
                            Lines = new[] { "dialogue.idle.shane.grumpy.1", "dialogue.idle.shane.depressed.1" }
                        }),
                        // Conditional entry (7-14 hearts) - should NOT match
                        JObject.FromObject(new
                        {
                            Condition = "PLAYER_HEARTS Current Shane 7 14",
                            Lines = new[] { "dialogue.idle.shane.hopeful.1", "dialogue.idle.shane.better.1" }
                        }),
                        // Always included (no condition)
                        "dialogue.idle.shane.generic.1"
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "Shane"))).Returns(npcConfig);
            mockGameContext.Setup(c => c.Player).Returns(player);
            mockGameContext.Setup(c => c.CurrentLocation).Returns(location);

            // First condition matches, second doesn't
            mockGameStateChecker.Setup(c => c.CheckConditions("PLAYER_HEARTS Current Shane 0 6", location, player))
                .Returns(true);
            mockGameStateChecker.Setup(c => c.CheckConditions("PLAYER_HEARTS Current Shane 7 14", location, player))
                .Returns(false);

            List<string> capturedPool = null;
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<IList<string>>()))
                .Callback<IList<string>>(pool => capturedPool = new List<string>(pool))
                .Returns("dialogue.idle.shane.grumpy.1");

            // Act
            var result = manager.GetDialogue(npc, "Idle");

            // Assert
            capturedPool.Should().NotBeNull();
            capturedPool.Should().Contain("dialogue.idle.shane.grumpy.1");
            capturedPool.Should().Contain("dialogue.idle.shane.depressed.1");
            capturedPool.Should().Contain("dialogue.idle.shane.generic.1");
            capturedPool.Should().NotContain("dialogue.idle.shane.hopeful.1"); // Condition didn't match
            capturedPool.Should().NotContain("dialogue.idle.shane.better.1"); // Condition didn't match
            capturedPool.Should().HaveCount(3); // 2 from matching condition + 1 unconditional
        }

        [Fact]
        public void GetDialogue_ShouldPoolMultipleMatchingConditions()
        {
            // Arrange
            var (manager, mockConfigManager, mockRandomSelector, mockGameStateChecker, _, mockGameContext) = CreateTestContext();
            var npc = CreateMockNPC("TestNpc");
            // Use null for player/location - they're only passed to mocks for verification
            Farmer player = null;
            GameLocation location = null;

            var npcConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Idle = new List<object>
                    {
                        JObject.FromObject(new
                        {
                            Condition = "TIME 600 1200",
                            Lines = new[] { "dialogue.idle.morning.1", "dialogue.idle.morning.2" }
                        }),
                        JObject.FromObject(new
                        {
                            Condition = "LOCATION_NAME Target Farm",
                            Lines = new[] { "dialogue.idle.farm.1", "dialogue.idle.farm.2" }
                        }),
                        "dialogue.idle.default.1"
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "TestNpc"))).Returns(npcConfig);
            mockGameContext.Setup(c => c.Player).Returns(player);
            mockGameContext.Setup(c => c.CurrentLocation).Returns(location);

            // Both conditions match
            mockGameStateChecker.Setup(c => c.CheckConditions("TIME 600 1200", location, player)).Returns(true);
            mockGameStateChecker.Setup(c => c.CheckConditions("LOCATION_NAME Target Farm", location, player)).Returns(true);

            List<string> capturedPool = null;
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<IList<string>>()))
                .Callback<IList<string>>(pool => capturedPool = new List<string>(pool))
                .Returns("dialogue.idle.morning.1");

            // Act
            var result = manager.GetDialogue(npc, "Idle");

            // Assert - ALL matching conditions are pooled
            capturedPool.Should().NotBeNull();
            capturedPool.Should().Contain("dialogue.idle.morning.1");
            capturedPool.Should().Contain("dialogue.idle.morning.2");
            capturedPool.Should().Contain("dialogue.idle.farm.1");
            capturedPool.Should().Contain("dialogue.idle.farm.2");
            capturedPool.Should().Contain("dialogue.idle.default.1");
            capturedPool.Should().HaveCount(5); // All lines from both conditions + default
        }

        #endregion

        #region Exclusive Pools Tests (using negation)

        [Fact]
        public void GetDialogue_ShouldUseExclusivePools_WithNegation()
        {
            // Arrange - Penny has exclusive married vs not-married dialogue
            var (manager, mockConfigManager, mockRandomSelector, mockGameStateChecker, _, mockGameContext) = CreateTestContext();
            var npc = CreateMockNPC("Penny");
            // Use null for player/location - they're only passed to mocks for verification
            Farmer player = null;
            GameLocation location = null;

            var npcConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Recruit = new List<object>
                    {
                        JObject.FromObject(new
                        {
                            Condition = "PLAYER_NPC_RELATIONSHIP Current Penny Married",
                            Lines = new[] { "dialogue.recruit.penny.spouse.1", "dialogue.recruit.penny.spouse.2" }
                        }),
                        JObject.FromObject(new
                        {
                            Condition = "PLAYER_NPC_RELATIONSHIP Current Penny !Married",
                            Lines = new[] { "dialogue.recruit.penny.normal.1", "dialogue.recruit.penny.normal.2" }
                        })
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "Penny"))).Returns(npcConfig);
            mockGameContext.Setup(c => c.Player).Returns(player);
            mockGameContext.Setup(c => c.CurrentLocation).Returns(location);

            // Married condition matches, not-married doesn't
            mockGameStateChecker.Setup(c => c.CheckConditions("PLAYER_NPC_RELATIONSHIP Current Penny Married", location, player))
                .Returns(true);
            mockGameStateChecker.Setup(c => c.CheckConditions("PLAYER_NPC_RELATIONSHIP Current Penny !Married", location, player))
                .Returns(false);

            List<string> capturedPool = null;
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<IList<string>>()))
                .Callback<IList<string>>(pool => capturedPool = new List<string>(pool))
                .Returns("dialogue.recruit.penny.spouse.1");

            // Act
            var result = manager.GetDialogue(npc, "Recruit");

            // Assert - Only spouse dialogue, not normal dialogue
            capturedPool.Should().NotBeNull();
            capturedPool.Should().Contain("dialogue.recruit.penny.spouse.1");
            capturedPool.Should().Contain("dialogue.recruit.penny.spouse.2");
            capturedPool.Should().NotContain("dialogue.recruit.penny.normal.1");
            capturedPool.Should().NotContain("dialogue.recruit.penny.normal.2");
            capturedPool.Should().HaveCount(2); // Only matching exclusive pool
        }

        #endregion

        #region Token Replacement Tests

        [Fact]
        public void GetDialogue_ShouldReplaceEndearmentToken()
        {
            // Arrange
            var (manager, mockConfigManager, mockRandomSelector, _, mockNpcDialogueService, _) = CreateTestContext();
            var npc = CreateMockNPC("Penny");

            var npcConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Idle = new List<object> { "Good morning, {{endearment}}!" }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "Penny"))).Returns(npcConfig);
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<List<string>>()))
                .Returns("Good morning, {{endearment}}!");
            mockNpcDialogueService.Setup(s => s.GetTermOfSpousalEndearment(npc))
                .Returns("dear");

            // Act
            var result = manager.GetDialogue(npc, "Idle");

            // Assert
            result.Should().Be("Good morning, dear!");
        }

        #endregion

        #region ShowDialogueBubble Tests

        [Fact]
        public void ShowDialogueBubble_ShouldCallNpcDialogueService()
        {
            // Arrange
            var (manager, _, _, _, mockNpcDialogueService, _) = CreateTestContext();
            var npc = CreateMockNPC("Abigail");

            // Act
            manager.ShowDialogueBubble(npc, "Test message");

            // Assert
            mockNpcDialogueService.Verify(s => s.ShowTextAboveHead(npc, "Test message"), Times.Once);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void GetDialogue_ShouldReturnNull_WhenNoConditionsMatch()
        {
            // Arrange
            var (manager, mockConfigManager, _, mockGameStateChecker, _, mockGameContext) = CreateTestContext();
            var npc = CreateMockNPC("TestNpc");
            // Use null for player/location - they're only passed to mocks for verification
            Farmer player = null;
            GameLocation location = null;

            var npcConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Idle = new List<object>
                    {
                        JObject.FromObject(new
                        {
                            Condition = "TIME 2000 2400",
                            Lines = new[] { "dialogue.idle.night.1" }
                        })
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(new NpcConfigData());
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "TestNpc"))).Returns(npcConfig);
            mockGameContext.Setup(c => c.Player).Returns(player);
            mockGameContext.Setup(c => c.CurrentLocation).Returns(location);
            mockGameStateChecker.Setup(c => c.CheckConditions("TIME 2000 2400", location, player)).Returns(false);

            // Act
            var result = manager.GetDialogue(npc, "Idle");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetDialogue_ShouldReplaceCurrentNpcPlaceholder_InConditions()
        {
            // Arrange
            var (manager, mockConfigManager, mockRandomSelector, mockGameStateChecker, _, mockGameContext) = CreateTestContext();
            var npc = CreateMockNPC("Penny");
            Farmer player = null;
            GameLocation location = null;

            // Generic config with {CurrentNpc} placeholder for spouse-specific dialogue
            var genericConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Idle = new List<object>
                    {
                        JObject.FromObject(new
                        {
                            Condition = "PLAYER_NPC_RELATIONSHIP Current {CurrentNpc} Married",
                            Lines = new[] { "dialogue.idle.spouse.1", "dialogue.idle.spouse.2" }
                        }),
                        JObject.FromObject(new
                        {
                            Condition = "PLAYER_NPC_RELATIONSHIP Current {CurrentNpc} !Married",
                            Lines = new[] { "dialogue.idle.friend.1" }
                        })
                    }
                }
            };

            mockConfigManager.Setup(m => m.GetGenericConfig()).Returns(genericConfig);
            mockConfigManager.Setup(m => m.GetConfig(It.Is<NPC>(n => n.Name == "Penny"))).Returns(new NpcConfigData());
            mockGameContext.Setup(c => c.Player).Returns(player);
            mockGameContext.Setup(c => c.CurrentLocation).Returns(location);

            // Verify that {CurrentNpc} gets replaced with "Penny" in the condition check
            mockGameStateChecker.Setup(c => c.CheckConditions("PLAYER_NPC_RELATIONSHIP Current Penny Married", location, player))
                .Returns(true);
            mockGameStateChecker.Setup(c => c.CheckConditions("PLAYER_NPC_RELATIONSHIP Current Penny !Married", location, player))
                .Returns(false);

            List<string> capturedPool = null;
            mockRandomSelector.Setup(s => s.ChooseFrom(It.IsAny<IList<string>>()))
                .Callback<IList<string>>(pool => capturedPool = new List<string>(pool))
                .Returns("dialogue.idle.spouse.1");

            // Act
            var result = manager.GetDialogue(npc, "Idle");

            // Assert - Should use spouse dialogue because {CurrentNpc} was replaced with "Penny"
            capturedPool.Should().NotBeNull();
            capturedPool.Should().Contain("dialogue.idle.spouse.1");
            capturedPool.Should().Contain("dialogue.idle.spouse.2");
            capturedPool.Should().NotContain("dialogue.idle.friend.1"); // Not married condition failed
            capturedPool.Should().HaveCount(2);

            // Verify the condition was checked with "Penny" substituted
            mockGameStateChecker.Verify(c => c.CheckConditions("PLAYER_NPC_RELATIONSHIP Current Penny Married", location, player), Times.Once);
        }

        #endregion
    }
}
