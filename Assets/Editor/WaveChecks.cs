using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class WaveChecks
    {
        public static void Run()
        {
            var root = new GameObject("Wave fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var movement = root.GetComponent<EnemyGridMovement>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blue Enemy.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Enemy.prefab"));
                int blocked = 0;
                spawner.BottomReached += () => blocked++;
                Check(!spawner.SpawnBatch(0) && !spawner.SpawnBatch(11), "Invalid batch sizes rejected");
                Check(spawner.SpawnBatch(3) && grid.Model.Count == 3 * GridModel.Columns, "Fresh batch has three complete rows");
                Check(!spawner.SpawnBatch(3) && grid.Model.Count == 3 * GridModel.Columns, "Cannot duplicate batch in occupied grid");
                movement.ResetSweep();
                movement.Tick(1);
                movement.ResetSweep();
                Check(root.transform.localPosition == Vector3.zero, "Reset restores starting position");
                int turns = 0;
                movement.SweepEnded += () => turns++;
                movement.Tick(1);
                Check(turns == 0, "Reset gives fresh sweep timer");
                movement.Tick(2.75);
                Check(turns == 1, "First post-reset sweep ends normally");
                for (int i = 0; i < 7; i++) Check(spawner.TryAdvance(), "Fill grid toward bottom");
                Check(!spawner.TryAdvance() && blocked == 1 && grid.Model.Count == GridModel.Columns * GridModel.Rows, "Blocked descent signals bottom without mutation");
                int clears = 0;
                grid.FleetCleared += () => clears++;
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>()) grid.SetColor(enemy.Id, EnemyColor.Red);
                int id = grid.Model.At(0, 0).Id;
                Check(grid.ClearMatchingChain(id, EnemyColor.Red) == GridModel.Columns * GridModel.Rows && clears == 1, "One fleet-clear event for final group");
                Check(grid.ClearMatchingChain(id, EnemyColor.Red) == 0 && clears == 1, "Repeated hit cannot schedule another batch");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("Wave checks passed: batches, reset, boundary event and exactly-once fleet clear.");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Wave check failed: " + message); }
    }
}
