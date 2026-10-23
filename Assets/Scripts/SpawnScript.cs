using System;
using System.Collections.Generic;

namespace CandyCruisers
{
    public static class SpawnScript
    {
        public static bool TryColor(char symbol, out EnemyColor color)
        {
            switch (char.ToLowerInvariant(symbol))
            {
                case 'r': color = EnemyColor.Red; return true;
                case 'b': color = EnemyColor.Blue; return true;
                case 'g': color = EnemyColor.Green; return true;
                case 'p': color = EnemyColor.Purple; return true;
                case 'y': color = EnemyColor.Yellow; return true;
                case 'o': color = EnemyColor.Orange; return true;
                default: color = default; return false;
            }
        }

        // The small, bounded search guarantees valid assignments without random retry failures.
        public static FleetPlan Plan(LevelDefinition definition, string pattern, bool randomize = true)
        {
            var mandatory = new HashSet<EnemyColor>(definition.requiredColors);
            var digits = new List<char>();
            var scripts = new List<string> { pattern }; scripts.AddRange(definition.lineSpawns);
            foreach (string script in scripts)
            foreach (char raw in script)
            {
                char symbol = char.ToLowerInvariant(raw);
                if (TryColor(symbol, out var color)) mandatory.Add(color);
                else if (symbol >= '1' && symbol <= '6' && !digits.Contains(symbol)) digits.Add(symbol);
            }
            if (mandatory.Count > definition.numberOfColors || digits.Count > definition.numberOfColors) return null;
            var pool = LevelDefinition.UniqueColors(definition.possibleColors);
            if (randomize) Shuffle(pool);
            var chosen = new List<EnemyColor>();
            FleetPlan result = null;
            bool Choose(int start)
            {
                if (chosen.Count == definition.numberOfColors)
                {
                    foreach (var color in mandatory) if (!chosen.Contains(color)) return false;
                    var bindings = new Dictionary<char, EnemyColor>();
                    var used = new HashSet<EnemyColor>();
                    bool Bind(int index)
                    {
                        if (index == digits.Count)
                        {
                            var cells = Decode(pattern, bindings);
                            var actual = new HashSet<EnemyColor>(); int orange = 0;
                            foreach (var cell in cells) if (cell.HasValue)
                            { actual.Add(cell.Value); if (cell == EnemyColor.Orange) orange++; }
                            if (actual.Count == 0 || orange > 1) return false;
                            foreach (var required in definition.requiredColors) if (!actual.Contains(required)) return false;
                            foreach (string line in definition.lineSpawns)
                            {
                                int lineOrange = 0;
                                foreach (var cell in Decode(line, bindings)) if (cell == EnemyColor.Orange) lineOrange++;
                                if (lineOrange > 1) return false;
                            }
                            result = new FleetPlan(cells, pattern.Split('/')[0].Length, chosen, bindings, definition.lineSpawns);
                            return true;
                        }
                        foreach (var color in chosen)
                        {
                            if (!used.Add(color)) continue;
                            bindings[digits[index]] = color;
                            if (Bind(index + 1)) return true;
                            used.Remove(color); bindings.Remove(digits[index]);
                        }
                        return false;
                    }
                    return Bind(0);
                }
                for (int i = start; i <= pool.Count - (definition.numberOfColors - chosen.Count); i++)
                {
                    chosen.Add(pool[i]);
                    if (Choose(i + 1)) return true;
                    chosen.RemoveAt(chosen.Count - 1);
                }
                return false;
            }
            Choose(0);
            return result;
        }

        public static EnemyColor?[] Decode(string pattern, IReadOnlyDictionary<char, EnemyColor> bindings)
        {
            var result = new List<EnemyColor?>();
            foreach (char raw in pattern)
            {
                char symbol = char.ToLowerInvariant(raw);
                if (symbol == '/') continue;
                if (symbol == '0') result.Add(null);
                else if (TryColor(symbol, out var color)) result.Add(color);
                else if (bindings.TryGetValue(symbol, out color)) result.Add(color);
                else throw new FormatException("Unassigned spawn character '" + raw + "'.");
            }
            return result.ToArray();
        }

        private static void Shuffle<T>(List<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            { int j = UnityEngine.Random.Range(0, i + 1); var swap = items[i]; items[i] = items[j]; items[j] = swap; }
        }
    }
}
