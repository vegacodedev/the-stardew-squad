using System.Reflection;

namespace TheStardewSquad.Tests.Helpers;

/// <summary>
/// Helper for creating ModConfig instances in tests without triggering SMAPI dependencies.
/// </summary>
public static class ConfigTestHelper
{
    /// <summary>
    /// Creates a ModConfig instance with default task modes, bypassing keybind initialization.
    /// This avoids the SMAPI.Toolkit dependency that would fail in unit tests.
    /// </summary>
    public static ModConfig CreateTestConfig()
    {
        // Use reflection to create ModConfig without calling the constructor
        // which would initialize keybinds and fail without SMAPI.Toolkit
        var config = (ModConfig)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(ModConfig));

        // Manually set default values for TaskMode properties
        // These defaults match what's in ModConfig.cs
        config.FriendshipRequirement = 4;
        config.MaxSquadSize = 3;
        config.RecruitAllNpcs = false;
        config.DisableInteraction = InteractionPreventionMode.CombatOnly;
        config.FriendshipPointsPerHour = 7;
        config.DisableTrashRummagingReaction = TrashReactionMode.Everyone;
        config.EnableCommunication = true;
        config.EnableIdleAnimations = true;
        config.EnableGathering = true;

        // Task modes (matching ModConfig defaults)
        config.WateringMode = TaskMode.Mimicking;
        config.LumberingMode = TaskMode.Mimicking;
        config.MiningMode = TaskMode.Mimicking;
        config.AttackingMode = TaskMode.Mimicking;
        config.HarvestingMode = TaskMode.Mimicking;
        config.ForagingMode = TaskMode.Autonomous;
        config.FishingMode = TaskMode.Mimicking;
        config.PettingMode = TaskMode.Mimicking;

        // Task toggle and beehouse protection
        config.TasksEnabled = true;
        config.ProtectBeehouseFlowers = 5;

        return config;
    }

    /// <summary>
    /// Creates a ModConfig with custom task modes for testing.
    /// </summary>
    public static ModConfig CreateTestConfigWithModes(
        TaskMode? watering = null,
        TaskMode? lumbering = null,
        TaskMode? mining = null,
        TaskMode? attacking = null,
        TaskMode? harvesting = null,
        TaskMode? foraging = null,
        TaskMode? fishing = null,
        TaskMode? petting = null)
    {
        var config = CreateTestConfig();

        if (watering.HasValue) config.WateringMode = watering.Value;
        if (lumbering.HasValue) config.LumberingMode = lumbering.Value;
        if (mining.HasValue) config.MiningMode = mining.Value;
        if (attacking.HasValue) config.AttackingMode = attacking.Value;
        if (harvesting.HasValue) config.HarvestingMode = harvesting.Value;
        if (foraging.HasValue) config.ForagingMode = foraging.Value;
        if (fishing.HasValue) config.FishingMode = fishing.Value;
        if (petting.HasValue) config.PettingMode = petting.Value;

        return config;
    }
}
