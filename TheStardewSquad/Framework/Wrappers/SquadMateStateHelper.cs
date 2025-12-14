using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Character;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>A helper class to manage the state transitions of squad members.</summary>
    internal class SquadMateStateHelper : ISquadMateStateHelper
    {
        /// <summary>Resets an NPC's state to prepare them for following the player.</summary>
        public void PrepareForRecruitment(NPC npc)
        {
            npc.drawOffset = Vector2.Zero;
            npc.appliedRouteAnimationOffset = Vector2.Zero;

            if (npc.layingDown)
            {
                npc.layingDown = false;
                npc.HideShadow = false;
            }
            npc.isSleeping.Value = false;

            // Clear any active animations or dialogue.
            npc.Sprite.ClearAnimation();
            npc.clearTextAboveHead();

            // Reset sprite size to default (fixes bug where NPCs recruited during schedule animations retain enlarged sprites)
            var data = npc.GetData();
            if (data != null)
            {
                // Use character data to get the proper base sprite size
                // This handles regular NPCs (16x32), pets (32x32), and modded NPCs with custom sizes
                npc.Sprite.SpriteWidth = data.Size.X;
                npc.Sprite.SpriteHeight = data.Size.Y;
            }
            else
            {
                // Fallback for NPCs without character data (shouldn't normally happen)
                if (npc is Pet)
                {
                    // Pets use 32x32 sprites
                    npc.Sprite.SpriteWidth = 32;
                    npc.Sprite.SpriteHeight = 32;
                }
                else
                {
                    // Regular NPCs use 16x32 sprites
                    npc.Sprite.SpriteWidth = 16;
                    npc.Sprite.SpriteHeight = 32;
                }
            }

            npc.farmerPassesThrough = true;
            npc.Halt();

            // Clear all movement controllers to prevent stuttering when recruiting moving NPCs
            // - controller: main PathFindController
            // - temporaryController: used for schedule movement (especially spouses leaving FarmHouse)
            // - DirectionsToNewLocation: schedule path directions
            npc.controller = null;
            npc.temporaryController = null;
            npc.DirectionsToNewLocation = null;

            // Disable pacing behavior (schedule points like "square_1_5_3")
            npc.IsWalkingInSquare = false;
        }

        /// <summary>Resets an NPC's state when they are dismissed from the squad.</summary>
        public void PrepareForDismissal(NPC npc)
        {
            npc.Sprite.StopAnimation();
            npc.farmerPassesThrough = false;
            npc.controller = null;
        }

        /// <summary>Continuously ensures the mod retains control over the squad mate during updates.</summary>
        public static void MaintainControl(NPC npc)
        {
            // Prevent vanilla game logic from taking over movement.
            // Must clear all three movement systems:
            // - controller: main PathFindController
            // - temporaryController: schedule movement (spouses leaving FarmHouse)
            // - DirectionsToNewLocation: schedule path directions
            npc.controller = null;
            if (npc.temporaryController != null) npc.temporaryController = null;
            if (npc.DirectionsToNewLocation != null) npc.DirectionsToNewLocation = null;

            // Ensures that, even after cutscenes (which change this property for some reason), recruited NPCs stay passable.
            if (!npc.farmerPassesThrough) npc.farmerPassesThrough = true;

            // Ensure pacing behavior stays disabled during recruitment
            if (npc.IsWalkingInSquare) npc.IsWalkingInSquare = false;

            // For pets, ensure they stay in their passive "recruited" state.
            // The vanilla game constantly tries to make them wander, so this override is crucial.
            if (npc is Pet pet && pet.CurrentBehavior != Pet.behavior_Sleep)
            {
                pet.CurrentBehavior = Pet.behavior_Sleep;
            }
        }
    }
}