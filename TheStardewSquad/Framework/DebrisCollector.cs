using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using TheStardewSquad.Framework.Squad;
using System.Linq;

namespace TheStardewSquad.Framework.Gathering
{
    /// <summary>Manages the state and logic for squad members automatically collecting debris.</summary>
    /// <remarks>
    /// Multiplayer-aware: scans each online farmer's location independently so each recruiter's
    /// mates only target debris near their recruiter, and collected items deposit into the right
    /// farmer's inventory.
    /// </remarks>
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

            var allMates = squadMembers.ToList();
            if (!allMates.Any()) return;

            // Per-online-farmer scan: each recruiter's location is scanned independently
            // and only that recruiter's mates can pick up that location's debris. In SP this
            // loops once over Game1.player.
            foreach (var rec in Game1.getOnlineFarmers())
            {
                var location = rec.currentLocation;
                if (location == null) continue;

                var mates = allMates
                    .Where(m => m.RecruiterUniqueId == rec.UniqueMultiplayerID
                             && m.Npc.currentLocation == location)
                    .ToList();
                if (!mates.Any()) continue;

                int magneticRadius = rec.GetAppliedMagneticRadius();
                Vector2 playerPosition = rec.Position;

                // Iterate through all debris in this farmer's location.
                foreach (var debris in location.debris)
                {
                    if (this._targetedDebris.ContainsKey(debris)) continue;

                    if (debris.debrisType.Value is not (Debris.DebrisType.OBJECT or Debris.DebrisType.RESOURCE or Debris.DebrisType.ARCHAEOLOGY)) continue;

                    // First, ensure the debris has a valid item representation for the check.
                    Item tempItem = debris.item;
                    if (tempItem == null && !string.IsNullOrEmpty(debris.itemId.Value))
                    {
                        tempItem = ItemRegistry.Create(debris.itemId.Value, 1, debris.itemQuality);
                    }
                    if (tempItem == null) continue;

                    // If the appropriate inventory can't hold this item, skip it entirely.
                    if (!TaskManager.CanAcceptItem(tempItem, rec)) continue;

                    Vector2 debrisPosition = debris.Chunks.FirstOrDefault()?.position.Value ?? Vector2.Zero;
                    if (debrisPosition == Vector2.Zero) continue;

                    // Check if the recruiting farmer is within magnetic range AND has room for the item.
                    bool playerHasPriority =
                        Math.Abs(debrisPosition.X - playerPosition.X) <= magneticRadius &&
                        Math.Abs(debrisPosition.Y - playerPosition.Y) <= magneticRadius &&
                        rec.couldInventoryAcceptThisItem(tempItem);

                    if (playerHasPriority)
                    {
                        // If the player has priority, do not assign it to an NPC.
                        continue;
                    }

                    // Find the closest available mate (of this recruiter, in this location) to this debris.
                    ISquadMate? closestMate = null;
                    float closestDist = float.MaxValue;

                    foreach (var mate in mates)
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
        }

        /// <summary>Called by our Harmony patch when an NPC is close enough to collect a targeted debris object.</summary>
        public void CollectDebris(Debris debris)
        {
            // Resolve the mate that targeted this debris and the location they're in.
            // In MP we MUST use the mate's location (not Game1.currentLocation, which is the
            // local screen's location). The depositing inventory is the recruiter's.
            if (!this._targetedDebris.TryGetValue(debris, out var targetMate))
                return;

            var depositLocation = targetMate.Npc.currentLocation;
            if (depositLocation == null) return;

            var depositOwner = targetMate.TryGetRecruiter(out var rec) ? rec : (Game1.MasterPlayer ?? Game1.player);

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
                // Use the recruiter's per-player squad inventory (handles partial stacks).
                // Each recruiter gets their own iventory.
                var squadChest = new StardewValley.Objects.Chest(playerChest: true)
                {
                    GlobalInventoryId = TaskManager.GetSquadInventoryId(depositOwner)
                };

                // Use the chest's AddItem method, which correctly handles stacking and returns the remainder.
                Item remaining = squadChest.addItem(debris.item);

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
                // Use the recruiter's inventory (all-or-nothing). In MP a farmhand's recruited
                // mate deposits into the farmhand's bag, not the host's.
                if (depositOwner.addItemToInventoryBool(debris.item))
                {
                    fullyCollected = true;
                }
                else
                {
                    // Recruiter inventory is full, don't collect
                    fullyCollected = false;
                }
            }

            if (fullyCollected)
            {
                depositLocation.debris.Remove(debris);
                this._targetedDebris.Remove(debris);

                // Play the collection sound, respecting the game's interval to avoid sound spam.
                if (Game1.debrisSoundInterval <= 0f)
                {
                    Game1.debrisSoundInterval = 10f;
                    depositLocation.localSound("coin");
                }
            }
        }

        /// <summary>A helper for the Harmony patch to check if a Debris object is being targeted.</summary>
        public bool TryGetTarget(Debris debris, out ISquadMate mate)
        {
            return this._targetedDebris.TryGetValue(debris, out mate!);
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
