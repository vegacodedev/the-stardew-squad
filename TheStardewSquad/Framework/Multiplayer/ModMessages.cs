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
    public record RecruitRequest(int Version, long RequesterId, string NpcName, string LocationName, int MaxSquadSize);

    public record RecruitResult(int Version, long RequesterId, string NpcName, bool Success, string ReasonKey);

    public record DismissRequest(int Version, long RequesterId, string NpcName);

    public record DismissResult(int Version, long RequesterId, string NpcName, bool Success, string ReasonKey);

    /// <summary>
    /// Farmhand asks the host to put one of their squad mates into the "waiting" state
    /// (the "Stay here" UI action). Authority + waiting list mutation lives on the host;
    /// the farmhand only receives the resulting <see cref="SquadSnapshot"/> and a
    /// <see cref="WaitResult"/> for HUD feedback.
    /// </summary>
    public record WaitRequest(int Version, long RequesterId, string NpcName);

    public record WaitResult(int Version, long RequesterId, string NpcName, bool Success, string ReasonKey);

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

    /// <summary>
    /// Cosmetic-only broadcast: tells all peers to show a speech bubble above the named
    /// NPC. Vanilla SDV's <c>NPC.showTextAboveHead</c> writes only local protected fields
    /// (no NetField), so the host's call doesn't propagate. Sent host→peers when the
    /// host fires a bubble (e.g., Idle, Fishing_Waiting). No result reply.
    /// </summary>
    public record ShowBubble(int Version, string NpcName, string LocationName, string Text);

    /// <summary>
    /// Cosmetic-only broadcast: tells all peers to play the named idle animation on the
    /// recruited NPC identified by (NpcName, RecruiterId). The host picks the animation
    /// (random + GSQ-conditional pool in <see cref="BehaviorManager.GetRandomIdleAnimation"/>)
    /// and plays it locally; peers replay so their NPC instance shows the same frames.
    /// Vanilla doesn't propagate <c>Sprite.CurrentAnimation</c>, so without this peers see
    /// no idle animations at all (host-only gate in FollowerManager hides the call).
    /// </summary>
    public record PlayIdleAnim(int Version, string NpcName, long RecruiterId, string AnimationId, bool Loop);

    /// <summary>
    /// Cosmetic-only broadcast: tells all peers to clear an in-progress idle animation
    /// on the recruited NPC. Host detects an <c>IsAnimating</c> true→false transition
    /// each tick (any caller of <c>mate.Halt()</c> can produce one — distance check, task
    /// assignment, cooldown finish, etc.) and broadcasts. Without this, looping idles
    /// (Loop=true → ActionCooldown=int.MaxValue) keep cycling on peers forever because
    /// the host's interrupting Halt never propagates.
    /// </summary>
    public record ClearIdleAnim(int Version, string NpcName, long RecruiterId);

    /// <summary>
    /// Cosmetic-only broadcast: tells all peers to apply a task sprite animation on the
    /// recruited NPC identified by (NpcName, RecruiterId). Sprite.CurrentAnimation,
    /// Sprite.currentFrame, and npc.flip are non-netfielded local fields, so without
    /// this peers see no task sprite changes at all (host-only gate hides the call).
    /// AppliedTexturePath carries the asset path when the task swaps the NPC's texture
    /// sheet (Sitting today; null for tasks that don't swap).
    /// </summary>
    public record PlayTaskAnim(int Version, string NpcName, long RecruiterId, string TaskType, int FacingDirection, string? AppliedTexturePath);

    /// <summary>
    /// Cosmetic-only broadcast: tells all peers to clear an in-progress task animation
    /// and restore the NPC's original texture if one was swapped. Sent host->peers when
    /// the host clears a sustained-task mate or completes a task with a custom sprite sheet.
    /// Non-sustained tasks self-terminate via Sprite.loop=false + the configured last-frame
    /// callback, so the host filters in ClearMateTask to avoid clobbering peer animations
    /// before they can render.
    /// </summary>
    public record ClearTaskAnim(int Version, string NpcName, long RecruiterId);

    /// <summary>
    /// Cosmetic-only broadcast: tells all peers to play a Character.doEmote on the
    /// FarmAnimal or Pet at the given tile. Vanilla <c>Character.doEmote</c> writes only
    /// local fields (<c>isEmoting</c>, <c>currentEmote</c>, <c>currentEmoteFrame</c>,
    /// <c>emoteInterval</c>); none are NetField-wrapped, so without this peers never see
    /// the host's emote. Sent host->peers when an NPC pets/milks an animal. Identifies
    /// the target by tile rather than name because farm-animal/pet names can collide.
    /// </summary>
    public record ShowAnimalEmote(int Version, string LocationName, int TileX, int TileY, int EmoteIndex);

    /// <summary>
    /// Cosmetic-only broadcast: tells all peers to wobble the named NPC for the given
    /// duration. Vanilla <c>NPC.shake</c> writes only <c>shakeTimer</c> (a plain int,
    /// not NetField-wrapped); the host's call doesn't propagate. Sent host->peers
    /// after every task action that calls <c>npc.shake(...)</c>.
    /// </summary>
    public record ShakeNpc(int Version, string NpcName, string LocationName, int DurationMs);

    /// <summary>One mate's serialized state inside a <see cref="SquadSnapshot"/>.</summary>
    /// <remarks>
    /// <see cref="InteractionTile"/> and <see cref="TaskTile"/> are populated for sustained tasks
    /// whose render paths key off the tiles (Fishing line draw checks <c>npc.TilePoint == InteractionTile</c>).
    /// Null when no task or for tasks that don't need them.
    /// </remarks>
    public record SquadEntry(string NpcName, long RecruiterId, string LocationName, TaskType? CurrentTask, Point? InteractionTile, Point? TaskTile);

    /// <summary>
    /// Authoritative full-squad snapshot sent by the host. Farmhands clear their local
    /// squad and rebuild it from these entries. Sent on every authoritative mutation.
    /// </summary>
    public record SquadSnapshot(int Version, SquadEntry[] Entries);

    /// <summary>Shared message-versioning + type-name constants.</summary>
    public static class MessageVersion
    {
        public const int Current = 5;
    }
}
