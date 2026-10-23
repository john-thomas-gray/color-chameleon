using System;
using System.Reflection;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class DeveloperOptionsChecks
    {
        public static void Run()
        {
            CheckBeatCounter();
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

        private static void CheckBeatCounter()
        {
            for (int offset = 0; offset < 4; offset++)
            for (int measure = -2; measure < 4; measure++)
            for (int beat = 0; beat < 4; beat++)
            {
                float position = offset + measure * 4 + beat;
                Check(GameplayMusicPlayer.BeatInMeasure(position, offset) == beat + 1 &&
                    GameplayMusicPlayer.BeatInMeasure(position + .99f, offset) == beat + 1,
                    "The development counter counts 1-2-3-4 from each song's downbeat, including pre-roll");
            }
            foreach (var screen in new[] { new Vector2(320, 320), new Vector2(390, 844), new Vector2(960, 540), new Vector2(1206, 2622) })
            foreach (bool mobile in new[] { false, true })
            {
                float scale = GameSession.GuiScaleFor(mobile, screen.x, screen.y);
                var safe = new Rect(16, screen.y * .02f, screen.x - 32, screen.y * .92f);
                var bounds = new Rect(safe.xMin / scale, (screen.y - safe.yMax) / scale, safe.width / scale, safe.height / scale);
                var rect = GameSession.DevelopmentMetronomeRect(screen, safe, scale);
                Check(rect.xMin >= bounds.xMin && rect.xMax < bounds.xMax && rect.yMin > bounds.yMin && rect.yMax <= bounds.yMax,
                    "The development metronome fits the safe top-right corner at desktop and phone scales");
            }
        }
    }
}
