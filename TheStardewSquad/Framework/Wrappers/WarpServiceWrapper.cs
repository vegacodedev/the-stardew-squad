using Microsoft.Xna.Framework;
using StardewValley;
using TheStardewSquad.Abstractions.Character;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of IWarpService that uses Game1.warpCharacter().
    /// </summary>
    public class WarpServiceWrapper : IWarpService
    {
        public void WarpCharacter(NPC npc, string locationName, Point tilePosition)
        {
            Game1.warpCharacter(npc, locationName, tilePosition);
        }
    }
}
