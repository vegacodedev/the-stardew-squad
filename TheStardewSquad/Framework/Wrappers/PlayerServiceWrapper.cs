using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Tools;
using TheStardewSquad.Abstractions.Core;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of IPlayerService that accesses Game1.player properties.
    /// </summary>
    public class PlayerServiceWrapper : IPlayerService
    {
        public Farmer Player => Game1.player;

        public Point TilePoint => Game1.player.TilePoint;

        public Vector2 Tile => Game1.player.Tile;

        public bool IsSwimming => Game1.player.swimming.Value;

        public bool IsSitting => Game1.player.sittingFurniture != null || Game1.player.isSitting.Value;

        public Tool CurrentTool => Game1.player.CurrentTool;

        public float MovementSpeed => Game1.player.getMovementSpeed();

        public float Speed => Game1.player.speed;

        public GameLocation CurrentLocation => Game1.player.currentLocation;

        public Vector2 StandingPosition => Game1.player.getStandingPosition();

        public void ChangeFriendship(int amount, NPC npc)
        {
            Game1.player.changeFriendship(amount, npc);
        }
    }
}
