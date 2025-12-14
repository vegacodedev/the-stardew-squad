namespace TheStardewSquad.Framework.NpcConfig.Models
{
    /// <summary>
    /// Represents a single animation frame with optional horizontal flip.
    /// Used in sprite configurations to specify both the frame index and whether it should be flipped.
    /// </summary>
    public class SpriteAnimationFrame
    {
        /// <summary>
        /// The frame index from the sprite sheet.
        /// </summary>
        public int Frame { get; set; }

        /// <summary>
        /// Whether to flip the sprite horizontally for this frame.
        /// Default is false (no flip).
        /// </summary>
        public bool Flip { get; set; } = false;
    }
}
