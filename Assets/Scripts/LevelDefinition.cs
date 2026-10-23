using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // Runtime data loaded from LevelMap.json, not a Unity authoring asset.
    public sealed class LevelDefinition
    {
        public int enemiesToDefeat = 20;
        public int numberOfColors = 2;
        public EnemyColor[] possibleColors = { EnemyColor.Red, EnemyColor.Blue };
        public EnemyColor[] requiredColors = Array.Empty<EnemyColor>();
        public Sprite backgroundImage;
        public string[] possibleSoundtrackTracks = Array.Empty<string>();
        public string[] fleetSpawns = { "11221/22112" };
        public string[] lineSpawns = { "11221", "22112" };

        public List<string> ValidationErrors()
        {
            var errors = new List<string>();
            var possible = UniqueColors(possibleColors);
            var required = UniqueColors(requiredColors);
            if (enemiesToDefeat < 1) errors.Add("enemiesToDefeat must be at least 1.");
            if (possibleColors == null || possible.Count != possibleColors.Length)
                errors.Add("possibleColors must contain valid, unique colors.");
            if (requiredColors == null || required.Count != requiredColors.Length)
                errors.Add("requiredColors must contain valid, unique colors.");
            if (numberOfColors < 1 || numberOfColors > possible.Count)
                errors.Add("numberOfColors must be between 1 and the size of possibleColors.");
            if (required.Count > numberOfColors) errors.Add("requiredColors exceeds numberOfColors.");
            foreach (var color in required)
                if (!possible.Contains(color)) errors.Add(color + " is required but is missing from possibleColors.");
            if (possible.Contains(EnemyColor.Orange) && numberOfColors < 2)
                errors.Add("Orange needs another color because only one Orange can be alive.");
            var tracks = new HashSet<string>(StringComparer.Ordinal);
            foreach (string track in possibleSoundtrackTracks ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(track) || !tracks.Add(track)) errors.Add("Soundtrack names must be nonempty and unique.");
            if (fleetSpawns == null || fleetSpawns.Length == 0) errors.Add("fleetSpawns needs at least one fleet.");
            int width = 0;
            ValidatePatterns(fleetSpawns, false, possible, errors, ref width);
            ValidatePatterns(lineSpawns, true, possible, errors, ref width);
            if (errors.Count == 0)
                for (int i = 0; i < fleetSpawns.Length; i++)
                    if (SpawnScript.Plan(this, fleetSpawns[i], false) == null)
                        errors.Add("fleetSpawns[" + i + "] cannot satisfy the color count, required colors, numbered groups and one-Orange limit.");
            return errors;
        }

        private static void ValidatePatterns(string[] patterns, bool singleLine, List<EnemyColor> possible,
            List<string> errors, ref int width)
        {
            if (patterns == null) { errors.Add((singleLine ? "lineSpawns" : "fleetSpawns") + " must be a list."); return; }
            for (int index = 0; index < patterns.Length; index++)
            {
                string label = (singleLine ? "lineSpawns[" : "fleetSpawns[") + index + "]";
                string pattern = patterns[index];
                if (string.IsNullOrEmpty(pattern)) { errors.Add(label + " must not be empty."); continue; }
                string[] rows = pattern.Split('/');
                if (rows.Length > GridModel.Rows || singleLine && rows.Length != 1) errors.Add(label + " has an invalid row count.");
                bool occupied = false;
                foreach (string row in rows)
                {
                    if (row.Length < 1 || row.Length > GridModel.Columns) errors.Add(label + " rows must have 1–6 positions.");
                    if (width == 0) width = row.Length;
                    if (width != row.Length) errors.Add(label + " must use the same row width as the rest of this level.");
                    int orange = 0;
                    foreach (char raw in row)
                    {
                        char symbol = char.ToLowerInvariant(raw);
                        if (symbol == '0') continue;
                        occupied = true;
                        if (symbol >= '1' && symbol <= '6') continue;
                        if (!SpawnScript.TryColor(symbol, out var color)) errors.Add(label + " contains unknown character '" + raw + "'.");
                        else
                        {
                            if (!possible.Contains(color)) errors.Add(label + " uses " + color + " outside possibleColors.");
                            if (color == EnemyColor.Orange) orange++;
                        }
                    }
                    if (orange > 1) errors.Add(label + " cannot spawn more than one Orange.");
                }
                if (!singleLine && !occupied) errors.Add(label + " must contain at least one enemy.");
                if (!singleLine && pattern.ToLowerInvariant().Split('o').Length > 2) errors.Add(label + " cannot spawn more than one Orange.");
            }
        }

        public static List<EnemyColor> UniqueColors(EnemyColor[] source)
        {
            var result = new List<EnemyColor>();
            foreach (var color in source ?? Array.Empty<EnemyColor>())
                if (Enum.IsDefined(typeof(EnemyColor), color) && !result.Contains(color)) result.Add(color);
            return result;
        }
    }
}
