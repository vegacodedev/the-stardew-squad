using StardewValley;

namespace TheStardewSquad.Framework.Squad
{
    /// <summary>Manages the state of all recruited squad members.</summary>
    /// <remarks>
    /// Identity is keyed by the tuple <c>(NPC.Name, RecruiterUniqueId)</c> so two farmhands
    /// can each recruit different copies of the same vanilla NPC in multiplayer (rare with
    /// vanilla NPCs but real with mods that spawn multiple instances).
    /// </remarks>
    public class SquadManager
    {
        private readonly List<ISquadMate> _squad = new();

        public IEnumerable<ISquadMate> Members => _squad;

        public int Count => _squad.Count;

        /// <summary>True if any recruiter has a mate with this character's name.</summary>
        public bool IsRecruited(Character character) => _squad.Any(m => m.Npc.Name == character.Name);

        /// <summary>True if the specific (name, recruiterId) tuple is in the squad.</summary>
        public bool IsRecruited(NPC npc, long recruiterId) =>
            _squad.Any(m => m.Npc.Name == npc.Name && m.RecruiterUniqueId == recruiterId);

        /// <summary>True if the specific (name, recruiterId) tuple is in the squad.</summary>
        public bool IsRecruited(string name, long recruiterId) =>
            _squad.Any(m => m.Npc.Name == name && m.RecruiterUniqueId == recruiterId);

        /// <summary>Returns the first mate matching this character's name (any recruiter).</summary>
        public ISquadMate GetMember(Character character)
        {
            return _squad.FirstOrDefault(m => m.Npc.Name == character.Name);
        }

        /// <summary>Returns the mate matching the specific (name, recruiterId) tuple, or null.</summary>
        public ISquadMate? GetMember(NPC npc, long recruiterId)
        {
            return _squad.FirstOrDefault(m => m.Npc.Name == npc.Name && m.RecruiterUniqueId == recruiterId);
        }

        /// <summary>
        /// Adds a mate. Dedup is by <c>(Name, RecruiterUniqueId)</c> tuple — the same NPC name
        /// can appear multiple times under different recruiters.
        /// </summary>
        public void Add(ISquadMate mate)
        {
            if (IsRecruited(mate.Npc, mate.RecruiterUniqueId))
            {
                return;
            }
            _squad.Add(mate);
        }

        /// <summary>Removes all mates with this character's name (any recruiter).</summary>
        public void Remove(Character character)
        {
            _squad.RemoveAll(m => m.Npc.Name == character.Name);
        }

        /// <summary>Removes only the mate matching the specific (name, recruiterId) tuple.</summary>
        public void Remove(NPC npc, long recruiterId)
        {
            _squad.RemoveAll(m => m.Npc.Name == npc.Name && m.RecruiterUniqueId == recruiterId);
        }

        public void Clear()
        {
            _squad.Clear();
        }
    }
}
