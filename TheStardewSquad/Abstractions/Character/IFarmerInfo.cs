using Microsoft.Xna.Framework;

namespace TheStardewSquad.Abstractions.Character
{
    /// <summary>
    /// Interface for accessing Farmer properties needed for formation positioning.
    /// This abstraction allows for easy testing without requiring a real Farmer instance.
    /// </summary>
    public interface IFarmerInfo
    {
        /// <summary>Gets the farmer's current tile position.</summary>
        Point TilePoint { get; }

        /// <summary>Gets the farmer's facing direction (0=up, 1=right, 2=down, 3=left).</summary>
        int FacingDirection { get; }
    }
}
