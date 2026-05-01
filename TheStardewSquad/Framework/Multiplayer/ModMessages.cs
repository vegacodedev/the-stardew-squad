using Microsoft.Xna.Framework;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Multiplayer
{
    /// <summary>
    /// Mod-message record types for the multiplayer runtime. Every record carries an
    /// <c>int Version</c> so peers running mismatched mod versions can detect and warn.
    /// </summary>
    /// <remarks>
    /// Authority model: the host runs all squad mutations. Farmhands send <c>*Request</c>
    /// records; the host replies with the matching <c>*Result</c> record. The host
    /// broadcasts <see cref="SquadSnapshot"/> after every authoritative mutation, on
    /// <c>SaveLoaded</c>, and on <c>PeerConnected</c>; farmhands rehydrate their local
    /// <see cref="SquadManager"/> from it.
    /// </remarks>
    public record RecruitRequest(int Version, long RequesterId, string NpcName, string LocationName);

    public record RecruitResult(int Version, long RequesterId, string NpcName, bool Success, string ReasonKey);

    public record DismissRequest(int Version, long RequesterId, string NpcName);

    public record DismissResult(int Version, long RequesterId, string NpcName, bool Success, string ReasonKey);

    /// <summary>
    /// Farmhand-issued manual task assignment. Contains only tile + location; the host
    /// re-runs task-type detection at handler time so farmhand-side state desync can't
    /// produce a stale type.
    /// </summary>
    public record TaskAssignRequest(int Version, long RequesterId, Vector2 Tile, string LocationName);

    public record TaskAssignResult(int Version, long RequesterId, bool Success, string ReasonKey);

    /// <summary>
    /// Farmhand client tells the host "I just did a mimicking-relevant action" so the
    /// host can set timers on the matching mates (filtered by recruiter id + location).
    /// </summary>
    public record MimickingRequest(int Version, long FarmerId, TaskType Type, string LocationName);

    /// <summary>
    /// Sent by a farmhand client whose local screen just exited a cutscene. The host
    /// re-warps that farmer's mates to them. Each peer detects its own cutscene end via
    /// a <c>PerScreen&lt;bool&gt;</c>; the host detects locally and warps directly.
    /// </summary>
    public record CutsceneEnded(int Version, long FarmerId);

    /// <summary>One mate's serialized state inside a <see cref="SquadSnapshot"/>.</summary>
    public record SquadEntry(string NpcName, long RecruiterId, string LocationName, TaskType? CurrentTask);

    /// <summary>
    /// Authoritative full-squad snapshot sent by the host. Farmhands clear their local
    /// squad and rebuild it from these entries. Sent on every authoritative mutation.
    /// </summary>
    public record SquadSnapshot(int Version, SquadEntry[] Entries);

    /// <summary>Shared message-versioning + type-name constants.</summary>
    public static class MessageVersion
    {
        public const int Current = 1;
    }
}
