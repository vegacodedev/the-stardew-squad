using Moq;
using StardewValley;
using StardewValley.Characters;
using Microsoft.Xna.Framework;

namespace TheStardewSquad.Tests.Helpers;

/// <summary>
/// Factory class for creating mock game objects for testing.
/// Uses Moq to create test doubles of Stardew Valley and SMAPI objects.
/// </summary>
public static class MockFactory
{
    /// <summary>
    /// Creates a mock NPC with basic properties configured.
    /// </summary>
    /// <param name="name">The NPC's name</param>
    /// <param name="position">The NPC's position in the game world</param>
    /// <param name="location">The game location (optional)</param>
    /// <returns>A configured mock NPC</returns>
    public static Mock<NPC> CreateMockNpc(string name, Vector2? position = null, GameLocation? location = null)
    {
        var mockNpc = new Mock<NPC>();
        mockNpc.Setup(n => n.Name).Returns(name);
        mockNpc.Setup(n => n.Position).Returns(position ?? Vector2.Zero);

        if (location != null)
        {
            mockNpc.Setup(n => n.currentLocation).Returns(location);
        }

        // Set up IsVillager to return true by default
        mockNpc.Setup(n => n.IsVillager).Returns(true);

        return mockNpc;
    }

    /// <summary>
    /// Creates a mock Pet with basic properties configured.
    /// </summary>
    /// <param name="position">The pet's position in the game world</param>
    /// <returns>A configured mock Pet</returns>
    public static Mock<Pet> CreateMockPet(Vector2? position = null)
    {
        var mockPet = new Mock<Pet>();
        mockPet.Setup(p => p.Position).Returns(position ?? Vector2.Zero);

        return mockPet;
    }

    /// <summary>
    /// Creates a mock Farmer (player character) with basic properties.
    /// Note: Many Farmer properties (Position, TilePoint, etc.) are not virtual and cannot be mocked.
    /// This factory provides a basic mock for simple tests only.
    /// </summary>
    /// <param name="currentLocation">The farmer's current location</param>
    /// <returns>A configured mock Farmer</returns>
    public static Mock<Farmer> CreateMockFarmer(GameLocation? currentLocation = null)
    {
        var mockFarmer = new Mock<Farmer>();

        if (currentLocation != null)
        {
            mockFarmer.Setup(f => f.currentLocation).Returns(currentLocation);
        }

        return mockFarmer;
    }

    /// <summary>
    /// Creates a mock GameLocation with basic properties.
    /// </summary>
    /// <param name="name">The location name</param>
    /// <returns>A configured mock GameLocation</returns>
    public static Mock<GameLocation> CreateMockLocation(string name = "TestLocation")
    {
        var mockLocation = new Mock<GameLocation>();
        mockLocation.Setup(l => l.Name).Returns(name);

        return mockLocation;
    }

    /// <summary>
    /// Creates a mock StardewValley.Object (item/debris).
    /// </summary>
    /// <param name="itemId">The item ID</param>
    /// <param name="quality">The item quality</param>
    /// <param name="stackSize">The stack size</param>
    /// <returns>A configured mock Object</returns>
    public static Mock<StardewValley.Object> CreateMockObject(
        string itemId = "388",
        int quality = 0,
        int stackSize = 1)
    {
        var mockObject = new Mock<StardewValley.Object>();
        mockObject.Setup(o => o.ItemId).Returns(itemId);
        mockObject.Setup(o => o.Quality).Returns(quality);
        mockObject.Setup(o => o.Stack).Returns(stackSize);

        return mockObject;
    }
}
