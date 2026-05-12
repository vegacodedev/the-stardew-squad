using StardewValley.Menus;
using StardewValley;
using StardewModdingAPI;
using StardewValley.Objects;
using System.Collections.Generic;
using System.Linq;

namespace TheStardewSquad.Framework.UI
{
    internal class SquadInventoryMenu : ItemGrabMenu
    {
        // The opening player always sees their own squad chest. In MP each farmhand opens their own
        // pool, so two farmhands looking at "Squad Inventory" see different inventories,
        // matching the per-recruiter deposit behavior in DebrisCollector / TaskManager.
        private static IList<Item> GetAndPrepareInventory()
        {
            var inventoryId = TaskManager.GetSquadInventoryId(Game1.player);
            var inventory = Game1.player.team.GetOrCreateGlobalInventory(inventoryId);

            if (inventory.Count != 36)
            {
                var oldItems = inventory.ToList();
                inventory.Clear();
                for (int i = 0; i < 36; i++)
                {
                    inventory.Add(null);
                }
                for (int i = 0; i < oldItems.Count && i < inventory.Count; i++)
                {
                    inventory[i] = oldItems[i];
                }
            }
            return inventory;
        }

        private static Chest GetSquadChest()
        {
            var chest = new Chest();
            chest.GlobalInventoryId = TaskManager.GetSquadInventoryId(Game1.player);
            return chest;
        }

        public SquadInventoryMenu(IModHelper helper)
            : base(
                  GetAndPrepareInventory(),
                  reverseGrab: false,
                  showReceivingMenu: true,
                  highlightFunction: item => true,
                  behaviorOnItemSelectFunction: null,
                  message: helper.Translation.Get("squad.inventory.title"),
                  behaviorOnItemGrab: null,
                  snapToBottom: false,
                  canBeExitedWithKey: true,
                  playRightClickSound: true,
                  allowRightClick: true,
                  showOrganizeButton: true,
                  source: source_chest,
                  sourceItem: null,
                  whichSpecialButton: -1,
                  context: null,
                  heldItemExitBehavior: ItemExitBehavior.Drop,
                  allowExitWithHeldItem: true
                )
        {
            Chest _sourceChest = GetSquadChest();

            this.behaviorOnItemGrab = _sourceChest.grabItemFromChest;
            behaviorFunction = _sourceChest.grabItemFromInventory;
        }
    }
}
