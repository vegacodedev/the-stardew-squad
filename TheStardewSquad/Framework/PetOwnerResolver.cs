using System.Linq;
using StardewValley;
using StardewValley.Characters;

namespace TheStardewSquad.Framework
{
    /// <summary>
    /// Resolves the actual <see cref="Farmer"/> owner of a <see cref="Pet"/>.
    /// </summary>
    /// <remarks>
    /// In SDV 1.6 each pet is anchored to its owner via <c>Pet.homeLocationName</c>
    /// (e.g., "FarmHouse" for the host, a cabin name for a farmhand). This resolver
    /// matches that name against every farmer's home location to find the owner —
    /// works for both online and offline farmers, so dismissal-while-disconnected
    /// still routes the pet to the correct cabin.
    ///
    /// Falls back to <see cref="Game1.MasterPlayer"/> if the home can't be matched
    /// (e.g., a brand-new pet that hasn't been adopted yet, or a save without its
    /// owning farmer).
    /// </remarks>
    internal static class PetOwnerResolver
    {
        public static Farmer ResolveOwner(Pet pet)
        {
            if (pet == null)
                return Game1.MasterPlayer;

            string? homeName = pet.homeLocationName?.Value;
            if (string.IsNullOrEmpty(homeName))
                return Game1.MasterPlayer;

            // getAllFarmers includes offline farmers, so a farmhand's pet still routes
            // to their cabin even when they're disconnected.
            foreach (var farmer in Game1.getAllFarmers())
            {
                var home = Utility.getHomeOfFarmer(farmer);
                if (home != null && home.NameOrUniqueName == homeName)
                    return farmer;
            }

            return Game1.MasterPlayer;
        }
    }
}
