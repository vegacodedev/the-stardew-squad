using Microsoft.Xna.Framework;

namespace TheStardewSquad.Abstractions.Location
{
    /// <summary>
    /// Interface for accessing map/tile information needed for pathfinding.
    /// This abstraction allows for easy testing without requiring a real GameLocation.
    /// </summary>
    public interface IMapInfo
    {
        /// <summary>
        /// Checks if a tile is passable for a character (follower).
        /// This includes checking for objects, terrain features, building tiles, and tile properties.
        /// </summary>
        /// <param name="tile">The tile position to check.</param>
        /// <returns>True if the tile is passable, false otherwise.</returns>
        bool IsTilePassable(Point tile);
    }
}
