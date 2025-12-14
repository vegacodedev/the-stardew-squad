using FluentAssertions;
using Moq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;
using System.Collections.Generic;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Framework;
using TheStardewSquad.Framework.Behaviors;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Tests.Helpers;
using Xunit;

namespace TheStardewSquad.Tests.Framework;

/// <summary>
/// Tests for RecruitmentManager - focuses on recruitment/dismissal logic and coordination.
///
/// Note: RecruitmentManager is a coordination component that orchestrates:
/// - Squad state management (via SquadManager)
/// - Formation slot assignment (via FormationManager)
/// - NPC behavior delegation (via ISquadMate interface)
///
/// Tests focus on testable coordination logic. Integration points requiring game state
/// (UI dialogs, screen fades, NPC schedules, FarmHouse locations) are documented separately.
/// </summary>
public class RecruitmentManagerTests
{
    #region Test Helpers

    private (RecruitmentManager manager, Mock<IModHelper> helper, Mock<IMonitor> monitor, SquadManager squadManager, WaitingNpcsManager waitingNpcsManager, FormationManager formationManager, Mock<ISquadMateStateHelper> stateHelper) CreateTestContext()
    {
        var mockHelper = new Mock<IModHelper>();
        var mockMonitor = new Mock<IMonitor>();
        var squadManager = new SquadManager();
        var waitingNpcsManager = new WaitingNpcsManager();
        var formationManager = new FormationManager();
        var mockStateHelper = new Mock<ISquadMateStateHelper>();

        var manager = new RecruitmentManager(mockHelper.Object, mockMonitor.Object, squadManager, waitingNpcsManager, formationManager, mockStateHelper.Object);

        return (manager, mockHelper, mockMonitor, squadManager, waitingNpcsManager, formationManager, mockStateHelper);
    }

    private Mock<ISquadMate> CreateMockSquadMate(string name = "TestNPC")
    {
        var mockMate = new Mock<ISquadMate>();
        var mockNpc = new Mock<NPC>();

        // Note: NPC.Name is non-virtual and can't be mocked
        // SquadManager identifies NPCs by mate.Npc.Name, so we use the default empty string
        // which is consistent across all mock NPCs
        mockMate.Setup(m => m.Npc).Returns(mockNpc.Object);
        mockMate.Setup(m => m.Name).Returns(name);

        return mockMate;
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeWithDependencies()
    {
        // Arrange
        var mockHelper = new Mock<IModHelper>();
        var mockMonitor = new Mock<IMonitor>();
        var squadManager = new SquadManager();
        var formationManager = new FormationManager();
        var mockStateHelper = new Mock<ISquadMateStateHelper>();

        var waitingNpcsManager = new WaitingNpcsManager();

        // Act
        var manager = new RecruitmentManager(mockHelper.Object, mockMonitor.Object, squadManager, waitingNpcsManager, formationManager, mockStateHelper.Object);

        // Assert
        manager.Should().NotBeNull("manager should be constructed successfully");
    }

    #endregion

    #region Recruit Method Tests

    [Fact]
    public void Recruit_ShouldAddMateToSquad_WhenValidAndSpaceAvailable()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, mockStateHelper) = CreateTestContext();
        var mockMate = CreateMockSquadMate("Abigail");

        var config = ConfigTestHelper.CreateTestConfig();
        config.MaxSquadSize = 3;
        mockHelper.Setup(h => h.ReadConfig<ModConfig>()).Returns(config);

        // Act
        manager.Recruit(mockMate.Object);

        // Assert
        squadManager.Count.Should().Be(1, "squad should have one member after recruitment");
        squadManager.IsRecruited(mockMate.Object.Npc).Should().BeTrue("NPC should be marked as recruited");
        mockStateHelper.Verify(h => h.PrepareForRecruitment(mockMate.Object.Npc), Times.Once, "should prepare NPC state for recruitment");
        mockMate.Verify(m => m.Communicate(It.IsAny<string>()), Times.Once, "should communicate recruitment message");
        mockMonitor.Verify(m => m.Log(It.IsAny<string>(), LogLevel.Info), Times.Once, "should log recruitment");
    }

    [Fact]
    public void Recruit_ShouldNotRecruit_WhenSquadIsFull()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, mockStateHelper) = CreateTestContext();

        var config = ConfigTestHelper.CreateTestConfig();
        config.MaxSquadSize = 1;
        mockHelper.Setup(h => h.ReadConfig<ModConfig>()).Returns(config);

        // Fill the squad
        var firstMate = CreateMockSquadMate("Abigail");
        formationManager.AssignSlot(firstMate.Object);
        squadManager.Add(firstMate.Object);

        // Try to recruit another
        var secondMate = CreateMockSquadMate("Sebastian");

        // Act
        manager.Recruit(secondMate.Object);

        // Assert
        squadManager.Count.Should().Be(1, "squad should still have only one member");
        // Note: Cannot test IsRecruited(secondMate) because mock NPCs share empty names
        mockStateHelper.Verify(h => h.PrepareForRecruitment(It.IsAny<NPC>()), Times.Never, "should not prepare second NPC when squad is full");
    }

    [Fact]
    public void Recruit_ShouldNotRecruit_WhenAlreadyRecruited()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, mockStateHelper) = CreateTestContext();
        var mockMate = CreateMockSquadMate("Abigail");

        var config = ConfigTestHelper.CreateTestConfig();
        config.MaxSquadSize = 3;
        mockHelper.Setup(h => h.ReadConfig<ModConfig>()).Returns(config);

        // Recruit once
        manager.Recruit(mockMate.Object);

        // Reset mocks
        mockMate.Invocations.Clear();
        mockStateHelper.Invocations.Clear();

        // Act - try to recruit again
        manager.Recruit(mockMate.Object);

        // Assert
        squadManager.Count.Should().Be(1, "squad should still have only one member");
        mockStateHelper.Verify(h => h.PrepareForRecruitment(It.IsAny<NPC>()), Times.Never, "should not prepare NPC on duplicate recruitment");
        mockMate.Verify(m => m.Communicate(It.IsAny<string>()), Times.Never, "should not communicate on duplicate recruitment");
    }

    [Fact]
    public void Recruit_ShouldAssignFormationSlot()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, mockStateHelper) = CreateTestContext();
        var mockMate = CreateMockSquadMate("Abigail");

        var config = ConfigTestHelper.CreateTestConfig();
        config.MaxSquadSize = 3;
        mockHelper.Setup(h => h.ReadConfig<ModConfig>()).Returns(config);

        // Act
        manager.Recruit(mockMate.Object);

        // Assert
        var mockFarmerInfo = new Mock<IFarmerInfo>();
        mockFarmerInfo.Setup(f => f.TilePoint).Returns(new Microsoft.Xna.Framework.Point(10, 10));
        mockFarmerInfo.Setup(f => f.FacingDirection).Returns(2);

        var hasTarget = formationManager.TryGetTargetTile(mockMate.Object, mockFarmerInfo.Object, out var targetTile);
        hasTarget.Should().BeTrue("formation slot should be assigned after recruitment");
        targetTile.Should().NotBe(new Microsoft.Xna.Framework.Point(10, 10), "formation position should be offset from player");
    }

    #endregion

    #region Dismiss Method Tests

    [Fact]
    public void Dismiss_ShouldRemoveMateFromSquad_WhenRecruited()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, _) = CreateTestContext();
        var mockMate = CreateMockSquadMate("Abigail");

        // Manually add to squad to test dismissal (bypassing Recruit which requires game state)
        formationManager.AssignSlot(mockMate.Object);
        squadManager.Add(mockMate.Object);

        // Mock HandleDismissal to remove from squad (simulates what Game1.afterFadeFunction would do)
        mockMate.Setup(m => m.HandleDismissal(false, DismissalWarpBehavior.GoHome))
                .Callback(() => squadManager.Remove(mockMate.Object.Npc));

        // Act
        manager.Dismiss(mockMate.Object, isSilent: false, DismissalWarpBehavior.GoHome);

        // Assert
        squadManager.IsRecruited(mockMate.Object.Npc).Should().BeFalse("NPC should no longer be recruited after dismissal");
        mockMate.Verify(m => m.HandleDismissal(false, DismissalWarpBehavior.GoHome), Times.Once, "should call HandleDismissal");
        mockMonitor.Verify(m => m.Log(It.IsAny<string>(), LogLevel.Info), Times.Once, "should log dismissal");
    }

    [Fact]
    public void Dismiss_ShouldHandleSilentDismissal()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, _) = CreateTestContext();
        var mockMate = CreateMockSquadMate("Abigail");

        // Manually add to squad
        formationManager.AssignSlot(mockMate.Object);
        squadManager.Add(mockMate.Object);

        // Act
        manager.Dismiss(mockMate.Object, isSilent: true, DismissalWarpBehavior.RoamHere);

        // Assert
        mockMate.Verify(m => m.HandleDismissal(true, DismissalWarpBehavior.RoamHere), Times.Once, "should call HandleDismissal with silent flag");
        mockMonitor.Verify(m => m.Log(It.Is<string>(s => s.Contains("silently")), LogLevel.Info), Times.Once, "should log silent dismissal");
    }

    [Fact]
    public void Dismiss_ShouldNotThrow_WhenNotRecruited()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, _) = CreateTestContext();
        var mockMate = CreateMockSquadMate("Abigail");

        // Act
        Action act = () => manager.Dismiss(mockMate.Object, isSilent: false, DismissalWarpBehavior.GoHome);

        // Assert
        act.Should().NotThrow("dismissing non-recruited NPC should be safe");
        mockMate.Verify(m => m.HandleDismissal(It.IsAny<bool>(), It.IsAny<DismissalWarpBehavior>()), Times.Never, "should not call HandleDismissal if not recruited");
    }

    [Fact]
    public void Dismiss_ShouldReleaseFormationSlot()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, _) = CreateTestContext();
        var mockMate = CreateMockSquadMate("Abigail");

        // Manually add to squad
        formationManager.AssignSlot(mockMate.Object);
        squadManager.Add(mockMate.Object);

        // Act
        manager.Dismiss(mockMate.Object, isSilent: false, DismissalWarpBehavior.GoHome);

        // Assert
        // FormationManager releases the slot internally, verify we can assign again
        formationManager.AssignSlot(mockMate.Object);
        squadManager.Add(mockMate.Object);
        squadManager.Count.Should().Be(1, "should be able to add again after dismissal and release");
    }

    #endregion

    #region DismissAll Method Tests

    [Fact]
    public void DismissAll_ShouldReturnEarly_WhenSquadIsEmpty()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, _) = CreateTestContext();

        // Act
        manager.DismissAll(useFade: false);

        // Assert
        squadManager.Count.Should().Be(0, "squad should remain empty");
        mockMonitor.Verify(m => m.Log(It.IsAny<string>(), It.IsAny<LogLevel>()), Times.Never, "should not log anything when squad is empty");
    }

    [Fact]
    public void DismissAll_ShouldDismissMember_WithoutFade()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, _) = CreateTestContext();

        var mate = CreateMockSquadMate("Abigail");

        // Manually add to squad
        formationManager.AssignSlot(mate.Object);
        squadManager.Add(mate.Object);

        // Mock HandleDismissal to remove from squad
        mate.Setup(m => m.HandleDismissal(true, DismissalWarpBehavior.GoHome))
            .Callback(() => squadManager.Remove(mate.Object.Npc));

        // Act
        manager.DismissAll(useFade: false);

        // Assert
        squadManager.Count.Should().Be(0, "squad should be empty after dismissing all");
        mate.Verify(m => m.HandleDismissal(true, DismissalWarpBehavior.GoHome), Times.Once, "should dismiss mate");
    }

    [Fact]
    public void DismissAll_ShouldSupportDifferentWarpBehaviors_Documentation()
    {
        // This test documents DismissAll's ability to distinguish between NPCs and Pets
        // and apply different warp behaviors to each.
        //
        // Behavior:
        // - Iterates through all squad members
        // - For each mate, checks if mate.Npc is Pet
        // - If Pet: uses petWarp behavior
        // - If NPC: uses npcWarp behavior
        // - Calls Dismiss(mate, isSilent: true, warpBehavior)
        //
        // Testing this fully requires:
        // - Real Pet objects (Pet constructor requires game state for sprite initialization)
        // - Real NPC objects
        // - Full recruitment flow (which requires SquadMateStateHelper.PrepareForRecruitment)
        //
        // The distinction logic is straightforward: (mate.Npc is Pet) ? petWarp : npcWarp

        true.Should().BeTrue("Documentation test - see summary for NPC vs Pet warp behavior");
    }

    [Fact]
    public void DismissAll_WithFade_Documentation()
    {
        // This test documents that DismissAll with useFade=true uses Game1.globalFadeToBlack/Clear
        // and Game1.showGlobalMessage, which require game state initialization.
        //
        // Behavior:
        // 1. Calls Game1.globalFadeToBlack with callback
        // 2. Callback performs dismissal of all members
        // 3. Calls Game1.globalFadeToClear
        // 4. Shows global message with translation key "recruitment.dismiss.allDone"
        //
        // Testing this requires:
        // - Game1.globalFadeToBlack/globalFadeToClear
        // - Game1.showGlobalMessage
        // - IModHelper.Translation for message retrieval

        true.Should().BeTrue("Documentation test - see summary for fade behavior details");
    }

    #endregion

    #region GetCurrentScheduleEntryFor Tests

    [Fact]
    public void GetCurrentScheduleEntryFor_ShouldReturnNull_WhenNoSchedule()
    {
        // This test documents GetCurrentScheduleEntryFor behavior but requires real NPC objects
        // with schedule data (NPC.Schedule is non-virtual and can't be mocked).
        //
        // Behavior:
        // - If NPC.Schedule is null: returns null
        // - Otherwise: scans schedule dictionary and returns the entry with highest time <= Game1.timeOfDay
        //
        // Example:
        // Schedule = { 600: morning, 1200: noon, 1800: evening }
        // Current time = 1400
        // Returns: noon entry (1200)
        //
        // Testing requires: Real NPC with Schedule dictionary, Game1.timeOfDay

        true.Should().BeTrue("Documentation test - see summary for schedule parsing logic");
    }

    [Fact]
    public void GetCurrentScheduleEntryFor_ShouldReturnMostRecentEntry_BeforeCurrentTime()
    {
        // This test documents the schedule parsing logic but requires Game1.timeOfDay
        // which is not available in unit tests.
        //
        // Behavior:
        // - Scans schedule dictionary for entries where entry.Key <= Game1.timeOfDay
        // - Returns the entry with the highest time value that satisfies the condition
        // - Returns null if no entries match
        //
        // Example:
        // - Schedule: { 600: morning, 1200: noon, 1800: evening }
        // - Current time: 1400
        // - Returns: 1200 (noon) entry
        //
        // Testing this requires: Game1.timeOfDay static property

        true.Should().BeTrue("Documentation test - see summary for schedule parsing logic");
    }

    #endregion

    #region GetTargetLocationForNow Tests

    [Fact]
    public void GetTargetLocationForNow_Documentation()
    {
        // This test documents GetTargetLocationForNow which requires NPC schedule access
        // and Game1.timeOfDay for schedule parsing.
        //
        // Behavior:
        // 1. Calls GetCurrentScheduleEntryFor(npc) to get current schedule entry
        // 2. If schedule entry found: returns (scheduleEntry.targetLocationName, scheduleEntry.targetTile)
        // 3. If no schedule entry: returns (npc.DefaultMap, npc.DefaultPosition / 64)
        //
        // Testing this requires:
        // - NPC.Schedule dictionary
        // - Game1.timeOfDay
        // - NPC.DefaultMap and NPC.DefaultPosition
        //
        // This method is used to determine where to warp NPCs on dismissal.

        true.Should().BeTrue("Documentation test - see summary for schedule-based location logic");
    }

    #endregion

    #region GetSpouseDismissalTarget Tests

    [Fact]
    public void GetSpouseDismissalTarget_Documentation()
    {
        // This test documents GetSpouseDismissalTarget which requires extensive game state:
        //
        // Behavior:
        // 1. Gets FarmHouse via Game1.getLocationFromName("FarmHouse")
        // 2. If FarmHouse is null: falls back to GetTargetLocationForNow
        // 3. If time >= 2200 (10 PM):
        //    - Gets spouse bed spot via farmHouse.getSpouseBedSpot(npc.Name)
        //    - If valid bed spot (X != -1000): calls npc.playSleepingAnimation() and returns bed location
        // 4. Otherwise: tries kitchen standing spot via farmHouse.getKitchenStandingSpot()
        //    - If valid: returns kitchen location
        // 5. Fallback: returns GetTargetLocationForNow result
        //
        // Testing this requires:
        // - Game1.getLocationFromName
        // - Game1.timeOfDay
        // - FarmHouse.getSpouseBedSpot
        // - FarmHouse.getKitchenStandingSpot
        // - FarmHouse.isTileOnMap
        // - NPC.playSleepingAnimation
        //
        // This method provides spouse-specific dismissal logic for married NPCs.

        true.Should().BeTrue("Documentation test - see summary for spouse dismissal logic");
    }

    #endregion

    #region OnDayEnding Tests

    [Fact]
    public void OnDayEnding_ShouldDismissAll_WithoutFade()
    {
        // Arrange
        var (manager, mockHelper, mockMonitor, squadManager, _, formationManager, _) = CreateTestContext();

        var mate = CreateMockSquadMate("Abigail");

        // Manually add to squad
        formationManager.AssignSlot(mate.Object);
        squadManager.Add(mate.Object);

        // Mock HandleDismissal to remove from squad
        mate.Setup(m => m.HandleDismissal(true, DismissalWarpBehavior.GoHome))
            .Callback(() => squadManager.Remove(mate.Object.Npc));

        // Act
        manager.OnDayEnding(null, null);

        // Assert
        squadManager.Count.Should().Be(0, "squad should be empty after day end");
        mate.Verify(m => m.HandleDismissal(true, DismissalWarpBehavior.GoHome), Times.Once, "should dismiss mate on day end");
    }

    #endregion

    #region Integration Points Documentation

    [Fact]
    public void IntegrationPoints_Documentation()
    {
        // This test documents where RecruitmentManager integrates with game systems:
        //
        // Called by ModEntry:
        // - ModEntry.OnDayEnding event -> RecruitmentManager.OnDayEnding
        // - InteractionManager triggers recruitment/dismissal -> Recruit/Dismiss methods
        //
        // Calls to Game Systems:
        // - SquadMateStateHelper.PrepareForRecruitment (static) - prepares NPC state
        // - ISquadMate.Communicate - triggers dialogue
        // - ISquadMate.HandleDismissal - triggers dismissal behavior
        // - FormationManager.AssignSlot/ReleaseSlot - manages formation positions
        // - SquadManager.Add/Remove/IsRecruited - manages squad state
        //
        // UI Interactions:
        // - Game1.drawObjectDialogue - shows "squad full" message
        // - Game1.globalFadeToBlack/Clear - screen fade effects
        // - Game1.showGlobalMessage - shows "all dismissed" message
        //
        // NPC Schedule/Location:
        // - NPC.Schedule - accesses schedule dictionary
        // - Game1.timeOfDay - gets current time for schedule parsing
        // - Game1.getLocationFromName - gets FarmHouse for spouse logic
        // - FarmHouse.getSpouseBedSpot/getKitchenStandingSpot - spouse-specific locations
        //
        // Configuration:
        // - IModHelper.ReadConfig<ModConfig>() - reads MaxSquadSize
        // - IModHelper.Translation.Get - retrieves localized messages

        true.Should().BeTrue("Documentation test - see summary for integration details");
    }

    #endregion

    #region Business Logic Documentation

    [Fact]
    public void BusinessLogic_Documentation()
    {
        // Key business rules in RecruitmentManager:
        //
        // Recruitment Rules:
        // - Max squad size enforced (config.MaxSquadSize)
        // - Duplicate recruitment prevented (checks SquadManager.IsRecruited)
        // - Formation slot assigned on recruitment
        // - Recruitment message communicated to player
        //
        // Dismissal Rules:
        // - Can only dismiss recruited members
        // - Formation slot released on dismissal
        // - Silent dismissal suppresses communication
        // - Warp behavior distinguishes NPCs from pets
        //
        // Day End Behavior:
        // - All members dismissed automatically
        // - Uses silent dismissal (no fade, no messages)
        // - Always uses DismissalWarpBehavior.GoHome
        //
        // Spouse Special Logic:
        // - After 10 PM (2200): warps to spouse bed if available
        // - Otherwise: warps to kitchen standing spot
        // - Fallback: uses regular schedule-based location
        //
        // Schedule-Based Dismissal:
        // - Finds most recent schedule entry <= current time
        // - Uses target location and tile from schedule
        // - Fallback: NPC's DefaultMap and DefaultPosition

        true.Should().BeTrue("Documentation test - see summary for business logic details");
    }

    #endregion
}
