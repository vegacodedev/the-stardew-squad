using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Multiplayer
{
    /// <summary>
    /// Routes multiplayer mod messages between host and farmhands. The host queues
    /// incoming requests in <see cref="_inbox"/> and drains them in
    /// <see cref="OnUpdateTicked"/>; farmhands handle <c>*Result</c> messages and
    /// <see cref="SquadSnapshot"/> rehydration immediately on receive.
    /// </summary>
    public class MessageDispatcher
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly ModConfig _config;
        private readonly SquadManager _squad;
        private readonly WaitingNpcsManager _waiting;
        private readonly RecruitmentManager _recruitment;
        private readonly FollowerManager _follower;
        private readonly InteractionManager _interaction;
        private readonly SquadMateFactory _mateFactory;
        private readonly Abstractions.Character.ISquadMateStateHelper _stateHelper;
        private readonly string _modUniqueId;

        private readonly ConcurrentQueue<QueuedMessage> _inbox = new();

        public MessageDispatcher(
            IModHelper helper,
            IMonitor monitor,
            ModConfig config,
            SquadManager squad,
            WaitingNpcsManager waiting,
            RecruitmentManager recruitment,
            FollowerManager follower,
            InteractionManager interaction,
            SquadMateFactory mateFactory,
            Abstractions.Character.ISquadMateStateHelper stateHelper,
            string modUniqueId)
        {
            this._helper = helper;
            this._monitor = monitor;
            this._config = config;
            this._squad = squad;
            this._waiting = waiting;
            this._recruitment = recruitment;
            this._follower = follower;
            this._interaction = interaction;
            this._mateFactory = mateFactory;
            this._stateHelper = stateHelper;
            this._modUniqueId = modUniqueId;
        }

        // === Event handlers wired by ModEntry ===

        /// <summary>
        /// Drains the host inbox each tick. Farmhand peers always early-return — they
        /// handle <c>*Result</c> messages and snapshots inline in <see cref="OnModMessageReceived"/>.
        /// </summary>
        public void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsMainPlayer) return;
            while (_inbox.TryDequeue(out var msg))
                DispatchHostHandler(msg);
        }

        public void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != _modUniqueId) return;

            switch (e.Type)
            {
                case nameof(RecruitRequest):
                    if (Context.IsMainPlayer)
                        _inbox.Enqueue(new QueuedMessage(e.Type, e.ReadAs<RecruitRequest>(), e.FromPlayerID));
                    break;
                case nameof(RecruitResult):
                    if (!Context.IsMainPlayer)
                        HandleRecruitResult(e.ReadAs<RecruitResult>());
                    break;
                case nameof(DismissRequest):
                    if (Context.IsMainPlayer)
                        _inbox.Enqueue(new QueuedMessage(e.Type, e.ReadAs<DismissRequest>(), e.FromPlayerID));
                    break;
                case nameof(DismissResult):
                    if (!Context.IsMainPlayer)
                        HandleDismissResult(e.ReadAs<DismissResult>());
                    break;
                case nameof(WaitRequest):
                    if (Context.IsMainPlayer)
                        _inbox.Enqueue(new QueuedMessage(e.Type, e.ReadAs<WaitRequest>(), e.FromPlayerID));
                    break;
                case nameof(WaitResult):
                    if (!Context.IsMainPlayer)
                        HandleWaitResult(e.ReadAs<WaitResult>());
                    break;
                case nameof(TaskAssignRequest):
                    if (Context.IsMainPlayer)
                        _inbox.Enqueue(new QueuedMessage(e.Type, e.ReadAs<TaskAssignRequest>(), e.FromPlayerID));
                    break;
                case nameof(TaskAssignResult):
                    if (!Context.IsMainPlayer)
                        HandleTaskAssignResult(e.ReadAs<TaskAssignResult>());
                    break;
                case nameof(MimickingRequest):
                    if (Context.IsMainPlayer)
                        _inbox.Enqueue(new QueuedMessage(e.Type, e.ReadAs<MimickingRequest>(), e.FromPlayerID));
                    break;
                case nameof(CutsceneEnded):
                    if (Context.IsMainPlayer)
                        _inbox.Enqueue(new QueuedMessage(e.Type, e.ReadAs<CutsceneEnded>(), e.FromPlayerID));
                    break;
                case nameof(SquadSnapshot):
                    if (!Context.IsMainPlayer)
                        ApplySnapshot(e.ReadAs<SquadSnapshot>());
                    break;
                case nameof(ShowBubble):
                    OnShowBubble(e.ReadAs<ShowBubble>());
                    break;
            }
        }

        /// <summary>
        /// Cosmetic bubble receiver. Renders the speech bubble locally on this peer's
        /// NPC instance because <c>NPC.showTextAboveHead</c> writes only local fields.
        /// Silently drops mismatched-version messages (bubbles are non-load-bearing).
        /// </summary>
        private void OnShowBubble(ShowBubble msg)
        {
            if (msg.Version != MessageVersion.Current) return;
            var loc = Game1.getLocationFromName(msg.LocationName);
            NPC? npc = loc?.characters.FirstOrDefault(c => c.Name == msg.NpcName);
            npc?.showTextAboveHead(msg.Text);
        }

        public void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
        {
            if (!Context.IsMainPlayer) return;
            BroadcastSnapshot(toPeerId: e.Peer.PlayerID);
        }

        // === Host-side dispatch ===

        private void DispatchHostHandler(QueuedMessage msg)
        {
            switch (msg.Type)
            {
                case nameof(RecruitRequest):
                    OnRecruitRequest((RecruitRequest)msg.Payload);
                    break;
                case nameof(DismissRequest):
                    OnDismissRequest((DismissRequest)msg.Payload);
                    break;
                case nameof(WaitRequest):
                    OnWaitRequest((WaitRequest)msg.Payload);
                    break;
                case nameof(TaskAssignRequest):
                    OnTaskAssignRequest((TaskAssignRequest)msg.Payload);
                    break;
                case nameof(MimickingRequest):
                    OnMimickingRequest((MimickingRequest)msg.Payload);
                    break;
                case nameof(CutsceneEnded):
                    OnCutsceneEnded((CutsceneEnded)msg.Payload);
                    break;
            }
        }

        /// <summary>
        /// Race-safe recruitment handler. Re-checks everything on the host (festival,
        /// already-recruited, friendship, squad-size) - first request to a given NPC on a
        /// given tick wins, subsequent ones reply with a specific reason key for the
        /// farmhand's HUD toast.
        /// </summary>
        private void OnRecruitRequest(RecruitRequest req)
        {
            if (req.Version != MessageVersion.Current)
            {
                _monitor.Log($"RecruitRequest version mismatch from {req.RequesterId}: got {req.Version}, expected {MessageVersion.Current}.", LogLevel.Warn);
                SendRecruitResult(req.RequesterId, req.NpcName, false, "version_mismatch");
                return;
            }

            Farmer? requester = Game1.getOnlineFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == req.RequesterId);
            if (requester == null)
            {
                SendRecruitResult(req.RequesterId, req.NpcName, false, "requester_offline");
                return;
            }

            var loc = Game1.getLocationFromName(req.LocationName);
            NPC? npc = loc?.characters.FirstOrDefault(c => c.Name == req.NpcName);
            if (npc == null)
            {
                SendRecruitResult(req.RequesterId, req.NpcName, false, "npc_not_found");
                return;
            }

            if (Game1.isFestival())
            {
                SendRecruitResult(req.RequesterId, req.NpcName, false, "festival_active");
                return;
            }

            if (_squad.IsRecruited(npc))
            {
                SendRecruitResult(req.RequesterId, req.NpcName, false, "already_recruited");
                return;
            }

            // Friendship gate (skip for pets and spouses; honor RecruitAllNpcs).
            if (npc is not Pet && !npc.isMarried() && !_config.RecruitAllNpcs
                && requester.getFriendshipHeartLevelForNPC(npc.Name) < _config.FriendshipRequirement)
            {
                SendRecruitResult(req.RequesterId, req.NpcName, false, "friendship_too_low");
                return;
            }

            // Per-recruiter cap check uses the FARMHAND's MaxSquadSize from the request
            // (each player's own config governs their cap). Defensive minimum of 1 in case
            // a malformed request arrives with a zero/negative value.
            int requesterCap = Math.Max(1, req.MaxSquadSize);
            int recruiterCount = _squad.Members.Count(m => m.RecruiterUniqueId == req.RequesterId);
            if (recruiterCount >= requesterCap)
            {
                SendRecruitResult(req.RequesterId, req.NpcName, false, "squadFull");
                return;
            }

            ISquadMate? mate = _mateFactory.Create(npc);
            if (mate == null)
            {
                SendRecruitResult(req.RequesterId, req.NpcName, false, "factory_failed");
                return;
            }

            // Distinguish a fresh recruit ("ok") from resuming a waiting mate
            // ("resumed_from_waiting") so the farmhand can decide whether to play the
            // recruit dialogue. Detected before calling Recruit since Recruit clears the
            // waiting state internally.
            bool isResume = _waiting.IsWaiting(npc);

            // Recruit broadcasts a SquadSnapshot itself on success, so we don't duplicate here.
            _recruitment.Recruit(mate, requester);
            SendRecruitResult(req.RequesterId, req.NpcName, true, isResume ? "resumed_from_waiting" : "ok");
        }

        private void SendRecruitResult(long toPlayerId, string npcName, bool success, string reasonKey)
        {
            _helper.Multiplayer.SendMessage(
                new RecruitResult(MessageVersion.Current, toPlayerId, npcName, success, reasonKey),
                nameof(RecruitResult),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { toPlayerId });
        }

        /// <summary>
        /// Race-safe dismissal handler. Resolves the mate by (NpcName, RequesterId) so a
        /// farmhand can only dismiss their own recruits, never another player's.
        /// </summary>
        private void OnDismissRequest(DismissRequest req)
        {
            if (req.Version != MessageVersion.Current)
            {
                _monitor.Log($"DismissRequest version mismatch from {req.RequesterId}: got {req.Version}, expected {MessageVersion.Current}.", LogLevel.Warn);
                SendDismissResult(req.RequesterId, req.NpcName, false, "version_mismatch");
                return;
            }

            ISquadMate? mate = _squad.Members.FirstOrDefault(m =>
                m.Npc.Name == req.NpcName && m.RecruiterUniqueId == req.RequesterId);

            if (mate == null)
            {
                SendDismissResult(req.RequesterId, req.NpcName, false, "not_recruited_by_you");
                return;
            }

            // Dismiss broadcasts a SquadSnapshot itself, so we don't duplicate here.
            _recruitment.Dismiss(mate, requesterId: req.RequesterId);
            SendDismissResult(req.RequesterId, req.NpcName, true, "ok");
        }

        /// <summary>
        /// Race-safe wait handler. Resolves the mate by (NpcName, RequesterId) so a farmhand
        /// can only wait their own recruits. SetWaiting broadcasts a SquadSnapshot itself,
        /// so the farmhand's local SquadManager / WaitingNpcsManager will sync via that.
        /// </summary>
        private void OnWaitRequest(WaitRequest req)
        {
            if (req.Version != MessageVersion.Current)
            {
                _monitor.Log($"WaitRequest version mismatch from {req.RequesterId}: got {req.Version}, expected {MessageVersion.Current}.", LogLevel.Warn);
                SendWaitResult(req.RequesterId, req.NpcName, false, "version_mismatch");
                return;
            }

            ISquadMate? mate = _squad.Members.FirstOrDefault(m =>
                m.Npc.Name == req.NpcName && m.RecruiterUniqueId == req.RequesterId);

            if (mate == null)
            {
                SendWaitResult(req.RequesterId, req.NpcName, false, "not_recruited_by_you");
                return;
            }

            // SetWaiting broadcasts a SquadSnapshot itself. Pass isSilent=true so the
            // host's local "X is now waiting" global message doesn't fire here.
            // The HUD message is shown on the farmhand via HandleWaitResult.
            _recruitment.SetWaiting(mate, isSilent: true);
            SendWaitResult(req.RequesterId, req.NpcName, true, "ok");
        }

        /// <summary>
        /// Manual-task assignment handler. Re-runs task detection in the requester's
        /// world view (so farmhand-side desync can't produce a stale type) and assigns
        /// the closest capable mate that the requester recruited.
        /// </summary>
        private void OnTaskAssignRequest(TaskAssignRequest req)
        {
            if (req.Version != MessageVersion.Current)
            {
                _monitor.Log($"TaskAssignRequest version mismatch from {req.RequesterId}: got {req.Version}, expected {MessageVersion.Current}.", LogLevel.Warn);
                SendTaskAssignResult(req.RequesterId, false, "version_mismatch");
                return;
            }

            Farmer? requester = Game1.getOnlineFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == req.RequesterId);
            if (requester == null)
            {
                SendTaskAssignResult(req.RequesterId, false, "requester_offline");
                return;
            }

            // Re-validate that the requester is actually in the location they claimed.
            // Trust the host's view of requester.currentLocation as authoritative.
            int hasMatesHere = _squad.Members.Count(m =>
                m.RecruiterUniqueId == requester.UniqueMultiplayerID
                && m.Npc.currentLocation == requester.currentLocation);
            if (hasMatesHere == 0)
            {
                SendTaskAssignResult(req.RequesterId, false, "no_mates_for_task");
                return;
            }

            // Manual-task assignment changes a mate's Task but not squad membership; task state
            // is not tracked in SquadSnapshot for any realtime-meaningful purpose, and netfields
            // keep the mate's animation/position in sync. Broadcasting here would be expensive
            // churn during heavy task usage.
            var tilePoint = new Microsoft.Xna.Framework.Point((int)req.Tile.X, (int)req.Tile.Y);
            _interaction.AssignManualTaskFor(requester, tilePoint);
            SendTaskAssignResult(req.RequesterId, true, "ok");
        }

        private void SendDismissResult(long toPlayerId, string npcName, bool success, string reasonKey)
        {
            _helper.Multiplayer.SendMessage(
                new DismissResult(MessageVersion.Current, toPlayerId, npcName, success, reasonKey),
                nameof(DismissResult),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { toPlayerId });
        }

        private void SendWaitResult(long toPlayerId, string npcName, bool success, string reasonKey)
        {
            _helper.Multiplayer.SendMessage(
                new WaitResult(MessageVersion.Current, toPlayerId, npcName, success, reasonKey),
                nameof(WaitResult),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { toPlayerId });
        }

        private void SendTaskAssignResult(long toPlayerId, bool success, string reasonKey)
        {
            _helper.Multiplayer.SendMessage(
                new TaskAssignResult(MessageVersion.Current, toPlayerId, success, reasonKey),
                nameof(TaskAssignResult),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { toPlayerId });
        }

        /// <summary>
        /// Farmhand reports they just performed a mimickable task. Re-route to the
        /// host's <see cref="FollowerManager.OnFarmerAction"/>, which sets timers on
        /// matching mates (filtered by recruiter id + co-location with the farmer).
        /// </summary>
        private void OnMimickingRequest(MimickingRequest req)
        {
            if (req.Version != MessageVersion.Current)
            {
                _monitor.Log($"MimickingRequest version mismatch from {req.FarmerId}: got {req.Version}, expected {MessageVersion.Current}.", LogLevel.Warn);
                return;
            }

            Farmer? farmer = Game1.getOnlineFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == req.FarmerId);
            if (farmer == null) return;

            _follower.OnFarmerAction(farmer, req.Type);
        }

        /// <summary>
        /// Farmhand reports its own local cutscene just ended. The host re-warps the
        /// farmhand's mates to the farmhand's current location via
        /// <see cref="FollowerManager.WarpSquadToFarmer"/>.
        /// </summary>
        private void OnCutsceneEnded(CutsceneEnded msg)
        {
            if (msg.Version != MessageVersion.Current)
            {
                _monitor.Log($"CutsceneEnded version mismatch from {msg.FarmerId}: got {msg.Version}, expected {MessageVersion.Current}.", LogLevel.Warn);
                return;
            }

            Farmer? farmer = Game1.getOnlineFarmers().FirstOrDefault(f => f.UniqueMultiplayerID == msg.FarmerId);
            if (farmer == null) return;

            _follower.WarpSquadToFarmer(farmer);
        }

        // === Farmhand-side result handlers ===

        private void HandleRecruitResult(RecruitResult res)
        {
            if (res.Version != MessageVersion.Current) { ShowVersionToast(); return; }
            if (Game1.player.UniqueMultiplayerID != res.RequesterId) return;
            if (!res.Success)
            {
                Game1.addHUDMessage(new HUDMessage(
                    _helper.Translation.Get($"recruitment.{res.ReasonKey}"),
                    HUDMessage.error_type));
                return;
            }
            // Success: SquadSnapshot has already arrived (sent before this result by the
            // host), so the mate is in the local SquadManager. Play the recruit dialogue
            // for fresh recruits only; resumed-from-waiting mates skip the dialogue but
            // get a "X resumed from waiting" HUD instead (mirrors the host's local
            // Game1.showGlobalMessage in RecruitmentManager.Recruit, which isn't peer-
            // propagated and so wouldn't reach the farmhand without this).
            if (res.ReasonKey == "ok")
            {
                ISquadMate? mate = _squad.Members.FirstOrDefault(m =>
                    m.Npc.Name == res.NpcName && m.RecruiterUniqueId == res.RequesterId);
                mate?.Communicate(DialogueKeys.Recruit);
            }
            else if (res.ReasonKey == "resumed_from_waiting")
            {
                Game1.addHUDMessage(new HUDMessage(
                    _helper.Translation.Get("management.resumedFromWaiting", new { name = res.NpcName }),
                    HUDMessage.newQuest_type));
            }
        }

        private void HandleDismissResult(DismissResult res)
        {
            if (res.Version != MessageVersion.Current) { ShowVersionToast(); return; }
            if (Game1.player.UniqueMultiplayerID != res.RequesterId) return;
            if (!res.Success)
            {
                Game1.addHUDMessage(new HUDMessage(
                    _helper.Translation.Get($"recruitment.{res.ReasonKey}"),
                    HUDMessage.error_type));
            }
        }

        private void HandleWaitResult(WaitResult res)
        {
            if (res.Version != MessageVersion.Current) { ShowVersionToast(); return; }
            if (Game1.player.UniqueMultiplayerID != res.RequesterId) return;
            if (!res.Success)
            {
                Game1.addHUDMessage(new HUDMessage(
                    _helper.Translation.Get($"recruitment.{res.ReasonKey}"),
                    HUDMessage.error_type));
                return;
            }
            // Mirror the host's local "X is now waiting" message on the farmhand. The
            // host's Game1.showGlobalMessage isn't peer-propagated, so without this the
            // farmhand who triggered the wait sees no feedback at all.
            Game1.addHUDMessage(new HUDMessage(
                _helper.Translation.Get("management.waiting", new { name = res.NpcName }),
                HUDMessage.newQuest_type));
        }

        private void HandleTaskAssignResult(TaskAssignResult res)
        {
            if (res.Version != MessageVersion.Current) { ShowVersionToast(); return; }
            if (Game1.player.UniqueMultiplayerID != res.RequesterId) return;
            if (!res.Success)
            {
                Game1.addHUDMessage(new HUDMessage(
                    _helper.Translation.Get($"recruitment.{res.ReasonKey}"),
                    HUDMessage.error_type));
            }
        }

        // === Send helpers (called from InteractionManager / FollowerManager / Harmony) ===

        /// <summary>
        /// Broadcasts a speech bubble to all peers so every screen renders it (vanilla
        /// <c>showTextAboveHead</c> isn't netfielded). No-op in SP.
        /// </summary>
        public void BroadcastBubble(string npcName, string locationName, string text)
        {
            if (!Context.IsMultiplayer) return;
            _helper.Multiplayer.SendMessage(
                new ShowBubble(MessageVersion.Current, npcName, locationName, text),
                nameof(ShowBubble),
                modIDs: new[] { _modUniqueId });
        }

        public void SendRecruitRequest(string npcName, string locationName, int maxSquadSize)
        {
            _helper.Multiplayer.SendMessage(
                new RecruitRequest(MessageVersion.Current, Game1.player.UniqueMultiplayerID, npcName, locationName, maxSquadSize),
                nameof(RecruitRequest),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
        }

        public void SendDismissRequest(string npcName)
        {
            _helper.Multiplayer.SendMessage(
                new DismissRequest(MessageVersion.Current, Game1.player.UniqueMultiplayerID, npcName),
                nameof(DismissRequest),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
        }

        public void SendWaitRequest(string npcName)
        {
            _helper.Multiplayer.SendMessage(
                new WaitRequest(MessageVersion.Current, Game1.player.UniqueMultiplayerID, npcName),
                nameof(WaitRequest),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
        }

        public void SendTaskAssignRequest(Microsoft.Xna.Framework.Vector2 tile, string locationName)
        {
            _helper.Multiplayer.SendMessage(
                new TaskAssignRequest(MessageVersion.Current, Game1.player.UniqueMultiplayerID, tile, locationName),
                nameof(TaskAssignRequest),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
        }

        public void SendMimickingRequest(long farmerId, TaskType type, string locationName)
        {
            _helper.Multiplayer.SendMessage(
                new MimickingRequest(MessageVersion.Current, farmerId, type, locationName),
                nameof(MimickingRequest),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
        }

        public void SendCutsceneEnded(long farmerId)
        {
            _helper.Multiplayer.SendMessage(
                new CutsceneEnded(MessageVersion.Current, farmerId),
                nameof(CutsceneEnded),
                modIDs: new[] { _modUniqueId },
                playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
        }

        // === Snapshot ===

        /// <summary>
        /// Sends the host's full squad state to one peer (after they connect or after
        /// <see cref="OnSaveLoaded"/>) or to all peers (after every authoritative mutation).
        /// Farmhands consume this in <see cref="ApplySnapshot"/> to rebuild their local
        /// <see cref="SquadManager"/>.
        /// </summary>
        public void BroadcastSnapshot(long? toPeerId = null)
        {
            if (!Context.IsMainPlayer) return;
            if (!Context.IsMultiplayer) return; // SP: nobody to broadcast to.

            var entries = _squad.Members.Select(m => new SquadEntry(
                m.Npc.Name,
                m.RecruiterUniqueId,
                m.Npc.currentLocation?.NameOrUniqueName ?? string.Empty,
                m.Task?.Type
            )).ToArray();

            var snap = new SquadSnapshot(MessageVersion.Current, entries);

            if (toPeerId.HasValue)
            {
                _helper.Multiplayer.SendMessage(
                    snap, nameof(SquadSnapshot),
                    modIDs: new[] { _modUniqueId },
                    playerIDs: new[] { toPeerId.Value });
            }
            else
            {
                _helper.Multiplayer.SendMessage(
                    snap, nameof(SquadSnapshot),
                    modIDs: new[] { _modUniqueId });
            }
        }

        /// <summary>
        /// Farmhand-side: rebuild local <see cref="SquadManager"/> from the host's authoritative
        /// snapshot. We clear and re-add so the local state always matches the host's view -
        /// the host's modData writes have already synced to peers via vanilla netfields, so
        /// every <see cref="SquadEntry"/> NPC should already carry the correct
        /// <c>RecruiterId</c>/<c>SchemaVersion</c> modData; we re-stamp defensively.
        /// </summary>
        private void ApplySnapshot(SquadSnapshot snap)
        {
            if (snap.Version != MessageVersion.Current)
            {
                ShowVersionToast();
                return;
            }

            // Capture pre-existing mate identity → NPC mapping so we can detect both
            // newly-recruited mates (run PrepareForRecruitment) and newly-dismissed mates
            // (run PrepareForDismissal). Both helpers do local state changes that aren't
            // netfielded by SDV (Sprite animation state, farmerPassesThrough, controller),
            // so the host's call doesn't propagate to peers.
            var existingByKey = new Dictionary<(string, long), NPC>();
            foreach (var m in _squad.Members)
                existingByKey[(m.Npc.Name, m.RecruiterUniqueId)] = m.Npc;

            _squad.Clear();

            var snapKeys = new HashSet<(string, long)>(snap.Entries.Length);
            foreach (var entry in snap.Entries)
            {
                var loc = Game1.getLocationFromName(entry.LocationName);
                NPC? npc = loc?.characters.FirstOrDefault(c => c.Name == entry.NpcName);
                if (npc == null) continue;

                // Defensive re-stamp
                npc.modData[SquadMate.RecruiterIdKey] = entry.RecruiterId.ToString();
                npc.modData[SquadMate.SchemaVersionKey] = SquadMate.CurrentSchemaVersion;

                var key = (entry.NpcName, entry.RecruiterId);
                snapKeys.Add(key);

                if (!existingByKey.ContainsKey(key))
                    _stateHelper.PrepareForRecruitment(npc);

                var mate = _mateFactory.Create(npc);
                if (mate != null)
                    _squad.Add(mate);
            }

            foreach (var kvp in existingByKey)
            {
                if (snapKeys.Contains(kvp.Key)) continue;

                // Apply dismissal cleanup AND replay the host's end-of-route schedule animation
                // to every live same-name NPC instance on this peer.
                foreach (var live in FollowerManager.ResolveAllLiveNpcs(kvp.Value))
                {
                    _stateHelper.PrepareForDismissal(live);
                    _recruitment.PlayCurrentScheduleAnimation(live);
                }
            }
        }

        // === Helpers ===

        private void ShowVersionToast()
        {
            _monitor.Log("Multiplayer mod-message version mismatch — peers are running different mod versions.", LogLevel.Warn);
            Game1.addHUDMessage(new HUDMessage(
                _helper.Translation.Get("multiplayer.version_mismatch"),
                HUDMessage.error_type));
        }

        private readonly struct QueuedMessage
        {
            public string Type { get; }
            public object Payload { get; }
            public long FromPlayerId { get; }

            public QueuedMessage(string type, object payload, long fromPlayerId)
            {
                this.Type = type;
                this.Payload = payload;
                this.FromPlayerId = fromPlayerId;
            }
        }
    }
}
