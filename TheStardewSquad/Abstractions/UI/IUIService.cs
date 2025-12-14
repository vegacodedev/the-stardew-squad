using StardewValley;
using StardewValley.Menus;
using System;

namespace TheStardewSquad.Abstractions.UI
{
    /// <summary>
    /// Abstraction for UI operations.
    /// Enables testing of components that show messages and menus without requiring Game1 state.
    /// </summary>
    public interface IUIService
    {
        /// <summary>Shows an error message to the player.</summary>
        void ShowErrorMessage(string message);

        /// <summary>Shows an informational message to the player.</summary>
        void ShowMessage(string message);

        /// <summary>Sets the active clickable menu.</summary>
        void SetActiveMenu(IClickableMenu menu);

        /// <summary>Gets a translated string for the given key.</summary>
        string GetTranslation(string key);

        /// <summary>Gets a translated string for the given key with token replacements.</summary>
        string GetTranslation(string key, object tokens);

        /// <summary>
        /// Creates a question dialogue with multiple choice responses.
        /// Enables testing of dialog interactions without requiring Game1.currentLocation.
        /// </summary>
        /// <param name="question">The question text to display</param>
        /// <param name="responses">Array of response options</param>
        /// <param name="onResponse">Callback when user selects a response</param>
        void ShowQuestionDialog(string question, Response[] responses, Action<Farmer, string> onResponse);
    }
}
