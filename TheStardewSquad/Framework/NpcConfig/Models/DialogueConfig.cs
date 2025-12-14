using System.Collections.Generic;

namespace TheStardewSquad.Framework.NpcConfig.Models
{
    /// <summary>
    /// Configuration for NPC dialogue with conditional support.
    /// Can be either a conditional entry with specific lines or a simple string.
    /// </summary>
    public class DialogueConfigEntry
    {
        /// <summary>
        /// Game State Query condition for when these dialogue lines should be available.
        /// If null, lines are always available (default/fallback).
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Array of i18n dialogue keys to use when condition matches.
        /// For pooled dialogue types (most), all matching conditions are combined.
        /// </summary>
        public List<string> Lines { get; set; }
    }

    /// <summary>
    /// Collection of dialogue for various task types and interactions.
    /// Each task type can have priority-based arrays where all matching conditions are pooled.
    /// </summary>
    public class DialogueConfig
    {
        public List<object> Recruit { get; set; }
        public List<object> Dismiss { get; set; }
        public List<object> Idle { get; set; }
        public List<object> Attacking { get; set; }
        public List<object> Mining { get; set; }
        public List<object> Fishing { get; set; }
        public List<object> Watering { get; set; }
        public List<object> Lumbering { get; set; }
        public List<object> Harvesting { get; set; }
        public List<object> Foraging { get; set; }
        public List<object> Petting { get; set; }
        public List<object> FriendshipTooLow { get; set; }
        public List<object> RecruitmentRefusal { get; set; }
    }
}
