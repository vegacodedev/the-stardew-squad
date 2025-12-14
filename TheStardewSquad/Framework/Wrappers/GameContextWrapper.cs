using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using TheStardewSquad.Abstractions.Core;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of IGameContext that wraps Game1 and Context static state.
    /// </summary>
    public class GameContextWrapper : IGameContext
    {
        public bool IsPlayerFree => Context.IsPlayerFree;

        public Farmer Player => Game1.player;

        public GameLocation CurrentLocation => Game1.currentLocation;

        public bool IsFestival => Game1.isFestival();

        public bool IsGamepadControls => Game1.options.gamepadControls;

        public Character GetCharacterAtTile(GameLocation location, Point tile)
        {
            return location.isCharacterAtTile(tile.ToVector2());
        }

        public bool HasFriendship(string npcName)
        {
            return Game1.player.friendshipData.ContainsKey(npcName);
        }
    }
}
