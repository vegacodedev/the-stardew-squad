using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using TheStardewSquad.Abstractions.Core;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of IGameStateService that accesses Context and Game1 static properties.
    /// </summary>
    public class GameStateServiceWrapper : IGameStateService
    {
        public bool IsWorldReady => Context.IsWorldReady;

        public bool IsPlayerFree => Context.IsPlayerFree;

        public bool IsEventUp => Game1.eventUp;

        public bool IsGameActive => Game1.game1.IsActive;

        public bool IsFestival() => Game1.isFestival();

        public int TimeOfDay => Game1.timeOfDay;

        public GameTime CurrentGameTime => Game1.currentGameTime;
    }
}
