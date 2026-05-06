using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Framework.Multiplayer;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.UI;
using TheStardewSquad.Pathfinding;

namespace TheStardewSquad.Framework
{
    public class InteractionManager
    {
        private static readonly Shears _sharedShears = new();
        private static readonly MilkPail _sharedMilkPail = new();

        private readonly IModHelper _helper;
        private readonly SquadManager _squadManager;
        private readonly BehaviorManager _behaviorManager;
        private readonly IGameContext _gameContext;
        private readonly ModConfig _config;

        // Tool-attempt flag is per-screen: each split-screen player has their own pending
        // tool action. Reads/writes go through the property surface unchanged.
        private readonly PerScreen<bool> _playerIsAttemptingToUseTool = new(() => false);
        public bool PlayerIsAttemptingToUseTool
        {
            get => _playerIsAttemptingToUseTool.Value;
            set => _playerIsAttemptingToUseTool.Value = value;
        }
        public SquadMateFactory SquadMateFactory { get; internal set; }

        private MessageDispatcher? _dispatcher;

        public InteractionManager(IModHelper helper, SquadManager squadManager, SquadMateFactory squadMateFactory, BehaviorManager behaviorManager, IGameContext gameContext, ModConfig config)
        {
            this._helper = helper;
            this._squadManager = squadManager;
            this.SquadMateFactory = squadMateFactory;
            this._behaviorManager = behaviorManager;
            this._gameContext = gameContext;
            this._config = config;
        }

        public void AttachDispatcher(MessageDispatcher dispatcher)
        {
            this._dispatcher = dispatcher;
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
                    Game1.addHUDMessage(new HUDMessage(_helper.Translation.Get("recruitment.festival_block"), HUDMessage.error_type));
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
                // In MP, only the local player's own squad can be commanded — check per-recruiter
                // count rather than the global count so farmhands don't accidentally see a "no
                // followers" message when only the host has mates (and vice versa).
                int myMateCount = this._squadManager.Members.Count(m => m.RecruiterUniqueId == player.UniqueMultiplayerID);
                if (myMateCount == 0)
                {
                    Game1.addHUDMessage(new HUDMessage(_helper.Translation.Get("commands.noFollower"), HUDMessage.error_type));
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
                Game1.addHUDMessage(new HUDMessage(_helper.Translation.Get(messageKey), HUDMessage.newQuest_type));
                return;
            }
        }

        private void HandleManualCommand(Point tile)
        {
            // MP routing: farmhand forwards to the host with tile + location only;
            // the host re-runs detection in its own world view, scoped to the requester's mates.
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                _dispatcher?.SendTaskAssignRequest(
                    tile.ToVector2(),
                    _gameContext.CurrentLocation?.NameOrUniqueName ?? string.Empty);
                return;
            }

            AssignManualTaskFor(_gameContext.Player, tile);
        }

        /// <summary>
        /// Authoritative manual-task assignment. Detects the task at the given tile in the
        /// requester's location and assigns the closest capable mate that the requester
        /// recruited. Called locally on the host/SP, and via <see cref="MessageDispatcher"/>
        /// when a farmhand sends a <see cref="TaskAssignRequest"/>.
        /// </summary>
        public void AssignManualTaskFor(Farmer requester, Point tile)
        {
            var location = requester?.currentLocation;
            if (location == null) return;

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
            else if (FindAnimalTaskAt(location, tile) is var animalMatch && animalMatch.taskType.HasValue)
            {
                taskType = animalMatch.taskType;
                targetObjectTile = animalMatch.animalTile;
                targetCharacter = animalMatch.animal!;
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

            // 2. Now find the best follower, restricted to the requester's own mates in
            //    the requester's location.
            ISquadMate bestMate = this._squadManager.Members
                .Where(m => m.RecruiterUniqueId == requester.UniqueMultiplayerID)
                .Where(m => m.Npc.currentLocation == location)
                .Where(m => m.CanPerformTask(taskType.Value))
                .OrderBy(m => Vector2.DistanceSquared(m.Npc.Tile, tile.ToVector2()))
                .FirstOrDefault();

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

        private static (TaskType? taskType, Point? animalTile, Character? animal) FindAnimalTaskAt(GameLocation location, Point tile)
        {
            Rectangle tileBounds = new(tile.X * 64, tile.Y * 64, 64, 64);

            var farm = Game1.getFarm();
            if (farm != null)
            {
                var animal = farm.getAllFarmAnimals()
                    .FirstOrDefault(a => a.currentLocation == location
                        && a.GetBoundingBox().Intersects(tileBounds));

                if (animal != null)
                {
                    var animalTile = animal.TilePoint;

                    if (animal.isAdult() && animal.currentProduce.Value != null)
                    {
                        if (animal.CanGetProduceWithTool(_sharedMilkPail))
                            return (TaskType.Milking, animalTile, animal);
                        if (animal.CanGetProduceWithTool(_sharedShears))
                            return (TaskType.Shearing, animalTile, animal);
                    }

                    if (!animal.wasPet.Value)
                        return (TaskType.Petting, animalTile, animal);
                }
            }

            var pet = location.characters.OfType<Pet>()
                .FirstOrDefault(p => p.GetBoundingBox().Intersects(tileBounds));

            if (pet != null)
            {
                bool alreadyPettedToday = pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out var lastDay)
                    && lastDay == Game1.Date.TotalDays;

                if (!alreadyPettedToday)
                    return (TaskType.Petting, pet.TilePoint, pet);
            }

            return (null, null, null);
        }

        public void ShowSquadInventory()
        {
            Game1.activeClickableMenu = new SquadInventoryMenu(_helper);
        }

        private void DisableIdleAnimation(ISquadMate mate)
        {
            if (mate.IsAnimating)
            {
                mate.Halt();
                mate.ActionCooldown = 0;
                // Sync the clear to peers — Sprite/IsAnimating/ActionCooldown are non-netfield
                // local state, so without this the farmhand's right-click interrupts only THEIR
                // screen while the host (and other peers) keep cycling the loop. Host's own
                // FollowerManager.DetectAndBroadcastAnimationClears handles the host-self path
                // automatically; this covers the farmhand-initiated case. No-op in SP.
                _dispatcher?.BroadcastClearIdleAnim(mate);
            }
        }
    }
}
