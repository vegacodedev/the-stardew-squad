using StardewModdingAPI;
using StardewModdingAPI.Events;
using TheStardewSquad.Integrations;

namespace TheStardewSquad.Config
{
    public class GenericModConfigMenuIntegration
    {
        private readonly ModEntry _modEntry;
        private readonly IModHelper _helper;

        public GenericModConfigMenuIntegration(ModEntry modEntry, IModHelper helper)
        {
            this._modEntry = modEntry;
            this._helper = helper;
        }

        public void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = _helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            configMenu.Register(
                mod: _modEntry.ModManifest,
                reset: () => _helper.WriteConfig(new ModConfig()),
                save: () => _helper.WriteConfig(_modEntry.Config)
            );

            configMenu.AddKeybindList(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.RecruitKey,
                setValue: value => _modEntry.Config.RecruitKey = value,
                name: () => _helper.Translation.Get("config.recruitKey.name"),
                tooltip: () => _helper.Translation.Get("config.recruitKey.description")
            );

            configMenu.AddKeybindList(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.ManualTaskKey,
                setValue: value => _modEntry.Config.ManualTaskKey = value,
                name: () => _helper.Translation.Get("config.manualTaskKey.name"),
                tooltip: () => _helper.Translation.Get("config.manualTaskKey.description")
            );

            configMenu.AddBoolOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.UseSquadInventory,
                setValue: value => _modEntry.Config.UseSquadInventory = value,
                name: () => _helper.Translation.Get("config.useSquadInventory.name"),
                tooltip: () => _helper.Translation.Get("config.useSquadInventory.description")
            );

            configMenu.AddKeybindList(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.OpenSquadInventoryKey,
                setValue: value => _modEntry.Config.OpenSquadInventoryKey = value,
                name: () => _helper.Translation.Get("config.openSquadInventoryKey.name"),
                tooltip: () => _helper.Translation.Get("config.openSquadInventoryKey.description")
            );

            configMenu.AddKeybindList(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.TasksToggleKey,
                setValue: value => _modEntry.Config.TasksToggleKey = value,
                name: () => _helper.Translation.Get("config.tasksToggleKey.name"),
                tooltip: () => _helper.Translation.Get("config.tasksToggleKey.description")
            );

            configMenu.AddNumberOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.FriendshipRequirement,
                setValue: value => _modEntry.Config.FriendshipRequirement = value,
                name: () => _helper.Translation.Get("config.friendshipRequirement.name"),
                tooltip: () => _helper.Translation.Get("config.friendshipRequirement.description"),
                min: 0,
                max: 8
            );

            configMenu.AddNumberOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.MaxSquadSize,
                setValue: value => _modEntry.Config.MaxSquadSize = value,
                name: () => _helper.Translation.Get("config.maxSquadSize.name"),
                tooltip: () => _helper.Translation.Get("config.maxSquadSize.description"),
                min: 1,
                max: 100
            );

            configMenu.AddBoolOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.RecruitAllNpcs,
                setValue: value => _modEntry.Config.RecruitAllNpcs = value,
                name: () => _helper.Translation.Get("config.recruitAllNpcs.name"),
                tooltip: () => _helper.Translation.Get("config.recruitAllNpcs.description")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.DisableInteraction.ToString(),
                setValue: value => _modEntry.Config.DisableInteraction = (InteractionPreventionMode)Enum.Parse(typeof(InteractionPreventionMode), value),
                name: () => _helper.Translation.Get("config.disableInteraction.name"),
                tooltip: () => _helper.Translation.Get("config.disableInteraction.description"),
                allowedValues: Enum.GetNames(typeof(InteractionPreventionMode)),
                formatAllowedValue: value => _helper.Translation.Get($"config.disableInteraction.option.{value}")
            );

            configMenu.AddNumberOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.FriendshipPointsPerHour,
                setValue: value => _modEntry.Config.FriendshipPointsPerHour = value,
                name: () => _helper.Translation.Get("config.friendshipPointsPerHour.name"),
                tooltip: () => _helper.Translation.Get("config.friendshipPointsPerHour.description"),
                min: 0,
                max: 20
            );

            configMenu.AddBoolOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.EnableCommunication,
                setValue: value => _modEntry.Config.EnableCommunication = value,
                name: () => _helper.Translation.Get("config.enableCommunication.name"),
                tooltip: () => _helper.Translation.Get("config.enableCommunication.description")
            );

            configMenu.AddNumberOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.DialogueCooldownSeconds,
                setValue: value => _modEntry.Config.DialogueCooldownSeconds = value,
                name: () => _helper.Translation.Get("config.dialogueCooldownSeconds.name"),
                tooltip: () => _helper.Translation.Get("config.dialogueCooldownSeconds.description"),
                min: 0,
                max: 600
            );

            configMenu.AddBoolOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.EnableIdleAnimations,
                setValue: value => _modEntry.Config.EnableIdleAnimations = value,
                name: () => _helper.Translation.Get("config.enableIdleAnimations.name"),
                tooltip: () => _helper.Translation.Get("config.enableIdleAnimations.description")
            );

            configMenu.AddBoolOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.EnableGathering,
                setValue: value => _modEntry.Config.EnableGathering = value,
                name: () => _helper.Translation.Get("config.enableGathering.name"),
                tooltip: () => _helper.Translation.Get("config.enableGathering.description")
            );

            configMenu.AddBoolOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.EnableSitting,
                setValue: value => _modEntry.Config.EnableSitting = value,
                name: () => _helper.Translation.Get("config.enableSitting.name"),
                tooltip: () => _helper.Translation.Get("config.enableSitting.description")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.DisableTrashRummagingReaction.ToString(),
                setValue: value => _modEntry.Config.DisableTrashRummagingReaction = (TrashReactionMode)Enum.Parse(typeof(TrashReactionMode), value),
                name: () => _helper.Translation.Get("config.disableTrashRummagingReaction.name"),
                tooltip: () => _helper.Translation.Get("config.disableTrashRummagingReaction.description"),
                allowedValues: Enum.GetNames(typeof(TrashReactionMode)),
                formatAllowedValue: value => _helper.Translation.Get($"config.disableTrashRummagingReaction.option.{value}")
            );

            configMenu.AddSectionTitle(
                mod: _modEntry.ModManifest,
                text: () => _helper.Translation.Get("config.enabledTasks.name")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.AttackingMode.ToString(),
                setValue: value => _modEntry.Config.AttackingMode = (TaskMode)Enum.Parse(typeof(TaskMode), value),
                name: () => _helper.Translation.Get("config.attackingMode.name"),
                tooltip: () => _helper.Translation.Get("config.attackingMode.description"),
                allowedValues: Enum.GetNames(typeof(TaskMode)),
                formatAllowedValue: value => _helper.Translation.Get($"config.taskMode.option.{value}")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.HarvestingMode.ToString(),
                setValue: value => _modEntry.Config.HarvestingMode = (TaskMode)Enum.Parse(typeof(TaskMode), value),
                name: () => _helper.Translation.Get("config.harvestingMode.name"),
                tooltip: () => _helper.Translation.Get("config.harvestingMode.description"),
                allowedValues: Enum.GetNames(typeof(TaskMode)),
                formatAllowedValue: value => _helper.Translation.Get($"config.taskMode.option.{value}")
            );

            configMenu.AddNumberOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.ProtectBeehouseFlowers,
                setValue: value => _modEntry.Config.ProtectBeehouseFlowers = value,
                name: () => _helper.Translation.Get("config.protectBeehouseFlowers.name"),
                tooltip: () => _helper.Translation.Get("config.protectBeehouseFlowers.description"),
                min: 0,
                max: 20
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.ForagingMode.ToString(),
                setValue: value => _modEntry.Config.ForagingMode = (TaskMode)Enum.Parse(typeof(TaskMode), value),
                name: () => _helper.Translation.Get("config.foragingMode.name"),
                tooltip: () => _helper.Translation.Get("config.foragingMode.description"),
                allowedValues: new[] { "Disabled", "Autonomous" }, // Foraging has no Mimicking mode
                formatAllowedValue: value => _helper.Translation.Get($"config.taskMode.option.{value}")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.LumberingMode.ToString(),
                setValue: value => _modEntry.Config.LumberingMode = (TaskMode)Enum.Parse(typeof(TaskMode), value),
                name: () => _helper.Translation.Get("config.lumberingMode.name"),
                tooltip: () => _helper.Translation.Get("config.lumberingMode.description"),
                allowedValues: Enum.GetNames(typeof(TaskMode)), // Autonomous mode: auto-hit twigs, mimic for trees
                formatAllowedValue: value => _helper.Translation.Get($"config.taskMode.option.{value}")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.MiningMode.ToString(),
                setValue: value => _modEntry.Config.MiningMode = (TaskMode)Enum.Parse(typeof(TaskMode), value),
                name: () => _helper.Translation.Get("config.miningMode.name"),
                tooltip: () => _helper.Translation.Get("config.miningMode.description"),
                allowedValues: Enum.GetNames(typeof(TaskMode)),
                formatAllowedValue: value => _helper.Translation.Get($"config.taskMode.option.{value}")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.WateringMode.ToString(),
                setValue: value => _modEntry.Config.WateringMode = (TaskMode)Enum.Parse(typeof(TaskMode), value),
                name: () => _helper.Translation.Get("config.wateringMode.name"),
                tooltip: () => _helper.Translation.Get("config.wateringMode.description"),
                allowedValues: Enum.GetNames(typeof(TaskMode)),
                formatAllowedValue: value => _helper.Translation.Get($"config.taskMode.option.{value}")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.FishingMode.ToString(),
                setValue: value => _modEntry.Config.FishingMode = (TaskMode)Enum.Parse(typeof(TaskMode), value),
                name: () => _helper.Translation.Get("config.fishingMode.name"),
                tooltip: () => _helper.Translation.Get("config.fishingMode.description"),
                allowedValues: new[] { "Disabled", "Mimicking" }, // Fishing has no Autonomous mode
                formatAllowedValue: value => _helper.Translation.Get($"config.taskMode.option.{value}")
            );

            configMenu.AddTextOption(
                mod: _modEntry.ModManifest,
                getValue: () => _modEntry.Config.PettingMode.ToString(),
                setValue: value => _modEntry.Config.PettingMode = (TaskMode)Enum.Parse(typeof(TaskMode), value),
                name: () => _helper.Translation.Get("config.pettingMode.name"),
                tooltip: () => _helper.Translation.Get("config.pettingMode.description"),
                allowedValues: Enum.GetNames(typeof(TaskMode)),
                formatAllowedValue: value => _helper.Translation.Get($"config.taskMode.option.{value}")
            );
        }
    }
}
