using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
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

        public void ShowQuestionDialog(string question, Response[] responses, Action<Farmer, string> onResponse)
        {
            // Stardew Valley's createQuestionDialogue uses afterQuestion for callbacks
            // Store the callback and use afterQuestion event
            Game1.currentLocation.afterQuestion = (Farmer who, string responseKey) =>
            {
                Game1.currentLocation.afterQuestion = null; // Clear after use
                onResponse(who, responseKey);
            };

            Game1.currentLocation.createQuestionDialogue(question, responses, "petDismissalChoice");
        }
    }
}
