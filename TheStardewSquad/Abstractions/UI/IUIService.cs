using StardewValley.Menus;

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
    }
}
