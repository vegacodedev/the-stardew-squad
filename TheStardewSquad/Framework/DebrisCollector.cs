using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Framework.Squad;
using System.Linq;

namespace TheStardewSquad.Framework.Gathering
{
    /// <summary>Manages the state and logic for squad members automatically collecting debris.</summary>
    public class DebrisCollector
    {
        private readonly Dictionary<Debris, ISquadMate> _targetedDebris = new();
        private readonly ModConfig _config;
        private int _updateCounter = 0;

        public DebrisCollector(ModConfig config)
        {
            _config = config;
        }

        /// <summary>Scans for nearby debris and assigns it to the closest available squad member.</summary>
        public void Update(IEnumerable<ISquadMate> squadMembers)
        {
            this._updateCounter++;

            if (!_config.EnableGathering) return;

            // Only scan every half-second to save performance.
            if (this._updateCounter % 30 != 0) return;

            var location = Game1.currentLocation;
            if (location == null) return;

            var availableMates = squadMembers.ToList();
            if (!availableMates.Any()) return;

            Farmer player = Game1.player;
            int magneticRadius = player.GetAppliedMagneticRadius();
            Vector2 playerPosition = player.Position;

            // Iterate through all debris in the current location.
            foreach (var debris in location.debris)
            {
                if (this._targetedDebris.ContainsKey(debris)) continue;

                if (debris.debrisType.Value is not (Debris.DebrisType.OBJECT or Debris.DebrisType.RESOURCE or Debris.DebrisType.ARCHAEOLOGY)) continue;

                // First, ensure the debris has a valid item representation for the check.
                // We create a temporary item instance without modifying the actual debris.
                Item tempItem = debris.item;
                if (tempItem == null && !string.IsNullOrEmpty(debris.itemId.Value))
                {
                    tempItem = ItemRegistry.Create(debris.itemId.Value, 1, debris.itemQuality);
                }
                // If we can't determine what the item is, skip it.
                if (tempItem == null) continue;

                // If the appropriate inventory can't hold this item, skip it entirely.
                if (!TaskManager.CanAcceptItem(tempItem)) continue;

                Vector2 debrisPosition = debris.Chunks.FirstOrDefault()?.position.Value ?? Vector2.Zero;
                if (debrisPosition == Vector2.Zero) continue;
                
                // Check if the player is within magnetic range AND has room for the item.
                bool playerHasPriority =
                    Math.Abs(debrisPosition.X - playerPosition.X) <= magneticRadius &&
                    Math.Abs(debrisPosition.Y - playerPosition.Y) <= magneticRadius &&
                    player.couldInventoryAcceptThisItem(tempItem);

                if (playerHasPriority)
                {
                    // If the player has priority, do not assign it to an NPC.
                    continue;
                }

                // Find the closest available squad member to this debris.
                ISquadMate closestMate = null;
                float closestDist = float.MaxValue;

                foreach (var mate in availableMates)
                {
                    float dist = Vector2.DistanceSquared(mate.Npc.Position, debrisPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestMate = mate;
                    }
                }

                // If a suitable mate was found within a reasonable range, assign the debris to them.
                if (closestMate != null && closestDist < 150000)
                {
                    this._targetedDebris[debris] = closestMate;
                }
            }
        }
        
        /// <summary>Called by our Harmony patch when an NPC is close enough to collect a targeted debris object.</summary>
        public void CollectDebris(Debris debris)
        {
            // Ensure the debris has a valid item to collect.
            if (debris.item == null)
            {
                if (string.IsNullOrEmpty(debris.itemId.Value)) return;
                debris.item = ItemRegistry.Create(debris.itemId.Value, 1, debris.itemQuality);
                if (debris.item == null) return;
            }

            bool fullyCollected = false;

            if (_config.UseSquadInventory)
            {
                // Use squad inventory (handles partial stacks)
                var squadChest = new StardewValley.Objects.Chest(playerChest: true)
                {
                    GlobalInventoryId = "TheStardewSquad_SquadInventory"
                };

                // Use the chest's AddItem method, which correctly handles stacking and returns the remainder.
                Item remaining = squadChest.addItem(debris.item);

                // If some of the stack couldn't be added, update the debris item.
                // Otherwise, the collection was successful.
                if (remaining != null && remaining.Stack > 0)
                {
                    debris.item = remaining;
                    fullyCollected = false;
                }
                else
                {
                    fullyCollected = true;
                }
            }
            else
            {
                // Use player inventory (all-or-nothing)
                if (Game1.player.addItemToInventoryBool(debris.item))
                {
                    fullyCollected = true;
                }
                else
                {
                    // Player inventory is full, don't collect
                    fullyCollected = false;
                }
            }

            if (fullyCollected)
            {
                Game1.currentLocation.debris.Remove(debris);
                this._targetedDebris.Remove(debris);

                // Play the collection sound, respecting the game's interval to avoid sound spam.
                if (Game1.debrisSoundInterval <= 0f)
                {
                    Game1.debrisSoundInterval = 10f;
                    Game1.currentLocation.localSound("coin");
                }
            }
        }

        /// <summary>A helper for the Harmony patch to check if a Debris object is being targeted.</summary>
        public bool TryGetTarget(Debris debris, out ISquadMate mate)
        {
            return this._targetedDebris.TryGetValue(debris, out mate);
        }

        /// <summary>A helper for the Harmony patch to stop targeting a Debris object.</summary>
        public void RemoveTarget(Debris debris)
        {
            this._targetedDebris.Remove(debris);
        }
        
        /// <summary>Resets the state of the collector, clearing any targeted debris.</summary>
        public void Reset()
        {
            _targetedDebris.Clear();
        }
    }
}