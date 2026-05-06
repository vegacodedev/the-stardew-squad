using System;
using System.Reflection;
using System.Runtime.Serialization;
using Netcode;
using StardewValley.Characters;

namespace TheStardewSquad.Tests.Helpers
{
    /// <summary>
    /// Creates a Pet for tests, bypassing the Pet ctor (which calls AnimatedSprite.LoadTexture
    /// and NREs in environments without Game1.content). Initializes only the netfields the
    /// production code under test reads: Character.name, Pet.petType, Pet.whichBreed.
    /// </summary>
    public static class TestPetFactory
    {
        public static Pet CreatePet(string name, string petType = "Cat", int breed = 0)
        {
            var pet = (Pet)FormatterServices.GetUninitializedObject(typeof(Pet));

            SetReadonlyField(pet, typeof(Pet), "petType", new NetString(petType));
            SetReadonlyField(pet, typeof(Pet), "whichBreed", new NetString(breed.ToString()));

            // Character.name lives on the Character base; field name is "name".
            var characterType = typeof(Pet).BaseType?.BaseType
                ?? throw new InvalidOperationException("Could not find Character base type via Pet → NPC → Character.");
            SetReadonlyField(pet, characterType, "name", new NetString(name));

            return pet;
        }

        private static void SetReadonlyField(object instance, Type declaringType, string fieldName, object value)
        {
            var field = declaringType.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException(
                    $"Field '{fieldName}' not found on {declaringType.FullName}; SDV API may have shifted.");
            field.SetValue(instance, value);
        }
    }
}
