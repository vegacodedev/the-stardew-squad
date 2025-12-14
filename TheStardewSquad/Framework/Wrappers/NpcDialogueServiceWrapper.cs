using StardewValley;
using TheStardewSquad.Abstractions.Character;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of INpcDialogueService that calls NPC methods directly.
    /// </summary>
    public class NpcDialogueServiceWrapper : INpcDialogueService
    {
        public Farmer GetSpouse(NPC npc)
        {
            return npc.getSpouse();
        }

        public string GetTermOfSpousalEndearment(NPC npc)
        {
            return npc.getTermOfSpousalEndearment();
        }

        public void ShowTextAboveHead(NPC npc, string text)
        {
            npc.showTextAboveHead(text);
        }
    }
}
