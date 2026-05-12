using StardewModdingAPI;
using StardewValley;
using TheStardewSquad.Framework.Multiplayer;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.Behaviors
{
    internal class NpcCommunicationBehavior : ICommunicationBehavior
    {
        private readonly ModConfig _config;
        private readonly DialogueManager _dialogueManager;
        private readonly SquadManager _squadManager;
        private readonly IMonitor _monitor;
        private MessageDispatcher? _dispatcher;

        public NpcCommunicationBehavior(ModConfig config, DialogueManager dialogueManager, SquadManager squadManager, IMonitor monitor)
        {
            this._config = config;
            this._dialogueManager = dialogueManager;
            this._squadManager = squadManager;
            this._monitor = monitor;
        }

        public void AttachDispatcher(MessageDispatcher dispatcher)
        {
            this._dispatcher = dispatcher;
        }

        public void Communicate(ISquadMate mate, string dialogueKey)
        {
            NPC npc = mate.Npc;

            _monitor.Log($"[Communication] {npc.Name} attempting to communicate: {dialogueKey}", LogLevel.Trace);

            // Resolve dialogue using the recruiter's context
            GameLocation contextLocation = npc.currentLocation;
            Farmer contextPlayer = mate.TryGetRecruiter(out var rec) ? rec : null;

            string dialogueText = _dialogueManager.GetDialogue(npc, dialogueKey, contextLocation, contextPlayer);

            if (string.IsNullOrEmpty(dialogueText))
            {
                _monitor.Log($"[Communication] {npc.Name}: No dialogue text found for {dialogueKey}, skipping", LogLevel.Trace);
                return;
            }

            _monitor.Log($"[Communication] {npc.Name}: Retrieved dialogue text for {dialogueKey}", LogLevel.Trace);

            // Keys for UI dialogues
            if (dialogueKey is DialogueKeys.Recruit or DialogueKeys.Dismiss or DialogueKeys.FriendshipTooLow)
            {
                _monitor.Log($"[Communication] {npc.Name}: Showing UI dialogue for {dialogueKey}", LogLevel.Trace);
                Game1.DrawDialogue(new StardewValley.Dialogue(npc, null, dialogueText));
            }
            // Keys for speech bubbles
            else
            {
                if (!_config.EnableCommunication)
                {
                    _monitor.Log($"[Communication] {npc.Name}: Communication disabled in config, skipping {dialogueKey} bubble", LogLevel.Trace);
                    return;
                }

                // Check dialogue cooldown (convert seconds to ticks: 60 FPS assumed)
                int cooldownTicks = _config.DialogueCooldownSeconds * 60;
                int currentTick = Game1.ticks;

                if (cooldownTicks > 0 && currentTick - mate.LastCommunicationTick < cooldownTicks)
                {
                    _monitor.Log($"[Communication] {npc.Name}: Still on cooldown (last: {mate.LastCommunicationTick}, current: {currentTick}, cooldown: {cooldownTicks}), skipping {dialogueKey}", LogLevel.Trace);
                    return; // Still on cooldown, skip dialogue
                }

                // Apply squad-size-based probability reduction
                int squadSize = _squadManager.Count;
                double probabilityMultiplier = TaskManager.CalculateDialogueProbabilityMultiplier(squadSize);

                if (Game1.random.NextDouble() > probabilityMultiplier)
                {
                    _monitor.Log($"[Communication] {npc.Name}: Failed probability check (multiplier: {probabilityMultiplier:F2}, squad size: {squadSize}), skipping {dialogueKey}", LogLevel.Trace);
                    return; // Skip dialogue based on squad size
                }

                _monitor.Log($"[Communication] {npc.Name}: Showing dialogue bubble for {dialogueKey}: \"{dialogueText}\"", LogLevel.Trace);
                _dialogueManager.ShowDialogueBubble(npc, dialogueText);
                mate.LastCommunicationTick = currentTick;

                // Broadcast to peers so every screen renders the bubble — vanilla
                // showTextAboveHead writes to local protected fields only (no NetField).
                _dispatcher?.BroadcastBubble(
                    npc.Name,
                    npc.currentLocation?.NameOrUniqueName ?? string.Empty,
                    dialogueText);
            }
        }
    }
}
