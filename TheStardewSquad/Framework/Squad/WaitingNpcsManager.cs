using StardewValley;

namespace TheStardewSquad.Framework.Squad
{
    /// <summary>Manages the state of NPCs that are waiting (not actively in the squad but under player control).</summary>
    /// <remarks>
    /// Identity is keyed by the tuple <c>(NPC.Name, RecruiterUniqueId)</c> so two farmhands
    /// can each have a distinct waiting NPC of the same vanilla name.
    /// </remarks>
    public class WaitingNpcsManager
    {
        private readonly List<ISquadMate> _waitingNpcs = new();

        public IEnumerable<ISquadMate> WaitingMembers => _waitingNpcs;

        public int Count => _waitingNpcs.Count;

        /// <summary>True if any recruiter has a waiting mate with this character's name.</summary>
        public bool IsWaiting(Character character) => _waitingNpcs.Any(m => m.Npc.Name == character.Name);

        /// <summary>True if the specific (name, recruiterId) tuple is in the waiting list.</summary>
        public bool IsWaiting(NPC npc, long recruiterId) =>
            _waitingNpcs.Any(m => m.Npc.Name == npc.Name && m.RecruiterUniqueId == recruiterId);

        /// <summary>Returns the first waiting mate matching this character's name (any recruiter).</summary>
        public ISquadMate GetWaitingMember(Character character)
        {
            return _waitingNpcs.FirstOrDefault(m => m.Npc.Name == character.Name);
        }

        /// <summary>Returns the waiting mate matching the specific (name, recruiterId) tuple, or null.</summary>
        public ISquadMate? GetWaitingMember(NPC npc, long recruiterId)
        {
            return _waitingNpcs.FirstOrDefault(m => m.Npc.Name == npc.Name && m.RecruiterUniqueId == recruiterId);
        }

        public void Add(ISquadMate mate)
        {
            if (IsWaiting(mate.Npc, mate.RecruiterUniqueId))
            {
                return;
            }
            _waitingNpcs.Add(mate);
        }

        /// <summary>Removes all waiting mates with this character's name (any recruiter).</summary>
        public void Remove(Character character)
        {
            _waitingNpcs.RemoveAll(m => m.Npc.Name == character.Name);
        }

        /// <summary>Removes only the waiting mate matching the specific (name, recruiterId) tuple.</summary>
        public void Remove(NPC npc, long recruiterId)
        {
            _waitingNpcs.RemoveAll(m => m.Npc.Name == npc.Name && m.RecruiterUniqueId == recruiterId);
        }

        public void Clear()
        {
            _waitingNpcs.Clear();
        }
    }
}
