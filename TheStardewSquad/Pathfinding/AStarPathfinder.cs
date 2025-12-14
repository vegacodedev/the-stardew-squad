using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System.Threading;
using TheStardewSquad.Abstractions.Location;
using TheStardewSquad.Framework.Wrappers;

namespace TheStardewSquad.Pathfinding
{
    public static class AStarPathfinder
    {
        private const int MaxIterations = 500;

        /// <summary>
        /// Finds a path from start to end using A* algorithm with the testable IMapInfo interface.
        /// </summary>
        /// <param name="mapInfo">Map information provider for passability checks.</param>
        /// <param name="start">Starting tile position.</param>
        /// <param name="end">Ending tile position.</param>
        /// <param name="monitor">Monitor for logging (can be null for tests).</param>
        /// <returns>A stack of points representing the path, or null if no path exists.</returns>
        public static Stack<Point> FindPath(IMapInfo mapInfo, Point start, Point end, IMonitor monitor = null)
        {
            var openList = new List<AStarNode>();
            var closedList = new HashSet<AStarNode>();
            var startNode = new AStarNode(start);
            var endNode = new AStarNode(end);
            int iterationCount = 0;

            openList.Add(startNode);

            while (openList.Count > 0)
            {

                if (iterationCount > MaxIterations)
                {
                    monitor?.Log($"Pathfinding aborted after {MaxIterations} iterations. Destination may be unreachable.", LogLevel.Trace);
                    return null;
                }
                iterationCount++;
                // 1. Get the most promising node from the open list (lowest F cost).
                var currentNode = openList.OrderBy(n => n.FCost).ThenBy(n => n.HCost).First();

                // 2. Move it from the open list to the closed list.
                openList.Remove(currentNode);
                closedList.Add(currentNode);

                // 3. If we've reached the end, reconstruct and return the path.
                if (currentNode.Equals(endNode))
                {
                    return ReconstructPath(currentNode);
                }

                // 4. Get all valid neighbors for the current node.
                var neighbors = GetNeighbors(mapInfo, currentNode);

                foreach (var neighbor in neighbors)
                {
                    // 5. If the neighbor is already evaluated, skip it.
                    if (closedList.Contains(neighbor))
                        continue;

                    // 6. Calculate the new cost to get to this neighbor.
                    int newGCost = currentNode.GCost + GetDistance(currentNode.Position, neighbor.Position);

                    // 7. If this path is better, or if we haven't seen this node before...
                    if (newGCost < neighbor.GCost || !openList.Contains(neighbor))
                    {
                        neighbor.GCost = newGCost;
                        neighbor.HCost = GetDistance(neighbor.Position, endNode.Position);
                        neighbor.Parent = currentNode;

                        if (!openList.Contains(neighbor))
                            openList.Add(neighbor);
                    }
                }
            }

            // If the loop finishes without finding a path, return null.
            return null;
        }

        /// <summary>
        /// Backward-compatible overload that wraps GameLocation and Character.
        /// Existing code continues to work without changes.
        /// </summary>
        public static Stack<Point> FindPath(GameLocation location, Point start, Point end, Character character, IMonitor monitor)
        {
            return FindPath(new MapInfoWrapper(location, character), start, end, monitor);
        }

        /// <summary>
        /// Finds the closest passable neighbor to a target tile using the testable IMapInfo interface.
        /// </summary>
        /// <param name="mapInfo">Map information provider for passability checks.</param>
        /// <param name="targetTile">The tile to find neighbors around.</param>
        /// <param name="characterTile">The character's current position (for distance sorting and reachability checks).</param>
        /// <param name="claimedSpots">Optional set of positions that are already claimed by other characters.</param>
        /// <param name="validateReachability">If true, verifies that returned neighbors are pathfinding-reachable from characterTile. Defaults to false for backward compatibility.</param>
        /// <param name="monitor">Optional monitor for logging pathfinding failures (only used when validateReachability is true).</param>
        /// <returns>The closest valid neighbor, or null if none found.</returns>
        public static Point? FindClosestPassableNeighbor(IMapInfo mapInfo, Point targetTile, Point characterTile, ISet<Vector2> claimedSpots = null, bool validateReachability = false, IMonitor monitor = null)
        {
            var neighbors = new List<Point>
            {
                new Point(targetTile.X, targetTile.Y + 1), // Down
                new Point(targetTile.X - 1, targetTile.Y), // Left
                new Point(targetTile.X + 1, targetTile.Y), // Right
                new Point(targetTile.X, targetTile.Y - 1)  // Up
            };

            var validNeighbors = neighbors
                .Where(p => mapInfo.IsTilePassable(p));

            if (claimedSpots != null)
            {
                validNeighbors = validNeighbors.Where(p => !claimedSpots.Contains(p.ToVector2()));
            }

            // If reachability validation is enabled, filter neighbors that cannot be reached via pathfinding
            if (validateReachability)
            {
                validNeighbors = validNeighbors.Where(p =>
                {
                    // Quick check: If there's an unobstructed path, it's definitely reachable
                    if (IsPathUnobstructed(mapInfo, characterTile, p))
                        return true;

                    // Slower check: Use A* pathfinding to verify reachability
                    // This catches cases where the path is blocked but could be navigated around
                    var path = FindPath(mapInfo, characterTile, p, monitor);
                    return path != null && path.Count > 0;
                });
            }

            return validNeighbors
                .OrderBy(p => Vector2.DistanceSquared(p.ToVector2(), characterTile.ToVector2()))
                .Cast<Point?>()
                .FirstOrDefault();
        }

        /// <summary>
        /// Backward-compatible overload that wraps GameLocation and Character.
        /// </summary>
        public static Point? FindClosestPassableNeighbor(GameLocation location, Point targetTile, Character character, ISet<Vector2> claimedSpots = null, bool validateReachability = false, IMonitor monitor = null)
        {
            using (new ScopedTerrainFeatureRemoval(location, targetTile))
            {
                return FindClosestPassableNeighbor(new MapInfoWrapper(location, character), targetTile, character.TilePoint, claimedSpots, validateReachability, monitor);
            }
        }

        private static Stack<Point> ReconstructPath(AStarNode endNode)
        {
            var path = new Stack<Point>();
            var currentNode = endNode;
            while (currentNode != null)
            {
                path.Push(currentNode.Position);
                currentNode = currentNode.Parent;
            }
            return path;
        }

        private static List<AStarNode> GetNeighbors(IMapInfo mapInfo, AStarNode node)
        {
            var neighbors = new List<AStarNode>();
            Point currentPos = node.Position;

            // Check all 8 directions (including diagonals)
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue; // Skip the center tile

                    Point neighborPos = new Point(currentPos.X + x, currentPos.Y + y);

                    // IMPORTANT: Prevent cutting corners through walls
                    if (x != 0 && y != 0) // It's a diagonal move
                    {
                        // Check if adjacent cardinal tiles are blocked
                        if (!mapInfo.IsTilePassable(new Point(currentPos.X + x, currentPos.Y)) ||
                            !mapInfo.IsTilePassable(new Point(currentPos.X, currentPos.Y + y)))
                        {
                            continue; // Can't move diagonally past a corner
                        }
                    }

                    // Check if the tile is passable
                    if (mapInfo.IsTilePassable(neighborPos))
                    {
                        neighbors.Add(new AStarNode(neighborPos));
                    }
                }
            }

            return neighbors;
        }


        /// <summary>
        /// A custom passability check that ignores NPC-specific barriers, allowing followers
        /// to go where the player can. This is the original implementation kept for backward compatibility.
        /// </summary>
        public static bool IsTilePassableForFollower(GameLocation location, Point tile, Character character)
        {
            if (location.objects.TryGetValue(tile.ToVector2(), out var obj) && !obj.isPassable())
            {
                return false;
            }

            if (location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature) && !feature.isPassable(character))
            {
                return false;
            }

            var buildingLayer = location.Map.GetLayer("Buildings");
            if (buildingLayer != null && tile.X >= 0 && tile.Y >= 0 && tile.X < buildingLayer.LayerWidth && tile.Y < buildingLayer.LayerHeight)
            {
                if (buildingLayer.Tiles[tile.X, tile.Y] != null)
                {
                    // Tiles on the Buildings layer are impassable by default unless explicitly marked as such.
                    string passableProperty = location.doesTileHaveProperty(tile.X, tile.Y, "Passable", "Buildings");
                    if (passableProperty == null || !passableProperty.Equals("T", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            // Check the Back layer for impassable tiles
            string backPassableProperty = location.doesTileHaveProperty(tile.X, tile.Y, "Passable", "Back");
            if (backPassableProperty != null && backPassableProperty.Equals("F", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static int GetDistance(Point a, Point b)
        {
            // Manhattan distance with a factor for diagonals
            int dstX = Math.Abs(a.X - b.X);
            int dstY = Math.Abs(a.Y - b.Y);

            if (dstX > dstY)
                return 14 * dstY + 10 * (dstX - dstY);
            return 14 * dstX + 10 * (dstY - dstX);
        }

        /// <summary>
        /// Checks for a direct, unobstructed path between two points by sampling tiles along the line (testable version).
        /// </summary>
        /// <returns>True if the path is clear, false if an obstacle is found.</returns>
        public static bool IsPathUnobstructed(IMapInfo mapInfo, Point startTile, Point endTile)
        {
            Vector2 startPixels = startTile.ToVector2() * 64f + new Vector2(32, 32);
            Vector2 endPixels = endTile.ToVector2() * 64f + new Vector2(32, 32);

            Vector2 direction = endPixels - startPixels;
            float distance = direction.Length();
            if (distance == 0) return true;
            direction.Normalize();

            // Sample points every 16 pixels (a quarter of a tile) along the line.
            for (float i = 0; i < distance; i += 16f)
            {
                Vector2 currentPixelPos = startPixels + direction * i;
                Point currentTile = new Point((int)currentPixelPos.X / 64, (int)currentPixelPos.Y / 64);

                // If any tile along the path is not passable, the path is obstructed.
                if (!mapInfo.IsTilePassable(currentTile))
                {
                    // As a small exception, allow passing through the start tile itself,
                    // as it can sometimes be flagged as impassable (e.g., inside a tree's bounding box).
                    if (currentTile != startTile)
                    {
                        return false;
                    }
                }
            }

            // If we checked all points and found no obstructions, the path is clear.
            return true;
        }

        /// <summary>
        /// Backward-compatible overload that wraps GameLocation and Character.
        /// </summary>
        public static bool IsPathUnobstructed(GameLocation location, Point startTile, Point endTile, Character character)
        {
            return IsPathUnobstructed(new MapInfoWrapper(location, character), startTile, endTile);
        }

        /// <summary>
        /// Checks if every single tile along a direct path from start to end is passable using Bresenham's line algorithm (testable version).
        /// </summary>
        /// <remarks>This is more thorough than IsPathUnobstructed as it checks EVERY tile without sampling.</remarks>
        /// <returns>True if all tiles along the direct path are passable, false otherwise.</returns>
        public static bool IsDirectPathFullyPassable(IMapInfo mapInfo, Point start, Point end)
        {
            // Use Bresenham's line algorithm to get all tiles along the direct path
            int dx = Math.Abs(end.X - start.X);
            int dy = Math.Abs(end.Y - start.Y);
            int sx = start.X < end.X ? 1 : -1;
            int sy = start.Y < end.Y ? 1 : -1;
            int err = dx - dy;

            int x = start.X;
            int y = start.Y;

            while (true)
            {
                Point currentTile = new Point(x, y);

                // Check if this tile is passable
                if (!mapInfo.IsTilePassable(currentTile))
                {
                    return false; // Found an impassable tile along the direct path
                }

                // Reached the end
                if (x == end.X && y == end.Y)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            return true; // All tiles along the direct path are passable
        }

        /// <summary>
        /// Backward-compatible overload that wraps GameLocation and Character.
        /// </summary>
        public static bool IsDirectPathFullyPassable(GameLocation location, Point start, Point end, Character character)
        {
            return IsDirectPathFullyPassable(new MapInfoWrapper(location, character), start, end);
        }
    }
}