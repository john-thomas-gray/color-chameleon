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
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow));
                SpawnOverride.Enabled = true;
                var orders = new HashSet<string>();
                for (int mask = 1; mask < 32; mask++)
                {
                    SpawnOverride.Types = mask;
                    Verify(spawner.PlanOpening(), mask);
                    for (int sample = 0; sample < 40; sample++)
                    {
                        var plan = spawner.PlanBatch(GridModel.Rows);
                        Verify(plan, mask);
                        if (mask == 31) orders.Add(string.Join(",", plan));
                    }
                    Check(spawner.SpawnBatch(2) && spawner.TryAdvance(), "Batch and descending row spawn");
                    var live = new EnemyColor[3 * GridModel.Columns];
                    for (int row = 0; row < 3; row++)
                    for (int column = 0; column < GridModel.Columns; column++)
                        live[row * GridModel.Columns + column] = grid.Model.At(column, row).Color;
                    Verify(live, mask);
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                    { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
                }
                Check(orders.Count > 1, "Grouped rows retain randomized composition");
                SpawnOverride.Enabled = false;
                Verify(spawner.PlanOpening(), (1 << (int)EnemyColor.Red) | (1 << (int)EnemyColor.Blue));
                Check(spawner.PlanBatch(0) == null && spawner.PlanBatch(GridModel.Rows + 1) == null, "Invalid row counts rejected");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                SpawnOverride.Types = types;
                SpawnOverride.Enabled = enabled;
                UnityEngine.Random.state = random;
            }
            Debug.Log("Row composition checks passed: every eligible-color subset, 12,400 planned rows, openings, live batches and descending rows.");
        }

        private static void Verify(EnemyColor[] plan, int mask)
        {
            Check(plan != null && plan.Length > 0 && plan.Length % GridModel.Columns == 0, "Complete five-enemy rows");
            for (int start = 0; start < plan.Length; start += GridModel.Columns)
            {
                var groups = new HashSet<EnemyColor>();
                for (int column = 0; column < GridModel.Columns; column++)
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
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Row composition check failed: " + message); }
    }
}
