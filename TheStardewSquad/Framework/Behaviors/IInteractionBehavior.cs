using StardewValley;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Behaviors
{
    public interface IInteractionBehavior
    {
        void HandleRecruitment(ISquadMate mate, Farmer interactor);
        void HandleManagement(ISquadMate mate);
        void HandleDismissal(ISquadMate mate, bool isSilent, DismissalWarpBehavior dismissalWarpBehavior, bool suppressVisual = false);
    }
    public enum DismissalWarpBehavior
    {
        GoHome,
        RoamHere
    }
}