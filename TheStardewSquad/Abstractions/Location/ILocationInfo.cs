using Microsoft.Xna.Framework;
using StardewValley;
using System.Collections.Generic;

namespace TheStardewSquad.Abstractions.Location
{
    /// <summary>
    /// Interface for accessing location/map information needed for task finding.
    /// Extends IMapInfo to include both passability checks and task-specific queries.
    /// This abstraction allows for easy testing without requiring a real GameLocation.
    /// </summary>
    public interface ILocationInfo : IMapInfo
    {
        /// <summary>
        /// Checks if a tile has a crop that is ready to be harvested.
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>True if there's a harvestable crop at this tile, false otherwise.</returns>
        bool HasHarvestableCropAt(Point tile);

        /// <summary>
        /// Checks if a tile has a trellis crop (raised seeds like hops, grapes, green beans).
        /// Trellis crops require NPCs to stand adjacent to harvest them.
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>True if there's a trellis crop at this tile, false otherwise.</returns>
        bool IsTrellisCropAt(Point tile);

        /// <summary>
        /// Checks if a tile has dry HoeDirt that needs watering.
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>True if there's dry soil at this tile, false otherwise.</returns>
        bool HasDryHoeDirtAt(Point tile);

        /// <summary>
        /// Gets whether this location is a Farm or Greenhouse (where watering tasks are allowed).
        /// </summary>
        bool IsFarmOrGreenhouse { get; }

        /// <summary>
        /// Gets all forageable items within a radius of a center point.
        /// Includes both wild forage (berries, mushrooms, etc.) and animal products (eggs, wool, feathers, truffles).
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tile positions containing forageable items and animal products.</returns>
        IEnumerable<Point> GetForageableItems(Point center, int radius);

        /// <summary>
        /// Gets all harvestable berry bushes within a radius of a center point.
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tile positions containing harvestable bushes.</returns>
        IEnumerable<Point> GetHarvestableBushes(Point center, int radius);

        /// <summary>
        /// Gets all tiles within a rectangular radius around a center point.
        /// Used for scanning areas for tasks.
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles (forms a square search area).</param>
        /// <returns>An enumerable of all tile points in the search area.</returns>
        IEnumerable<Point> GetTilesInRadius(Point center, int radius);

        /// <summary>
        /// Gets all unpetted farm animals within a radius of a center point.
        /// Only includes animals that are in this location and haven't been petted today.
        /// </summary>
        /// <param name="searchCenter">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tuples containing the animal character and its tile position.</returns>
        IEnumerable<(StardewValley.Character animal, Point tile)> GetUnpettedFarmAnimals(Point searchCenter, int radius);

        /// <summary>
        /// Gets the player's pet (cat/dog) if it's unpetted and within radius of a center point.
        /// Returns null if there's no pet, it's already been petted today, or it's not in this location.
        /// </summary>
        /// <param name="searchCenter">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Tuple containing the pet character and its tile position, or null if no valid pet found.</returns>
        (StardewValley.Character pet, Point tile)? GetUnpettedPlayerPet(Point searchCenter, int radius);

        /// <summary>
        /// Gets all hostile monsters within a radius of a center point.
        /// Includes monster state information for filtering (armored, hiding, reviving, etc.).
        /// </summary>
        /// <param name="searchCenter">The center point of the search area (typically player position).</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tuples containing monster reference (may be null in tests), tile position, and state.</returns>
        IEnumerable<(StardewValley.Monsters.Monster? monster, Point tile, MonsterState state)> GetHostileMonsters(Point searchCenter, int radius);

        /// <summary>
        /// Gets whether this location is a SlimeHutch (where squad members should not attack).
        /// </summary>
        bool IsSlimeHutch { get; }

        /// <summary>
        /// Checks if a tile is water (for fishing tasks).
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>True if the tile is water, false otherwise.</returns>
        bool IsWaterTile(Point tile);

        /// <summary>
        /// Gets the player's current tile position (for distance checks in fishing tasks).
        /// </summary>
        /// <returns>The player's current tile position.</returns>
        Point GetPlayerPosition();

        /// <summary>
        /// Gets all trees within a radius of a center point (for lumbering tasks).
        /// Returns tree tile, health value, and whether it has a tapper.
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tuples containing tile position, health (0.0-1.0), and tapper status.</returns>
        IEnumerable<(Point tile, float health, bool hasTapper)> GetTrees(Point center, int radius);

        /// <summary>
        /// Gets all twigs (tree stumps) within a radius of a center point (for lumbering tasks).
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tile positions containing twigs.</returns>
        IEnumerable<Point> GetTwigs(Point center, int radius);

        /// <summary>
        /// Gets all rocks (stones) within a radius of a center point (for mining tasks).
        /// Returns rock tile, parent sheet index (rock type), and ready for harvest status.
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tuples containing tile position, parent sheet index, and ready status.</returns>
        IEnumerable<(Point tile, int parentSheetIndex, bool readyForHarvest)> GetRocks(Point center, int radius);

        /// <summary>
        /// Gets the player's pickaxe upgrade level based on their Mining skill level (for mining tasks).
        /// Level 10+: Iridium (4), Level 8+: Gold (3), Level 5+: Steel (2), Level 2+: Copper (1), Default: Basic (0)
        /// </summary>
        /// <returns>Pickaxe upgrade level (0-4).</returns>
        int GetPlayerPickaxeLevel();

        /// <summary>
        /// Gets the object (item) at a specific tile position.
        /// Used for checking inventory capacity before assigning foraging tasks.
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>The Object at the tile, or null if none exists.</returns>
        StardewValley.Object GetObjectAt(Point tile);

        /// <summary>
        /// Gets all beehouses within a radius of a center point (for beehouse flower protection).
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tile positions containing beehouses.</returns>
        IEnumerable<Point> GetBeehousesInRadius(Point center, int radius);

        /// <summary>
        /// Gets the HoeDirt terrain feature at a specific tile, if it exists.
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>The HoeDirt at this tile, or null if there's no HoeDirt.</returns>
        StardewValley.TerrainFeatures.HoeDirt? GetHoeDirtAt(Point tile);

        /// <summary>
        /// Checks if the crop at the specified tile is a flower (category -80).
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>True if the tile has a flower crop, false otherwise.</returns>
        bool IsFlowerCropAt(Point tile);

        /// <summary>
        /// Gets all individual seat positions from sittable furniture within a radius of a center point.
        /// Returns each seat position with its furniture tile and facing direction.
        /// Uses Vector2 to preserve fractional Y-offsets from Furniture.GetSeatPositions().
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tuples containing seat position, furniture tile, and sitting direction.</returns>
        IEnumerable<(Vector2 seatPosition, Point furnitureTile, int direction)> GetSittableFurniture(Point center, int radius);

        /// <summary>
        /// Gets all map seats within a radius of a center point.
        /// Map seats are benches, picnic tables, etc. placed on the map.
        /// Uses Vector2 to preserve fractional Y-offsets from MapSeat.GetSeatPositions().
        /// </summary>
        /// <param name="center">The center point of the search area.</param>
        /// <param name="radius">The radius in tiles to search.</param>
        /// <returns>Enumerable of tuples containing tile position and facing direction.</returns>
        IEnumerable<(Vector2 tile, int direction)> GetMapSeats(Point center, int radius);

        /// <summary>
        /// Gets the sitting direction for a furniture or map seat at a specific tile.
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>The facing direction for sitting (0=up, 1=right, 2=down, 3=left), or null if no seat at tile.</returns>
        int? GetSittingDirection(Point tile);
    }
}
