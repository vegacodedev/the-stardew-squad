using Microsoft.Xna.Framework;
using System.Collections.Generic;
using TheStardewSquad.Abstractions.Location;

namespace TheStardewSquad.Tests.Pathfinding
{
    /// <summary>
    /// Mock implementation of IMapInfo for testing pathfinding without requiring a real GameLocation.
    /// Allows tests to define custom passability grids.
    /// </summary>
    public class MockMapInfo : IMapInfo
    {
        private readonly HashSet<Point> _passableTiles;
        private readonly HashSet<Point> _impassableTiles;
        private readonly bool _defaultPassable;

        /// <summary>
        /// Creates a mock map where all tiles are passable by default unless explicitly marked impassable.
        /// </summary>
        public MockMapInfo() : this(defaultPassable: true)
        {
        }

        /// <summary>
        /// Creates a mock map with a specific default passability.
        /// </summary>
        /// <param name="defaultPassable">If true, tiles are passable unless in impassableTiles. If false, tiles are impassable unless in passableTiles.</param>
        public MockMapInfo(bool defaultPassable)
        {
            _passableTiles = new HashSet<Point>();
            _impassableTiles = new HashSet<Point>();
            _defaultPassable = defaultPassable;
        }

        /// <summary>
        /// Marks a specific tile as passable.
        /// </summary>
        public MockMapInfo WithPassableTile(Point tile)
        {
            _passableTiles.Add(tile);
            _impassableTiles.Remove(tile); // Ensure no conflict
            return this;
        }

        /// <summary>
        /// Marks a specific tile as passable.
        /// </summary>
        public MockMapInfo WithPassableTile(int x, int y)
        {
            return WithPassableTile(new Point(x, y));
        }

        /// <summary>
        /// Marks multiple tiles as passable.
        /// </summary>
        public MockMapInfo WithPassableTiles(params Point[] tiles)
        {
            foreach (var tile in tiles)
            {
                WithPassableTile(tile);
            }
            return this;
        }

        /// <summary>
        /// Marks a specific tile as impassable (obstacle).
        /// </summary>
        public MockMapInfo WithImpassableTile(Point tile)
        {
            _impassableTiles.Add(tile);
            _passableTiles.Remove(tile); // Ensure no conflict
            return this;
        }

        /// <summary>
        /// Marks a specific tile as impassable (obstacle).
        /// </summary>
        public MockMapInfo WithImpassableTile(int x, int y)
        {
            return WithImpassableTile(new Point(x, y));
        }

        /// <summary>
        /// Marks multiple tiles as impassable (obstacles).
        /// </summary>
        public MockMapInfo WithImpassableTiles(params Point[] tiles)
        {
            foreach (var tile in tiles)
            {
                WithImpassableTile(tile);
            }
            return this;
        }

        /// <summary>
        /// Creates a rectangular region of passable tiles.
        /// </summary>
        public MockMapInfo WithPassableRegion(int startX, int startY, int width, int height)
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
        /// Creates a rectangular region of impassable tiles (wall).
        /// </summary>
        public MockMapInfo WithImpassableRegion(int startX, int startY, int width, int height)
        {
            for (int x = startX; x < startX + width; x++)
            {
                for (int y = startY; y < startY + height; y++)
                {
                    WithImpassableTile(x, y);
                }
            }
            return this;
        }

        /// <summary>
        /// Creates a vertical wall of impassable tiles.
        /// </summary>
        public MockMapInfo WithVerticalWall(int x, int startY, int height)
        {
            return WithImpassableRegion(x, startY, 1, height);
        }

        /// <summary>
        /// Creates a horizontal wall of impassable tiles.
        /// </summary>
        public MockMapInfo WithHorizontalWall(int startX, int y, int width)
        {
            return WithImpassableRegion(startX, y, width, 1);
        }

        /// <summary>
        /// Checks if a tile is passable based on the defined passability rules.
        /// </summary>
        public bool IsTilePassable(Point tile)
        {
            // Explicit impassable takes precedence
            if (_impassableTiles.Contains(tile))
                return false;

            // Explicit passable takes precedence
            if (_passableTiles.Contains(tile))
                return true;

            // Fall back to default
            return _defaultPassable;
        }
    }
}
