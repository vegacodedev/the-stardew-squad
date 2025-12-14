using Microsoft.Xna.Framework;
using StardewValley;

namespace TheStardewSquad.Abstractions.Character
{
    /// <summary>
    /// Abstracts character warping operations from Game1.warpCharacter().
    /// </summary>
    public interface IWarpService
    {
        /// <summary>Warps an NPC to a specific location and tile.</summary>
        /// <param name="npc">The NPC to warp.</param>
        /// <param name="locationName">The name of the destination location.</param>
        /// <param name="tilePosition">The destination tile position.</param>
        void WarpCharacter(NPC npc, string locationName, Point tilePosition);
    }
}
