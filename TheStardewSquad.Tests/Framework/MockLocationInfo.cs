using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;
using System.Collections.Generic;
using System.Linq;
using TheStardewSquad.Abstractions.Location;

namespace TheStardewSquad.Tests.Framework
{
    /// <summary>
    /// Mock implementation of ILocationInfo for testing task finding logic.
    /// Allows tests to define custom crop locations and properties without requiring a real GameLocation.
    /// </summary>
    public class MockLocationInfo : ILocationInfo
    {
        private readonly HashSet<Point> _harvestableCrops = new();
        private readonly HashSet<Point> _trellisCrops = new();
        private readonly HashSet<Point> _flowerCrops = new(); // Track which crops are flowers
        private readonly HashSet<Point> _beehouses = new(); // Track beehouse locations
        private readonly HashSet<Point> _dryHoeDirt = new();
        private readonly HashSet<Point> _forageableItems = new();
        private readonly HashSet<Point> _harvestableBushes = new();
        private readonly HashSet<Point> _animalProducts = new();
        private readonly HashSet<Point> _passableTiles = new();
        private readonly List<Point> _unpettedFarmAnimals = new();
        private Point? _unpettedPlayerPet = null;
        private readonly List<(Point tile, MonsterState state)> _hostileMonsters = new();
        private readonly HashSet<Point> _waterTiles = new();
        private Point _playerPosition = new Point(0, 0);
        private readonly List<(Point tile, float health, bool hasTapper)> _trees = new();
        private readonly HashSet<Point> _twigs = new();
        private readonly List<(Point tile, int parentSheetIndex, bool readyForHarvest)> _rocks = new();
        private int _playerPickaxeLevel = 0; // Default to basic pickaxe
        private readonly bool _defaultPassable;
        private bool _isFarmOrGreenhouse = true; // Default to true for convenience
        private bool _isSlimeHutch = false;

        /// <summary>
        /// Creates a mock location where all tiles are passable by default unless explicitly marked impassable.
        /// </summary>
        public MockLocationInfo() : this(defaultPassable: true)
        {
        }

        /// <summary>
        /// Creates a mock location with a specific default passability.
        /// </summary>
        public MockLocationInfo(bool defaultPassable)
        {
            _defaultPassable = defaultPassable;
        }

        /// <summary>
        /// Adds a harvestable crop at a specific tile.
        /// </summary>
        public MockLocationInfo WithHarvestableCrop(Point tile, bool isTrellis = false)
        {
            _harvestableCrops.Add(tile);
            if (isTrellis)
            {
                _trellisCrops.Add(tile);
            }
            return this;
        }

        /// <summary>
        /// Adds a harvestable crop at a specific tile.
        /// </summary>
        public MockLocationInfo WithHarvestableCrop(int x, int y, bool isTrellis = false)
        {
            return WithHarvestableCrop(new Point(x, y), isTrellis);
        }

        /// <summary>
        /// Adds a harvestable flower crop at a specific tile.
        /// </summary>
        public MockLocationInfo WithFlowerCrop(Point tile)
        {
            _harvestableCrops.Add(tile);
            _flowerCrops.Add(tile);
            return this;
        }

        /// <summary>
        /// Adds a harvestable flower crop at a specific tile.
        /// </summary>
        public MockLocationInfo WithFlowerCrop(int x, int y)
        {
            return WithFlowerCrop(new Point(x, y));
        }

        /// <summary>
        /// Adds a beehouse at a specific tile.
        /// </summary>
        public MockLocationInfo WithBeehouse(Point tile)
        {
            _beehouses.Add(tile);
            return this;
        }

        /// <summary>
        /// Adds a beehouse at a specific tile.
        /// </summary>
        public MockLocationInfo WithBeehouse(int x, int y)
        {
            return WithBeehouse(new Point(x, y));
        }

        /// <summary>
        /// Adds multiple harvestable crops.
        /// </summary>
        public MockLocationInfo WithHarvestableCrops(params Point[] tiles)
        {
            foreach (var tile in tiles)
            {
                WithHarvestableCrop(tile);
            }
            return this;
        }

        /// <summary>
        /// Adds dry HoeDirt at a specific tile (needs watering).
        /// </summary>
        public MockLocationInfo WithDryHoeDirt(Point tile, bool isTrellis = false)
        {
            _dryHoeDirt.Add(tile);
            if (isTrellis)
            {
                _trellisCrops.Add(tile);
            }
            return this;
        }

        /// <summary>
        /// Adds dry HoeDirt at a specific tile (needs watering).
        /// </summary>
        public MockLocationInfo WithDryHoeDirt(int x, int y, bool isTrellis = false)
        {
            return WithDryHoeDirt(new Point(x, y), isTrellis);
        }

        /// <summary>
        /// Sets whether this location is a Farm or Greenhouse.
        /// </summary>
        public MockLocationInfo SetIsFarmOrGreenhouse(bool value)
        {
            _isFarmOrGreenhouse = value;
            return this;
        }

        /// <summary>
        /// Adds a forageable item at a specific tile.
        /// </summary>
        public MockLocationInfo WithForageableItem(Point tile)
        {
            _forageableItems.Add(tile);
            return this;
        }

        /// <summary>
        /// Adds a forageable item at a specific tile.
        /// </summary>
        public MockLocationInfo WithForageableItem(int x, int y)
        {
            return WithForageableItem(new Point(x, y));
        }

        /// <summary>
        /// Adds a harvestable berry bush at a specific tile.
        /// </summary>
        public MockLocationInfo WithHarvestableBush(Point tile)
        {
            _harvestableBushes.Add(tile);
            return this;
        }

        /// <summary>
        /// Adds a harvestable berry bush at a specific tile.
        /// </summary>
        public MockLocationInfo WithHarvestableBush(int x, int y)
        {
            return WithHarvestableBush(new Point(x, y));
        }

        /// <summary>
        /// Adds an animal product at a specific tile (eggs, wool, feathers, truffles, etc.).
        /// </summary>
        public MockLocationInfo WithAnimalProduct(Point tile)
        {
            _animalProducts.Add(tile);
            return this;
        }

        /// <summary>
        /// Adds an animal product at a specific tile (eggs, wool, feathers, truffles, etc.).
        /// </summary>
        public MockLocationInfo WithAnimalProduct(int x, int y)
        {
            return WithAnimalProduct(new Point(x, y));
        }

        /// <summary>
        /// Marks a tile as passable.
        /// </summary>
        public MockLocationInfo WithPassableTile(Point tile)
        {
            _passableTiles.Add(tile);
            return this;
        }

        /// <summary>
        /// Marks a tile as passable.
        /// </summary>
        public MockLocationInfo WithPassableTile(int x, int y)
        {
            return WithPassableTile(new Point(x, y));
        }

        /// <summary>
        /// Marks a rectangular region as passable.
        /// </summary>
        public MockLocationInfo WithPassableRegion(int startX, int startY, int width, int height)
        {
            for (int x = startX; x < startX + width; x++)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    WithPassableTile(x, y);
                }
            }
            return this;
        }

        /// <summary>
        /// Adds an unpetted farm animal at a specific tile.
        /// </summary>
        public MockLocationInfo WithUnpettedFarmAnimal(Point tile)
        {
            _unpettedFarmAnimals.Add(tile);
            return this;
        }

        /// <summary>
        /// Adds an unpetted farm animal at a specific tile.
        /// </summary>
        public MockLocationInfo WithUnpettedFarmAnimal(int x, int y)
        {
            return WithUnpettedFarmAnimal(new Point(x, y));
        }

        /// <summary>
        /// Sets the player's pet location (unpetted).
        /// </summary>
        public MockLocationInfo WithUnpettedPlayerPet(Point tile)
        {
            _unpettedPlayerPet = tile;
            return this;
        }

        /// <summary>
        /// Sets the player's pet location (unpetted).
        /// </summary>
        public MockLocationInfo WithUnpettedPlayerPet(int x, int y)
        {
            return WithUnpettedPlayerPet(new Point(x, y));
        }

        /// <summary>
        /// Adds a hostile monster at a specific tile with the given state.
        /// </summary>
        public MockLocationInfo WithMonster(Point tile, MonsterState state)
        {
            _hostileMonsters.Add((tile, state));
            return this;
        }

        /// <summary>
        /// Adds a hostile monster at a specific tile with the given state.
        /// </summary>
        public MockLocationInfo WithMonster(int x, int y, MonsterState state)
        {
            return WithMonster(new Point(x, y), state);
        }

        /// <summary>
        /// Adds a generic targetable monster at a specific tile.
        /// </summary>
        public MockLocationInfo WithTargetableMonster(int x, int y)
        {
            return WithMonster(x, y, MonsterState.Targetable());
        }

        /// <summary>
        /// Sets whether this location is a SlimeHutch.
        /// </summary>
        public MockLocationInfo SetIsSlimeHutch(bool value)
        {
            _isSlimeHutch = value;
            return this;
        }

        /// <summary>
        /// Adds a water tile at a specific location.
        /// </summary>
        public MockLocationInfo WithWaterTile(Point tile)
        {
            _waterTiles.Add(tile);
            return this;
        }

        /// <summary>
        /// Adds a water tile at a specific location.
        /// </summary>
        public MockLocationInfo WithWaterTile(int x, int y)
        {
            return WithWaterTile(new Point(x, y));
        }

        /// <summary>
        /// Adds multiple water tiles.
        /// </summary>
        public MockLocationInfo WithWaterTiles(params Point[] tiles)
        {
            foreach (var tile in tiles)
            {
                WithWaterTile(tile);
            }
            return this;
        }

        /// <summary>
        /// Adds a rectangular region of water tiles.
        /// </summary>
        public MockLocationInfo WithWaterRegion(int startX, int startY, int width, int height)
        {
            for (int x = startX; x < startX + width; x++)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    WithWaterTile(x, y);
                }
            }
            return this;
        }

        /// <summary>
        /// Sets the player's position.
        /// </summary>
        public MockLocationInfo WithPlayerAt(Point position)
        {
            _playerPosition = position;
            return this;
        }

        /// <summary>
        /// Sets the player's position.
        /// </summary>
        public MockLocationInfo WithPlayerAt(int x, int y)
        {
            return WithPlayerAt(new Point(x, y));
        }

        /// <summary>
        /// Adds a tree at a specific location with given health and tapper status.
        /// </summary>
        /// <param name="tile">The tile position.</param>
        /// <param name="health">Health as a value from 0.0 (dead) to 1.0 (full health).</param>
        /// <param name="hasTapper">Whether the tree has a tapper attached.</param>
        public MockLocationInfo WithTree(Point tile, float health = 0.5f, bool hasTapper = false)
        {
            _trees.Add((tile, health, hasTapper));
            return this;
        }

        /// <summary>
        /// Adds a tree at a specific location with given health and tapper status.
        /// </summary>
        public MockLocationInfo WithTree(int x, int y, float health = 0.5f, bool hasTapper = false)
        {
            return WithTree(new Point(x, y), health, hasTapper);
        }

        /// <summary>
        /// Adds a damaged tree (default 50% health) at a specific location.
        /// </summary>
        public MockLocationInfo WithDamagedTree(int x, int y, float health = 0.5f)
        {
            return WithTree(x, y, health, hasTapper: false);
        }

        /// <summary>
        /// Adds a full health tree (1.0 health) - should not be found for lumbering.
        /// </summary>
        public MockLocationInfo WithFullHealthTree(int x, int y)
        {
            return WithTree(x, y, health: 1.0f, hasTapper: false);
        }

        /// <summary>
        /// Adds a dead tree (0.0 health) - should not be found for lumbering.
        /// </summary>
        public MockLocationInfo WithDeadTree(int x, int y)
        {
            return WithTree(x, y, health: 0.0f, hasTapper: false);
        }

        /// <summary>
        /// Adds a tapped tree at a specific location - should be skipped for lumbering.
        /// </summary>
        public MockLocationInfo WithTappedTree(int x, int y, float health = 0.5f)
        {
            return WithTree(x, y, health, hasTapper: true);
        }

        /// <summary>
        /// Adds a twig (tree stump) at a specific location.
        /// </summary>
        public MockLocationInfo WithTwig(Point tile)
        {
            _twigs.Add(tile);
            return this;
        }

        /// <summary>
        /// Adds a twig (tree stump) at a specific location.
        /// </summary>
        public MockLocationInfo WithTwig(int x, int y)
        {
            return WithTwig(new Point(x, y));
        }

        /// <summary>
        /// Adds a rock at a specific location with given parent sheet index and ready status.
        /// </summary>
        /// <param name="tile">The tile position.</param>
        /// <param name="parentSheetIndex">The rock type (e.g., 46 for Mystic Stone, 765 for Iridium Node).</param>
        /// <param name="readyForHarvest">Whether the rock is ready for harvest (typically false for mineable rocks).</param>
        public MockLocationInfo WithRock(Point tile, int parentSheetIndex, bool readyForHarvest = false)
        {
            _rocks.Add((tile, parentSheetIndex, readyForHarvest));
            return this;
        }

        /// <summary>
        /// Adds a rock at a specific location with given parent sheet index and ready status.
        /// </summary>
        public MockLocationInfo WithRock(int x, int y, int parentSheetIndex, bool readyForHarvest = false)
        {
            return WithRock(new Point(x, y), parentSheetIndex, readyForHarvest);
        }

        /// <summary>
        /// Adds a regular stone (breakable with any pickaxe level) at a specific location.
        /// </summary>
        public MockLocationInfo WithRegularStone(int x, int y)
        {
            return WithRock(x, y, parentSheetIndex: 668); // 668 is a common regular stone
        }

        /// <summary>
        /// Adds a Gem Node (requires Copper pickaxe or better) at a specific location.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="gemType">Gem node type (2, 4, 6, 8, 10, 12, or 14).</param>
        public MockLocationInfo WithGemNode(int x, int y, int gemType = 2)
        {
            return WithRock(x, y, parentSheetIndex: gemType);
        }

        /// <summary>
        /// Adds an Iridium Node (requires Steel pickaxe or better) at a specific location.
        /// </summary>
        public MockLocationInfo WithIridiumNode(int x, int y)
        {
            return WithRock(x, y, parentSheetIndex: 765);
        }

        /// <summary>
        /// Adds a Mystic Stone (requires Gold pickaxe or better) at a specific location.
        /// </summary>
        public MockLocationInfo WithMysticStone(int x, int y)
        {
            return WithRock(x, y, parentSheetIndex: 46);
        }

        /// <summary>
        /// Adds a rock that is ready for harvest (should be skipped by mining task).
        /// </summary>
        public MockLocationInfo WithReadyRock(int x, int y)
        {
            return WithRock(x, y, parentSheetIndex: 668, readyForHarvest: true);
        }

        /// <summary>
        /// Sets the player's pickaxe upgrade level.
        /// 0 = Basic, 1 = Copper, 2 = Steel, 3 = Gold, 4 = Iridium
        /// </summary>
        public MockLocationInfo SetPlayerPickaxeLevel(int level)
        {
            _playerPickaxeLevel = level;
            return this;
        }

        #region ILocationInfo Implementation

        public bool HasHarvestableCropAt(Point tile)
        {
            return _harvestableCrops.Contains(tile);
        }

        public bool IsTrellisCropAt(Point tile)
        {
            return _trellisCrops.Contains(tile);
        }

        public bool HasDryHoeDirtAt(Point tile)
        {
            return _dryHoeDirt.Contains(tile);
        }

        public bool IsFarmOrGreenhouse => _isFarmOrGreenhouse;

        public IEnumerable<Point> GetForageableItems(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            // Include both forageable items and animal products
            foreach (var item in _forageableItems)
            {
                if (Vector2.Distance(centerVec, item.ToVector2()) < radius)
                {
                    yield return item;
                }
            }

            foreach (var animalProduct in _animalProducts)
            {
                if (Vector2.Distance(centerVec, animalProduct.ToVector2()) < radius)
                {
                    yield return animalProduct;
                }
            }
        }

        /// <summary>
        /// Gets harvestable bushes. Bushes in this mock are assumed to meet all criteria:
        /// tileSheetOffset == 1, readyForHarvest(), inBloom(), and !townBush.
        /// </summary>
        public IEnumerable<Point> GetHarvestableBushes(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var bush in _harvestableBushes)
            {
                if (Vector2.Distance(centerVec, bush.ToVector2()) < radius)
                {
                    yield return bush;
                }
            }
        }

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

        public bool IsTilePassable(Point tile)
        {
            // Explicit passable marking takes precedence
            if (_passableTiles.Contains(tile))
                return true;

            // Fall back to default
            return _defaultPassable;
        }

        public IEnumerable<(Character animal, Point tile)> GetUnpettedFarmAnimals(Point searchCenter, int radius)
        {
            var centerVec = searchCenter.ToVector2();

            foreach (var animalTile in _unpettedFarmAnimals)
            {
                if (Vector2.Distance(centerVec, animalTile.ToVector2()) < radius)
                {
                    // Return null! for Character as it's not used in the test logic
                    yield return (null!, animalTile);
                }
            }
        }

        public (Character pet, Point tile)? GetUnpettedPlayerPet(Point searchCenter, int radius)
        {
            if (!_unpettedPlayerPet.HasValue)
                return null;

            var centerVec = searchCenter.ToVector2();
            var petTile = _unpettedPlayerPet.Value;

            if (Vector2.Distance(centerVec, petTile.ToVector2()) < radius)
            {
                // Return null! for Character as it's not used in the test logic
                return (null!, petTile);
            }

            return null;
        }

        public IEnumerable<(Monster? monster, Point tile, MonsterState state)> GetHostileMonsters(Point searchCenter, int radius)
        {
            var centerVec = searchCenter.ToVector2();

            foreach (var (tile, state) in _hostileMonsters)
            {
                if (Vector2.Distance(centerVec, tile.ToVector2()) < radius)
                {
                    // Return null! for Monster as it's not used in the test logic
                    yield return (null!, tile, state);
                }
            }
        }

        public bool IsSlimeHutch => _isSlimeHutch;

        public bool IsWaterTile(Point tile)
        {
            return _waterTiles.Contains(tile);
        }

        public Point GetPlayerPosition()
        {
            return _playerPosition;
        }

        public IEnumerable<(Point tile, float health, bool hasTapper)> GetTrees(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var (tile, health, hasTapper) in _trees)
            {
                if (Vector2.Distance(centerVec, tile.ToVector2()) < radius)
                {
                    yield return (tile, health, hasTapper);
                }
            }
        }

        public IEnumerable<Point> GetTwigs(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var twig in _twigs)
            {
                if (Vector2.Distance(centerVec, twig.ToVector2()) < radius)
                {
                    yield return twig;
                }
            }
        }

        public IEnumerable<(Point tile, int parentSheetIndex, bool readyForHarvest)> GetRocks(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var (tile, parentSheetIndex, readyForHarvest) in _rocks)
            {
                if (Vector2.Distance(centerVec, tile.ToVector2()) < radius)
                {
                    yield return (tile, parentSheetIndex, readyForHarvest);
                }
            }
        }

        public int GetPlayerPickaxeLevel()
        {
            return _playerPickaxeLevel;
        }

        public StardewValley.Object GetObjectAt(Point tile)
        {
            // In test environment, return null to skip inventory checks
            // Production code will get real objects from LocationInfoWrapper
            return null;
        }

        public IEnumerable<Point> GetBeehousesInRadius(Point center, int radius)
        {
            var centerVec = center.ToVector2();

            foreach (var beehouse in _beehouses)
            {
                if (Vector2.Distance(centerVec, beehouse.ToVector2()) < radius)
                {
                    yield return beehouse;
                }
            }
        }

        public StardewValley.TerrainFeatures.HoeDirt? GetHoeDirtAt(Point tile)
        {
            // Cannot create real HoeDirt objects in test environment (requires Game1 initialization)
            // Return null - flower detection is handled by IsFlowerCropAt instead
            return null;
        }

        public bool IsFlowerCropAt(Point tile)
        {
            return _flowerCrops.Contains(tile);
        }

        public IEnumerable<(Vector2 seatPosition, Point furnitureTile, int direction)> GetSittableFurniture(Point center, int radius)
        {
            // Stub implementation - can be extended as needed for sitting tests
            return Enumerable.Empty<(Vector2, Point, int)>();
        }

        public IEnumerable<(Vector2 tile, int direction)> GetMapSeats(Point center, int radius)
        {
            // Stub implementation - can be extended as needed for sitting tests
            return Enumerable.Empty<(Vector2, int)>();
        }

        public int? GetSittingDirection(Point tile)
        {
            // Stub implementation - can be extended as needed for sitting tests
            return null;
        }

        #endregion
    }
}
