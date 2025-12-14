using StardewValley;
using TheStardewSquad.Abstractions.Character;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of INpcSpriteService that accesses NPC sprite properties directly.
    /// </summary>
    public class NpcSpriteServiceWrapper : INpcSpriteService
    {
        public int GetFacingDirection(NPC npc)
        {
            return npc.FacingDirection;
        }

        public void SetCurrentAnimation(NPC npc, List<FarmerSprite.AnimationFrame> frames)
        {
            npc.Sprite.setCurrentAnimation(frames);
        }
    }
}
