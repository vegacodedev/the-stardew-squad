using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheStardewSquad.Config;
using TheStardewSquad.Framework;
using TheStardewSquad.Framework.Gathering;
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
            var gameStateService = factory.CreateGameStateService();
            var gameStateChecker = factory.CreateGameStateChecker();
            var playerService = factory.CreatePlayerService();
            var randomService = factory.CreateRandomService();

            // Character services
            var stateHelper = factory.CreateSquadMateStateHelper();
            var npcDialogueService = factory.CreateNpcDialogueService();
            var npcSpriteService = factory.CreateNpcSpriteService();
            var warpService = factory.CreateWarpService();

            // Data providers
            var npcConfigDataProvider = factory.CreateNpcConfigDataProvider();

            // Task services
            var taskService = factory.CreateTaskService();

            // UI services
            var uiService = factory.CreateUIService();

            // Utility services
            var randomSelector = factory.CreateRandomSelector();

            // Initialize NPC configuration managers (unified system)
            this.NpcConfigManager = new NpcConfigManager(npcConfigDataProvider, this.Monitor);
            this.DialogueManager = new DialogueManager(this.NpcConfigManager, randomSelector, gameStateChecker, npcDialogueService, gameContext, this.Monitor);
            this.VanillaSpriteDetector = new VanillaSpriteDetector(this.Monitor);
            this.SpriteManager = new SpriteManager(this.NpcConfigManager, this.Monitor, gameStateChecker, gameContext, this.VanillaSpriteDetector);
            this.BehaviorManager = new BehaviorManager(this.NpcConfigManager, npcConfigDataProvider, this.Monitor, randomSelector, gameStateChecker, gameContext, npcSpriteService, npcDialogueService);

            // Initialize other managers with dependencies
            this.RecruitmentManager = new RecruitmentManager(helper, this.Monitor, this.SquadManager, this.WaitingNpcsManager, this.FormationManager, stateHelper);
            this.InteractionManager = new InteractionManager(helper, this.SquadManager, null, this.BehaviorManager, gameContext, uiService, this.Config);
            this.SquadMateFactory = new SquadMateFactory(helper, this.RecruitmentManager, this.SquadManager, this.Config, this.InteractionManager, this.BehaviorManager, stateHelper, this.DialogueManager, this.Monitor);
            this.InteractionManager.SquadMateFactory = this.SquadMateFactory;
            this.FollowerManager = new FollowerManager(this.Monitor, this.SquadManager, this.WaitingNpcsManager, this.Config, this.DebrisCollector, this.UnifiedTaskManager, this.FormationManager, this.BehaviorManager, gameStateService, warpService, randomService, taskService, playerService);
            this.RecruitmentManager.SetFollowerManager(this.FollowerManager);
            this.AssetManager = new AssetManager();
            TaskManager.Initialize(this.Config, this.FollowerManager, this.SpriteManager);
            TaskManager.SetMonitor(this.Monitor);

            var harmony = new Harmony(this.ModManifest.UniqueID);
            HarmonyPatches.Initialize(this.InteractionManager, this.SquadManager, this.Config, this.FollowerManager, this.DebrisCollector, this);
            HarmonyPatches.Apply(harmony);

            helper.Events.GameLoop.GameLaunched += _genericModConfigMenuIntegration.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.InteractionManager.OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += this.FollowerManager.OnUpdateTicked;
            helper.Events.GameLoop.DayEnding += this.RecruitmentManager.OnDayEnding;
            helper.Events.Content.AssetRequested += this.AssetManager.OnAssetRequested;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
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

        /// <summary>Draws fishing lines for NPCs with fishing tasks.</summary>
        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            TaskManager.RenderFishing(e.SpriteBatch, this.SquadManager);
        }

    }
}