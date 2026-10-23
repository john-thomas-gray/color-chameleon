using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class BeatArrivalRuntimeChecks
    {
        private const string Pending = "CandyCruisers.BeatArrivalChecks";
        private static readonly FieldInfo RestingScale = typeof(EnemyPresentation).GetField("restingBodyScale",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ArrivalSeconds = typeof(GameSession).GetField("refillArrivalSeconds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static int stage;
        private static float landingBeat, startedBeat, firstFullBeat = float.NaN;
        private static float openingStartedBeat = float.NaN, openingLandingBeat;
        private static double began;
        private static bool capturedBefore, capturedLanding;
        private static int barSpikes, barCues;
        static BeatArrivalRuntimeChecks() => EditorApplication.update += Poll;

        public static void Run() => RunSized(960, 540);
        public static void RunPortrait() => RunSized(440, 820);
        private static void RunSized(int width, int height)
        {
            MetronomeCheckSettings.BeginRuntime(Pending, voice: true, visual: true);
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (var existing in Resources.FindObjectsOfTypeAll(type)) ((EditorWindow)existing).Close();
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            window.ShowUtility(); window.position = new Rect(40, 40, width, height); window.Focus();
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            try
            {
                Application.runInBackground = true;
                if (began == 0) began = EditorApplication.timeSinceStartup;
                Require(EditorApplication.timeSinceStartup - began < 45, "Real playback timed out at stage " + stage);
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (session == null) return;
                if (session.IsPaused) session.Resume();
                var grid = session.GetComponent<EnemyGrid>();
                var music = session.GetComponent<GameplayMusicPlayer>();
                if (stage == 0)
                {
                    if (session.State != GameSession.RunState.MainMenu) return;
                    session.LevelCatalog = null;
                    if (session.OpeningWarningActive) session.HandleMenuKey(KeyCode.Space);
                    if (!music.Source.isPlaying) return;
                    DeveloperOptions.PlayerInvincible = true;
                    session.WaveSpawned += () => { if (stage == 1) openingStartedBeat = music.BeatPosition; };
                    session.StartRun();
                    openingLandingBeat = music.BeatPositionAtTime((float)ArrivalSeconds.GetValue(session));
                    stage = 1;
                }
                else if (stage == 1 && session.State == GameSession.RunState.Playing && grid.Model.Count > 0)
                {
                    foreach (var presentation in grid.GetComponentsInChildren<EnemyPresentation>())
                        if (presentation.IsPhasing) return;
                    Require(openingStartedBeat >= openingLandingBeat - 1.08f && openingStartedBeat < openingLandingBeat - .7f,
                        "The opening fleet also begins its build-up on four");
                    Require(Mathf.Abs(music.BeatPosition - openingLandingBeat) < .12f && music.CurrentBeatInMeasure == 1,
                        "The opening fleet first reaches full size on one");
                    Debug.Log($"Real opening arrival: grow-in started at {openingStartedBeat:F3}; full fleet at {music.BeatPosition:F3}; downbeat {openingLandingBeat:F0}.");
                    Require(music.Source.clip.name == GameplayMusicPlayer.CountingResourceName || music.PlayCountingMetronome(),
                        "Real playback uses the spoken 1-2-3-4 track");
                    stage = 2;
                }
                else if (stage == 2 && music.BeatPosition > 2.25f && Mathf.Repeat(music.BeatPosition, 1) < .65f)
                {
                    int bars = grid.SeenColors().Count;
                    Require(bars == 2, "The reported regression uses two filled bar colors");
                    landingBeat = GameSession.RefillDownbeat(music, music.BeatPosition, bars, true);
                    grid.GetComponent<SoundEffects>().CuePlayed += (cue, pitch) =>
                    {
                        if (cue != SoundEffect.BarPowerUp) return;
                        Require(music.CurrentBeatInMeasure == 3 + barCues,
                            "Bar sounds count three and four at the start of each animation");
                        Debug.Log($"Real bar cue {barCues + 1}: count {music.CurrentBeatInMeasure}, beat {music.BeatPosition:F3}.");
                        barCues++;
                    };
                    session.ClearCelebration.BarSpiked += (color, duration) =>
                    {
                        int count = music.CurrentBeatInMeasure;
                        Require(count == 3 + barSpikes, "Two visible bar pulses occur on three and four, not two and three");
                        var resting = new Rect(0, 100, 40, 5);
                        Require(session.ClearCelebration.Present(color, resting).height > resting.height,
                            "The counted bar is visibly rising on its spoken beat");
                        Debug.Log($"Real bar pulse {barSpikes + 1}: count {count}, beat {music.BeatPosition:F3}.");
                        ScreenCapture.CaptureScreenshot($"TestResults/beat-countoff-bar-{barSpikes + 1}.png");
                        barSpikes++;
                    };
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>()) grid.ClearMagicChain(enemy.Id, 0);
                    Require(session.State == GameSession.RunState.Refilling, "Clearing the fleet starts the countdown");
                    session.WaveSpawned += () => startedBeat = music.BeatPosition;
                    stage = 3;
                }
                else if (stage == 3)
                {
                    Require(music.Source.clip != null && music.Source.clip.name == GameplayMusicPlayer.CountingResourceName &&
                        music.Source.isPlaying, "The actual audio clock keeps playing through the transition");
                    float beat = music.BeatPosition;
                    if (session.State != GameSession.RunState.Playing) return;
                    Require(barSpikes == 2 && barCues == 2, "Both bars visibly pulse and sound on three and four before the fleet grows in");
                    Require(startedBeat >= landingBeat - .52f && startedBeat < landingBeat - .3f,
                        $"Fleet grows in during the latter half of four (start={startedBeat}, one={landingBeat})");
                    var presentations = grid.GetComponentsInChildren<EnemyPresentation>();
                    bool complete = Array.TrueForAll(presentations, presentation => !presentation.IsPhasing && AtFullSize(presentation));
                    if (complete && float.IsNaN(firstFullBeat))
                    {
                        firstFullBeat = beat;
                        Require(firstFullBeat >= landingBeat - .02f && firstFullBeat <= landingBeat + .12f,
                            $"The first complete pose is on the downbeat frame (first={firstFullBeat}, one={landingBeat})");
                    }
                    if (beat < landingBeat - .08f)
                    {
                        foreach (var presentation in presentations)
                            Require(presentation.IsPhasing && !AtFullSize(presentation), "The fleet is still growing before one");
                        if (!capturedBefore && beat >= landingBeat - .45f)
                        {
                            Require(music.CurrentBeatInMeasure == 4, "The visible counter reads four during the build-up");
                            ScreenCapture.CaptureScreenshot("TestResults/beat-arrival-before-one.png");
                            capturedBefore = true;
                        }
                    }
                    if (beat >= landingBeat + .08f && !capturedLanding)
                    {
                        Require(beat < landingBeat + .3f && music.CurrentBeatInMeasure == 1,
                            "The visible counter reads one at the landing");
                        foreach (var presentation in presentations)
                            Require(!presentation.IsPhasing && AtFullSize(presentation), "The whole fleet has landed on one");
                        Require(capturedBefore, "Real frames show the build-up before the downbeat");
                        ScreenCapture.CaptureScreenshot("TestResults/beat-arrival-on-one.png");
                        Debug.Log($"Real beat arrival: grow-in started at {startedBeat:F3}; first full fleet at {firstFullBeat:F3}; downbeat {landingBeat:F0}.");
                        capturedLanding = true;
                    }
                    if (capturedLanding && beat >= landingBeat + .6f)
                    {
                        foreach (var presentation in presentations)
                            Require(AtFullSize(presentation), "The fleet stays at full size after the impact, without settling");
                        Debug.Log("Beat arrival runtime checks passed: visible bar pulses on three and four, late-four grow-in, beat-one impact and stable final pose.");
                        SessionState.SetBool(Pending, false);
                        MetronomeCheckSettings.EndRuntime(Pending);
                        EditorApplication.Exit(0);
                    }
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                SessionState.SetBool(Pending, false);
                MetronomeCheckSettings.EndRuntime(Pending);
                EditorApplication.Exit(1);
            }
        }

        private static bool AtFullSize(EnemyPresentation presentation)
        {
            var body = presentation.GetComponent<GridEnemy>().Visuals.Body;
            return Vector3.Distance(body.transform.localScale, (Vector3)RestingScale.GetValue(presentation)) < .0001f && body.color.a == 1;
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception("Beat arrival runtime check failed: " + message); }
    }
}
