using StardewValley;

namespace TheStardewSquad.Abstractions.Core
{
    /// <summary>
    /// Abstraction for checking game state conditions.
    /// Enables testing without requiring GameStateQuery static calls.
    /// </summary>
    public interface IGameStateChecker
    {
        /// <summary>
        /// Checks if the given game state conditions are met.
        /// </summary>
        /// <param name="condition">Condition string to evaluate</param>
        /// <param name="location">Game location context</param>
        /// <param name="player">Player context</param>
        /// <returns>True if conditions are met, false otherwise</returns>
        bool CheckConditions(string condition, GameLocation location, Farmer player);
    }
}
