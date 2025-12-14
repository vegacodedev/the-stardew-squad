using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace TheStardewSquad.Pathfinding
{
    /// <summary>
    /// A helper class that temporarily removes a terrain feature for the duration of a 'using' block.
    /// This exists because some TerrainFeatures' BoundingBoxes are bigger than they actually are in game, and it caused issues to identify passable adjacent tiles.
    /// </summary>
    public class ScopedTerrainFeatureRemoval : IDisposable
    {
        private readonly GameLocation _location;
        private readonly Vector2 _tile;
        private readonly TerrainFeature _feature;

        public ScopedTerrainFeatureRemoval(GameLocation location, Point tile)
        {
            this._location = location;
            this._tile = tile.ToVector2();

            // Find and remove the feature, if it exists.
            if (this._location.terrainFeatures.TryGetValue(this._tile, out this._feature))
            {
                this._location.terrainFeatures.Remove(this._tile);
            }
        }

        /// <summary>
        /// This method is called automatically at the end of a 'using' block.
        /// It adds the feature back to the location if it was removed.
        /// </summary>
        public void Dispose()
        {
            if (this._feature != null)
            {
                this._location.terrainFeatures[this._tile] = this._feature;
            }
        }
    }
}