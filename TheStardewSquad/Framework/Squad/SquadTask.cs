using Microsoft.Xna.Framework;
using StardewValley;

namespace TheStardewSquad.Framework.Squad
{
    public enum TaskType
    {
        Watering,
        Lumbering,
        Mining,
        Attacking,
        Harvesting,
        Foraging,
        Following,
        Fishing,
        Petting,
        Sitting
    }

    public class SquadTask
    {
        public TaskType Type { get; }
        public Point Tile { get; set; }
        public Character TargetCharacter { get; }
        public Point InteractionTile { get; }
        public bool IsManual { get; }

        /// <summary>
        /// Precise seat position with fractional coordinates for sitting tasks.
        /// Uses Vector2 to preserve Y-offsets from MapSeat/Furniture.GetSeatPositions().
        /// </summary>
        public Vector2? SeatPosition { get; }

        public SquadTask(TaskType type, Point tile, Point interactionTile, Character targetCharacter = null, bool isManual = false, Vector2? seatPosition = null)
        {
            this.Type = type;
            this.Tile = tile;
            this.TargetCharacter = targetCharacter;
            this.InteractionTile = interactionTile;
            this.IsManual = isManual;
            this.SeatPosition = seatPosition;
        }
    }
}