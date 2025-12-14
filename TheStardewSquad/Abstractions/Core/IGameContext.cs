using Microsoft.Xna.Framework;
using StardewValley;

namespace TheStardewSquad.Abstractions.Core
{
    /// <summary>
    /// Abstraction for accessing game state and context.
    /// Enables testing of components that depend on Game1 static state and SMAPI Context.
    /// </summary>
    public interface IGameContext
    {
        /// <summary>Gets whether the player is free to interact (not in menu, dialogue, etc.).</summary>
        bool IsPlayerFree { get; }

        /// <summary>Gets the current player.</summary>
        Farmer Player { get; }

        /// <summary>Gets the current game location.</summary>
        GameLocation CurrentLocation { get; }

        /// <summary>Gets whether a festival is currently active.</summary>
        bool IsFestival { get; }

        /// <summary>Gets whether gamepad controls are enabled.</summary>
        bool IsGamepadControls { get; }

        /// <summary>Gets the character at the specified tile in the given location, or null if none.</summary>
        StardewValley.Character GetCharacterAtTile(GameLocation location, Point tile);

        /// <summary>Checks if the player has friendship data for the given NPC name.</summary>
        bool HasFriendship(string npcName);
    }
}
