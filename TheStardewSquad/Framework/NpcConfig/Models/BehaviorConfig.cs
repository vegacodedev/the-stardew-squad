using System.Collections.Generic;

namespace TheStardewSquad.Framework.NpcConfig.Models
{
    /// <summary>
    /// Conditional idle animation entry with Game State Query support.
    /// </summary>
    public class ConditionalIdleAnimationEntry
    {
        /// <summary>
        /// Game State Query condition for when these animations should be available.
        /// If null, animations are always available (default/fallback).
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Array of animation IDs or animation objects.
        /// For pooled idle animations, all matching conditions are combined.
        /// </summary>
        public List<object> Animations { get; set; }
    }

    /// <summary>
    /// Individual idle animation specification.
    /// </summary>
    public class IdleAnimationSpec
    {
        /// <summary>
        /// Animation ID from Data/animationDescriptions.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Whether the animation should loop infinitely.
        /// Default: true
        /// </summary>
        public bool Loop { get; set; } = true;
    }

    /// <summary>
    /// Conditional allowed tasks entry (first-match-wins logic).
    /// </summary>
    public class ConditionalAllowedTasksEntry
    {
        /// <summary>
        /// Game State Query condition for when this task list applies.
        /// If null, this is the default task list.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Comma-separated list of allowed task types.
        /// Example: "Foraging, Harvesting, Watering"
        /// </summary>
        public string Tasks { get; set; }
    }

    /// <summary>
    /// Recruitment restrictions and conditions.
    /// </summary>
    public class RecruitmentConfig
    {
        /// <summary>
        /// Whether this NPC can be recruited at all.
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Game State Query condition that must be met for recruitment.
        /// Default: null (no condition, always recruitable if Enabled=true)
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// i18n key for dialogue shown when recruitment is refused.
        /// </summary>
        public string RefusalDialogueKey { get; set; }
    }

    /// <summary>
    /// Behavior configuration for NPCs including idle animations, task restrictions, and recruitment.
    /// </summary>
    public class BehaviorConfig
    {
        /// <summary>
        /// Priority-based array of idle animations.
        /// Pools all matching conditions - can be strings (animation IDs) or objects.
        /// </summary>
        public List<object> IdleAnimations { get; set; }

        /// <summary>
        /// Allowed tasks for this NPC.
        /// Can be a simple string or priority array (first-match-wins).
        /// </summary>
        public object AllowedTasks { get; set; }

        /// <summary>
        /// Recruitment settings for this NPC.
        /// </summary>
        public RecruitmentConfig Recruitment { get; set; }
    }
}
