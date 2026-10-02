using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class WaveTransitionChecks
    {
        public static void RunRefillInPlayMode()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            SessionState.SetBool("CandyCruisers.RefillBeatChecks", true);
            EditorApplication.playModeStateChanged += OnRefillPlayMode;
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        private static void RestoreRefillChecks()
        {
            if (SessionState.GetBool("CandyCruisers.RefillBeatChecks", false))
                EditorApplication.playModeStateChanged += OnRefillPlayMode;
        }
        private static void OnRefillPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnRefillPlayMode;
            SessionState.SetBool("CandyCruisers.RefillBeatChecks", false);
            try { RunRefillDelayEnvelope(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
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
            CheckDownbeatPlanning();
            if (Application.isPlaying) CheckPlayingDownbeatSequence();
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
                float barBeats = GameSession.MinimumColorClearBarSequenceBeats(bars);
                float barStep = GameSession.ColorClearBarStepBeats(barBeats, bars);
                float duration = barBeats * beat;
                Check(session.State == GameSession.RunState.Refilling &&
                    Mathf.Abs(session.ClearCelebration.PlaybackDuration - duration) < .0001f,
                    "Silent celebration divides the fallback beat across the earned bars");
                session.Tick(barStep * beat * .75f);
                Check(removals == 0 && grid.Model.Count == 0 && !session.ClearCelebration.Finished,
                    "The first earned bar gets a visible color flash before powering down");
                session.Tick(duration - barStep * beat * .75f - .001f);
                Check(grid.Model.Count == 0 && session.State == GameSession.RunState.Refilling,
                    "Next fleet waits until the earned bars have run out");
                session.Tick(longFrame ? 100 : .002f);
                if (partial)
                {
                    Check(removals == bars && grid.Model.Count == session.Progress.BatchEnemies &&
                        session.State == GameSession.RunState.Playing && arrivals == 1,
                        "Partial bars power down together and the next fleet begins on the fallback downbeat");
                    session.Tick(1);
                    Check(arrivals == 1 && !session.ClearCelebration.Active && !player.CelebrationColor.HasValue,
                        "The partial refill completes once without a post-bar hold");
                    return;
                }
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
            Debug.Log("Refill delay envelope checks passed: downbeat planning, full/partial bars, dynamic bar spans, silent fallback, pause, negative/long frames and exactly-once arrival.");
        }

        private static void CheckDownbeatPlanning()
        {
            foreach (bool full in new[] { false, true })
            foreach (bool lifeAwarded in new[] { false, true })
            for (int bars = 1; bars <= FullSetCelebration.MaxRewardBars; bars++)
            for (float clearBeat = -.25f; clearBeat <= 8; clearBeat += .125f)
            {
                float minimumBarBeats = GameSession.MinimumColorClearBarSequenceBeats(bars);
                float spawnBeat = GameSession.RefillDownbeat(clearBeat, minimumBarBeats, full, lifeAwarded: lifeAwarded);
                int postBarBeats = lifeAwarded ? 2 : full ? 1 : 0;
                float barBeats = GameSession.ColorClearBarSequenceBeats(clearBeat, spawnBeat, postBarBeats);
                float animationStart = spawnBeat - barBeats - postBarBeats;
                Near((spawnBeat - GameplayMusicPlayer.DownbeatOffsetBeats) % GameplayMusicPlayer.BeatsPerMeasure, 0,
                    "Spawn uses the corrected measure downbeat, not the preceding beat");
                Near(animationStart, clearBeat,
                    "Dynamic bar choreography begins at the clear and fills the available pre-downbeat window");
                Check(barBeats + .0001f >= minimumBarBeats,
                    "The planned downbeat leaves at least the minimum bar animation span");
                Check(spawnBeat - GameplayMusicPlayer.BeatsPerMeasure < clearBeat + minimumBarBeats + postBarBeats,
                    "The earliest eligible downbeat is chosen without adding an unnecessary measure");
                Near(animationStart + barBeats, spawnBeat - postBarBeats,
                    "The bar choreography ends at the correct beat before spawning");
            }
            Near(GameSession.RefillDownbeat(7, GameSession.MinimumColorClearBarSequenceBeats(6), true), 9,
                "A full bar sequence can use the next downbeat without a mandatory hold");
            Near(GameSession.RefillDownbeat(7.01f, GameSession.MinimumColorClearBarSequenceBeats(6), true), 13,
                "A clear just after the fitting point stretches into the following measure");
            foreach (int offset in new[] { 0, 1 })
            foreach (bool full in new[] { false, true })
            foreach (float afterOne in new[] { 0f, .001f, .02f, .5f, .99f })
                Near(GameSession.RefillDownbeat(offset + afterOne,
                        GameSession.MinimumColorClearBarSequenceBeats(1), full, offset), offset + 4,
                    "One bar cleared on the first beat always fits the next downbeat, including frame delay");
        }

        private static void CheckPlayingDownbeatSequence()
        {
            foreach (bool single in new[] { false, true })
            foreach (bool partial in new[] { false, true })
            foreach (bool mapped in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!single) session.Progress.Reset(2);
                var music = grid.GetComponent<GameplayMusicPlayer>();
                music.StopPlayback();
                var clip = AudioClip.Create(single ? GameplayMusicPlayer.CountingResourceName : "DiscoDescent", 44100 * 20, 1, 44100, false);
                try
                {
                    music.Source.clip = clip;
                    var cache = (System.Collections.Generic.Dictionary<AudioClip, SongBeatMap>)typeof(GameplayMusicPlayer)
                        .GetField("beatMaps", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(music);
                    cache[clip] = mapped ? new SongBeatMap
                    {
                        durationSeconds = 20,
                        beatTimes = new[] { .1f, .6f, 1.1f, 1.6f, 1.85f, 2.1f, 2.35f, 3.1f, 3.85f, 4.6f, 5.35f, 6.1f, 6.85f }
                    } : null;
                    music.Source.Play();
                    SeekBeat(music, single ? .02f : 2.125f);
                    Check(music.Source.isPlaying, "Fixture runs against a playing music clock");
                    int removals = 0, arrivals = 0;
                    session.ClearCelebration.ColorRemoved += _ => removals++;
                    session.WaveSpawned += () => arrivals++;
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                    var blue = ProgressionChecks.Add(grid, single ? EnemyColor.Red : EnemyColor.Blue, 2, 0);
                    var green = ProgressionChecks.Add(grid, single ? EnemyColor.Red : EnemyColor.Green, 4, 0);
                    grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                    if (partial && !single) grid.ResetColorClearStreak();
                    grid.ClearMatchingChain(blue.Id, blue.Color);
                    grid.ClearMatchingChain(green.Id, green.Color);
                    float began = music.PlaybackSeconds;
                    float clearBeat = music.BeatPosition;
                    bool partialSet = partial && !single;
                    int bars = single ? 1 : partial ? 2 : 3;
                    float minimumBarBeats = GameSession.MinimumColorClearBarSequenceBeats(bars);
                    float downbeat = GameSession.RefillDownbeat(clearBeat, minimumBarBeats, !partialSet,
                        music.CurrentDownbeatOffsetBeats);
                    int postBarBeats = partialSet ? 0 : 1;
                    float barBeats = GameSession.ColorClearBarSequenceBeats(clearBeat, downbeat, postBarBeats);
                    float barStep = GameSession.ColorClearBarStepBeats(barBeats, bars);
                    float startBeat = downbeat - barBeats - postBarBeats;
                    Near(session.ClearCelebration.PlaybackDuration, music.SecondsAtBeat(downbeat - postBarBeats) - began,
                        "Alignment time is included before the removals, including through tempo changes");
                    Near(startBeat, clearBeat, "Bar power-down stretches from the clear moment toward the planned downbeat");
                    var resting = new Rect(0, 100, 40, 5);
                    Check(removals == 0 && arrivals == 0 && session.ClearCelebration.Color == Color.white,
                        "Bar choreography starts white immediately after the clear");
                    foreach (var color in new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green })
                        Check(session.ClearCelebration.Present(color, resting) == resting,
                            "No power-down growth begins before music advances into the first dynamic slot");
                    SeekBeat(music, startBeat + barStep * .75f);
                    session.Tick(.001f);
                    if (partialSet)
                    {
                        var blueSpike = session.ClearCelebration.Present(EnemyColor.Blue, resting);
                        var greenSpike = session.ClearCelebration.Present(EnemyColor.Green, resting);
                        Check(removals == 0 && session.ClearCelebration.Color == Color.white &&
                            blueSpike.height > resting.height && blueSpike == greenSpike &&
                            blueSpike.x == resting.x && blueSpike.width == resting.width,
                            "Partial clears spike all earned bars vertically while waiting for the downbeat");
                    }
                    else Check(removals == 0 && session.ClearCelebration.Color != Color.white,
                        "The first bar gets its full color-flash animation before disappearing");
                    session.Pause();
                    var held = player.DisplayColor;
                    session.Tick(100);
                    Check(removals == 0 && player.DisplayColor == held, "Pause freezes the planned removal sequence");
                    session.Resume();
                    // Resume may restore the configured soundtrack; continue the same controlled clock.
                    music.Source.clip = clip;
                    music.Source.Play();
                    if (partialSet)
                    {
                        SeekBeat(music, downbeat - .01f);
                        session.Tick(.001f);
                        Check(removals == 0 && arrivals == 0 && !session.ClearCelebration.Finished,
                            "Partial bars stay powered through the last moment before the downbeat");
                        SeekBeat(music, downbeat + .01f);
                        session.Tick(.001f);
                        Check(removals == bars && arrivals == 1 && session.State == GameSession.RunState.Playing &&
                            grid.Model.Count == session.Progress.BatchEnemies,
                            "All partial bars power down together and the next fleet starts on the planned downbeat");
                        session.Tick(100);
                        Check(arrivals == 1, "The planned arrival fires exactly once");
                        return;
                    }
                    for (int i = 0; i < bars; i++)
                    {
                        float removalBeat = startBeat + (i + 1) * barStep;
                        SeekBeat(music, removalBeat - .01f);
                        session.Tick(.001f);
                        Check(removals == i && arrivals == 0, "Each bar remains until its scheduled removal beat");
                        SeekBeat(music, removalBeat + .01f);
                        session.Tick(.001f);
                        Check(removals == i + 1 && arrivals == 0, "One bar disappears per dynamic slot without spawning");
                    }
                    Check(session.ClearCelebration.Finished && grid.Model.Count == 0,
                        "Final removal leaves the field empty for the beat before the downbeat");
                    SeekBeat(music, downbeat - .01f);
                    session.Tick(100);
                    Check(arrivals == 0, "The spawn waits for its originally planned downbeat");
                    SeekBeat(music, downbeat + .01f);
                    session.Tick(.001f);
                    Check(arrivals == 1 && session.State == GameSession.RunState.Playing &&
                        grid.Model.Count == session.Progress.BatchEnemies, "The next beat after the final removal spawns the fleet");
                    session.Tick(100);
                    Check(arrivals == 1, "The planned arrival fires exactly once");
                }
                finally
                {
                    music.StopPlayback();
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            });
        }

        private static void SeekBeat(GameplayMusicPlayer music, float beat) =>
            music.Source.timeSamples = Mathf.RoundToInt(music.SecondsAtBeat(beat) * music.Source.clip.frequency);
        private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Wave transition check failed: " + message); }
    }
}
