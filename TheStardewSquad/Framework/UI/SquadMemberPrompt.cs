using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Abstractions.Character;
using TheStardewSquad.Framework.Squad;

namespace TheStardewSquad.Framework.UI
{
    /// <summary>
    /// Shows the recruitment and management prompts for a squad member, routing between the
    /// custom <see cref="SquadMemberMenu"/> and Stardew Valley's vanilla question dialogue based
    /// on <see cref="ModConfig.UseVanillaDialogueUI"/>. The vanilla path uses the native
    /// <see cref="StardewValley.Menus.DialogueBox"/>, which gamepad, mobile, and the
    /// stardew-access screenreader mod already support.
    /// </summary>
    public class SquadMemberPrompt
    {
        private readonly IModHelper _helper;
        private readonly ModConfig _config;
        private readonly SquadManager _squadManager;

        public SquadMemberPrompt(IModHelper helper, ModConfig config, SquadManager squadManager)
        {
            _helper = helper;
            _config = config;
            _squadManager = squadManager;
        }

        public void PromptForRecruitment(ISquadMate mate, Farmer player, Action onRecruitConfirmed)
        {
            if (_config.UseVanillaDialogueUI)
                ShowVanillaRecruitmentDialogue(mate, onRecruitConfirmed);
            else
                ShowCustomRecruitmentMenu(mate, onRecruitConfirmed);
        }

        public void PromptForManagement(ISquadMate mate, Action<string> onActionSelected)
        {
            if (_config.UseVanillaDialogueUI)
                ShowVanillaManagementDialogue(mate, onActionSelected);
            else
                ShowCustomManagementMenu(mate, onActionSelected);
        }

        private void ShowCustomRecruitmentMenu(ISquadMate mate, Action onRecruitConfirmed)
        {
            var menu = new SquadMemberMenu(_helper, mate, isRecruited: false, action =>
            {
                if (action == "recruit")
                    onRecruitConfirmed();
            });
            Game1.activeClickableMenu = menu;
        }

        private void ShowCustomManagementMenu(ISquadMate mate, Action<string> onActionSelected)
        {
            var menu = new SquadMemberMenu(_helper, mate, isRecruited: true, onActionSelected);
            Game1.activeClickableMenu = menu;
        }

        private void ShowVanillaRecruitmentDialogue(ISquadMate mate, Action onRecruitConfirmed)
        {
            NPC npc = mate.Npc;
            string name = GetDisplayName(npc);
            string question = _helper.Translation.Get("recruitment.recruitAsk", new { name });

            Response[] responses = new[]
            {
                new Response("recruit", _helper.Translation.Get("ui.button.recruit")),
                MakeCancelResponse(_helper.Translation.Get("generic.cancel"))
            };

            Game1.currentLocation.createQuestionDialogue(
                question,
                responses,
                (who, key) =>
                {
                    if (key == "recruit")
                        onRecruitConfirmed();
                },
                GetSpeaker(npc));
        }

        private void ShowVanillaManagementDialogue(ISquadMate mate, Action<string> onActionSelected)
        {
            NPC npc = mate.Npc;
            string name = GetDisplayName(npc);
            string question = _helper.Translation.Get("recruitment.recruitedAsk", new { name });

            bool isPet = npc is Pet;
            List<Response> responses = new()
            {
                new Response("inventory", _helper.Translation.Get("squad.inventory.open")),
                new Response("wait", _helper.Translation.Get(isPet ? "ui.button.roamHere" : "ui.button.waitHere")),
                new Response("dismiss", _helper.Translation.Get("ui.button.dismiss"))
            };

            if (_squadManager.Count > 1)
                responses.Add(new Response("dismissAll", _helper.Translation.Get("ui.button.dismissAll")));

            responses.Add(MakeCancelResponse(_helper.Translation.Get("generic.cancel")));

            Game1.currentLocation.createQuestionDialogue(
                question,
                responses.ToArray(),
                (who, key) =>
                {
                    switch (key)
                    {
                        case "inventory":
                        case "wait":
                        case "dismiss":
                        case "dismissAll":
                            onActionSelected(key);
                            break;
                    }
                },
                GetSpeaker(npc));
        }

        private static Response MakeCancelResponse(string text)
        {
            var response = new Response("cancel", text);
            response.hotkey = Keys.Escape;
            return response;
        }

        private static NPC? GetSpeaker(NPC npc)
        {
            // Pets don't have portrait textures (see SquadMemberMenu.LoadPortrait), so the
            // vanilla dialogue box would render awkwardly — pass null to suppress the portrait.
            return npc is Pet ? null : npc;
        }

        private static string GetDisplayName(NPC npc)
        {
            return npc is Pet pet ? pet.displayName : npc.displayName;
        }
    }
}
