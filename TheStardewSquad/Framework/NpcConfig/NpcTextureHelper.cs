using StardewValley;
using StardewValley.Characters;

namespace TheStardewSquad.Framework.NpcConfig
{
    /// <summary>Asset-path helpers for squad-member textures.</summary>
    public static class NpcTextureHelper
    {
        /// <summary>
        /// Returns the content asset path for the NPC or Pet's current sprite
        /// </summary>
        public static string GetTextureAssetPath(NPC npc)
        {
            if (npc is Pet pet)
                return pet.getPetTextureName();
            return "Characters/" + npc.getTextureName();
        }
    }
}
