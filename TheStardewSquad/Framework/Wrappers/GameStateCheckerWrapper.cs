using StardewValley;
using TheStardewSquad.Abstractions.Core;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of IGameStateChecker that uses GameStateQuery.
    /// </summary>
    public class GameStateCheckerWrapper : IGameStateChecker
    {
        public bool CheckConditions(string condition, GameLocation location, Farmer player)
        {
            return GameStateQuery.CheckConditions(condition, location, player);
        }
    }
}
