namespace TheStardewSquad.Abstractions.Core
{
    /// <summary>
    /// Abstracts random number generation from Game1.random.
    /// </summary>
    public interface IRandomService
    {
        /// <summary>Returns a random double between 0.0 and 1.0.</summary>
        double NextDouble();
    }
}
