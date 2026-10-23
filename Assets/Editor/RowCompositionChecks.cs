using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class RowCompositionChecks
    {
        [MenuItem("Candy Cruisers/Run Row Composition Checks")]
        public static void Run()
        {
            bool enabled = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            var random = UnityEngine.Random.state;
            var root = new GameObject("Row composition fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            try
            {
                UnityEngine.Random.InitState(20831);
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow), Prefab(EnemyColor.Orange));
                SpawnOverride.Enabled = true;
                var orders = new HashSet<string>();
                for (int mask = 1; mask < 32; mask++)
                {
                    SpawnOverride.Types = mask;
                    Verify(spawner.PlanOpening(), mask, RunProgress.StandardRowWidth);
                    for (int sample = 0; sample < 40; sample++)
                    {
                        var plan = spawner.PlanBatch(GridModel.Rows);
                        Verify(plan, mask, RunProgress.StandardRowWidth);
                        if (mask == 31) orders.Add(string.Join(",", plan));
                    }
                    Check(spawner.SpawnBatch(2) && spawner.TryAdvance(), "Batch and descending row spawn");
                    var live = new EnemyColor[3 * RunProgress.StandardRowWidth];
                    for (int row = 0; row < 3; row++)
                    for (int column = 0; column < RunProgress.StandardRowWidth; column++)
                        live[row * RunProgress.StandardRowWidth + column] = grid.Model.At(column, row).Color;
                    Verify(live, mask, RunProgress.StandardRowWidth);
                    Empty(grid);
                }
                Check(orders.Count > 1, "Grouped rows retain randomized composition");
                CheckTwoColorPatterns(spawner);
                var session = root.AddComponent<GameSession>();
                session.Progress.Reset(RunProgress.WideRowsStartLevel - 1);
                SpawnOverride.Types = 3;
                for (int sample = 0; sample < 80; sample++)
                    Verify(spawner.PlanBatch(2), SpawnOverride.Types, RunProgress.StandardRowWidth);
                while (session.Progress.Level < RunProgress.WideRowsStartLevel)
                    session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated, true);
                SpawnOverride.Types = 31;
                Check(spawner.CurrentRowWidth == RunProgress.WideRowWidth, "Level seven uses six-wide rows");
                Verify(spawner.PlanBatch(2), SpawnOverride.Types, RunProgress.WideRowWidth);
                Check(spawner.SpawnBatch(1) && grid.Model.Count == RunProgress.WideRowWidth, "Level seven batch spawns six enemies per row");
                Check(spawner.TryAdvance() && grid.Model.Count == 2 * RunProgress.WideRowWidth, "Level seven new top row is six enemies wide");
                for (int column = 0; column < RunProgress.WideRowWidth; column++)
                    Check(grid.Model.At(column, 0) != null, "Level seven top row fills all six columns");
                Empty(grid);
                SpawnOverride.Types = 3;
                var wide = spawner.PlanBatch(2);
                Verify(wide, SpawnOverride.Types, RunProgress.WideRowWidth);
                Check(HasTriple(wide, RunProgress.WideRowWidth), "Six-wide two-color rows retain their longer runs");
                CheckNarrowOrange(spawner, grid, session);
                UnityEngine.Object.DestroyImmediate(session);
                SpawnOverride.Enabled = false;
                Verify(spawner.PlanOpening(), (1 << (int)EnemyColor.Red) | (1 << (int)EnemyColor.Blue), RunProgress.StandardRowWidth);
                Check(spawner.PlanBatch(0) == null && spawner.PlanBatch(GridModel.Rows + 1) == null, "Invalid row counts rejected");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                SpawnOverride.Types = types;
                SpawnOverride.Enabled = enabled;
                UnityEngine.Random.state = random;
            }
            Debug.Log("Row composition checks passed: every eligible-color subset, all six two-color patterns, no narrow triples, single-color fallback, Orange isolation, openings, live batches, descending rows and the six-wide boundary.");
        }

        private static void CheckTwoColorPatterns(EnemyRowSpawner spawner, EnemyColor first = EnemyColor.Red, EnemyColor second = EnemyColor.Blue)
        {
            int mask = (1 << (int)first) | (1 << (int)second);
            SpawnOverride.Types = mask;
            var patterns = new HashSet<string>();
            for (int sample = 0; sample < 80; sample++)
            {
                var plan = spawner.PlanBatch(2);
                Verify(plan, mask, RunProgress.StandardRowWidth);
                for (int start = 0; start < plan.Length; start += RunProgress.StandardRowWidth)
                {
                    string pattern = "";
                    for (int column = 0; column < RunProgress.StandardRowWidth; column++)
                        pattern += plan[start + column] == first ? "A" : "B";
                    patterns.Add(pattern);
                }
            }
            Check(patterns.SetEquals(new[] { "AABAA", "BBABB", "BAABB", "ABBAA", "AABBA", "BBAAB" }),
                "Two-color five-wide rows generate exactly the six requested patterns");
        }

        private static void CheckNarrowOrange(EnemyRowSpawner spawner, EnemyGrid grid, GameSession session)
        {
            session.Progress.Reset(1);
            SpawnOverride.Types = (1 << (int)EnemyColor.Red) | (1 << (int)EnemyColor.Orange);
            var plan = spawner.PlanBatch(1);
            Check(plan[2] == EnemyColor.Orange && !HasTriple(plan, RunProgress.StandardRowWidth),
                "A narrow Orange row separates its four ordinary enemies into two pairs");
            Check(spawner.SpawnBatch(plan) && grid.Fleet.Count == 5 && grid.Fleet.ColorCount(EnemyColor.Orange) == 1,
                "Narrow Orange plan spawns in full while preserving the unique Orange");
            Check(spawner.TryAdvance() && grid.Fleet.Count == 10 && grid.Fleet.ColorCount(EnemyColor.Orange) == 1,
                "With Orange already alive, the only eligible ordinary color may fill the next row");
            for (int column = 0; column < RunProgress.StandardRowWidth; column++)
                Check(grid.Model.At(column, 0).Color == EnemyColor.Red, "Single eligible spawn color uses the allowed longer-run fallback");
            Empty(grid);
        }

        private static bool HasTriple(EnemyColor[] plan, int width)
        {
            for (int start = 0; start < plan.Length; start += width)
            for (int column = 2; column < width; column++)
                if (plan[start + column] == plan[start + column - 1] && plan[start + column] == plan[start + column - 2]) return true;
            return false;
        }

        private static void Verify(EnemyColor[] plan, int mask, int rowWidth)
        {
            Check(plan != null && plan.Length > 0 && plan.Length % rowWidth == 0, "Complete rows");
            for (int start = 0; start < plan.Length; start += rowWidth)
            {
                var groups = new HashSet<EnemyColor>();
                int runs = 0, runLength = 0;
                for (int column = 0; column < rowWidth; column++)
                {
                    var color = plan[start + column];
                    Check((mask & (1 << (int)color)) != 0, "Only eligible colors appear");
                    if (column == 0 || plan[start + column - 1] != color)
                    {
                        runs++; runLength = 0;
                        if (rowWidth == RunProgress.WideRowWidth)
                            Check(!groups.Contains(color), "Six-wide rows retain one contiguous run per color");
                        groups.Add(color);
                    }
                    runLength++;
                    if (rowWidth < RunProgress.WideRowWidth && (mask & (mask - 1)) != 0)
                        Check(runLength <= 2, "Five-wide rows never spawn three matching neighbors when multiple colors are eligible");
                }
                Check(runs <= 3 && groups.Count <= 3, "At most three color runs per row");
            }
        }

        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Empty(EnemyGrid grid)
        {
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Row composition check failed: " + message); }
    }
}
