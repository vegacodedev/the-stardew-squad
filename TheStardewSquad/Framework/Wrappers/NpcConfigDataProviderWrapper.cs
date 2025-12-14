using System.Collections.Generic;
using StardewValley;
using TheStardewSquad.Abstractions.Data;
using TheStardewSquad.Framework.NpcConfig.Models;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of INpcConfigDataProvider that loads from Game1.content.
    /// </summary>
    public class NpcConfigDataProviderWrapper : INpcConfigDataProvider
    {
        public Dictionary<string, NpcConfigData> LoadNpcConfigData()
        {
            return Game1.content.Load<Dictionary<string, NpcConfigData>>("ThaliaFawnheart.TheStardewSquad/NpcConfig");
        }

        public Dictionary<string, string> LoadAnimationDescriptions()
        {
            return Game1.content.Load<Dictionary<string, string>>("Data/animationDescriptions");
        }
    }
}
