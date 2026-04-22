using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TheStardewSquad.Framework.NpcConfig.Models
{
    /// <summary>
    /// A 2D point in sprite-local pixel coordinates (unscaled).
    /// X = pixels from the left edge of the sprite frame.
    /// Y = pixels from the top edge of the sprite frame.
    /// </summary>
    public class SpritePixelPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

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
        /// Frame duration(s) in milliseconds. Accepts either:
        /// - A single number applied to every frame (e.g. 400)
        /// - An array matching the frame count, one entry per frame (e.g. [150, 250])
        /// If the array length does not match the direction's frame count, the first entry is used for all frames.
        /// </summary>
        [JsonConverter(typeof(FlexibleIntArrayConverter))]
        public int[] FrameDuration { get; set; }

        /// <summary>
        /// Whether the animation loops.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Frame index from original NPC sheet to use if extension sheet fails to load.
        /// </summary>
        public int FallbackFrame { get; set; }

        /// <summary>
        /// Optional anchor pixel used by the Riding task to align the NPC on the horse saddle.
        /// The chosen sprite pixel (X, Y) is pinned to the riding target so sprites of any
        /// height can be placed precisely regardless of how much empty space is above/below
        /// the character in the frame.
        /// When null, the anchor defaults to (SpriteWidth/2, SpriteHeight) — i.e. the
        /// bottom-center of the frame — which reproduces the prior behavior for stock 16x32 sprites.
        /// For now only consulted by the Sitting sprite config when the NPC is riding.
        /// </summary>
        public SpritePixelPoint AnchorPixel { get; set; }
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
        public object Shearing { get; set; }
        public object Milking { get; set; }
    }

    /// <summary>
    /// Deserializes either a single integer or an integer array into an int[].
    /// </summary>
    internal class FlexibleIntArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(int[]);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Integer)
                return new[] { token.ToObject<int>() };
            if (token.Type == JTokenType.Array)
                return token.ToObject<int[]>();
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is int[] arr && arr.Length == 1)
                serializer.Serialize(writer, arr[0]);
            else
                serializer.Serialize(writer, value);
        }
    }
}
