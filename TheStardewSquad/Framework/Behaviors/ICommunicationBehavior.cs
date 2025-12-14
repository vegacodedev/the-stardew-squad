using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Behaviors
{
    public interface ICommunicationBehavior
    {
        void Communicate(ISquadMate mate, string dialogueKey);
    }
}