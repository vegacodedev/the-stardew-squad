using StardewValley;

namespace TheStardewSquad.Abstractions.Character
{
    /// <summary>
    /// Abstraction for NPC sprite and animation operations.
    /// Enables testing without requiring real NPC sprite instances.
    /// </summary>
    public interface INpcSpriteService
    {
        /// <summary>
        /// Gets the NPC's current facing direction.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <returns>Facing direction (0=up, 1=right, 2=down, 3=left)</returns>
        int GetFacingDirection(NPC npc);

        /// <summary>
        /// Sets the current animation for an NPC's sprite.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <param name="frames">List of animation frames</param>
        void SetCurrentAnimation(NPC npc, List<FarmerSprite.AnimationFrame> frames);
    }
}
