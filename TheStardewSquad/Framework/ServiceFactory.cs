using StardewModdingAPI;
using TheStardewSquad.Abstractions.Core;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Abstractions.Data;
using TheStardewSquad.Abstractions.Utilities;
using TheStardewSquad.Framework.Wrappers;

namespace TheStardewSquad.Framework
{
    /// <summary>
    /// Factory for creating service wrapper instances.
    /// Encapsulates the instantiation logic for all 23+ abstraction implementations,
    /// making ModEntry cleaner and easier to maintain.
    /// </summary>
    public class ServiceFactory
    {
        private readonly IModHelper _helper;

        public ServiceFactory(IModHelper helper)
        {
            _helper = helper;
        }

        // Core Services
        public IGameContext CreateGameContext() => new GameContextWrapper();
        public IGameStateChecker CreateGameStateChecker() => new GameStateCheckerWrapper();
        public IRandomService CreateRandomService() => new RandomServiceWrapper();

        // Character Services
        public INpcDialogueService CreateNpcDialogueService() => new NpcDialogueServiceWrapper();
        public INpcSpriteService CreateNpcSpriteService() => new NpcSpriteServiceWrapper();
        public SquadMateStateHelper CreateSquadMateStateHelper() => new SquadMateStateHelper();

        // Data Providers
        public INpcConfigDataProvider CreateNpcConfigDataProvider() => new NpcConfigDataProviderWrapper();

        // Utility Services
        public IRandomSelector CreateRandomSelector() => new RandomSelectorWrapper();
    }
}
