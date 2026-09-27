using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class ComboLeaderboardBackgroundChecks
    {
        public static void Run()
        {
            CheckRunProgressCombo();
            CheckPlayerShotCombo();
            CheckLeaderboard();
            CheckBackgroundLevelTransition();
            Debug.Log("Combo, leaderboard and background checks passed: streak scoring, miss reset, saved ranking, wave pulses and level backgrounds.");
        }

        private static void CheckRunProgressCombo()
        {
            var progress = new RunProgress();
            progress.BeginShot();
            Check(progress.RegisterClear(2, false) == 200 && progress.ActiveComboMultiplier == 1,
                "First accepted shot uses base scoring");
            progress.FinishShot(true);
            Check(progress.ComboStreak == 1 && progress.ComboMultiplier == 2, "Successful shot advances the next combo");
            progress.BeginShot();
            Check(progress.RegisterClear(1, false) == 200 && progress.ActiveComboMultiplier == 2,
                "Second consecutive hit applies combo multiplier");
            progress.FinishShot(false);
            Check(progress.ComboStreak == 0 && progress.ComboMultiplier == 1 && progress.ActiveComboMultiplier == 1,
                "Miss resets combo multiplier");
        }

        private static void CheckPlayerShotCombo()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                try
                {
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 5);
                    var sentinel = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 5);
                    player.transform.position = new Vector3(red.transform.position.x, player.transform.position.y, 0);
                    player.RefreshColor(true);
                    Check(player.Fire(), "First combo shot fires");
                    tongue.Tick(10, grid);
                    Check(session.Progress.Score == 100 && session.Progress.ComboStreak == 1 &&
                        session.Progress.ComboMultiplier == 2, "First hit scores base points and starts the streak");
                    grid.Unregister(sentinel); UnityEngine.Object.DestroyImmediate(sentinel.gameObject);

                    red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 5);
                    sentinel = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 5);
                    player.transform.position = new Vector3(red.transform.position.x, player.transform.position.y, 0);
                    player.RefreshColor(true);
                    Check(player.Fire(), "Second combo shot fires");
                    tongue.Tick(10, grid);
                    Check(session.Progress.Score == 300 && session.Progress.ComboStreak == 2 &&
                        session.Progress.ComboMultiplier == 3, "Second hit uses combo x2 and advances the streak");
                    grid.Unregister(sentinel); UnityEngine.Object.DestroyImmediate(sentinel.gameObject);
                    Check(!player.Fire() && session.Progress.ComboMultiplier == 3,
                        "Rejected empty-field fire leaves combo intact");

                    var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, GridModel.Columns - 1, 5);
                    player.transform.position = new Vector3(-PlayerMovement.HalfWidth + .1f, player.transform.position.y, 0);
                    player.RefreshColor(true);
                    Check(player.Fire() && player.ReadyColor == EnemyColor.Blue, "Miss shot fires with a represented color");
                    tongue.Tick(10, grid);
                    Check(grid.View(blue.Id) == blue && session.Progress.ComboStreak == 0 &&
                        session.Progress.ComboMultiplier == 1, "Shot that hits no enemy resets the combo");
                }
                finally
                {
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                    UnityEngine.Object.DestroyImmediate(session);
                }
            });
        }

        private static void CheckLeaderboard()
        {
            var previous = LocalLeaderboard.Entries();
            try
            {
                LocalLeaderboard.Clear();
                Check(LocalLeaderboard.Submit(0, 1) == 0 && LocalLeaderboard.Entries().Count == 0,
                    "Zero scores stay out of the leaderboard");
                Check(LocalLeaderboard.Submit(500, 2) == 1, "First score takes rank one");
                Check(LocalLeaderboard.Submit(250, 3) == 2, "Lower score ranks below");
                Check(LocalLeaderboard.Submit(800, 1) == 1, "Higher score moves to rank one");
                for (int i = 0; i < 8; i++) LocalLeaderboard.Submit(100 + i, 1);
                var entries = LocalLeaderboard.Entries();
                Check(entries.Count == LocalLeaderboard.MaxEntries, "Leaderboard keeps only its top entries");
                for (int i = 1; i < entries.Count; i++)
                    Check(entries[i - 1].Score >= entries[i].Score, "Leaderboard remains sorted");
            }
            finally
            {
                LocalLeaderboard.Replace(previous);
            }
        }

        private static void CheckBackgroundLevelTransition()
        {
            var texture = new Texture2D(8, 8);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 8);
            var cameraObject = new GameObject("Background test camera", typeof(Camera), typeof(GameplayFraming));
            var sessionRoot = new GameObject("Background test session", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            var background = new GameObject("Background test stars", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            try
            {
                background.sprite = sprite;
                var session = sessionRoot.AddComponent<GameSession>();
                var framing = cameraObject.GetComponent<GameplayFraming>();
                framing.SetBackground(background);
                framing.SetSession(session);
                framing.TickBackground(0);
                var start = background.transform.localPosition;
                var firstSprite = background.sprite;
                Check(framing.DisplayedLevel == 1 && framing.DisplayedBackgroundIndex == 0 &&
                    Close(background.color, GameplayFraming.TintForLevel(1)), "Background starts at the first generated level-one starfield");
                framing.TriggerWaveSpawnEffect();
                framing.TickBackground(.35f);
                Check(framing.WavePulseRemaining > 0 && framing.LevelTransitionRemaining == 0 &&
                    background.sprite == firstSprite && !Close(background.color, GameplayFraming.TintForLevel(1)),
                    "Wave spawn pulse brightens the current starfield without changing level backgrounds");
                framing.TickBackground(2f);
                Check(framing.WavePulseRemaining == 0 && Close(background.color, GameplayFraming.TintForLevel(1)),
                    "Wave spawn pulse settles back to the level tint");
                float levelOneStart = framing.BackgroundSpinAngle;
                framing.TickBackground(1f);
                float levelOneSpin = Mathf.Abs(framing.BackgroundSpinAngle - levelOneStart);
                int levelOneDirection = framing.SpinDirection;
                session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated, false);
                framing.TickBackground(.2f);
                Check(framing.DisplayedLevel == 2 && framing.LevelTransitionRemaining > 0 &&
                    framing.DisplayedBackgroundIndex == 1 && background.sprite != firstSprite &&
                    framing.SpinDirection == -levelOneDirection, "Level change starts a new starfield and reverses spin");
                framing.TickBackground(2f);
                Check(framing.LevelTransitionRemaining == 0 && Close(background.color, GameplayFraming.TintForLevel(2)),
                    "Cross-fade settles on the new level tint");
                float levelTwoStart = framing.BackgroundSpinAngle;
                framing.TickBackground(1f);
                float levelTwoSpin = Mathf.Abs(framing.BackgroundSpinAngle - levelTwoStart);
                Check(levelTwoSpin > levelOneSpin, "Higher levels spin faster than the level-one baseline");
                Check(Vector3.Distance(start, background.transform.localPosition) > .001f,
                    "Background drifts over time");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(background.gameObject);
                UnityEngine.Object.DestroyImmediate(sessionRoot);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool Close(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < .01f;

        private static void InvokeLifecycle(GameSession session, string method) => typeof(GameSession)
            .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(session, null);

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Combo/leaderboard/background check failed: " + message);
        }
    }
}
