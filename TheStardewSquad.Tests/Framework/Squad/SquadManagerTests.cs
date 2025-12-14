using FluentAssertions;
using Moq;
using StardewValley;
using TheStardewSquad.Framework.Squad;
using Xunit;

namespace TheStardewSquad.Tests.Framework.Squad
{
    /// <summary>
    /// Tests for SquadManager - the state manager for recruited squad members.
    ///
    /// SquadManager is a simple collection wrapper that tracks squad membership
    /// by NPC name and provides Add/Remove/Query operations.
    /// </summary>
    public class SquadManagerTests
    {
        private Mock<ISquadMate> CreateMockSquadMate(string npcName)
        {
            // Create a real NPC with a name (NPC.Name is not virtual, cannot be mocked)
            var npc = new NPC
            {
                Name = npcName
            };

            var mockMate = new Mock<ISquadMate>();
            mockMate.Setup(m => m.Npc).Returns(npc);
            mockMate.Setup(m => m.Name).Returns(npcName);

            return mockMate;
        }

        [Fact]
        public void Constructor_ShouldInitializeEmptySquad()
        {
            // Act
            var manager = new SquadManager();

            // Assert
            manager.Count.Should().Be(0, "new SquadManager should start empty");
            manager.Members.Should().BeEmpty("Members collection should be empty");
        }

        [Fact]
        public void Add_ShouldAddMateToSquad()
        {
            // Arrange
            var manager = new SquadManager();
            var mockMate = CreateMockSquadMate("Abigail");

            // Act
            manager.Add(mockMate.Object);

            // Assert
            manager.Count.Should().Be(1);
            manager.Members.Should().ContainSingle();
            manager.Members.First().Should().Be(mockMate.Object);
        }

        [Fact]
        public void Add_ShouldAddMultipleMates()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");
            var mate3 = CreateMockSquadMate("Sam");

            // Act
            manager.Add(mate1.Object);
            manager.Add(mate2.Object);
            manager.Add(mate3.Object);

            // Assert
            manager.Count.Should().Be(3);
            manager.Members.Should().HaveCount(3);
            manager.Members.Should().Contain(mate1.Object);
            manager.Members.Should().Contain(mate2.Object);
            manager.Members.Should().Contain(mate3.Object);
        }

        [Fact]
        public void Add_ShouldNotAddDuplicateMate_WhenSameNameAlreadyExists()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Abigail"); // Same name, different instance

            // Act
            manager.Add(mate1.Object);
            manager.Add(mate2.Object); // Should be ignored

            // Assert
            manager.Count.Should().Be(1, "duplicate name should not be added");
            manager.Members.Should().ContainSingle();
            manager.Members.First().Should().Be(mate1.Object, "original mate should remain");
        }

        [Fact]
        public void Remove_ShouldRemoveMateByCharacterName()
        {
            // Arrange
            var manager = new SquadManager();
            var mockMate = CreateMockSquadMate("Abigail");
            manager.Add(mockMate.Object);

            // Act
            manager.Remove(mockMate.Object.Npc);

            // Assert
            manager.Count.Should().Be(0, "mate should be removed");
            manager.Members.Should().BeEmpty();
        }

        [Fact]
        public void Remove_ShouldRemoveAllMatesWithSameName()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");

            manager.Add(mate1.Object);
            manager.Add(mate2.Object);

            // Act
            manager.Remove(mate1.Object.Npc);

            // Assert
            manager.Count.Should().Be(1, "only Abigail should be removed");
            manager.Members.Should().ContainSingle();
            manager.Members.First().Should().Be(mate2.Object);
        }

        [Fact]
        public void Remove_ShouldDoNothing_WhenCharacterNotInSquad()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");
            manager.Add(mate1.Object);

            // Act
            manager.Remove(mate2.Object.Npc); // Remove non-existent

            // Assert
            manager.Count.Should().Be(1, "existing mate should remain");
            manager.Members.Should().ContainSingle();
            manager.Members.First().Should().Be(mate1.Object);
        }

        [Fact]
        public void Clear_ShouldRemoveAllMates()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");
            var mate3 = CreateMockSquadMate("Sam");

            manager.Add(mate1.Object);
            manager.Add(mate2.Object);
            manager.Add(mate3.Object);

            // Act
            manager.Clear();

            // Assert
            manager.Count.Should().Be(0, "all mates should be removed");
            manager.Members.Should().BeEmpty();
        }

        [Fact]
        public void Clear_ShouldWork_OnEmptySquad()
        {
            // Arrange
            var manager = new SquadManager();

            // Act
            manager.Clear();

            // Assert
            manager.Count.Should().Be(0);
            manager.Members.Should().BeEmpty();
        }

        [Fact]
        public void IsRecruited_ShouldReturnTrue_WhenCharacterIsInSquad()
        {
            // Arrange
            var manager = new SquadManager();
            var mockMate = CreateMockSquadMate("Abigail");
            manager.Add(mockMate.Object);

            // Act
            bool result = manager.IsRecruited(mockMate.Object.Npc);

            // Assert
            result.Should().BeTrue("Abigail is in the squad");
        }

        [Fact]
        public void IsRecruited_ShouldReturnFalse_WhenCharacterNotInSquad()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");
            manager.Add(mate1.Object);

            // Act
            bool result = manager.IsRecruited(mate2.Object.Npc);

            // Assert
            result.Should().BeFalse("Sebastian is not in the squad");
        }

        [Fact]
        public void IsRecruited_ShouldReturnFalse_OnEmptySquad()
        {
            // Arrange
            var manager = new SquadManager();
            var mockMate = CreateMockSquadMate("Abigail");

            // Act
            bool result = manager.IsRecruited(mockMate.Object.Npc);

            // Assert
            result.Should().BeFalse("squad is empty");
        }

        [Fact]
        public void GetMember_ShouldReturnMate_WhenCharacterIsInSquad()
        {
            // Arrange
            var manager = new SquadManager();
            var mockMate = CreateMockSquadMate("Abigail");
            manager.Add(mockMate.Object);

            // Act
            ISquadMate result = manager.GetMember(mockMate.Object.Npc);

            // Assert
            result.Should().NotBeNull();
            result.Should().Be(mockMate.Object);
        }

        [Fact]
        public void GetMember_ShouldReturnNull_WhenCharacterNotInSquad()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");
            manager.Add(mate1.Object);

            // Act
            ISquadMate result = manager.GetMember(mate2.Object.Npc);

            // Assert
            result.Should().BeNull("Sebastian is not in the squad");
        }

        [Fact]
        public void GetMember_ShouldReturnNull_OnEmptySquad()
        {
            // Arrange
            var manager = new SquadManager();
            var mockMate = CreateMockSquadMate("Abigail");

            // Act
            ISquadMate result = manager.GetMember(mockMate.Object.Npc);

            // Assert
            result.Should().BeNull("squad is empty");
        }

        [Fact]
        public void Members_ShouldReturnEnumerableOfAllMates()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");

            manager.Add(mate1.Object);
            manager.Add(mate2.Object);

            // Act
            var members = manager.Members.ToList();

            // Assert
            members.Should().HaveCount(2);
            members.Should().Contain(mate1.Object);
            members.Should().Contain(mate2.Object);
        }

        [Fact]
        public void Count_ShouldReflectCurrentSquadSize()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");
            var mate3 = CreateMockSquadMate("Sam");

            // Act & Assert
            manager.Count.Should().Be(0, "initially empty");

            manager.Add(mate1.Object);
            manager.Count.Should().Be(1);

            manager.Add(mate2.Object);
            manager.Count.Should().Be(2);

            manager.Add(mate3.Object);
            manager.Count.Should().Be(3);

            manager.Remove(mate2.Object.Npc);
            manager.Count.Should().Be(2);

            manager.Clear();
            manager.Count.Should().Be(0);
        }

        [Fact]
        public void SquadManager_ShouldHandleComplexSequence()
        {
            // Arrange
            var manager = new SquadManager();
            var mate1 = CreateMockSquadMate("Abigail");
            var mate2 = CreateMockSquadMate("Sebastian");
            var mate3 = CreateMockSquadMate("Sam");
            var mate4 = CreateMockSquadMate("Emily");

            // Act & Assert - Complex sequence
            manager.Add(mate1.Object);
            manager.Add(mate2.Object);
            manager.IsRecruited(mate1.Object.Npc).Should().BeTrue();
            manager.IsRecruited(mate3.Object.Npc).Should().BeFalse();

            manager.Add(mate3.Object);
            manager.Count.Should().Be(3);

            manager.Remove(mate2.Object.Npc);
            manager.Count.Should().Be(2);
            manager.IsRecruited(mate2.Object.Npc).Should().BeFalse();

            manager.Add(mate4.Object);
            manager.Add(mate1.Object); // Duplicate, should be ignored
            manager.Count.Should().Be(3, "duplicate should not increase count");

            manager.GetMember(mate1.Object.Npc).Should().NotBeNull();
            manager.GetMember(mate2.Object.Npc).Should().BeNull();

            manager.Clear();
            manager.Count.Should().Be(0);
            manager.Members.Should().BeEmpty();
        }
    }
}
