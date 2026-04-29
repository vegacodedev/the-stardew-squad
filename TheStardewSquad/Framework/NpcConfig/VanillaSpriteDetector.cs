using System;
using System.Collections.Generic;
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

        // Cache result by asset path.
        private readonly Dictionary<string, (Texture2D textureRef, bool isVanilla)> _cache = new();

        public VanillaSpriteDetector(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// Checks if the NPC's current sprite is vanilla (unmodified). Pet-aware: villagers
        /// are loaded from "Characters/...", pets from "Animals/..." (breed-dependent).
        /// Result is cached for performance.
        /// </summary>
        /// <param name="npc">The NPC (or Pet) whose sprite should be checked.</param>
        /// <returns>True if the texture is vanilla, false if it has been modified or on error.</returns>
        public bool IsVanillaSprite(NPC npc)
        {
            string assetPath = NpcTextureHelper.GetTextureAssetPath(npc);

            Texture2D currentTexture;
            try
            {
                currentTexture = Game1.content.Load<Texture2D>(assetPath);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Error loading current texture for {assetPath}: {ex.Message}", LogLevel.Warn);
                return false;
            }

            if (_cache.TryGetValue(assetPath, out var entry) && ReferenceEquals(entry.textureRef, currentTexture))
                return entry.isVanilla;

            bool result = ComputeIsVanilla(assetPath, currentTexture);
            _cache[assetPath] = (currentTexture, result);
            return result;
        }

        private bool ComputeIsVanilla(string assetPath, Texture2D currentTexture)
        {
            try
            {
                // Create a separate content manager that bypasses SMAPI's content pipeline
                // This loads the raw vanilla texture from the game's Content folder
                using var vanillaContent = new LocalizedContentManager(
                    Game1.content.ServiceProvider,
                    Game1.content.RootDirectory
                );
                var vanillaTexture = vanillaContent.Load<Texture2D>(assetPath);

                return ComputeTextureHash(vanillaTexture)
                    .SequenceEqual(ComputeTextureHash(currentTexture));
            }
            catch (Exception ex)
            {
                _monitor.LogOnce($"Error checking vanilla sprite for {assetPath}: {ex.Message}", LogLevel.Warn);
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
