using Microsoft.Xna.Framework;

namespace TheStardewSquad.Abstractions.Core
{
    /// <summary>
    /// Abstracts game state queries from Context and Game1 static properties.
    /// </summary>
    public interface IGameStateService
    {
        /// <summary>Gets whether the world is ready (Context.IsWorldReady).</summary>
        bool IsWorldReady { get; }

        /// <summary>Gets whether the player is free to act (Context.IsPlayerFree).</summary>
        bool IsPlayerFree { get; }

        /// <summary>Gets whether a cutscene or event is currently playing (Game1.eventUp).</summary>
        bool IsEventUp { get; }

        /// <summary>Gets whether the game window is currently active (Game1.game1.IsActive).</summary>
        bool IsGameActive { get; }

        /// <summary>Gets whether the player is currently in a festival (Game1.isFestival()).</summary>
        bool IsFestival();

        /// <summary>Gets the current in-game time of day (Game1.timeOfDay).</summary>
        int TimeOfDay { get; }

        /// <summary>Gets the current game time for animations (Game1.currentGameTime).</summary>
        GameTime CurrentGameTime { get; }
    }
}
