using StardewValley;

namespace TheStardewSquad.Abstractions.Character
{
    /// <summary>
    /// Abstraction for NPC dialogue-related operations.
    /// Enables testing without requiring real NPC instances.
    /// </summary>
    public interface INpcDialogueService
    {
        /// <summary>
        /// Gets the player who is married to this NPC, if any.
        /// </summary>
        /// <param name="npc">The NPC to check</param>
        /// <returns>The spouse Farmer, or null if not married to player</returns>
        Farmer GetSpouse(NPC npc);

        /// <summary>
        /// Gets the term of endearment the NPC uses for their spouse.
        /// </summary>
        /// <param name="npc">The NPC</param>
        /// <returns>Term of endearment string (e.g., "dear", "honey")</returns>
        string GetTermOfSpousalEndearment(NPC npc);

        /// <summary>
        /// Shows a text bubble above the NPC's head.
        /// </summary>
        /// <param name="npc">The NPC to show text above</param>
        /// <param name="text">The text to display</param>
        void ShowTextAboveHead(NPC npc, string text);
    }
}
