using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheStardewSquad.Config;
using TheStardewSquad.Framework;
using TheStardewSquad.Framework.Gathering;
using TheStardewSquad.Framework.Multiplayer;
using TheStardewSquad.Framework.NpcConfig;
using TheStardewSquad.Framework.Squad;
using TheStardewSquad.Framework.Tasks;
using TheStardewSquad.Patches;

namespace TheStardewSquad
{
    public class ModEntry : Mod
    {
        public FormationManager FormationManager { get; private set; }
        public SquadManager SquadManager { get; private set; }
        public WaitingNpcsManager WaitingNpcsManager { get; private set; }
        public FollowerManager FollowerManager { get; private set; }
        public InteractionManager InteractionManager { get; private set; }
        public NpcConfigManager NpcConfigManager { get; private set; }
        public DialogueManager DialogueManager { get; private set; }
        public SpriteManager SpriteManager { get; private set; }
        public VanillaSpriteDetector VanillaSpriteDetector { get; private set; }
        public BehaviorManager BehaviorManager { get; private set; }
        public RecruitmentManager RecruitmentManager { get; private set; }
        public AssetManager AssetManager { get; private set; }
        public DebrisCollector DebrisCollector { get; private set; }
        public UnifiedTaskManager UnifiedTaskManager { get; private set; }

        public SquadMateFactory SquadMateFactory { get; private set; }
        public MessageDispatcher MessageDispatcher { get; private set; }

        public ModConfig Config { get; private set; }
        private GenericModConfigMenuIntegration _genericModConfigMenuIntegration;

        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<ModConfig>();
            this._genericModConfigMenuIntegration = new GenericModConfigMenuIntegration(this, helper);

            // Initialize ServiceFactory for creating all abstraction implementations
            var factory = new ServiceFactory(helper);

            // Initialize managers
            this.FormationManager = new FormationManager();
            this.SquadManager = new SquadManager();
            this.WaitingNpcsManager = new WaitingNpcsManager();
            this.DebrisCollector = new DebrisCollector(this.Config);
            this.UnifiedTaskManager = new UnifiedTaskManager(this.Config, this.Monitor);

            // Core services
            var gameContext = factory.CreateGameContext();
            var gameStateChecker = factory.CreateGameStateChecker();
            var randomService = factory.CreateRandomService();

            // Character services
            var stateHelper = factory.CreateSquadMateStateHelper();
            var npcDialogueService = factory.CreateNpcDialogueService();
            var npcSpriteService = factory.CreateNpcSpriteService();

            // Data providers
            var npcConfigDataProvider = factory.CreateNpcConfigDataProvider();

            // Utility services
            var randomSelector = factory.CreateRandomSelector();

            var baselineLoader = new BaselineContentLoader(helper, this.Monitor);

            // Initialize NPC configuration managers (unified system)
            this.NpcConfigManager = new NpcConfigManager(npcConfigDataProvider, baselineLoader, this.Monitor);
            this.DialogueManager = new DialogueManager(this.NpcConfigManager, randomSelector, gameStateChecker, npcDialogueService, gameContext, this.Monitor);
            this.VanillaSpriteDetector = new VanillaSpriteDetector(this.Monitor);
            this.SpriteManager = new SpriteManager(this.NpcConfigManager, this.Monitor, gameStateChecker, gameContext, this.VanillaSpriteDetector);
            this.BehaviorManager = new BehaviorManager(this.NpcConfigManager, npcConfigDataProvider, this.Monitor, randomSelector, gameStateChecker, gameContext, npcSpriteService, npcDialogueService);

            // Initialize other managers with dependencies
            this.RecruitmentManager = new RecruitmentManager(helper, this.Monitor, this.Config, this.SquadManager, this.WaitingNpcsManager, this.FormationManager, stateHelper);
            this.InteractionManager = new InteractionManager(helper, this.SquadManager, null, this.BehaviorManager, gameContext, this.Config);
            this.SquadMateFactory = new SquadMateFactory(helper, this.RecruitmentManager, this.SquadManager, this.Config, this.InteractionManager, this.BehaviorManager, stateHelper, this.DialogueManager, this.Monitor);
            this.InteractionManager.SquadMateFactory = this.SquadMateFactory;
            this.FollowerManager = new FollowerManager(this.Monitor, this.SquadManager, this.WaitingNpcsManager, this.Config, this.DebrisCollector, this.UnifiedTaskManager, this.FormationManager, this.BehaviorManager, randomService);
            this.RecruitmentManager.SetFollowerManager(this.FollowerManager);
            this.FollowerManager.SetRecruitmentManager(this.RecruitmentManager);
            this.FollowerManager.SetSpriteManager(this.SpriteManager);

            // Multiplayer message dispatcher: wires host queue + per-tick drain and
            // routes farmhand requests / host results / squad snapshots.
            this.MessageDispatcher = new MessageDispatcher(
                helper, this.Monitor, this.Config,
                this.SquadManager, this.WaitingNpcsManager,
                this.RecruitmentManager, this.FollowerManager,
                this.InteractionManager, this.SquadMateFactory,
                stateHelper, this.BehaviorManager, this.SpriteManager,
                this.ModManifest.UniqueID);
            this.RecruitmentManager.AttachDispatcher(this.MessageDispatcher);
            this.InteractionManager.AttachDispatcher(this.MessageDispatcher);
            this.FollowerManager.AttachDispatcher(this.MessageDispatcher);
            this.SquadMateFactory.AttachDispatcher(this.MessageDispatcher);
            this.SpriteManager.AttachDispatcher(this.MessageDispatcher);

            this.AssetManager = new AssetManager();
            TaskManager.Initialize(this.Config, this.FollowerManager, this.SpriteManager);
            TaskManager.SetMonitor(this.Monitor);
            TaskManager.AttachDispatcher(this.MessageDispatcher);

            var harmony = new Harmony(this.ModManifest.UniqueID);
            HarmonyPatches.Initialize(this.InteractionManager, this.SquadManager, this.Config, this.FollowerManager, this.DebrisCollector, this);
            HarmonyPatches.Apply(harmony);

            helper.Events.GameLoop.GameLaunched += _genericModConfigMenuIntegration.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.InteractionManager.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += this.FollowerManager.OnUpdateTicked;
            helper.Events.GameLoop.UpdateTicked += this.MessageDispatcher.OnUpdateTicked;
            helper.Events.GameLoop.DayEnding += this.RecruitmentManager.OnDayEnding;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.Content.AssetRequested += this.AssetManager.OnAssetRequested;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
            helper.Events.Multiplayer.ModMessageReceived += this.MessageDispatcher.OnModMessageReceived;
            helper.Events.Multiplayer.PeerConnected += this.MessageDispatcher.OnPeerConnected;
        }

        /// <summary>Handles cleaning up squad data when the player returns to the title screen.</summary>
        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            // Clear the squad and related states to prevent data from carrying over to another save file.
            this.DebrisCollector.Reset();
            this.FormationManager.Reset();
            this.FollowerManager.ResetStateForNewSession();
            this.SquadManager.Clear();
            this.WaitingNpcsManager.Clear();
        }

        /// <summary>
        /// Host-only save-load rehydration. Walks every NPC in every location and reconciles
        /// their <c>TheStardewSquad/RecruiterId</c> modData against the actual farmer list:
        /// <list type="bullet">
        /// <item>If the recruiter id doesn't match any farmer in the save, strip both keys and
        /// restore the NPC's schedule flags (the farmer was deleted from the save).</item>
        /// <item>If the schema version is greater than 1, log a warning and leave the NPC alone
        /// (forward-compat: a future mod version may understand the data; we won't strip it).</item>
        /// <item>Otherwise, recreate the <see cref="SquadMate"/> via the factory and add it back
        /// to the <see cref="SquadManager"/>.</item>
        /// </list>
        /// Triggers a <see cref="MessageDispatcher.BroadcastSnapshot"/> so peers who joined late
        /// get the rehydrated state.
        /// </summary>
        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (!Context.IsMainPlayer) return;

            var farmerIds = Game1.getAllFarmers().Select(f => f.UniqueMultiplayerID).ToHashSet();
            int rehydrated = 0, stripped = 0, skipped = 0;

            foreach (var loc in Game1.locations)
            {
                // ToList: avoid mutation during iteration if we strip keys mid-walk.
                foreach (var npc in loc.characters.OfType<NPC>().ToList())
                {
                    if (!npc.modData.TryGetValue(SquadMate.RecruiterIdKey, out var ridStr))
                        continue;

                    if (!long.TryParse(ridStr, out var rid))
                    {
                        StripSquadKeys(npc);
                        stripped++;
                        continue;
                    }

                    if (npc.modData.TryGetValue(SquadMate.SchemaVersionKey, out var verStr)
                        && int.TryParse(verStr, out var ver) && ver > 1)
                    {
                        this.Monitor.Log($"[SaveLoaded] Skipping rehydration of {npc.Name}: SchemaVersion={ver} (forward-compat — leaving modData intact).", LogLevel.Warn);
                        skipped++;
                        continue;
                    }

                    if (!farmerIds.Contains(rid))
                    {
                        StripSquadKeys(npc);
                        stripped++;
                        continue;
                    }

                    var mate = this.SquadMateFactory.Create(npc);
                    if (mate != null)
                    {
                        this.SquadManager.Add(mate);
                        rehydrated++;
                    }
                }
            }

            this.Monitor.Log($"[SaveLoaded] Squad rehydration: rehydrated={rehydrated}, stripped={stripped}, skipped(forward-compat)={skipped}.", LogLevel.Info);

            // Sync any remote peers (filled in by R2.10).
            this.MessageDispatcher.BroadcastSnapshot();
        }

        /// <summary>Removes squad modData keys and restores normal scheduling on an NPC.</summary>
        private static void StripSquadKeys(NPC npc)
        {
            npc.modData.Remove(SquadMate.RecruiterIdKey);
            npc.modData.Remove(SquadMate.SchemaVersionKey);
            npc.followSchedule = true;
            npc.ignoreScheduleToday = false;
        }

        /// <summary>Draws fishing lines for NPCs with fishing tasks.</summary>
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            TaskManager.RenderFishing(e.SpriteBatch, this.SquadManager);
        }

    }
}