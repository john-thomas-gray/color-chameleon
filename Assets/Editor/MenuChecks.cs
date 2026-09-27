using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MenuChecks
    {
        public static void Run()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                session.StartLevel = RunProgress.MaxMenuStartLevel + 1;
                Check(session.StartLevel == RunProgress.MaxMenuStartLevel, "Menu start level clamps to the maximum");
                session.StartLevel = RunProgress.MinLevel - 1;
                Check(session.StartLevel == RunProgress.MinLevel, "Menu start level clamps to the minimum");
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                var ability = red.GetComponent<EnemyAbilities>();
                player.RefreshColor();
                Check(player.Fire(), "Shot in flight before pause");
                tongue.Tick(.1f);
                float length = tongue.Length, cooldown = ability.CooldownRemaining;
                float timeScale = Time.timeScale;
                try
                {
                    session.Pause();
                    session.Pause();
                    Check(session.IsPaused && Time.timeScale == 0 && player.ControlsLocked && tongue.Active,
                        "Pause freezes time and controls without canceling the tongue");
                    ability.Tick(100);
                    session.Tick(100);
                    Check(tongue.Length == length && ability.CooldownRemaining == cooldown && !player.Fire(),
                        "Paused run retains tongue, ability timer and shot lock");
                    typeof(GameSession).GetMethod("ActivateMenuTouch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(session, new object[] { new Vector2(Screen.width / 2f, Screen.height / 2f) });
                    Check(!session.IsPaused && Time.timeScale == timeScale && tongue.Active &&
                        !player.ControlsLocked && !ability.Suspended, "Resume restores playing state and time scale");
                    tongue.Cancel();
                    grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                    Check(session.State == GameSession.RunState.Refilling, "Clear starts refill");
                    session.Pause();
                    session.Tick(100);
                    Check(grid.Model.Count == 0 && session.State == GameSession.RunState.Refilling, "Pause freezes refill delay");
                    session.Resume();
                    Check(!grid.enabled && !grid.GetComponent<EnemyGridMovement>().enabled && !player.ControlsLocked,
                        "Resume restores refill suspension but allows player movement");
                    session.Pause();
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                    Check(Time.timeScale == timeScale, "Destroying a paused session restores global time");
                }
                finally { Time.timeScale = timeScale; }
            });
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                try
                {
                    session.Configure(player);
                    if (!Application.isPlaying)
                    {
                        InvokeLifecycle(session, "OnEnable");
                    }
                    InvokeLifecycle(session, "Start");
                    session.StartLevel = 12;
                    Check(session.State == GameSession.RunState.MainMenu, "Empty scene enters the main menu");
                    session.StartRun();
                    Check(session.Progress.Level == 12 && session.Progress.Defeated == session.Progress.PreviousThreshold &&
                        session.Progress.Score == 0, "Play starts at the selected level without score");
                    Check(session.State == GameSession.RunState.Refilling && grid.Model.Count == 0,
                        "Selected level start keeps the empty opening beat");
                    session.Tick(2f);
                    Check(session.State == GameSession.RunState.Playing &&
                        grid.Model.Count == session.Progress.BatchEnemies, "Selected level start spawns that level's batch size");
                    Check(grid.Model.Count == 6 * RunProgress.WideRowWidth,
                        "Level twelve opening batch uses six six-wide rows");
                    Check(grid.GetComponent<EnemyRowSpawner>().UnlockedColors().All(color => RunProgress.IsUnlocked(color, 12)),
                        "Selected start level drives opening eligibility");
                }
                finally
                {
                    session.StartLevel = RunProgress.MinLevel;
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
            Debug.Log("Menu checks passed: pause, resume, start-level selection, in-flight tongue, cooldowns, refill state and global-time cleanup.");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Menu check failed: " + message); }
        private static void InvokeLifecycle(GameSession session, string method) => typeof(GameSession)
            .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(session, null);
    }
}
