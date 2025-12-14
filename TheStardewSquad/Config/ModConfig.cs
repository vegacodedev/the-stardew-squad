using StardewModdingAPI;
using StardewModdingAPI.Utilities;
public enum InteractionPreventionMode
{
    /// <summary>Interaction is never disabled.</summary>
    Never,
    /// <summary>Interaction is disabled only in combat zones.</summary>
    CombatOnly,
    /// <summary>Interaction is disabled everywhere.</summary>
    Always
}
public enum TrashReactionMode
{
    /// <summary>The trash rummaging reaction is never disabled.</summary>
    Never,
    /// <summary>The reaction is disabled only for recruited pets.</summary>
    PetsOnly,
    /// <summary>The reaction is disabled for all recruited squad members.</summary>
    Everyone
}
public enum TaskMode
{
    /// <summary>Task is completely disabled.</summary>
    Disabled,
    /// <summary>NPCs only perform task when player is actively doing it.</summary>
    Mimicking,
    /// <summary>NPCs perform task autonomously.</summary>
    Autonomous
}
public sealed class ModConfig
{
    public KeybindList RecruitKey { get; set; } = new KeybindList(SButton.E);
    public KeybindList ManualTaskKey { get; set; } = new KeybindList(SButton.F);

    public bool UseSquadInventory { get; set; } = true;

    public KeybindList OpenSquadInventoryKey { get; set; } = KeybindList.Parse("LeftAlt+E");

    public KeybindList TasksToggleKey { get; set; } = KeybindList.Parse("LeftAlt+F");

    public bool TasksEnabled { get; set; } = true;

    public int FriendshipRequirement { get; set; } = 4;

    public int MaxSquadSize { get; set; } = 3;
    public bool RecruitAllNpcs { get; set; } = false;

    public InteractionPreventionMode DisableInteraction { get; set; } = InteractionPreventionMode.CombatOnly;

    public int FriendshipPointsPerHour { get; set; } = 7;

    public TrashReactionMode DisableTrashRummagingReaction { get; set; } = TrashReactionMode.Everyone;

    public bool EnableCommunication { get; set; } = true;

    public int DialogueCooldownSeconds { get; set; } = 120;

    public bool EnableIdleAnimations { get; set; } = true;

    public bool EnableGathering { get; set; } = true;

    public bool EnableSitting { get; set; } = true;

    public TaskMode WateringMode { get; set; } = TaskMode.Mimicking;
    public TaskMode LumberingMode { get; set; } = TaskMode.Mimicking;
    public TaskMode MiningMode { get; set; } = TaskMode.Mimicking;
    public TaskMode AttackingMode { get; set; } = TaskMode.Mimicking;
    public TaskMode HarvestingMode { get; set; } = TaskMode.Mimicking;
    public TaskMode ForagingMode { get; set; } = TaskMode.Autonomous;
    public TaskMode FishingMode { get; set; } = TaskMode.Mimicking;
    public TaskMode PettingMode { get; set; } = TaskMode.Mimicking;
    public TaskMode SittingMode { get; set; } = TaskMode.Mimicking;

    /// <summary>
    /// Number of tiles around beehouses where flowers are protected from harvesting.
    /// Set to 0 to disable protection.
    /// </summary>
    public int ProtectBeehouseFlowers { get; set; } = 5;
}