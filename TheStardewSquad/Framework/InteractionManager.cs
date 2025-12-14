using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Abstractions.UI;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.UI;
using TheStardewSquad.Pathfinding;

namespace TheStardewSquad.Framework
{
    public class InteractionManager
    {
        private readonly IModHelper _helper;
        private readonly SquadManager _squadManager;
        private readonly BehaviorManager _behaviorManager;
        private readonly IGameContext _gameContext;
        private readonly IUIService _uiService;
        private readonly ModConfig _config;

        public bool PlayerIsAttemptingToUseTool { get; set; } = false;
        public SquadMateFactory SquadMateFactory { get; internal set; }

        public InteractionManager(IModHelper helper, SquadManager squadManager, SquadMateFactory squadMateFactory, BehaviorManager behaviorManager, IGameContext gameContext, IUIService uiService, ModConfig config)
        {
            this._helper = helper;
            this._squadManager = squadManager;
            this.SquadMateFactory = squadMateFactory;
            this._behaviorManager = behaviorManager;
            this._gameContext = gameContext;
            this._uiService = uiService;
            this._config = config;
        }

        public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Don't process mod keybinds if the player is in a menu, dialogue, cutscene, or if the world isn't ready.
            if (!_gameContext.IsPlayerFree)
                return;

            Farmer player = _gameContext.Player;
            GameLocation currentLocation = _gameContext.CurrentLocation;

            // If the action button was pressed on a squad mate, cancel any idle animation.
            // This prevents them from getting stuck if they were animating when talked to.
            if (e.Button.IsActionButton())
            {
                Character character = _gameContext.GetCharacterAtTile(currentLocation, player.GetGrabTile().ToPoint())
                                   ?? _gameContext.GetCharacterAtTile(currentLocation, e.Cursor.Tile.ToPoint());

                if (character is NPC npc && this._squadManager.IsRecruited(npc))
                {
                    var mate = this._squadManager.GetMember(npc);
                    this.DisableIdleAnimation(mate);
                }
            }

            if (_config.RecruitKey.JustPressed())
            {
                Character character = _gameContext.GetCharacterAtTile(currentLocation, player.GetGrabTile().ToPoint())
                                   ?? _gameContext.GetCharacterAtTile(currentLocation, e.Cursor.Tile.ToPoint());

                if (_gameContext.IsFestival)
                {
                    _uiService.ShowErrorMessage(_uiService.GetTranslation("recruitment.festival_block"));
                }
                else if (character is NPC npc && (_config.RecruitAllNpcs || _gameContext.HasFriendship(npc.Name) || npc is Pet))
                {
                    // For pets, there's no friendship requirement. The Create() method
                    // will handle giving them the right behavior.
                    if (this._squadManager.IsRecruited(npc))
                    {
                        ISquadMate mate = this._squadManager.GetMember(npc);
                        this.DisableIdleAnimation(mate);
                        mate.HandleManagement();
                    }
                    else
                    {
                        // The factory will now correctly create a pet or NPC mate
                        ISquadMate potentialMate = this.SquadMateFactory.Create(npc);
                        if (potentialMate != null)
                        {
                            // Friendship checks are handled inside the behavior itself, so pets will bypass it.
                            potentialMate.HandleRecruitment(player);
                        }
                    }
                }
            }

            if (_config.ManualTaskKey.JustPressed())
            {
                if (this._squadManager.Count == 0)
                {
                    _uiService.ShowErrorMessage(_uiService.GetTranslation("commands.noFollower"));
                    return;
                }

                Point commandTile;
                if (_gameContext.IsGamepadControls)
                {
                    commandTile = player.GetGrabTile().ToPoint();
                }
                else
                {
                    commandTile = e.Cursor.Tile.ToPoint();
                }

                this.HandleManualCommand(commandTile);
            }

            if (_config.OpenSquadInventoryKey.JustPressed())
            {
                this.ShowSquadInventory();
                return;
            }

            if (_config.TasksToggleKey.JustPressed())
            {
                _config.TasksEnabled = !_config.TasksEnabled;
                _helper.WriteConfig(_config);

                string messageKey = _config.TasksEnabled ? "config.tasksToggle.enabled" : "config.tasksToggle.disabled";
                _uiService.ShowMessage(_uiService.GetTranslation(messageKey));
                return;
            }
        }

        private void HandleManualCommand(Point tile)
        {
            if (this._squadManager.Count == 0)
            {
                _uiService.ShowErrorMessage(_uiService.GetTranslation("commands.noFollower"));
                return;
            }

            var location = _gameContext.CurrentLocation;

            // 1. First, determine what the command is.
            Point? targetObjectTile = null;
            Character targetCharacter = null;
            TaskType? taskType = null;

            var monster = TaskManager.FindHostileMonsterAt(location, tile);
            if (monster != null)
            {
                taskType = TaskType.Attacking;
                targetObjectTile = monster.TilePoint;
                targetCharacter = monster;
            }
            else if (location.objects.TryGetValue(tile.ToVector2(), out var obj))
            {
                if (obj.Name == "Twig")
                {
                    taskType = TaskType.Lumbering;
                    targetObjectTile = tile;
                }
                else if (obj.BaseName == "Stone")
                {
                    taskType = TaskType.Mining;
                    targetObjectTile = tile;
                }
                else if (obj.isForage())
                {
                    taskType = TaskType.Foraging;
                    targetObjectTile = tile;
                }
            }
            else if (location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature))
            {
                if (feature is Tree tree && tree.health.Value > 0)
                {
                    taskType = TaskType.Lumbering;
                    targetObjectTile = tile;
                }
                else if (feature is HoeDirt dirt)
                {
                    if (dirt.crop != null && dirt.readyForHarvest())
                    {
                        taskType = TaskType.Harvesting;
                        targetObjectTile = tile;
                    }
                    else if (dirt.state.Value == HoeDirt.dry)
                    {
                        taskType = TaskType.Watering;
                        targetObjectTile = tile;
                    }
                }
                else if (feature is Bush bush && bush.tileSheetOffset.Value == 1 && bush.readyForHarvest() && bush.inBloom() && !bush.townBush.Value) // Ready berry bush
                {
                    taskType = TaskType.Foraging;
                    targetObjectTile = tile;
                }
            }

            // If no valid task was identified, do nothing.
            if (taskType == null || !targetObjectTile.HasValue) return;

            // 2. Now that we know the task, find WHO is the best follower for it.
            ISquadMate bestMate = this._squadManager.Members
                .Where(m => m.CanPerformTask(taskType.Value)) // A: Filter by who can do the task.
                .OrderBy(m => Vector2.DistanceSquared(m.Npc.Tile, tile.ToVector2())) // B: then sort the capable ones by distance.
                .FirstOrDefault(); // C: Get the best one.

            // If no one in the squad can perform this task, do nothing.
            if (bestMate == null) return;

            this.DisableIdleAnimation(bestMate);

            // 3. Find the best interaction spot for the chosen follower.
            Point? interactionTile = null;
            if (taskType == TaskType.Harvesting)
            {
                if (location.terrainFeatures[targetObjectTile.Value.ToVector2()] is HoeDirt { crop: { } } dirt && dirt.crop.raisedSeeds.Value)
                {
                    // It's a trellis crop, so stand next to it.
                    interactionTile = AStarPathfinder.FindClosestPassableNeighbor(location, targetObjectTile.Value, bestMate.Npc, null);
                }
                else
                {
                    // It's a normal crop, so stand on it.
                    interactionTile = targetObjectTile;
                }
            }
            else if (taskType == TaskType.Watering)
            {
                // Stand on the dry tile.
                interactionTile = targetObjectTile;
            }
            else
            {
                // For Attacking, Lumbering, Mining, and Foraging, stand next to the target.
                interactionTile = AStarPathfinder.FindClosestPassableNeighbor(location, targetObjectTile.Value, bestMate.Npc, null);
            }

            if (!interactionTile.HasValue) return;

            // 4. Create the task with the explicitly calculated interaction spot.
            var newTask = new SquadTask(taskType.Value, targetObjectTile.Value, interactionTile.Value, targetCharacter, isManual: true);

            bestMate.Halt();
            // 5. Assign the high-priority task.
            bestMate.Task = newTask;
            bestMate.IsCatchingUp = false;
            bestMate.Path.Clear();
            bestMate.ActionCooldown = 0;
            bestMate.CurrentMoveDirection = -1;
        }

        public void ShowSquadInventory()
        {
            _uiService.SetActiveMenu(new SquadInventoryMenu(_helper));
        }

        private void DisableIdleAnimation(ISquadMate mate)
        {
            if (mate.IsAnimating)
            {
                mate.Halt();
                mate.ActionCooldown = 0;
            }
        }
    }
}
