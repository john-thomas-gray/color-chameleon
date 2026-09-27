using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class RowCompositionChecks
    {
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
                var session = root.AddComponent<GameSession>();
                while (session.Progress.Level < RunProgress.WideRowsStartLevel)
                    session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated, false);
                SpawnOverride.Types = 31;
                Check(spawner.CurrentRowWidth == RunProgress.WideRowWidth, "Level seven uses six-wide rows");
                Verify(spawner.PlanBatch(2), SpawnOverride.Types, RunProgress.WideRowWidth);
                Check(spawner.SpawnBatch(1) && grid.Model.Count == RunProgress.WideRowWidth, "Level seven batch spawns six enemies per row");
                Check(spawner.TryAdvance() && grid.Model.Count == 2 * RunProgress.WideRowWidth, "Level seven new top row is six enemies wide");
                for (int column = 0; column < RunProgress.WideRowWidth; column++)
                    Check(grid.Model.At(column, 0) != null, "Level seven top row fills all six columns");
                Empty(grid);
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
            Debug.Log("Row composition checks passed: every eligible-color subset, planned rows, openings, live batches, six-wide level-seven rows and descending rows.");
        }

        private static void Verify(EnemyColor[] plan, int mask, int rowWidth)
        {
            Check(plan != null && plan.Length > 0 && plan.Length % rowWidth == 0, "Complete rows");
            for (int start = 0; start < plan.Length; start += rowWidth)
            {
                var groups = new HashSet<EnemyColor>();
                for (int column = 0; column < rowWidth; column++)
                {
                    var color = plan[start + column];
                    Check((mask & (1 << (int)color)) != 0, "Only eligible colors appear");
                    if (column == 0 || plan[start + column - 1] != color)
                        Check(groups.Add(color), "Each color occupies one contiguous run");
                }
                Check(groups.Count <= 3, "At most three colors per row");
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
