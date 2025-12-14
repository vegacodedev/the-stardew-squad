using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using System.Linq;
using TheStardewSquad.Abstractions.Location;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Wrapper that adapts a GameLocation and Character to the ILocationInfo interface.
    /// This is used in production code to bridge between the game's classes
    /// and our testable interface.
    /// </summary>
    public class LocationInfoWrapper : ILocationInfo
    {
        private readonly GameLocation _location;
        private readonly Character _character;

        public LocationInfoWrapper(GameLocation location, Character character)
        {
            _location = location;
            _character = character;
        }

        /// <summary>
        /// Checks if a tile has a crop that is ready to be harvested.
        /// </summary>
        public bool HasHarvestableCropAt(Point tile)
        {
            if (_location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature) &&
                feature is HoeDirt { crop: { } } dirt)
            {
                return dirt.readyForHarvest();
            }
            return false;
        }

        /// <summary>
        /// Checks if a tile has a trellis crop (raised seeds).
        /// </summary>
        public bool IsTrellisCropAt(Point tile)
        {
            if (_location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature) &&
                feature is HoeDirt { crop: { } } dirt)
            {
                return dirt.crop.raisedSeeds.Value;
            }
            return false;
        }

        /// <summary>
        /// Checks if a tile has dry HoeDirt that needs watering.
        /// </summary>
        public bool HasDryHoeDirtAt(Point tile)
        {
            if (_location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature) &&
                feature is HoeDirt dirt)
            {
                return dirt.state.Value == HoeDirt.dry;
            }
            return false;
        }

        /// <summary>
        /// Gets whether this location is a Farm or Greenhouse.
        /// </summary>
        public bool IsFarmOrGreenhouse => _location.IsFarm || _location.IsGreenhouse;

        /// <summary>
        /// Gets all forageable items within a radius of a center point.
        /// Includes both wild forage (berries, mushrooms, etc.) and animal products (eggs, wool, feathers, truffles).
        /// </summary>
        public IEnumerable<Point> GetForageableItems(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var pair in _location.objects.Pairs)
            {
                // Include both forage items and animal products
                // Use vanilla methods to determine item type
                if ((pair.Value.isForage() || pair.Value.isAnimalProduct()) &&
                    Vector2.Distance(centerVec, pair.Key) < radius)
                {
                    yield return pair.Key.ToPoint();
                }
            }
        }

        /// <summary>
        /// Gets all harvestable berry bushes within a radius of a center point.
        /// </summary>
        public IEnumerable<Point> GetHarvestableBushes(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var largeFeature in _location.largeTerrainFeatures)
            {
                if (largeFeature is Bush bush &&
                    bush.tileSheetOffset.Value == 1 &&
                    bush.readyForHarvest() &&
                    bush.inBloom() &&
                    !bush.townBush.Value &&
                    Vector2.Distance(centerVec, bush.Tile) < radius)
                {
                    yield return bush.Tile.ToPoint();
                }
            }
        }

        /// <summary>
        /// Gets all tiles within a rectangular radius around a center point.
        /// </summary>
        public IEnumerable<Point> GetTilesInRadius(Point center, int radius)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    yield return new Point(center.X + x, center.Y + y);
                }
            }
        }

        /// <summary>
        /// Gets all unpetted farm animals within a radius of a center point.
        /// </summary>
        public IEnumerable<(Character animal, Point tile)> GetUnpettedFarmAnimals(Point searchCenter, int radius)
        {
            var centerVec = searchCenter.ToVector2();
            var farm = Game1.getFarm();
            if (farm == null)
                yield break;

            foreach (var animal in farm.getAllFarmAnimals())
            {
                // Skip if already petted today
                if (animal.wasPet.Value)
                    continue;

                // Only consider animals in this location
                if (animal.currentLocation != _location)
                    continue;

                // Check distance from search center
                var animalTile = animal.TilePoint;
                if (Vector2.Distance(centerVec, animalTile.ToVector2()) < radius)
                {
                    yield return (animal, animalTile);
                }
            }
        }

        /// <summary>
        /// Gets the player's pet (cat/dog) if it's unpetted and within radius.
        /// </summary>
        public (Character pet, Point tile)? GetUnpettedPlayerPet(Point searchCenter, int radius)
        {
            var centerVec = searchCenter.ToVector2();
            var home = Utility.getHomeOfFarmer(Game1.player);
            if (home == null)
                return null;

            var pet = home.characters.OfType<StardewValley.Characters.Pet>().FirstOrDefault();
            if (pet == null)
                return null;

            // Only consider pet if in this location
            if (pet.currentLocation != _location)
                return null;

            // Check if already petted today using lastPetDay dictionary
            bool alreadyPetted = pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out var lastDay)
                && lastDay == Game1.Date.TotalDays;

            if (alreadyPetted)
                return null;

            // Check distance from search center
            var petTile = pet.TilePoint;
            if (Vector2.Distance(centerVec, petTile.ToVector2()) < radius)
            {
                return (pet, petTile);
            }

            return null;
        }

        /// <summary>
        /// Gets all hostile monsters within a radius of a center point.
        /// </summary>
        public IEnumerable<(Monster? monster, Point tile, MonsterState state)> GetHostileMonsters(Point searchCenter, int radius)
        {
            var centerVec = searchCenter.ToVector2();

            foreach (var character in _location.characters)
            {
                if (character is not Monster monster)
                    continue;

                var monsterTile = monster.TilePoint;
                if (Vector2.Distance(centerVec, monsterTile.ToVector2()) >= radius)
                    continue;

                // Determine monster type and state
                var state = new MonsterState();

                if (monster is Bug bug)
                {
                    state.Type = MonsterType.Bug;
                    state.IsArmoredBug = bug.isArmoredBug.Value;
                }
                else if (monster is RockCrab crab)
                {
                    state.Type = MonsterType.RockCrab;
                    state.IsHidingCrab = crab.waiter;
                }
                else if (monster is Mummy mummy)
                {
                    state.Type = MonsterType.Mummy;
                    state.IsRevivingMummy = mummy.reviveTimer.Value > 0;
                }
                else if (monster is Duggy duggy)
                {
                    state.Type = MonsterType.Duggy;
                    state.IsHidingDuggy = duggy.DamageToFarmer == 0;
                }
                else
                {
                    state.Type = MonsterType.Generic;
                }

                yield return (monster, monsterTile, state);
            }
        }

        /// <summary>
        /// Gets whether this location is a SlimeHutch.
        /// </summary>
        public bool IsSlimeHutch => _location is SlimeHutch;

        /// <summary>
        /// Checks if a tile is water (for fishing tasks).
        /// </summary>
        public bool IsWaterTile(Point tile)
        {
            return _location.isWaterTile(tile.X, tile.Y);
        }

        /// <summary>
        /// Gets the player's current tile position.
        /// </summary>
        public Point GetPlayerPosition()
        {
            return Game1.player.TilePoint;
        }

        /// <summary>
        /// Gets all trees within a radius of a center point (for lumbering tasks).
        /// </summary>
        public IEnumerable<(Point tile, float health, bool hasTapper)> GetTrees(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var pair in _location.terrainFeatures.Pairs)
            {
                if (pair.Value is not StardewValley.TerrainFeatures.Tree tree)
                    continue;

                var treeTile = pair.Key.ToPoint();
                if (Vector2.Distance(centerVec, pair.Key) >= radius)
                    continue;

                // Check if tree has a tapper
                bool hasTapper = false;
                if (_location.objects.TryGetValue(pair.Key, out var obj))
                {
                    hasTapper = obj.QualifiedItemId == "(BC)105" || obj.QualifiedItemId == "(BC)264";
                }

                // Normalize health to 0.0-1.0 range (tree.health is typically 0-10 or similar)
                float normalizedHealth = tree.health.Value / (float)StardewValley.TerrainFeatures.Tree.startingHealth;

                yield return (treeTile, normalizedHealth, hasTapper);
            }
        }

        /// <summary>
        /// Gets all twigs (tree stumps) within a radius of a center point (for lumbering tasks).
        /// </summary>
        public IEnumerable<Point> GetTwigs(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var pair in _location.objects.Pairs)
            {
                if (pair.Value.Name != "Twig")
                    continue;

                if (Vector2.Distance(centerVec, pair.Key) < radius)
                {
                    yield return pair.Key.ToPoint();
                }
            }
        }

        /// <summary>
        /// Gets all rocks (stones) within a radius of a center point (for mining tasks).
        /// </summary>
        public IEnumerable<(Point tile, int parentSheetIndex, bool readyForHarvest)> GetRocks(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var pair in _location.objects.Pairs)
            {
                var obj = pair.Value;

                // Only include rocks (BaseName is "Stone")
                if (obj.BaseName != "Stone")
                    continue;

                if (Vector2.Distance(centerVec, pair.Key) < radius)
                {
                    yield return (pair.Key.ToPoint(), obj.ParentSheetIndex, obj.readyForHarvest.Value);
                }
            }
        }

        /// <summary>
        /// Gets the player's pickaxe upgrade level based on their Mining skill level.
        /// </summary>
        public int GetPlayerPickaxeLevel()
        {
            switch (Game1.player.MiningLevel)
            {
                case >= 10: return 4; // Iridium
                case >= 8: return 3;  // Gold
                case >= 5: return 2;  // Steel
                case >= 2: return 1;  // Copper
                default: return 0;    // Basic
            }
        }

        /// <summary>
        /// Gets the object (item) at a specific tile position.
        /// </summary>
        public StardewValley.Object GetObjectAt(Point tile)
        {
            if (_location.objects.TryGetValue(tile.ToVector2(), out var obj))
            {
                return obj;
            }
            return null;
        }

        /// <summary>
        /// Gets all beehouses within a radius of a center point.
        /// </summary>
        public IEnumerable<Point> GetBeehousesInRadius(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var pair in _location.objects.Pairs)
            {
                var obj = pair.Value;

                // Beehouse has qualified item ID "(BC)10"
                if (obj.QualifiedItemId == "(BC)10" &&
                    Vector2.Distance(centerVec, pair.Key) < radius)
                {
                    yield return pair.Key.ToPoint();
                }
            }
        }

        /// <summary>
        /// Gets the HoeDirt terrain feature at a specific tile, if it exists.
        /// </summary>
        public HoeDirt? GetHoeDirtAt(Point tile)
        {
            if (_location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature) &&
                feature is HoeDirt dirt)
            {
                return dirt;
            }
            return null;
        }

        /// <summary>
        /// Checks if the crop at the specified tile is a flower (category -80).
        /// </summary>
        public bool IsFlowerCropAt(Point tile)
        {
            var dirt = GetHoeDirtAt(tile);
            if (dirt?.crop == null)
                return false;

            string harvestItemId = dirt.crop.indexOfHarvest.Value;
            if (string.IsNullOrEmpty(harvestItemId))
                return false;

            StardewValley.Item harvestedItem = StardewValley.ItemRegistry.Create(harvestItemId);
            return harvestedItem.Category == -80; // -80 is the flower category
        }

        /// <summary>
        /// Gets all individual seat positions from sittable furniture within a radius of a center point.
        /// Preserves fractional Y-offsets from Furniture.GetSeatPositions().
        /// Only returns unoccupied seats by checking vanilla's sittingFarmers dictionary.
        /// </summary>
        public IEnumerable<(Vector2 seatPosition, Point furnitureTile, int direction)> GetSittableFurniture(Point center, int radius)
        {
            if (_location.furniture == null)
                yield break;

            foreach (var tile in GetTilesInRadius(center, radius))
            {
                var furniture = _location.furniture.FirstOrDefault(f => f.TileLocation == tile.ToVector2());
                if (furniture != null)
                {
                    int seatCapacity = furniture.GetSeatCapacity();
                    if (seatCapacity > 0)
                    {
                        // Get actual seat positions from furniture
                        var seatPositions = furniture.GetSeatPositions();
                        if (seatPositions != null && seatPositions.Count > 0)
                        {
                            int direction = furniture.GetSittingDirection();
                            Point furnitureTilePoint = tile;

                            // Yield each individual seat position, but only if unoccupied
                            // Check vanilla's sittingFarmers dictionary to respect player/multiplayer occupancy
                            for (int seatIndex = 0; seatIndex < seatPositions.Count; seatIndex++)
                            {
                                var seatPos = seatPositions[seatIndex];

                                // Skip seats that are already occupied by checking vanilla's tracking
                                if (furniture.sittingFarmers.Values.Contains(seatIndex))
                                {
                                    continue; // Seat is occupied by player, skip it
                                }

                                // Skip seats occupied by NPCs (non-squad NPCs sitting via schedule)
                                if (IsNpcAtTile(seatPos))
                                {
                                    continue; // Seat is occupied by NPC, skip it
                                }

                                yield return (seatPos, furnitureTilePoint, direction);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets all map seats within a radius of a center point.
        /// Preserves fractional Y-offsets from MapSeat.GetSeatPositions().
        /// Only returns unoccupied seats by checking vanilla's sittingFarmers dictionary.
        /// </summary>
        public IEnumerable<(Vector2 tile, int direction)> GetMapSeats(Point center, int radius)
        {
            // Check if location has map seats (benches, picnic tables, etc.)
            var mapSeats = _location.mapSeats;
            if (mapSeats == null || mapSeats.Count == 0)
                yield break;

            // Collect all seat positions from all map seats
            foreach (var mapSeat in mapSeats)
            {
                // Get all seat positions for this map seat (preserving Vector2 for fractional offsets)
                var seatPositions = mapSeat.GetSeatPositions();
                if (seatPositions != null && seatPositions.Count > 0)
                {
                    // Use custom calculation to get correct direction (not the buggy localSittingDirection)
                    int direction = CalculateMapSeatSittingDirection(mapSeat);

                    // Check each seat position, tracking index to check occupancy
                    for (int seatIndex = 0; seatIndex < seatPositions.Count; seatIndex++)
                    {
                        var seatPos = seatPositions[seatIndex];

                        // Check if seat is within radius of center
                        if (Vector2.DistanceSquared(seatPos, center.ToVector2()) <= radius * radius * 64 * 64)
                        {
                            // Skip seats that are already occupied by checking vanilla's tracking
                            if (mapSeat.sittingFarmers.Values.Contains(seatIndex))
                            {
                                continue; // Seat is occupied by player, skip it
                            }

                            // Skip seats occupied by NPCs (non-squad NPCs sitting via schedule)
                            if (IsNpcAtTile(seatPos))
                            {
                                continue; // Seat is occupied by NPC, skip it
                            }

                            yield return (seatPos, direction);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if any NPC is positioned at the given tile.
        /// Uses bounding box intersection like GameLocation.isCharacterAtTile.
        /// </summary>
        private bool IsNpcAtTile(Vector2 tilePosition)
        {
            var tileBounds = new Rectangle(
                (int)tilePosition.X * 64,
                (int)tilePosition.Y * 64,
                64, 64);

            foreach (var npc in _location.characters)
            {
                if (npc.GetBoundingBox().Intersects(tileBounds))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a tile has sittable furniture or is a MapSeat position.
        /// </summary>
        private bool IsSittableTile(Point tile)
        {
            // Check if furniture exists at tile with seat capacity > 0
            if (_location.furniture != null)
            {
                var furniture = _location.furniture.FirstOrDefault(f => f.TileLocation == tile.ToVector2());
                if (furniture != null && furniture.GetSeatCapacity() > 0)
                {
                    return true;
                }
            }

            // Check if MapSeat exists at tile position
            var mapSeats = _location.mapSeats;
            if (mapSeats != null)
            {
                foreach (var mapSeat in mapSeats)
                {
                    var seatPositions = mapSeat.GetSeatPositions();
                    if (seatPositions != null && seatPositions.Any(seatPos => seatPos.ToPoint() == tile))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the priority of a furniture type for sitting direction detection.
        /// Returns -1 if the furniture type should not be faced.
        /// Lower values = higher priority.
        /// </summary>
        private static int GetFurnitureTypePriority(int furnitureType)
        {
            return furnitureType switch
            {
                11 => 0,  // table - highest priority
                5 => 1,   // longTable
                10 => 2,  // bookcase
                4 => 3,   // dresser
                8 => 4,   // decor
                9 => 5,   // other
                _ => -1   // Not a type we want to face
            };
        }

        /// <summary>
        /// Gets the furniture priority at a tile (checks both placeable furniture and Buildings layer).
        /// Returns null if no relevant furniture found.
        /// </summary>
        private int? GetTileFurniturePriority(Point tile)
        {
            int? bestPriority = null;

            // Check placeable Furniture objects first
            if (_location.furniture != null)
            {
                Rectangle tileBounds = new Rectangle(tile.X * 64, tile.Y * 64, 64, 64);
                foreach (var furniture in _location.furniture)
                {
                    if (furniture.GetBoundingBox().Intersects(tileBounds))
                    {
                        int priority = GetFurnitureTypePriority(furniture.furniture_type.Value);
                        if (priority >= 0 && (!bestPriority.HasValue || priority < bestPriority.Value))
                        {
                            bestPriority = priority;
                        }
                    }
                }
            }

            // Also check Buildings layer (existing logic for map-based furniture)
            if (IsMapTileTableOrFurniture(tile))
            {
                // Buildings layer furniture gets priority 6 (after all placeable types)
                if (!bestPriority.HasValue || 6 < bestPriority.Value)
                {
                    bestPriority = 6;
                }
            }

            return bestPriority;
        }

        /// <summary>
        /// Checks if a tile has a Buildings layer tile that is a table or furniture
        /// (not a wall and not a sittable tile).
        /// </summary>
        private bool IsMapTileTableOrFurniture(Point tile)
        {
            // Check if tile has a Buildings layer tile
            var buildingLayer = _location.Map?.GetLayer("Buildings");
            if (buildingLayer == null ||
                tile.X < 0 || tile.Y < 0 ||
                tile.X >= buildingLayer.LayerWidth ||
                tile.Y >= buildingLayer.LayerHeight)
            {
                return false;
            }

            // Must have a tile on Buildings layer
            var mapTile = buildingLayer.Tiles[tile.X, tile.Y];
            if (mapTile == null)
            {
                return false;
            }

            // Exclude tile index 0 (walls/map bounds)
            if (mapTile.TileIndex == 0)
            {
                return false;
            }

            // Exclude sittable tiles
            if (IsSittableTile(tile))
            {
                return false;
            }

            // Check if tile has interactive properties (furniture/tables often have these)
            string action = _location.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings");
            string type = _location.doesTileHaveProperty(tile.X, tile.Y, "Type", "Buildings");

            // If it has Action or Type properties, it's likely furniture/interactive
            if (action != null || type != null)
            {
                return true;
            }

            // For tiles without properties: include if impassable (likely furniture)
            // Exclude if passable (likely decorative or path)
            string passableProperty = _location.doesTileHaveProperty(tile.X, tile.Y, "Passable", "Buildings");
            if (passableProperty == null || !passableProperty.Equals("T", StringComparison.OrdinalIgnoreCase))
            {
                return true; // Impassable without properties = likely table/furniture
            }

            return false;
        }

        /// <summary>
        /// Calculates the direction (0-3) from one tile to another.
        /// Returns 0=Up, 1=Right, 2=Down, 3=Left
        /// </summary>
        private int GetDirectionTowards(Point from, Point to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            // Calculate which direction is dominant
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                return dx > 0 ? 1 : 3; // Right : Left
            }
            else
            {
                return dy > 0 ? 2 : 0; // Down : Up
            }
        }

        /// <summary>
        /// Finds the direction to face the highest-priority adjacent table/furniture.
        /// Only checks tiles directly adjacent (1 tile away in 4 cardinal directions).
        /// Returns null if no table found.
        /// </summary>
        private int? FindAdjacentTableDirection(Point seatTile)
        {
            // Check 4 cardinal adjacent tiles (1 tile away) with their directions
            // Order matters for tie-breaking: prefer Left/Right over Up/Down (more natural sitting at table)
            // If table spans multiple tiles, first match with best priority wins
            var adjacentTiles = new[]
            {
                (new Point(seatTile.X - 1, seatTile.Y), 3),  // Left - check first (preferred)
                (new Point(seatTile.X + 1, seatTile.Y), 1),  // Right
                (new Point(seatTile.X, seatTile.Y - 1), 0),  // Up
                (new Point(seatTile.X, seatTile.Y + 1), 2)   // Down - check last
            };

            int bestPriority = int.MaxValue;
            int? bestDirection = null;

            foreach (var (tile, direction) in adjacentTiles)
            {
                int? priority = GetTileFurniturePriority(tile);
                if (priority.HasValue && priority.Value < bestPriority)
                {
                    bestPriority = priority.Value;
                    bestDirection = direction;
                }
            }

            return bestDirection;
        }

        /// <summary>
        /// Calculates the correct sitting direction for a MapSeat by accessing its direction field directly.
        /// This replicates the vanilla logic from MapSeat.AddSittingFarmer() that sets localSittingDirection.
        /// MapSeat.GetSittingDirection() returns localSittingDirection which defaults to 2 (facing down)
        /// and is only updated when a player sits, causing NPCs to face wrong until player sits first.
        /// </summary>
        private int CalculateMapSeatSittingDirection(StardewValley.MapSeat mapSeat)
        {
            // Access the direction field via reflection
            var directionField = AccessTools.Field(typeof(StardewValley.MapSeat), "direction");
            var seatTypeField = AccessTools.Field(typeof(StardewValley.MapSeat), "seatType");

            if (directionField == null || seatTypeField == null)
                return 2; // Default to facing down

            // Get the NetInt/NetString field objects
            var directionNet = directionField.GetValue(mapSeat);
            var seatTypeNet = seatTypeField.GetValue(mapSeat);

            if (directionNet == null || seatTypeNet == null)
                return 2; // Default to facing down

            // Access the Value property via reflection (works for both NetInt and NetString)
            var directionValue = AccessTools.Property(directionNet.GetType(), "Value");
            var seatTypeValue = AccessTools.Property(seatTypeNet.GetType(), "Value");

            if (directionValue == null || seatTypeValue == null)
                return 2; // Default to facing down

            int direction = (int)directionValue.GetValue(directionNet);
            string seatType = seatTypeValue.GetValue(seatTypeNet) as string;

            // Get seat position for table detection
            var seatPositions = mapSeat.GetSeatPositions();
            Point seatTile = seatPositions != null && seatPositions.Count > 0
                ? seatPositions[0].ToPoint()
                : Point.Zero;

            bool isStool = seatType != null && seatType.StartsWith("stool");

            // For stools: always try to find adjacent table first, regardless of direction value
            // (direction 0-3 on stools is often just a default, not intentional)
            if (isStool || direction == -1)
            {
                int? tableDirection = FindAdjacentTableDirection(seatTile);
                if (tableDirection.HasValue)
                {
                    return tableDirection.Value;
                }

                // No table found - fall back based on direction field
                if (isStool && direction >= 0 && direction <= 3)
                {
                    // Stool with fixed direction but no table - use the fixed direction
                    return direction;
                }
                else if (Game1.player != null && Game1.player.currentLocation == _location)
                {
                    Point playerTile = Game1.player.TilePoint;
                    return GetDirectionTowards(seatTile, playerTile);
                }
                else
                {
                    return _character.FacingDirection;
                }
            }

            // Fixed directions (0-3) for non-stool seats - return immediately
            if (direction >= 0 && direction <= 3)
            {
                // Special case: bathchair facing up should face down instead
                if (seatType != null && seatType.StartsWith("bathchair") && direction == 0)
                {
                    return 2;
                }
                return direction;
            }

            // direction == -2: Use adjacent table, then opposite facing
            if (direction == -2)
            {
                int? tableDirection = FindAdjacentTableDirection(seatTile);
                if (tableDirection.HasValue)
                {
                    return tableDirection.Value;
                }
                else
                {
                    return Utility.GetOppositeFacingDirection(_character.FacingDirection);
                }
            }

            // Unknown direction value - default to facing down
            return 2;
        }

        /// <summary>
        /// Gets the sitting direction for furniture or map seat at a specific tile.
        /// </summary>
        public int? GetSittingDirection(Point tile)
        {
            // Check furniture first
            if (_location.furniture != null)
            {
                var furniture = _location.furniture.FirstOrDefault(f => f.TileLocation == tile.ToVector2());
                if (furniture != null && furniture.GetSeatCapacity() > 0)
                {
                    // Stools: dynamic direction based on adjacent table, then character facing
                    // (vanilla returns player.FacingDirection but we use character for NPCs)
                    if (furniture.Name.Contains("Stool"))
                    {
                        int? tableDirection = FindAdjacentTableDirection(tile);
                        if (tableDirection.HasValue)
                        {
                            return tableDirection.Value;
                        }
                        // Fall back to character's facing direction (like vanilla does for player)
                        return _character.FacingDirection;
                    }

                    // Pianos: fixed direction 0 (facing up)
                    if (furniture.QualifiedItemId == "(F)UprightPiano" ||
                        furniture.QualifiedItemId == "(F)DarkPiano")
                    {
                        return 0;
                    }

                    // Standard furniture: use rotation-based direction
                    int defaultDirection = furniture.GetSittingDirection();

                    // Fixed directions (0-3) take priority - use immediately
                    if (defaultDirection >= 0 && defaultDirection <= 3)
                    {
                        return defaultDirection;
                    }

                    // Dynamic directions (-1, -2) - check secondary clues like adjacent tables
                    int? tableDirection2 = FindAdjacentTableDirection(tile);
                    if (tableDirection2.HasValue)
                    {
                        return tableDirection2.Value;
                    }

                    // Fall back to default direction for dynamic cases
                    return defaultDirection;
                }
            }

            // Check map seats
            var mapSeats = _location.mapSeats;
            if (mapSeats != null)
            {
                foreach (var mapSeat in mapSeats)
                {
                    var seatPositions = mapSeat.GetSeatPositions();
                    if (seatPositions != null && seatPositions.Any(seatPos => seatPos.ToPoint() == tile))
                    {
                        // Calculate direction from MapSeat's direction field (not localSittingDirection)
                        // This avoids the bug where localSittingDirection defaults to 2 until player sits
                        return CalculateMapSeatSittingDirection(mapSeat);
                    }
                }
            }

            return null; // No seat at this tile
        }

        /// <summary>
        /// Checks if a tile is passable (from IMapInfo interface).
        /// Uses the same logic as MapInfoWrapper.
        /// </summary>
        public bool IsTilePassable(Point tile)
        {
            // Reuse the MapInfoWrapper logic
            var mapInfo = new MapInfoWrapper(_location, _character);
            return mapInfo.IsTilePassable(tile);
        }
    }
}
