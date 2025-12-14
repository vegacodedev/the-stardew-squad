using StardewValley;

namespace TheStardewSquad.Abstractions.Character
{
    /// <summary>
    /// Abstraction for managing NPC state changes during recruitment and dismissal.
    /// Enables testing of RecruitmentManager without requiring real NPC objects.
    /// </summary>
    public interface ISquadMateStateHelper
    {
        /// <summary>
        /// Prepares an NPC for recruitment by resetting visual state and clearing animations.
        /// </summary>
        void PrepareForRecruitment(NPC npc);

        /// <summary>
        /// Prepares an NPC for dismissal by resetting state and clearing tasks.
        /// </summary>
        void PrepareForDismissal(NPC npc);
    }
}
