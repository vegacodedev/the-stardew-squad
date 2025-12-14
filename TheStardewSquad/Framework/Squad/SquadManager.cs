using StardewValley;

namespace TheStardewSquad.Framework.Squad
{
    /// <summary>Manages the state of all recruited squad members.</summary>
    public class SquadManager
    {
        private readonly List<ISquadMate> _squad = new();

        public IEnumerable<ISquadMate> Members => _squad;

        public int Count => _squad.Count;

        public bool IsRecruited(Character character) => _squad.Any(m => m.Npc.Name == character.Name);

        public ISquadMate GetMember(Character character)
        {
            return _squad.FirstOrDefault(m => m.Npc.Name == character.Name);
        }

        public void Add(ISquadMate mate)
        {
            if (IsRecruited(mate.Npc))
            {
                return;
            }
            _squad.Add(mate);
        }

        public void Remove(Character character)
        {
            _squad.RemoveAll(m => m.Npc.Name == character.Name);
        }

        public void Clear()
        {
            _squad.Clear();
        }
    }
}