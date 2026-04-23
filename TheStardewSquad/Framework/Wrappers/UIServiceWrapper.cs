using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheStardewSquad.Abstractions.UI;

namespace TheStardewSquad.Framework.Wrappers
{
    /// <summary>
    /// Concrete implementation of IUIService that wraps Game1 UI operations and SMAPI translation.
    /// </summary>
    public class UIServiceWrapper : IUIService
    {
        private readonly IModHelper _helper;

        public UIServiceWrapper(IModHelper helper)
        {
            this._helper = helper;
        }

        public void ShowErrorMessage(string message)
        {
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.error_type));
        }

        public void ShowMessage(string message)
        {
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.newQuest_type));
        }

        public void SetActiveMenu(IClickableMenu menu)
        {
            Game1.activeClickableMenu = menu;
        }

        public string GetTranslation(string key)
        {
            return _helper.Translation.Get(key);
        }

        public string GetTranslation(string key, object tokens)
        {
            return _helper.Translation.Get(key, tokens);
        }
    }
}
