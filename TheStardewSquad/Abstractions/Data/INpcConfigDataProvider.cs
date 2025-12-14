using System.Collections.Generic;
using TheStardewSquad.Framework.NpcConfig.Models;

namespace TheStardewSquad.Abstractions.Data
{
    /// <summary>
    /// Abstraction for loading unified NPC configuration data from content files.
    /// Enables testing without requiring Game1.content access.
    /// </summary>
    public interface INpcConfigDataProvider
    {
        /// <summary>
        /// Loads NPC configuration data from the content pipeline.
        /// Includes "Generic" key for default settings and individual NPC keys.
        /// </summary>
        /// <returns>Dictionary of NPC configurations keyed by NPC name</returns>
        Dictionary<string, NpcConfigData> LoadNpcConfigData();

        /// <summary>
        /// Loads vanilla animation descriptions from Data/animationDescriptions.
        /// Used for parsing idle animation frame sequences.
        /// </summary>
        /// <returns>Dictionary of animation descriptions keyed by animation ID</returns>
        Dictionary<string, string> LoadAnimationDescriptions();
    }
}
