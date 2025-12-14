using Microsoft.Xna.Framework;
using StardewValley;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Framework.Wrappers;

namespace TheStardewSquad.Framework.Squad
{
    /// <summary>Manages the positions of squad members in a formation around the player.</summary>
    public class FormationManager
    {
        private readonly List<Point> _offsets;
        private readonly Dictionary<ISquadMate, int> _assignedSlots = new();
        private readonly HashSet<int> _occupiedSlotIndices = new();

        public FormationManager()
        {
            // Max squad size is 100, plus a buffer.
            this._offsets = GenerateSpiralOffsets(150);
        }

        /// <summary>Generates a list of formation offsets, starting close to the player and spiraling outwards behind them.</summary>
        private static List<Point> GenerateSpiralOffsets(int max)
        {
            var points = new List<Point>();
            var occupied = new HashSet<Point>();

            // Start with a natural-feeling order for the first few slots, all behind or to the sides.
            var priority = new[]
            {
                new Point(0, 1), new Point(-1, 1), new Point(1, 1),
                new Point(-1, 0), new Point(1, 0), new Point(0, 2),
                new Point(-1, 2), new Point(1, 2), new Point(-2, 1), new Point(2, 1)
            };
            foreach (var p in priority)
            {
                if (points.Count < max && occupied.Add(p))
                    points.Add(p);
            }

            // Procedurally generate the rest of the spiral.
            int x = 0, y = 0, dx = 1, dy = 0;
            int sideLength = 1, steps = 0, turnCount = 0;

            while (points.Count < max)
            {
                x += dx;
                y += dy;
                steps++;

                // MODIFICATION: Ensure we only add points behind or to the side of the canonical orientation (y >= 0).
                if (y >= 0 && occupied.Add(new Point(x, y)))
                {
                    points.Add(new Point(x, y));
                }

                if (steps == sideLength)
                {
                    steps = 0;
                    // Turn 90 degrees right.
                    (dx, dy) = (-dy, dx);
                    turnCount++;
                    if (turnCount == 2)
                    {
                        turnCount = 0;
                        sideLength++;
                    }
                }
            }
            return points;
        }

        /// <summary>Assigns the first available formation slot to a squad mate.</summary>
        public void AssignSlot(ISquadMate mate)
        {
            if (this._assignedSlots.ContainsKey(mate)) return;

            for (int i = 0; i < this._offsets.Count; i++)
            {
                if (!this._occupiedSlotIndices.Contains(i))
                {
                    this._assignedSlots[mate] = i;
                    this._occupiedSlotIndices.Add(i);
                    mate.FormationSlotIndex = i;
                    return;
                }
            }
            // No free slot found (should not happen if max offsets > max squad size).
            mate.FormationSlotIndex = -1;
        }

        /// <summary>Releases a squad mate's formation slot, making it available for others.</summary>
        public void ReleaseSlot(ISquadMate mate)
        {
            if (this._assignedSlots.TryGetValue(mate, out int slotIndex))
            {
                this._assignedSlots.Remove(mate);
                this._occupiedSlotIndices.Remove(slotIndex);
                mate.FormationSlotIndex = -1;
            }
        }

        /// <summary>Gets the world tile coordinate for a mate's assigned formation slot relative to the player, rotated based on the player's facing direction.</summary>
        /// <param name="mate">The squad mate to get the position for.</param>
        /// <param name="farmerInfo">The farmer information (position and facing direction).</param>
        /// <param name="targetTile">The calculated target tile position.</param>
        /// <returns>True if a valid target tile was calculated, false if the mate has no assigned slot.</returns>
        public bool TryGetTargetTile(ISquadMate mate, IFarmerInfo farmerInfo, out Point targetTile)
        {
            if (mate.FormationSlotIndex >= 0 && mate.FormationSlotIndex < this._offsets.Count)
            {
                Point baseOffset = this._offsets[mate.FormationSlotIndex];
                Point rotatedOffset;

                // MODIFICATION: Rotate the offset based on the player's facing direction to keep the formation behind them.
                // 0=up, 1=right, 2=down, 3=left
                switch (farmerInfo.FacingDirection)
                {
                    case 0: // Player is facing Up: Rotate formation 180 degrees.
                        rotatedOffset = new Point(-baseOffset.X, -baseOffset.Y);
                        break;
                    case 1: // Player is facing Right: Rotate formation 90 degrees clockwise.
                        rotatedOffset = new Point(baseOffset.Y, -baseOffset.X);
                        break;
                    case 3: // Player is facing Left: Rotate formation 90 degrees counter-clockwise.
                        rotatedOffset = new Point(-baseOffset.Y, baseOffset.X);
                        break;
                    case 2: // Player is facing Down: Use default formation.
                    default:
                        rotatedOffset = baseOffset;
                        break;
                }

                targetTile = new Point(farmerInfo.TilePoint.X + rotatedOffset.X, farmerInfo.TilePoint.Y + rotatedOffset.Y);
                return true;
            }

            targetTile = default;
            return false;
        }

        /// <summary>
        /// Gets the world tile coordinate for a mate's assigned formation slot relative to the player.
        /// This is a convenience overload that automatically wraps the Farmer.
        /// </summary>
        /// <param name="mate">The squad mate to get the position for.</param>
        /// <param name="player">The farmer/player.</param>
        /// <param name="targetTile">The calculated target tile position.</param>
        /// <returns>True if a valid target tile was calculated, false if the mate has no assigned slot.</returns>
        public bool TryGetTargetTile(ISquadMate mate, Farmer player, out Point targetTile)
        {
            // Delegate to the interface-based method by wrapping the Farmer
            return TryGetTargetTile(mate, new FarmerInfoWrapper(player), out targetTile);
        }

        /// <summary>Clears all slot assignments. Called when returning to title.</summary>
        public void Reset()
        {
            this._assignedSlots.Clear();
            this._occupiedSlotIndices.Clear();
        }
    }
}