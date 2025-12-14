using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Tools;

namespace TheStardewSquad.Abstractions.Core
{
    /// <summary>
    /// Abstracts player state and operations.
    /// </summary>
    public interface IPlayerService
    {
        /// <summary>Gets the current player.</summary>
        Farmer Player { get; }

        /// <summary>Gets the player's current tile position.</summary>
        Point TilePoint { get; }

        /// <summary>Gets the player's tile position as a Vector2.</summary>
        Vector2 Tile { get; }

        /// <summary>Gets whether the player is currently swimming.</summary>
        bool IsSwimming { get; }

        /// <summary>Gets whether the player is currently sitting on furniture or a bench.</summary>
        bool IsSitting { get; }

        /// <summary>Gets the player's current tool.</summary>
        Tool CurrentTool { get; }

        /// <summary>Gets the player's movement speed.</summary>
        float MovementSpeed { get; }

        /// <summary>Gets the player's speed value.</summary>
        float Speed { get; }

        /// <summary>Gets the player's current location.</summary>
        GameLocation CurrentLocation { get; }

        /// <summary>Gets the player's standing position.</summary>
        Vector2 StandingPosition { get; }

        /// <summary>Changes friendship with an NPC.</summary>
        void ChangeFriendship(int amount, NPC npc);
    }
}
