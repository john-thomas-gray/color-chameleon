using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CandyCruisers
{
    // The JSON file is the authoring source; this is its in-memory representation.
    public sealed class LevelCatalog
    {
        public const string FilePath = "Assets/Resources/LevelMap.json";
        public LevelDefinition[] levels = Array.Empty<LevelDefinition>();
        public LevelDefinition ForLevel(int number) => number < 1 || levels == null || levels.Length == 0 ? null :
            levels[Math.Min(number, levels.Length) - 1];

        public List<string> ValidationErrors()
        {
            var errors = new List<string>();
            if (levels == null || levels.Length == 0) errors.Add("The level map needs level 1.");
            else for (int index = 0; index < levels.Length; index++)
            {
                if (levels[index] == null) { errors.Add("Assign settings to Level " + (index + 1) + "."); continue; }
                foreach (string error in levels[index].ValidationErrors()) errors.Add("Level " + (index + 1) + ": " + error);
            }
            return errors;
        }

        public static LevelCatalog Load(TextAsset asset)
        {
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) return Parse(File.ReadAllText(path));
#endif
            return Parse(asset.text);
        }

        public static LevelCatalog Parse(string json)
        {
            try
            {
                var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Fields(root, "schemaVersion", "levels");
                if (Integer(root, "schemaVersion") != 1) throw new FormatException("schemaVersion must be 1.");
                if (!(root["levels"] is JObject entries) || entries.Count == 0) throw new FormatException("levels must be an object containing level 1.");
                var catalog = new LevelCatalog { levels = new LevelDefinition[entries.Count] };
                foreach (var entry in entries.Properties())
                {
                    if (!int.TryParse(entry.Name, out int number) || number < 1 || number > entries.Count || entry.Name != number.ToString())
                        throw new FormatException("Level keys must be consecutive numbers starting at 1.");
                    if (!(entry.Value is JObject data)) throw new FormatException("Level " + number + " must be an object.");
                    Fields(data, "enemiesToDefeat", "numberOfColors", "possibleColors", "requiredColors", "backgroundImage",
                        "possibleSoundtrackTracks", "fleetSpawns", "lineSpawns");
                    var level = new LevelDefinition {
                        enemiesToDefeat = Integer(data, "enemiesToDefeat"), numberOfColors = Integer(data, "numberOfColors"),
                        possibleColors = Colors(data, "possibleColors"), requiredColors = Colors(data, "requiredColors"),
                        possibleSoundtrackTracks = Strings(data, "possibleSoundtrackTracks"),
                        fleetSpawns = Strings(data, "fleetSpawns"), lineSpawns = Strings(data, "lineSpawns")
                    };
                    var background = data["backgroundImage"];
                    if (background != null && background.Type != JTokenType.String && background.Type != JTokenType.Null)
                        throw new FormatException("backgroundImage must be a resource name or an empty string.");
                    string image = background?.Value<string>();
                    if (!string.IsNullOrEmpty(image))
                    {
                        level.backgroundImage = Resources.Load<Sprite>(image);
                        if (level.backgroundImage == null) throw new FormatException("Level " + number + ": background sprite '" + image + "' was not found under Resources.");
                    }
                    catalog.levels[number - 1] = level;
                }
                var errors = catalog.ValidationErrors();
                if (errors.Count > 0) throw new FormatException(string.Join("\n", errors));
                return catalog;
            }
            catch (Exception error) when (error is JsonException || error is FormatException || error is OverflowException)
            { throw new FormatException("LevelMap.json: " + error.Message, error); }
        }

        private static void Fields(JObject data, params string[] allowed)
        {
            foreach (var property in data.Properties())
                if (Array.IndexOf(allowed, property.Name) < 0) throw new FormatException("Unknown setting '" + property.Path + "'.");
        }
        private static int Integer(JObject data, string name)
        {
            if (data[name]?.Type != JTokenType.Integer) throw new FormatException(name + " must be an integer.");
            return data[name].Value<int>();
        }
        private static string[] Strings(JObject data, string name)
        {
            if (!(data[name] is JArray array)) throw new FormatException(name + " must be a list of strings.");
            var result = new string[array.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (array[i].Type != JTokenType.String) throw new FormatException(name + "[" + i + "] must be a string.");
                result[i] = array[i].Value<string>();
            }
            return result;
        }
        private static EnemyColor[] Colors(JObject data, string name)
        {
            var names = Strings(data, name); var result = new EnemyColor[names.Length];
            for (int i = 0; i < names.Length; i++)
                if (!Enum.TryParse(names[i], true, out result[i]) || !Enum.IsDefined(typeof(EnemyColor), result[i]) ||
                    !string.Equals(names[i], result[i].ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new FormatException(name + " contains unknown color '" + names[i] + "'.");
            return result;
        }
    }
}
