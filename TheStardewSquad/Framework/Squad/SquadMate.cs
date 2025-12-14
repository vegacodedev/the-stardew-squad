using Microsoft.Xna.Framework;
using StardewValley;
using TheStardewSquad.Framework.Behaviors;

namespace TheStardewSquad.Framework.Squad
{
    /// <summary>Encapsulates a character who has been recruited into the squad, along with all their states and behaviors.</summary>
    public class SquadMate : ISquadMate
    {
        // Injected behaviors
        private readonly ITaskBehavior _taskBehavior;
        private readonly IInteractionBehavior _interactionBehavior;
        private readonly ICommunicationBehavior _communicationBehavior;

        // ISquadMate Properties
        public NPC Npc { get; }
        public string Name => this.Npc.displayName;

        public bool IsAnimating { get; set; }
        public SquadTask Task { get; set; }
        public Stack<Point> Path { get; set; }
        public int ActionCooldown { get; set; }
        public int StuckCounter { get; set; }
        public bool IsCatchingUp { get; set; }
        public int CurrentMoveDirection { get; set; }
        public int FormationSlotIndex { get; set; }
        public Vector2? ClaimedInteractionSpot { get; set; }
        public int IdleTicks { get; set; }
        public int MimickingTaskTimer { get; set; }
        public TaskType? MimickingTaskType { get; set; }
        public int LastCommunicationTick { get; set; }
        public int FramesSinceTaskCleared { get; set; }
        public Point? LastMonsterTile { get; set; }
        public int? WaitingFacingDirection { get; set; }
        public bool IsInPool { get; set; }
        public Point? LastTilePoint { get; set; }
        public bool WasWearingIslandAttireBeforeSwimming { get; set; }
        public string? OriginalTexture { get; set; }

        public SquadMate(NPC npc, ITaskBehavior taskBehavior, IInteractionBehavior interactionBehavior, ICommunicationBehavior communicationBehavior)
        {
            this.Npc = npc;
            this._taskBehavior = taskBehavior;
            this._interactionBehavior = interactionBehavior;
            this._communicationBehavior = communicationBehavior;

            this.IsAnimating = false;
            this.Path = new Stack<Point>();
            this.ActionCooldown = 0;
            this.StuckCounter = 0;
            this.CurrentMoveDirection = -1;
            this.FormationSlotIndex = -1;
            this.ClaimedInteractionSpot = null;
            this.IdleTicks = 0;
            this.MimickingTaskTimer = 0;
            this.MimickingTaskType = null;
            this.LastCommunicationTick = 0;
            this.FramesSinceTaskCleared = 999; // Large value = not recently cleared
            this.LastMonsterTile = null;
            this.IsInPool = false;
            this.LastTilePoint = null;
            this.WasWearingIslandAttireBeforeSwimming = false;
            this.OriginalTexture = null;
        }

        // Delegated Behavior Methods
        public bool CanPerformTask(TaskType type) => _taskBehavior.CanPerformTask(this, type);
        public bool ExecuteTask() => _taskBehavior.ExecuteTask(this);
        public void HandleRecruitment(Farmer interactor) => _interactionBehavior.HandleRecruitment(this, interactor);
        public void HandleDismissal(bool isSilent, DismissalWarpBehavior dismissalWarpBehavior) => _interactionBehavior.HandleDismissal(this, isSilent, dismissalWarpBehavior);
        public void HandleManagement() => _interactionBehavior.HandleManagement(this);
        public void Communicate(string dialogueKey) => _communicationBehavior.Communicate(this, dialogueKey);
        
        // Helper Methods
        public bool HasTask() => this.Task != null;
        public bool IsOnCooldown() => this.ActionCooldown > 0;
        
        public bool DecrementCooldown()
        {
            if (this.ActionCooldown > 0)
            {
                this.ActionCooldown--;
                if (this.ActionCooldown == 0)
                {
                    return true; // Cooldown has just finished.
                }
            }
            return false;
        }

        public void Halt()
        {
            this.IsAnimating = false;
            Npc.Halt();
        }
    }
}
