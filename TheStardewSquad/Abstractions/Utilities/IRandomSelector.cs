namespace TheStardewSquad.Abstractions.Utilities
{
    /// <summary>
    /// Abstraction for random selection operations.
    /// Enables testing with deterministic or controlled randomness.
    /// </summary>
    public interface IRandomSelector
    {
        /// <summary>
        /// Chooses a random item from a collection.
        /// </summary>
        /// <typeparam name="T">Type of items in the collection</typeparam>
        /// <param name="collection">Collection to choose from</param>
        /// <returns>A randomly selected item</returns>
        T ChooseFrom<T>(IList<T> collection);
    }
}
