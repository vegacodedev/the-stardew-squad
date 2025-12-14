using StardewValley;
using StardewValley.Extensions;
using TheStardewSquad.Abstractions.Utilities;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of IRandomSelector that uses Game1.random.
    /// </summary>
    public class RandomSelectorWrapper : IRandomSelector
    {
        public T ChooseFrom<T>(IList<T> collection)
        {
            return Game1.random.ChooseFrom(collection);
        }
    }
}
