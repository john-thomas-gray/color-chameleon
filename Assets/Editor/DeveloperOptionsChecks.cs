using System;
using System.Reflection;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class DeveloperOptionsChecks
    {
        public static void Run()
        {
            bool previousInvincible = DeveloperOptions.PlayerInvincible;
            try
            {
                Check(DeveloperOptions.Available, "Developer options are available in editor checks");
                DeveloperOptions.PlayerInvincible = false;
                Check(!DeveloperOptions.PlayerInvincible, "Invincibility starts disabled for the check");
                DeveloperOptions.PlayerInvincible = true;
                Check(DeveloperOptions.PlayerInvincible, "Invincibility can be enabled in development contexts");

                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    grid.transform.position = Vector3.up * 3;
                    var session = grid.gameObject.AddComponent<GameSession>();
                    session.Configure(player);
                    if (!Application.isPlaying) Invoke(session, "OnEnable");
                    int lives = player.Lives;
                    Check(!player.Hit() && player.Lives == lives && player.Alive && !player.Invulnerable,
                        "Direct hits do not spend lives while invincible");

                    var enemy = ProgressionChecks.Add(grid, EnemyColor.Red, 2, GridModel.Rows - 1);
                    player.transform.position = new Vector3(enemy.transform.position.x, -4.6f, 0);
                    Check(!session.CheckPlayerContact() && player.Lives == lives && session.State == GameSession.RunState.Playing,
                        "Body contact does not damage or stop the run while invincible");

                    for (int col = 0; col < grid.GetComponent<EnemyRowSpawner>().CurrentRowWidth; col++)
                        if (grid.Model.At(col, GridModel.Rows - 1) == null)
                            ProgressionChecks.Add(grid, EnemyColor.Blue, col, GridModel.Rows - 1);
                    Check(!session.CheckPlayerContact() && player.Lives == lives && session.State == GameSession.RunState.Playing,
                        "Full player-row contact cannot force game over while invincible");

                    Check(session.HandleMenuKey(KeyCode.I) && !DeveloperOptions.PlayerInvincible,
                        "Developer menu key toggles invincibility off");
                    Check(player.Hit() && player.Lives == lives - 1, "Damage resumes after disabling invincibility");
                    if (!Application.isPlaying) Invoke(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                });
            }
            finally
            {
                DeveloperOptions.PlayerInvincible = previousInvincible;
            }
            Debug.Log("Developer options checks passed: player invincibility blocks direct hits, contact and full-row defeat, and toggles from the menu key.");
        }

        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Developer options check failed: " + message); }
    }
}
