namespace TheStardewSquad.Framework.NpcConfig.Models
{
    /// <summary>
    /// Root configuration data for an NPC or the Generic defaults.
    /// Contains sprite, dialogue, and behavior configurations.
    /// </summary>
    public class NpcConfigData
    {
        /// <summary>
        /// Optional NPC type specification for non-standard NPCs (e.g., "Cat", "Dog").
        /// Only used for special NPC keys like "All_Cat", "Dog2", etc.
        /// Default: null (standard NPC)
        /// </summary>
        public string NpcType { get; set; }

        /// <summary>
        /// Sprite animation configuration for various tasks.
        /// STUB: Not yet implemented - reserved for future sprite system.
        /// </summary>
        public SpriteConfig Sprites { get; set; }

        /// <summary>
        /// Dialogue configuration for various tasks and interactions.
        /// Supports priority-based arrays with conditions (pool-all-matches logic).
        /// </summary>
        public DialogueConfig Dialogue { get; set; }

        /// <summary>
        /// Behavior configuration including idle animations, task restrictions, and recruitment.
        /// Note: Generic defaults cannot have Behavior configuration.
        /// </summary>
        public BehaviorConfig Behavior { get; set; }
    }
}
