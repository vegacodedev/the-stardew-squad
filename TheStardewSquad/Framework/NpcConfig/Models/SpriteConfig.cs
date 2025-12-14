using System.Collections.Generic;

namespace TheStardewSquad.Framework.NpcConfig.Models
{
    /// <summary>
    /// Sprite animation frame configuration.
    /// Supports custom sprite animations with optional horizontal flipping.
    /// </summary>
    public class SpriteAnimationConfig
    {
        /// <summary>
        /// Game State Query condition for when this sprite animation should be used.
        /// First matching condition wins (not pooled like dialogue).
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// When true, this sprite is designed for vanilla NPC appearance.
        /// System will verify NPC texture matches vanilla before using this sprite.
        /// If the NPC's texture has been modified by a retexture mod, this sprite will be skipped.
        /// </summary>
        public bool IsVanilla { get; set; }

        /// <summary>
        /// Path to extension sprite sheet asset.
        /// Example: "Mod.UniqueID/Sprites/FileName"
        /// </summary>
        public string ExtensionSheet { get; set; }

        /// <summary>
        /// Frame indices by direction. Supports mixed array formats:
        /// - Simple integers: [28, 29]
        /// - Frame objects with flip: [{"Frame": 28, "Flip": true}, {"Frame": 29, "Flip": true}]
        /// - Mixed: [28, {"Frame": 29, "Flip": true}]
        /// </summary>
        public Dictionary<string, List<object>> FramesByDirection { get; set; }

        /// <summary>
        /// Duration of each frame in milliseconds.
        /// </summary>
        public int FrameDuration { get; set; }

        /// <summary>
        /// Whether the animation loops.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Frame index from original NPC sheet to use if extension sheet fails to load.
        /// </summary>
        public int FallbackFrame { get; set; }
    }

    /// <summary>
    /// Sprite configuration for various task animations.
    /// STUB: Not yet implemented - reserved for future sprite system.
    /// Each task can have either a single sprite config or priority array (first-match-wins).
    /// </summary>
    public class SpriteConfig
    {
        public object Attacking { get; set; }
        public object Mining { get; set; }
        public object Fishing { get; set; }
        public object Watering { get; set; }
        public object Lumbering { get; set; }
        public object Harvesting { get; set; }
        public object Foraging { get; set; }
        public object Idle { get; set; }
        public object Sitting { get; set; }
        public object Petting { get; set; }
    }
}
