using Microsoft.Xna.Framework;

namespace TheStardewSquad.Tests.Helpers;

/// <summary>
/// Common helper methods and utilities for tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Checks if two Vector2 positions are approximately equal (within tolerance).
    /// Useful for floating-point comparisons in position calculations.
    /// </summary>
    public static bool ArePositionsApproximatelyEqual(Vector2 pos1, Vector2 pos2, float tolerance = 0.01f)
    {
        return Math.Abs(pos1.X - pos2.X) < tolerance &&
               Math.Abs(pos1.Y - pos2.Y) < tolerance;
    }

    /// <summary>
    /// Calculates the distance between two positions.
    /// </summary>
    public static float Distance(Vector2 pos1, Vector2 pos2)
    {
        return Vector2.Distance(pos1, pos2);
    }

    /// <summary>
    /// Creates a grid of positions for formation testing.
    /// </summary>
    /// <param name="rows">Number of rows</param>
    /// <param name="columns">Number of columns</param>
    /// <param name="spacing">Spacing between positions</param>
    /// <returns>List of Vector2 positions in a grid</returns>
    public static List<Vector2> CreatePositionGrid(int rows, int columns, float spacing = 64f)
    {
        var positions = new List<Vector2>();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                positions.Add(new Vector2(col * spacing, row * spacing));
            }
        }

        return positions;
    }

    /// <summary>
    /// Generates a list of consecutive integers for test data.
    /// </summary>
    public static IEnumerable<int> Range(int start, int count)
    {
        return Enumerable.Range(start, count);
    }

    /// <summary>
    /// Creates a predictable random seed for deterministic testing.
    /// </summary>
    public static Random CreateSeededRandom(int seed = 12345)
    {
        return new Random(seed);
    }
}
