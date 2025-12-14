using Microsoft.Xna.Framework;
using StardewValley;
using TheStardewSquad.Abstractions.Character;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Wrapper that adapts a Farmer instance to the IFarmerInfo interface.
    /// This is used in production code to bridge between the game's Farmer class
    /// and our testable interface.
    /// </summary>
    public class FarmerInfoWrapper : IFarmerInfo
    {
        private readonly Farmer _farmer;

        public FarmerInfoWrapper(Farmer farmer)
        {
            _farmer = farmer;
        }

        /// <summary>Gets the farmer's current tile position.</summary>
        public Point TilePoint => _farmer.TilePoint;

        /// <summary>Gets the farmer's facing direction.</summary>
        public int FacingDirection => _farmer.FacingDirection;

        /// <summary>
        /// Implicit conversion operator for convenience.
        /// Allows you to pass a Farmer where an IFarmerInfo is expected.
        /// Example: IFarmerInfo info = farmer; // automatically wraps
        /// </summary>
        public static implicit operator FarmerInfoWrapper(Farmer farmer)
        {
            return new FarmerInfoWrapper(farmer);
        }
    }
}
