using FluentAssertions;
using Moq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Data;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.NpcConfig.Models;
using TheStardewSquad.Tests.Helpers;
using Xunit;

namespace TheStardewSquad.Tests.Framework.NpcConfig
{
    /// <summary>
    /// Tests for NpcConfigManager - manages loading and access to unified NPC configuration data.
    /// Tests Generic defaults, NPC-specific configs, and lazy loading patterns.
    ///
    /// Based on specifications in:
    /// - [CP] TheStardewSquad.ContentAddons/docs/npcConfig.example.json
    /// - [CP] TheStardewSquad.ContentAddons/docs/NpcConfig Reference.md
    /// </summary>
    public class NpcConfigManagerTests
    {
        private (
            NpcConfigManager manager,
            Mock<INpcConfigDataProvider> mockDataProvider,
            Mock<IMonitor> mockMonitor
        ) CreateTestContext()
        {
            var mockDataProvider = new Mock<INpcConfigDataProvider>();
            var mockMonitor = new Mock<IMonitor>();
            var mockHelper = new Mock<IModHelper>();
            mockHelper.Setup(h => h.DirectoryPath).Returns(System.IO.Path.GetTempPath());
            var baselineLoader = new BaselineContentLoader(mockHelper.Object, mockMonitor.Object);

            var manager = new NpcConfigManager(
                mockDataProvider.Object,
                baselineLoader,
                mockMonitor.Object
            );

            return (manager, mockDataProvider, mockMonitor);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldInitializeAllDependencies()
        {
            // Arrange & Act
            var (manager, _, _) = CreateTestContext();

            // Assert
            manager.Should().NotBeNull();
        }

        #endregion

        #region GetConfig Tests

        [Fact]
        public void GetConfig_ShouldReturnEmptyConfig_WhenNpcNotFound()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var configData = new Dictionary<string, NpcConfigData>();

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var npc = new NPC { Name = "UnknownNpc" };

            // Act
            var result = manager.GetConfig(npc);

            // Assert
            result.Should().NotBeNull();
            result.Dialogue.Should().BeNull();
            result.Sprites.Should().BeNull();
            result.Behavior.Should().BeNull();
        }

        [Fact]
        public void GetConfig_ShouldReturnNpcSpecificConfig_WhenExists()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var abigailConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Attacking = new List<object> { "dialogue.attacking.abigail.excited.1" }
                }
            };

            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Abigail", abigailConfig }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var npc = new NPC { Name = "Abigail" };

            // Act
            var result = manager.GetConfig(npc);

            // Assert
            result.Should().BeSameAs(abigailConfig);
            result.Dialogue.Should().NotBeNull();
            result.Dialogue.Attacking.Should().Contain("dialogue.attacking.abigail.excited.1");
        }

        [Fact]
        public void GetConfig_ShouldLoadDataLazily_OnFirstAccess()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var configData = new Dictionary<string, NpcConfigData>();

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var npc = new NPC { Name = "TestNpc" };

            // Act
            manager.GetConfig(npc);
            manager.GetConfig(npc); // Second call

            // Assert - LoadNpcConfigData should only be called once
            mockDataProvider.Verify(p => p.LoadNpcConfigData(), Times.Once);
        }

        [Fact]
        public void GetConfig_ShouldReturnSpeciesConfig_ForPetWithAllSpeciesKey()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var catConfig = new NpcConfigData
            {
                NpcType = "Cat",
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "TSS_cat_sit", "TSS_cat_flop" }
                }
            };

            var configData = new Dictionary<string, NpcConfigData>
            {
                { "All_Cat", catConfig }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var pet = TestPetFactory.CreatePet("Fluffy", petType: "Cat");

            // Act
            var result = manager.GetConfig(pet);

            // Assert
            result.Should().BeSameAs(catConfig);
            result.NpcType.Should().Be("Cat");
            result.Behavior.Should().NotBeNull();
            result.Behavior.IdleAnimations.Should().Contain("TSS_cat_sit");
        }

        [Fact]
        public void GetConfig_ShouldReturnBreedSpecificConfig_WhenExactNameMatches()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var dog2Config = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "dog2_specific_animation" }
                }
            };

            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Dog2", dog2Config }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var pet = TestPetFactory.CreatePet("Dog2", petType: "Dog");

            // Act
            var result = manager.GetConfig(pet);

            // Assert
            result.Should().BeSameAs(dog2Config);
            result.Behavior.IdleAnimations.Should().Contain("dog2_specific_animation");
        }

        [Fact]
        public void GetConfig_ShouldReturnBreedConfig_ForPetWithBreedKey()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var cat1Config = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "cat_breed1_animation" }
                }
            };

            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Cat_1", cat1Config }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var pet = TestPetFactory.CreatePet("Mittens", petType: "Cat", breed: 1);

            // Act
            var result = manager.GetConfig(pet);

            // Assert
            result.Should().BeSameAs(cat1Config);
            result.Behavior.IdleAnimations.Should().Contain("cat_breed1_animation");
        }

        [Fact]
        public void GetConfig_ShouldPreferExactMatch_OverBreedAndSpeciesMatch()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var fluffyConfig = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "fluffy_specific_animation" }
                }
            };

            var cat1Config = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "cat_breed1_animation" }
                }
            };

            var catConfig = new NpcConfigData
            {
                NpcType = "Cat",
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "TSS_cat_sit" }
                }
            };

            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Fluffy", fluffyConfig },
                { "Cat_1", cat1Config },
                { "All_Cat", catConfig }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var pet = TestPetFactory.CreatePet("Fluffy", petType: "Cat", breed: 1);

            // Act
            var result = manager.GetConfig(pet);

            // Assert - Should use exact name match (highest priority)
            result.Should().BeSameAs(fluffyConfig);
            result.Behavior.IdleAnimations.Should().Contain("fluffy_specific_animation");
            result.Behavior.IdleAnimations.Should().NotContain("cat_breed1_animation");
            result.Behavior.IdleAnimations.Should().NotContain("TSS_cat_sit");
        }

        [Fact]
        public void GetConfig_ShouldPreferBreedMatch_OverSpeciesMatch()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var dog2Config = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "dog_breed2_animation" }
                }
            };

            var dogConfig = new NpcConfigData
            {
                NpcType = "Dog",
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "TSS_dog_sit" }
                }
            };

            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Dog_2", dog2Config },
                { "All_Dog", dogConfig }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var pet = TestPetFactory.CreatePet("Rover", petType: "Dog", breed: 2);

            // Act
            var result = manager.GetConfig(pet);

            // Assert - Should use breed match (higher priority than species-wide)
            result.Should().BeSameAs(dog2Config);
            result.Behavior.IdleAnimations.Should().Contain("dog_breed2_animation");
            result.Behavior.IdleAnimations.Should().NotContain("TSS_dog_sit");
        }

        [Fact]
        public void GetConfig_ShouldDifferentiateBetweenDogAndCatBreeds()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var cat1Config = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "cat_breed1_animation" }
                }
            };

            var dog1Config = new NpcConfigData
            {
                Behavior = new BehaviorConfig
                {
                    IdleAnimations = new List<object> { "dog_breed1_animation" }
                }
            };

            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Cat_1", cat1Config },
                { "Dog_1", dog1Config }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var cat = TestPetFactory.CreatePet("Whiskers", petType: "Cat", breed: 1);
            var dog = TestPetFactory.CreatePet("Buddy", petType: "Dog", breed: 1);

            // Act
            var catResult = manager.GetConfig(cat);
            var dogResult = manager.GetConfig(dog);

            // Assert - Cat and Dog breed 1 should have different configs
            catResult.Should().BeSameAs(cat1Config);
            catResult.Behavior.IdleAnimations.Should().Contain("cat_breed1_animation");

            dogResult.Should().BeSameAs(dog1Config);
            dogResult.Behavior.IdleAnimations.Should().Contain("dog_breed1_animation");
        }

        #endregion

        #region GetGenericConfig Tests

        [Fact]
        public void GetGenericConfig_ShouldReturnEmptyConfig_WhenGenericNotDefined()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var configData = new Dictionary<string, NpcConfigData>();

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            // Act
            var result = manager.GetGenericConfig();

            // Assert
            result.Should().NotBeNull();
            result.Dialogue.Should().BeNull();
            result.Sprites.Should().BeNull();
            result.Behavior.Should().BeNull(); // Generic should not have Behavior
        }

        [Fact]
        public void GetGenericConfig_ShouldReturnGenericDefaults_WhenDefined()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var genericConfig = new NpcConfigData
            {
                Dialogue = new DialogueConfig
                {
                    Recruit = new List<object>
                    {
                        "dialogue.recruit.generic.1",
                        "dialogue.recruit.generic.2"
                    },
                    Idle = new List<object>
                    {
                        "dialogue.idle.generic.1"
                    }
                },
                Sprites = new SpriteConfig
                {
                    // Generic sprites use original NPC sprite sheets (no ExtensionSheet)
                    Watering = new { FramesByDirection = new { Down = new[] { 1 } } }
                }
            };

            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Generic", genericConfig }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            // Act
            var result = manager.GetGenericConfig();

            // Assert
            result.Should().BeSameAs(genericConfig);
            result.Dialogue.Should().NotBeNull();
            result.Dialogue.Recruit.Should().HaveCount(2);
            result.Dialogue.Idle.Should().HaveCount(1);
            result.Sprites.Should().NotBeNull();
        }

        #endregion

        #region HasConfig Tests

        [Fact]
        public void HasConfig_ShouldReturnFalse_WhenNpcNotDefined()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var configData = new Dictionary<string, NpcConfigData>();

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var npc = new NPC { Name = "UnknownNpc" };

            // Act
            var result = manager.HasConfig(npc);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void HasConfig_ShouldReturnTrue_WhenNpcDefined()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Abigail", new NpcConfigData() }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var npc = new NPC { Name = "Abigail" };

            // Act
            var result = manager.HasConfig(npc);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasConfig_ShouldReturnTrue_ForPetWithSpeciesConfig()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var configData = new Dictionary<string, NpcConfigData>
            {
                { "All_Dog", new NpcConfigData { NpcType = "Dog" } }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var pet = TestPetFactory.CreatePet("Buddy", petType: "Dog");

            // Act
            var result = manager.HasConfig(pet);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void HasConfig_ShouldReturnTrue_ForPetWithBreedSpecificConfig()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var configData = new Dictionary<string, NpcConfigData>
            {
                { "Cat_1", new NpcConfigData() }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            var pet = TestPetFactory.CreatePet("Mittens", petType: "Cat", breed: 1);

            // Act
            var result = manager.HasConfig(pet);

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void GetConfig_ShouldHandleContentLoadException_Gracefully()
        {
            // Arrange
            var (manager, mockDataProvider, mockMonitor) = CreateTestContext();

            mockDataProvider.Setup(p => p.LoadNpcConfigData())
                .Throws(new Microsoft.Xna.Framework.Content.ContentLoadException("Asset not found"));

            var npc = new NPC { Name = "Abigail" };

            // Act
            var result = manager.GetConfig(npc);

            // Assert
            result.Should().NotBeNull();
            result.Dialogue.Should().BeNull(); // Should return empty config
            mockMonitor.Verify(m => m.Log(
                It.Is<string>(s => s.Contains("NpcConfig asset not found")),
                LogLevel.Warn), Times.Once);
        }

        [Fact]
        public void GetConfig_ShouldHandleGenericException_Gracefully()
        {
            // Arrange
            var (manager, mockDataProvider, mockMonitor) = CreateTestContext();

            mockDataProvider.Setup(p => p.LoadNpcConfigData())
                .Throws(new Exception("Unexpected error"));

            var npc = new NPC { Name = "Abigail" };

            // Act
            var result = manager.GetConfig(npc);

            // Assert
            result.Should().NotBeNull();
            result.Dialogue.Should().BeNull(); // Should return empty config
            mockMonitor.Verify(m => m.Log(
                It.Is<string>(s => s.Contains("Error loading NpcConfig")),
                LogLevel.Error), Times.Once);
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void GetConfig_ShouldLoadMultipleNpcsCorrectly()
        {
            // Arrange
            var (manager, mockDataProvider, _) = CreateTestContext();
            var configData = new Dictionary<string, NpcConfigData>
            {
                {
                    "Generic", new NpcConfigData
                    {
                        Dialogue = new DialogueConfig
                        {
                            Recruit = new List<object> { "dialogue.recruit.generic.1" }
                        }
                    }
                },
                {
                    "Abigail", new NpcConfigData
                    {
                        Dialogue = new DialogueConfig
                        {
                            Attacking = new List<object> { "dialogue.attacking.abigail.1" }
                        }
                    }
                },
                {
                    "Shane", new NpcConfigData
                    {
                        Behavior = new BehaviorConfig
                        {
                            IdleAnimations = new List<object> { "shane_drink" }
                        }
                    }
                }
            };

            mockDataProvider.Setup(p => p.LoadNpcConfigData()).Returns(configData);

            // Act
            var generic = manager.GetGenericConfig();
            var abigailNpc = new NPC { Name = "Abigail" };
            var shaneNpc = new NPC { Name = "Shane" };
            var abigail = manager.GetConfig(abigailNpc);
            var shane = manager.GetConfig(shaneNpc);

            // Assert
            generic.Dialogue.Recruit.Should().Contain("dialogue.recruit.generic.1");
            abigail.Dialogue.Attacking.Should().Contain("dialogue.attacking.abigail.1");
            shane.Behavior.IdleAnimations.Should().Contain("shane_drink");
        }

        #endregion
    }
}
