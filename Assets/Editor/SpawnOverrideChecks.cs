using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SpawnOverrideChecks
    {
        public static void Run()
        {
            bool enabled = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            var random = UnityEngine.Random.state;
            var root = new GameObject("Spawn override fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            try
            {
                UnityEngine.Random.InitState(1234);
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow), Prefab(EnemyColor.Orange));
                SpawnOverride.Enabled = true;
                foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                {
                    if (color == EnemyColor.Orange) continue; // Sparse Orange-only rows are covered by OrangeChecks.
                    SpawnOverride.Types = 1 << (int)color;
                    Check(spawner.SpawnBatch(1) && grid.Model.ColorCount(color) == RunProgress.StandardRowWidth, "Single-type batch bypasses level gate");
                    Check(spawner.TryAdvance() && grid.Model.ColorCount(color) == 2 * RunProgress.StandardRowWidth, "Row uses override");
                    var removed = grid.View(grid.Model.At(2, 0).Id);
                    grid.Unregister(removed); UnityEngine.Object.DestroyImmediate(removed.gameObject);
                    var summoned = spawner.TrySummon(grid.View(grid.Model.At(0, 0).Id));
                    Check(summoned != null && summoned.Color == color && grid.Model.Count == 2 * RunProgress.StandardRowWidth, "Summon uses override");
                    Empty(grid);
                }
                SpawnOverride.Types = (1 << (int)EnemyColor.Red) | (1 << (int)EnemyColor.Yellow);
                Check(spawner.SpawnBatch(GridModel.Rows), "Mixed batch");
                Check(grid.Model.ColorCount(EnemyColor.Red) > 0 && grid.Model.ColorCount(EnemyColor.Yellow) > 0 &&
                    grid.Model.ColorCount(EnemyColor.Red) + grid.Model.ColorCount(EnemyColor.Yellow) == RunProgress.StandardRowWidth * GridModel.Rows, "Only selected types spawn");
                SpawnOverride.Enabled = false;
                Check(grid.Model.ColorCount(EnemyColor.Yellow) > 0, "Changing override does not recolor existing enemies");
                Empty(grid);
                Check(spawner.SpawnBatch(3) && grid.Model.ColorCount(EnemyColor.Red) + grid.Model.ColorCount(EnemyColor.Blue) == 3 * RunProgress.StandardRowWidth,
                    "Disabled override restores normal level-one eligibility");
                bool rejected = false;
                try { SpawnOverride.Types = 0; } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "Empty selection rejected");
                Empty(grid);
                SpawnOverride.Enabled = true;
                SpawnOverride.Types = 1 << (int)EnemyColor.Yellow;
                spawner.ConfigureNewTypes(null, null, null);
                Check(!spawner.SpawnBatch(3) && grid.Model.Count == 0, "Unavailable selection cannot spawn unintended colors");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                SpawnOverride.Types = types;
                SpawnOverride.Enabled = enabled;
                UnityEngine.Random.state = random;
            }
            Debug.Log("Spawn override checks passed: per-type rows, batches, summons, mixed selection, level fallback and empty-pool safety.");
        }
        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Empty(EnemyGrid grid)
        {
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Spawn override check failed: " + message); }
    }
}
