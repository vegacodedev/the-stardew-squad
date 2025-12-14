using StardewValley;

namespace TheStardewSquad.Framework.Squad
{
    /// <summary>Manages the state of NPCs that are waiting (not actively in the squad but under player control).</summary>
    public class WaitingNpcsManager
    {
        private readonly List<ISquadMate> _waitingNpcs = new();

        public IEnumerable<ISquadMate> WaitingMembers => _waitingNpcs;

        public int Count => _waitingNpcs.Count;

        public bool IsWaiting(Character character) => _waitingNpcs.Any(m => m.Npc.Name == character.Name);

        public ISquadMate GetWaitingMember(Character character)
        {
            return _waitingNpcs.FirstOrDefault(m => m.Npc.Name == character.Name);
        }

        public void Add(ISquadMate mate)
        {
            if (IsWaiting(mate.Npc))
            {
                return;
            }
            _waitingNpcs.Add(mate);
        }

        public void Remove(Character character)
        {
            _waitingNpcs.RemoveAll(m => m.Npc.Name == character.Name);
        }

        public void Clear()
        {
            _waitingNpcs.Clear();
        }
    }
}
