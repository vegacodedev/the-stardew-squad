using System;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace TheStardewSquad.Framework.NpcConfig
{
    /// <summary>
    /// Detects whether an NPC's sprite texture is vanilla or has been modified by a retexture mod.
    /// Uses SHA256 hash comparison between the vanilla game texture and the currently loaded texture.
    /// </summary>
    public class VanillaSpriteDetector
    {
        private readonly IMonitor _monitor;

        public VanillaSpriteDetector(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// Checks if the specified NPC texture is vanilla (unmodified).
        /// </summary>
        /// <param name="npcTextureName">The NPC's texture name (e.g., "Abigail", "Abigail_Beach").</param>
        /// <returns>True if the texture is vanilla, false if it has been modified or on error.</returns>
        public bool IsVanillaSprite(string npcTextureName)
        {
            try
            {
                // Create a separate content manager that bypasses SMAPI's content pipeline
                // This loads the raw vanilla texture from the game's Content folder
                using var vanillaContent = new LocalizedContentManager(
                    Game1.content.ServiceProvider,
                    Game1.content.RootDirectory
                );

                var vanillaTexture = vanillaContent.Load<Texture2D>($"Characters/{npcTextureName}");
                var currentTexture = Game1.content.Load<Texture2D>($"Characters/{npcTextureName}");

                var vanillaHash = ComputeTextureHash(vanillaTexture);
                var currentHash = ComputeTextureHash(currentTexture);

                return vanillaHash.SequenceEqual(currentHash);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Error checking vanilla sprite for {npcTextureName}: {ex.Message}", LogLevel.Warn);
                // Assume retextured on error - safer to use fallback
                return false;
            }
        }

        /// <summary>
        /// Computes a SHA256 hash of the texture's pixel data.
        /// </summary>
        private byte[] ComputeTextureHash(Texture2D texture)
        {
            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);

            var bytes = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].R;
                bytes[i * 4 + 1] = pixels[i].G;
                bytes[i * 4 + 2] = pixels[i].B;
                bytes[i * 4 + 3] = pixels[i].A;
            }

            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(bytes);
        }
    }
}
