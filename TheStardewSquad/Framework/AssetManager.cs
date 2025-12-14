using StardewModdingAPI.Events;
using TheStardewSquad.Framework.NpcConfig.Models;

namespace TheStardewSquad.Framework
{
    /// <summary>Handles loading custom assets for the mod.</summary>
    public class AssetManager
    {
        /// <summary>Raised when the game is requesting an asset. This is where we load our custom JSON files.</summary>
        public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            // Load the unified NPC configuration asset
            if (e.NameWithoutLocale.IsEquivalentTo("ThaliaFawnheart.TheStardewSquad/NpcConfig"))
            {
                e.LoadFrom(() => new Dictionary<string, NpcConfigData>(), AssetLoadPriority.Exclusive);
            }
        }
    }
}