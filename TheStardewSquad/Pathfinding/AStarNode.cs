using Microsoft.Xna.Framework;

namespace TheStardewSquad.Pathfinding
{
    // A small helper class to represent each tile we evaluate. It needs to store its position and its "costs" for the A* algorithm.
    public class AStarNode
    {
        public Point Position { get; }
        public AStarNode Parent { get; set; }

        // G: The actual cost of moving from the start node to this node.
        public int GCost { get; set; }

        // H: The estimated (heuristic) cost from this node to the end node.
        public int HCost { get; set; }

        // F: The total cost of the node (G + H). A* prioritizes nodes with the lowest F cost.
        public int FCost => GCost + HCost;

        public AStarNode(Point position, AStarNode parent = null)
        {
            Position = position;
            Parent = parent;
            GCost = 0;
            HCost = 0;
        }

        // Used for comparing nodes in our lists.
        public override bool Equals(object obj)
        {
            return obj is AStarNode other && Position.Equals(other.Position);
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode();
        }
    }
}