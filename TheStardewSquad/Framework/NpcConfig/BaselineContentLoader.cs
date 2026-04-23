using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using TheStardewSquad.Framework.NpcConfig.Models;

namespace TheStardewSquad.Framework.NpcConfig
{
    /// <summary>
    /// Reads the baseline NPC configuration dictionary from JSON files
    /// inside the main mod (<c>assets/NpcConfig/</c>). This baseline is guaranteed
    /// to be present before community Content Patcher packs layer their overrides
    /// on top of the <c>ThaliaFawnheart.TheStardewSquad/NpcConfig</c> asset.
    /// </summary>
    public class BaselineContentLoader
    {
        private static readonly Regex I18nTokenRegex = new(@"^\{\{i18n:([^}]+)\}\}$", RegexOptions.Compiled);

        private const string NpcConfigTarget = "ThaliaFawnheart.TheStardewSquad/NpcConfig";
        private const string BaselineSubdir = "assets/NpcConfig";

        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;

        public BaselineContentLoader(IModHelper helper, IMonitor monitor)
        {
            this._helper = helper;
            this._monitor = monitor;
        }

        public Dictionary<string, NpcConfigData> Load()
        {
            var result = new Dictionary<string, NpcConfigData>(StringComparer.Ordinal);

            var assetsDir = Path.Combine(this._helper.DirectoryPath, BaselineSubdir);
            if (!Directory.Exists(assetsDir))
            {
                this._monitor.Log(
                    $"[BaselineContentLoader] Baseline directory missing at {BaselineSubdir}/. "
                    + "Community CP packs must supply their own Generic defaults.",
                    LogLevel.Warn);
                return result;
            }

            var files = Directory.EnumerateFiles(assetsDir, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

            foreach (var fullPath in files)
            {
                var fileName = Path.GetFileName(fullPath);
                var relative = $"{BaselineSubdir}/{fileName}";
                try
                {
                    var root = this._helper.Data.ReadJsonFile<JObject>(relative);
                    if (root == null)
                        continue;

                    ResolveI18nTokens(root, this._helper.Translation);

                    var entries = ExtractNpcConfigEntries(root);
                    if (entries == null)
                        continue;

                    var partial = entries.ToObject<Dictionary<string, NpcConfigData>>();
                    if (partial == null)
                        continue;

                    foreach (var kv in partial)
                        result[kv.Key] = kv.Value;

                    this._monitor.Log(
                        $"[BaselineContentLoader] Loaded {partial.Count} NPC entries from {relative}",
                        LogLevel.Trace);
                }
                catch (Exception ex)
                {
                    this._monitor.Log(
                        $"[BaselineContentLoader] Failed to read {relative}: {ex.Message}",
                        LogLevel.Warn);
                }
            }

            this._monitor.Log(
                $"[BaselineContentLoader] Loaded {result.Count} baseline NPC entries.",
                LogLevel.Info);

            return result;
        }

        private static JObject? ExtractNpcConfigEntries(JObject root)
        {
            // CP-shape: { Changes: [{ Action: "EditData", Target: "...NpcConfig", Entries: {...} }] }
            if (root["Changes"] is JArray changes)
            {
                var merged = new JObject();
                foreach (var change in changes.OfType<JObject>())
                {
                    var action = change["Action"]?.Value<string>();
                    var target = change["Target"]?.Value<string>();
                    if (!string.Equals(action, "EditData", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.Equals(target, NpcConfigTarget, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (change["Entries"] is not JObject entries)
                        continue;

                    foreach (var prop in entries.Properties())
                        merged[prop.Name] = prop.Value;
                }
                return merged.HasValues ? merged : null;
            }

            // Raw dict shape: { "Generic": {...}, "Alex": {...} }
            return LooksLikeNpcConfigDict(root) ? root : null;
        }

        private static bool LooksLikeNpcConfigDict(JObject root)
        {
            foreach (var prop in root.Properties())
            {
                if (prop.Value is not JObject entry)
                    return false;
                if (entry["Dialogue"] != null || entry["Behavior"] != null
                    || entry["Sprites"] != null || entry["NpcType"] != null)
                    return true;
            }
            return false;
        }

        private static void ResolveI18nTokens(JToken token, ITranslationHelper translation)
        {
            switch (token)
            {
                case JValue value when value.Type == JTokenType.String:
                    var raw = value.Value<string>();
                    if (string.IsNullOrEmpty(raw))
                        return;
                    var match = I18nTokenRegex.Match(raw);
                    if (match.Success)
                        value.Value = translation.Get(match.Groups[1].Value).ToString();
                    break;

                case JObject obj:
                    foreach (var prop in obj.Properties())
                        ResolveI18nTokens(prop.Value, translation);
                    break;

                case JArray arr:
                    foreach (var item in arr)
                        ResolveI18nTokens(item, translation);
                    break;
            }
        }
    }
}
