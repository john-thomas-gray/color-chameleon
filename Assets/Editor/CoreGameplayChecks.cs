using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class CoreGameplayChecks
    {
        public static void Run()
        {
            var model = new GridModel();
            Check(model.TryAdd(1, EnemyColor.Blue, 0, 0), "Add corner enemy");
            Check(!model.TryAdd(1, EnemyColor.Red, 1, 0), "Reject duplicate identity");
            Check(!model.TryAdd(2, EnemyColor.Red, 0, 0), "Reject occupied cell");
            Check(!model.TryAdd(2, EnemyColor.Red, GridModel.Columns, 0) && !model.TryAdd(2, EnemyColor.Red, 0, GridModel.Rows), "Reject out of bounds");
            Check(model.TryAdd(2, EnemyColor.Red, 1, 0) && model.TryAdd(3, EnemyColor.Blue, 0, 1), "Add neighbors");
            Check(model.TryAdd(4, EnemyColor.Red, 1, 1), "Add diagonal");
            Check(model.Neighbors(0, 0).Select(e => e.Id).OrderBy(id => id).SequenceEqual(new[] { 2, 3 }), "Orthogonal neighbors only");
            Check(!model.TryMove(1, 1, 0) && model.At(0, 0).Id == 1, "Occupied move preserves source");
            Check(model.TryMove(1, GridModel.Columns - 1, 9) && model.At(0, 0) == null && model.At(GridModel.Columns - 1, 9).Id == 1, "Move to last cell");
            Check(model.ColorCount(EnemyColor.Blue) == 2 && model.ColorCount(EnemyColor.Red) == 2, "Accurate counts");
            Check(model.SetColor(1, EnemyColor.Red) && model.ColorCount(EnemyColor.Blue) == 1 && model.ColorCount(EnemyColor.Red) == 3, "Recolor counts");
            Check(model.Remove(3) && !model.Remove(3), "Idempotent removal");
            Check(model.AvailableColors().SequenceEqual(new[] { EnemyColor.Red }), "Removed colors excluded");
            Check(model.Count == 3, "Total occupancy");
            var full = new GridModel();
            for (int row = 0; row < GridModel.Rows; row++)
            for (int col = 0; col < GridModel.Columns; col++) Check(full.TryAdd(row * GridModel.Columns + col, EnemyColor.Blue, col, row), "Fill every cell");
            Check(full.Count == GridModel.Columns * GridModel.Rows && full.Neighbors(3, 5).Count() == 4, "Full grid and interior neighbors");
            Check(Mathf.Abs(PlayerMovement.Wrap(3.2f) + 2.8f) < 0.0001f, "Right wrap");
            Check(Mathf.Abs(PlayerMovement.Wrap(-3.2f) - 2.8f) < 0.0001f, "Left wrap");
            Check(Mathf.Abs(PlayerMovement.Wrap(63.2f) + 2.8f) < 0.0001f, "Multi-width overshoot");
            Check(Mathf.Abs(PlayerMovement.MoveTowardWrapped(2.9f, -2.9f, 0.3f) + 2.9f) < 0.0001f, "Touch shortest path");
            Check(PlayerMovement.IsTap(0.2f, 5, 390), "Tap accepted");
            Check(!PlayerMovement.IsTap(0.5f, 5, 390) && !PlayerMovement.IsTap(0.2f, 50, 390), "Hold and drag do not fire");

            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            grid.RegisterChildren();
            Check(grid.Model.Count == 2 * RunProgress.StandardRowWidth && grid.Model.ColorCount(EnemyColor.Red) == RunProgress.StandardRowWidth && grid.Model.ColorCount(EnemyColor.Blue) == RunProgress.StandardRowWidth, "Scene registration");
            grid.RegisterChildren();
            Check(grid.Model.Count == 2 * RunProgress.StandardRowWidth, "Repeat registration does not double-count");
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            var tongue = player.GetComponentInChildren<TongueShot>();
            player.RefreshColor();
            Check(player.ReadyColor.HasValue, "Initial available shot color");
            Check(player.Fire() && !player.Fire(), "Only one active tongue");
            EnemyColor shotColor = tongue.ShotColor;
            tongue.Tick(0.25f);
            Check(tongue.Length > 0 && !tongue.Retracting, "Tongue extends");
            var position = player.transform.position;
            player.Move(1, 0.25f);
            Check(player.transform.position.x > position.x && tongue.ShotColor == shotColor, "Movement during shot preserves color");
            tongue.Tick(0.5f);
            Check(tongue.Retracting && tongue.Length > 0, "Tongue retracts");
            tongue.Tick(10);
            Check(!tongue.Active && tongue.Length == 0 && !tongue.GetComponent<LineRenderer>().enabled, "Long frame completes and hides shot");
            Check(player.Fire(), "Can fire again after return");
            tongue.Tick(10);
            player.transform.position = position;

            var enemies = grid.GetComponentsInChildren<GridEnemy>();
            foreach (var enemy in enemies) grid.SetColor(enemy.Id, EnemyColor.Red);
            player.RefreshColor(true);
            Check(player.ReadyColor == EnemyColor.Red, "Only represented color selected");
            foreach (var enemy in enemies) grid.Unregister(enemy);
            player.RefreshColor();
            Check(player.ReadyColor == EnemyColor.Red && !player.Fire(), "Empty grid preserves color but disables firing");
            foreach (var enemy in enemies)
            {
                enemy.Configure(enemy.name.StartsWith("Blue") ? EnemyColor.Blue : EnemyColor.Red, enemy.Column, enemy.Row);
                grid.Register(enemy);
                grid.SetColor(enemy.Id, enemy.Color);
            }
            player.RefreshColor();
            Debug.Log("Core gameplay checks passed: occupancy, neighbors, color changes, wrapping, gestures, firing and reset.");
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Core gameplay check failed: " + message); }
    }
}
