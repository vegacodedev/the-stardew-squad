using StardewValley;
using TheStardewSquad.Abstractions.Core;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of IRandomService that uses Game1.random.
    /// </summary>
    public class RandomServiceWrapper : IRandomService
    {
        public double NextDouble() => Game1.random.NextDouble();
    }
}
