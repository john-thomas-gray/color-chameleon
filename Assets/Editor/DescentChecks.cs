using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class DescentChecks
    {
        public static void Run()
        {
            var model = new GridModel();
            model.TryAdd(1, EnemyColor.Red, 0, 0);
            model.TryAdd(2, EnemyColor.Blue, GridModel.Columns - 1, GridModel.Rows - 2);
            Check(model.TryDescend(), "Sparse descent succeeds");
            Check(model.At(0, 0) == null && model.At(0, 1).Id == 1 && model.At(GridModel.Columns - 1, GridModel.Rows - 1).Id == 2,
                "Identity and holes shift one row");
            Check(!model.TryDescend() && model.At(0, 1).Id == 1 && model.Count == 2,
                "Blocked descent leaves every cell unchanged");
            model.Remove(2);
            Check(model.TryDescend() && model.At(0, 2).Id == 1, "Clearing bottom enables descent");
            var root = new GameObject("Descent fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blue Enemy.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Enemy.prefab"));
                Check(spawner.TryAdvance() && grid.Model.Count == RunProgress.StandardRowWidth, "Empty field gets one row on turn");
                int first = grid.Model.At(0, 0).Id;
                for (int i = 1; i < GridModel.Rows; i++) Check(spawner.TryAdvance(), "Successive rows descend");
                int standardCapacity = RunProgress.StandardRowWidth * GridModel.Rows;
                Check(grid.Model.Count == standardCapacity && grid.Model.At(0, GridModel.Rows - 1).Id == first, "Original identity reaches bottom");
                Check(!spawner.TryAdvance() && grid.Model.Count == standardCapacity && root.transform.childCount == standardCapacity,
                    "Full field neither overwrites nor creates extra views");
                Check(grid.Model.ColorCount(EnemyColor.Red) + grid.Model.ColorCount(EnemyColor.Blue) == standardCapacity,
                    "Rows only use level-one colors");
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                {
                    Check(grid.Model.At(enemy.Column, enemy.Row).Id == enemy.Id, "Model and views agree");
                    Check(Vector3.Distance(enemy.transform.localPosition, grid.CellPosition(enemy.Column, enemy.Row)) < 0.0001f,
                        "Visual positions agree with grid");
                }
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Row == GridModel.Rows - 1))
                {
                    grid.Unregister(enemy);
                    UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                }
                Check(spawner.TryAdvance() && grid.Model.Count == standardCapacity, "Spawning resumes after bottom is cleared");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("Descent checks passed: boundaries, sparse shifts, identity, row creation, colors, capacity and resume.");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Descent check failed: " + message); }
    }
}
