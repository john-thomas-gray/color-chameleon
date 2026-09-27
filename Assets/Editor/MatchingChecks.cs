using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MatchingChecks
    {
        public static void Run()
        {
            var model = new GridModel();
            model.TryAdd(1, EnemyColor.Red, 0, 0);
            model.TryAdd(2, EnemyColor.Red, 1, 0);
            model.TryAdd(3, EnemyColor.Red, 0, 1);
            model.TryAdd(4, EnemyColor.Red, 1, 1);
            model.TryAdd(5, EnemyColor.Blue, 2, 1);
            model.TryAdd(6, EnemyColor.Red, 2, 2);
            model.TryAdd(7, EnemyColor.Red, GridModel.Columns - 1, 9);
            Check(model.ClearMatchingChain(1, EnemyColor.Blue).Count == 0 && model.Count == 7, "Mismatch preserves all enemies");
            Check(model.ClearMatchingChain(1, EnemyColor.Red).OrderBy(id => id).SequenceEqual(new[] { 1, 2, 3, 4 }), "Cyclic group clears once per enemy");
            Check(model.Count == 3 && model.ColorCount(EnemyColor.Red) == 2 && model.ColorCount(EnemyColor.Blue) == 1, "Counts update after chain");
            Check(model.At(0, 0) == null && model.At(1, 1) == null, "Cleared cells are empty");
            Check(model.At(2, 2) != null && model.At(GridModel.Columns - 1, 9) != null, "Diagonal and disconnected enemies survive");
            Check(model.ClearMatchingChain(1, EnemyColor.Red).Count == 0, "Repeated hit is harmless");
            var full = new GridModel();
            for (int row = 0; row < GridModel.Rows; row++)
            for (int col = 0; col < GridModel.Columns; col++) full.TryAdd(row * GridModel.Columns + col, EnemyColor.Blue, col, row);
            Check(full.ClearMatchingChain(0, EnemyColor.Blue).Count == GridModel.Columns * GridModel.Rows && full.Count == 0 && full.AvailableColors().Count == 0, "Full fleet chain clears without stale colors");

            foreach (float frameTime in new[] { 1f / 30, 1f / 60, 1f / 120, 2f }) CheckShot(frameTime);
            Debug.Log("Matching checks passed: chains, diagonals, mismatches, count cleanup, nearest hits, moving formation and frame-rate coverage.");
        }

        private static void CheckShot(float frameTime)
        {
            var root = new GameObject("Matching fixture");
            var shotObject = new GameObject("Matching tongue", typeof(LineRenderer), typeof(TongueShot));
            try
            {
                var grid = root.AddComponent<EnemyGrid>();
                root.transform.position = new Vector3(0.4f, 3, 0);
                var sprite = GameObject.Find("Enemy Grid").GetComponentInChildren<SpriteRenderer>().sprite;
                var near = Add(grid, sprite, EnemyColor.Red, 0, 1);
                Add(grid, sprite, EnemyColor.Red, 1, 1);
                var far = Add(grid, sprite, EnemyColor.Red, 0, 0);
                // A gap separates the farther target from the first chain.
                grid.TryMove(far.Id, 0, 3);
                grid.TryMove(near.Id, 0, 0);
                var neighbor = grid.GetComponentsInChildren<GridEnemy>().First(e => e.Column == 1);
                grid.TryMove(neighbor.Id, 1, 0);
                // Fire upward from below: the lower, isolated Red is hit first.
                Add(grid, sprite, EnemyColor.Blue, 0, 4);
                shotObject.transform.position = new Vector3(far.transform.position.x, -4, 0);
                Check(grid.FindMatchingHit(shotObject.transform.position, 0, 12, EnemyColor.Red, 0.055f, out int id, out _)
                    && id == far.Id, "Nearest matching target despite mismatched blocker");
                root.transform.position += Vector3.right;
                Check(!grid.FindMatchingHit(shotObject.transform.position, 0, 12, EnemyColor.Red, 0.055f, out _, out _), "Hit queries follow moving grid");
                root.transform.position -= Vector3.right;
                var tongue = shotObject.GetComponent<TongueShot>();
                Check(tongue.TryFire(EnemyColor.Red, 12), "Fixture fires");
                for (int i = 0; i < 1000 && tongue.Active; i++) tongue.Tick(frameTime, grid);
                Check(!tongue.Active && grid.Model.Count == 3 && grid.Model.ColorCount(EnemyColor.Red) == 2, "One hit per shot even with a long frame");
                Check(grid.Model.At(0, 3) == null && grid.Model.At(0, 4).Color == EnemyColor.Blue, "Mismatch passed through and target removed");
                Check(tongue.TryFire(EnemyColor.Red, 12), "Second shot fires");
                for (int i = 0; i < 1000 && tongue.Active; i++) tongue.Tick(frameTime, grid);
                Check(grid.Model.Count == 1 && grid.Model.ColorCount(EnemyColor.Red) == 0, "Second shot clears connected pair");
                Check(grid.GetComponentsInChildren<GridEnemy>().Length == 1, "Views removed with logical cells");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shotObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GridEnemy Add(EnemyGrid grid, Sprite sprite, EnemyColor color, int column, int row)
        {
            var obj = new GameObject("Fixture enemy", typeof(SpriteRenderer), typeof(GridEnemy));
            obj.transform.SetParent(grid.transform, false);
            obj.transform.localScale = Vector3.one * 0.5f;
            obj.GetComponentInChildren<SpriteRenderer>().sprite = sprite;
            var enemy = obj.GetComponent<GridEnemy>();
            enemy.Configure(color, column, row);
            Check(grid.Register(enemy), "Fixture registration");
            return enemy;
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Matching check failed: " + message); }
    }
}
