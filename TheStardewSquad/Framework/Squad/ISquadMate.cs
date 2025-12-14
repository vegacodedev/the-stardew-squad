using Microsoft.Xna.Framework;
using StardewValley;
using TheStardewSquad.Framework.Behaviors;

namespace TheStardewSquad.Framework.Squad
{
    public interface ISquadMate
    {
        /// <summary>The underlying game character (e.g., NPC, Pet).</summary>
        NPC Npc { get; }
        
        /// <summary>The unique name of the character, used for internal tracking.</summary>
        string Name { get; }

        // State
        bool IsAnimating { get; set; }
        SquadTask Task { get; set; }
        Stack<Point> Path { get; set; }
        int ActionCooldown { get; set; }
        int StuckCounter { get; set; }
        bool IsCatchingUp { get; set; }
        int CurrentMoveDirection { get; set; }
        int FormationSlotIndex { get; set; }
        Vector2? ClaimedInteractionSpot { get; set; }
        int IdleTicks { get; set; }
        int MimickingTaskTimer { get; set; }
        TaskType? MimickingTaskType { get; set; }
        int LastCommunicationTick { get; set; }
        int FramesSinceTaskCleared { get; set; }
        Point? LastMonsterTile { get; set; }
        int? WaitingFacingDirection { get; set; }
        bool IsInPool { get; set; }
        Point? LastTilePoint { get; set; }
        bool WasWearingIslandAttireBeforeSwimming { get; set; }

        /// <summary>
        /// Stores the original texture asset path before a task sprite sheet was applied.
        /// Null if no custom sprite sheet is currently active.
        /// Used for any task that swaps the NPC's sprite sheet (Sitting, etc.).
        /// </summary>
        string? OriginalTexture { get; set; }

        // Behavior Methods
        bool CanPerformTask(TaskType type);
        bool ExecuteTask();
        void HandleRecruitment(Farmer interactor);
        void HandleDismissal(bool isSilent, DismissalWarpBehavior dismissalWarpBehavior);
        void HandleManagement();
        void Communicate(string dialogueKey);

        // Helper Methods
        bool HasTask();
        bool IsOnCooldown();
        bool DecrementCooldown();
        void Halt();
    }
}
