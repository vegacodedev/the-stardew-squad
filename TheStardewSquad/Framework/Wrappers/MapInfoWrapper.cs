using Microsoft.Xna.Framework;
using StardewValley;
using System;
using TheStardewSquad.Abstractions.Location;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Wrapper that adapts a GameLocation and Character to the IMapInfo interface.
    /// This is used in production code to bridge between the game's classes
    /// and our testable interface.
    /// </summary>
    public class MapInfoWrapper : IMapInfo
    {
        private readonly GameLocation _location;
        private readonly Character _character;

        public MapInfoWrapper(GameLocation location, Character character)
        {
            _location = location;
            _character = character;
        }

        /// <summary>
        /// Checks if a tile is passable using the original AStarPathfinder logic.
        /// </summary>
        public bool IsTilePassable(Point tile)
        {
            // Check for impassable objects
            if (_location.objects.TryGetValue(tile.ToVector2(), out var obj) && !obj.isPassable())
            {
                return false;
            }

            // Check for impassable terrain features
            if (_location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature) && !feature.isPassable(_character))
            {
                return false;
            }

            // Check building layer
            var buildingLayer = _location.Map.GetLayer("Buildings");
            if (buildingLayer != null && tile.X >= 0 && tile.Y >= 0 && tile.X < buildingLayer.LayerWidth && tile.Y < buildingLayer.LayerHeight)
            {
                if (buildingLayer.Tiles[tile.X, tile.Y] != null)
                {
                    // Tiles on the Buildings layer are impassable by default unless explicitly marked as such.
                    string passableProperty = _location.doesTileHaveProperty(tile.X, tile.Y, "Passable", "Buildings");
                    if (passableProperty == null || !passableProperty.Equals("T", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            // Check the Back layer for impassable tiles
            string backPassableProperty = _location.doesTileHaveProperty(tile.X, tile.Y, "Passable", "Back");
            if (backPassableProperty != null && backPassableProperty.Equals("F", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
    }
}
