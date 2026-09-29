using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class WaveTransitionChecks
    {
        public static void Run()
        {
            var root = new GameObject("Transition fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            var playerObject = new GameObject("Transition player", typeof(PlayerMovement), typeof(SpriteRenderer));
            var mouth = new GameObject("Transition tongue", typeof(LineRenderer), typeof(TongueShot));
            bool enabled = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            var random = UnityEngine.Random.state;
            try
            {
                var line = mouth.GetComponent<LineRenderer>();
                line.startWidth = .075f; line.endWidth = .11f;
                var tongue = mouth.GetComponent<TongueShot>();
                tongue.TryFire(EnemyColor.Red, 10);
                tongue.Tick(.1f);
                Near(tongue.Length, 1.4f, "Normal extension speed");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 10, true);
                tongue.Tick(.1f);
                Check(line.enabled && !tongue.TryFire(EnemyColor.Blue, 10), "Magic fires immediately and blocks repeated fire");
                Near(tongue.Length, 5.6f, "Magic extension is immediately four times normal speed");
                Near(line.startWidth, .15f, "Magic base is twice as thick");
                Near(line.endWidth, .22f, "Magic tip is twice as thick");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 2, true);
                tongue.Tick(2f / 56);
                tongue.Tick(.02f);
                Near(tongue.Length, .4f, "Magic retracts at eighty units per second");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 2);
                Near(line.endWidth, .11f, "Normal width restored after magic");
                tongue.Cancel();

                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blue Enemy.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Enemy.prefab"));
                var player = playerObject.GetComponent<PlayerMovement>();
                playerObject.GetComponentInChildren<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PlayerPlaceholder.png");
                mouth.transform.SetParent(playerObject.transform, false);
                player.Configure(grid, tongue, playerObject.GetComponentInChildren<SpriteRenderer>());
                var session = root.AddComponent<GameSession>();
                session.Configure(player);
                typeof(GameSession).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                var music = root.GetComponent<GameplayMusicPlayer>();
                music.StopPlayback();
                SpawnOverride.Enabled = true;
                SpawnOverride.Types = 1 << (int)EnemyColor.Blue;
                var target = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                playerObject.transform.position = new Vector3(target.transform.position.x, -4.6f, 0);
                Check(player.Fire(), "Fire at last enemy");
                tongue.Tick(.33f, grid);
                Check(session.State == GameSession.RunState.Refilling && tongue.Active && tongue.Retracting,
                    "Last normal hit retracts instead of disappearing");
                Check(player.ReadyColor == EnemyColor.Red, "Outgoing color stays until return");
                float length = tongue.Length;
                float firstWhitePhase = session.ClearCelebration.PlaybackDuration * .25f;
                session.Tick(firstWhitePhase);
                Check(grid.Model.Count == 0 && tongue.Active, "Refill waits for returning tongue");
                tongue.Tick(.01f, grid);
                Check(tongue.Length < length && tongue.Length > 0, "Retraction remains animated during refill");
                SpawnOverride.Types = 1 << (int)EnemyColor.Red;
                tongue.Tick(10, grid);
                player.RefreshColor(true);
                Check(player.ReadyColor == EnemyColor.Red && player.DisplayColor == Color.white && !player.Fire(),
                    "White/color celebration continues while the field is empty");
                session.Tick(session.ClearCelebration.PlaybackDuration - firstWhitePhase - .001f);
                Check(grid.Model.Count == 0 && player.DisplayColor == EnemyPalette.Get(EnemyColor.Red), "Final color appears before the beat-count spawn deadline");
                session.Tick(.002f);
                Check(grid.Model.Count == 0 && session.ClearCelebration.Finished,
                    "Last bar disappears before the following-beat spawn");
                session.Tick(music.BeatDuration + .001f);
                Check(grid.Model.ColorCount(EnemyColor.Red) == session.Progress.BatchEnemies && player.ReadyColor == EnemyColor.Red,
                    "Last earned bar reserves a matching next fleet even when the override excluded it");
                Check(player.DisplayColor == EnemyPalette.Get(EnemyColor.Red) && !player.CelebrationColor.HasValue,
                    "Player takes the new fleet color on the spawn frame");

                tongue.TryFire(EnemyColor.Red, 10, true);
                var bottom = grid.View(grid.Model.At(2, session.Progress.BatchRows - 1).Id).GetComponentInChildren<SpriteRenderer>().bounds.min.y;
                tongue.Tick((bottom - tongue.transform.position.y) / 56 + .001f, grid);
                Check(grid.Model.Count == 0 && tongue.Active && tongue.Retracting, "Last magic hit also returns visibly");
                session.Tick(.1f);
                Check(grid.Model.Count == 0, "Magic return cannot hit the next batch");
                tongue.Tick(10, grid);
                player.RefreshColor();
                session.Tick(session.ClearCelebration.PlaybackDuration);
                Check(grid.Model.Count == 0, "Magic refill also keeps the last bar-removal frame empty");
                session.Tick(music.BeatDuration + .001f);
                Check(player.ReadyColor == EnemyColor.Red && grid.Model.ColorCount(EnemyColor.Red) > 0,
                    "Following wave uses newly selected override and matching player color");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(playerObject);
                SpawnOverride.Enabled = enabled;
                SpawnOverride.Types = types;
                UnityEngine.Random.state = random;
            }
            Debug.Log("Wave transition checks passed: magic width/speed, visible final-hit returns, non-grey player and guaranteed next-batch color.");
            RunRefillDelayEnvelope();
        }
        public static void RunRefillDelayEnvelope()
        {
            foreach (float tempo in new[] { 115.03f, 125, 140, 142 })
            {
                float beat = 60 / tempo;
                Near(GameSession.SecondsToNextBeat(3, beat), beat, "A bar removed on a beat waits for the next one");
                Near(GameSession.SecondsToNextBeat(3.25f, beat), beat * .75f, "Quarter-beat removal aligns with the following beat");
                Near(GameSession.SecondsToNextBeat(3.75f, beat), beat * .25f, "Late-beat removal uses the remaining quarter beat");
                Near(GameSession.SecondsToNextBeat(-.25f, beat), beat * .25f, "Music's opening offset aligns with beat zero");
            }
            foreach (bool partial in new[] { false, true })
            foreach (bool longFrame in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                var music = grid.GetComponent<GameplayMusicPlayer>();
                music.StopPlayback();
                float beat = music.BeatDuration;
                float[] beatLengths = music.DurationsForBeats(2);
                int arrivals = 0, removals = 0;
                session.WaveSpawned += () => arrivals++;
                session.ClearCelebration.ColorRemoved += color => removals++;
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(session.State == GameSession.RunState.Playing && !session.ClearCelebration.Active,
                    "A non-final earned bar does not begin refill");
                if (partial) grid.ResetColorClearStreak();
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                int bars = partial ? 1 : 2;
                float duration = beatLengths[0] + (bars == 2 ? beatLengths[1] : 0);
                Check(session.State == GameSession.RunState.Refilling &&
                    Mathf.Abs(session.ClearCelebration.PlaybackDuration - duration) < .0001f,
                    "Clear celebration lasts one current-song beat for each earned bar");
                session.Tick(duration - .001f);
                Check(grid.Model.Count == 0 && session.State == GameSession.RunState.Refilling,
                    "Next fleet waits until the earned bars have run out");
                session.Tick(longFrame ? 100 : .002f);
                Check(session.ClearCelebration.Finished && removals == bars && grid.Model.Count == 0 && arrivals == 0 &&
                    !session.ClearCelebration.Visible(session.ClearCelebration.LastColor.Value),
                    "Removing the final bar never spawns the next fleet in the same update, even after a long frame");
                var heldColor = player.DisplayColor;
                session.Pause(); session.Tick(100);
                Check(grid.Model.Count == 0 && player.DisplayColor == heldColor, "Pause freezes the post-bar beat wait and final player color");
                session.Resume(); music.StopPlayback();
                session.Tick(0); session.Tick(-1); session.Tick(beat - .001f);
                Check(grid.Model.Count == 0 && arrivals == 0 && !player.Fire() && !player.ControlsLocked &&
                    player.DisplayColor == heldColor && removals == bars,
                    "The empty field and final color hold until the next beat, without replaying bar removals");
                session.Tick(.002f);
                Check(grid.Model.Count == session.Progress.BatchEnemies && session.State == GameSession.RunState.Playing && arrivals == 1,
                    "Next fleet begins spawning on the following beat");
                session.Tick(1);
                Check(arrivals == 1 && !session.ClearCelebration.Active && !player.CelebrationColor.HasValue,
                    "The next beat spawns one batch and ends the held celebration");
            });
            Debug.Log("Refill delay envelope checks passed: full/partial bars, next music beat, silent fallback, pause, negative/long frames and exactly-once arrival.");
        }
        private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Wave transition check failed: " + message); }
    }
}
