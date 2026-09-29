using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SafeRespawnChecks
    {
        public static void Run()
        {
            foreach (float x in new[] { -2.8f, 0, 2.8f })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 2, GridModel.Rows - 1);
                player.transform.position = new Vector3(x, -4.6f, 0);
                Check(player.Hit(), "Start recovery");
                // Placement uses the live fleet position at respawn, not at death.
                grid.transform.position += Vector3.right * (x - enemy.transform.position.x);
                player.TickSurvival(1.5f);
                Check(player.Alive && player.Invulnerable, "Respawn with protection");
                CheckClearance(grid, player);
                Check(player.HitBounds.min.x >= -3 && player.HitBounds.max.x <= 3, "Respawn fully inside the field");
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                grid.transform.position = Vector3.up * 3;
                for (int col = 0; col < GridModel.Columns; col++)
                    ProgressionChecks.Add(grid, EnemyColor.Blue, col, GridModel.Rows - 1);
                player.transform.position = new Vector3(0, -4.6f, 0);
                player.Hit(); player.TickSurvival(10);
                Check(!player.Alive && player.Lives == PlayerMovement.MaxLives - 1 && !player.Fire(), "Fully blocked row defers respawn without spending lives");
                player.TickSurvival(10);
                Check(!player.Alive && player.Invulnerable, "Blocked recovery cannot expire into danger");
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                    if (enemy.Column >= 3) { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
                player.TickSurvival(.02f);
                Check(player.Alive && player.Invulnerable, "A newly opened gap permits protected respawn");
                CheckClearance(grid, player);
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                var position = player.transform.position;
                player.Hit(); player.TickSurvival(1.5f);
                Check(player.Alive && player.transform.position == position, "Other rows do not relocate respawn");
            });
            Debug.Log("Safe respawn checks passed: live enemy clearance, both edges, blocked rows, delayed protection and unrelated rows.");
        }
        private static void CheckClearance(EnemyGrid grid, PlayerMovement player)
        {
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            {
                var bounds = enemy.HitBounds;
                float gap = Mathf.Max(player.HitBounds.min.x - bounds.max.x, bounds.min.x - player.HitBounds.max.x);
                Check(gap >= bounds.size.x, "At least one enemy width separates the bodies");
            }
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Safe respawn check failed: " + message); }
    }
}
