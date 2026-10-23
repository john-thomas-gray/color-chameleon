using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class GameplayTextRuntimeChecks
    {
        private const string Pending = "CandyCruisers.GameplayTextRuntimeChecks";
        private static int stage;
        private static double began, changed;
        private static readonly float[] Beats = { .5f, 1, .75f, 1.75f, 2.75f };
        private static readonly string[] Frames = { "rest", "beat", "magic-1", "magic-2", "magic-shot" };
        static GameplayTextRuntimeChecks() => EditorApplication.update += Poll;

        public static void RunDesktop() => RunSized(1100, 740, false);
        public static void RunPhone() => RunSized(440, 900, true);

        private static void RunSized(int width, int height, bool phone)
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (var existing in Resources.FindObjectsOfTypeAll(type)) ((EditorWindow)existing).Close();
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            window.ShowUtility(); window.position = new Rect(40, 40, width, height); window.Focus();
            SessionState.SetBool(Pending + ".Phone", phone);
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
                Check(EditorApplication.timeSinceStartup - began < 35, "Runtime text preview timed out");
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (session == null || Time.timeSinceLevelLoad < .2f) return;
                bool phone = SessionState.GetBool(Pending + ".Phone", false);
                GameSession.ForceMobileMenuForTests = phone;
                GameplayFraming.ForceIphoneNotchOffsetForTests = phone;
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
                var music = session.GetComponent<GameplayMusicPlayer>();
                if (stage == 0)
                {
                    session.PresentationPreview = true;
                    if (session.IsPaused) session.Resume();
                    session.LevelCatalog = null; session.StartLevel = 7; session.StartRun();
                    stage = 1;
                }
                else if (stage == 1 && session.State == GameSession.RunState.Playing && session.Fleet.Count > 0 &&
                    Time.timeSinceLevelLoad > 1.5f)
                {
                    session.Progress.RegisterClear(12, false);
                    for (int i = 0; i < 11; i++)
                    {
                        session.Progress.BeginShot();
                        session.Progress.RegisterShotColor(EnemyColor.Red);
                        session.Progress.FinishShot(true);
                    }
                    session.Tick(2);
                    foreach (var presentation in session.GetComponentsInChildren<EnemyPresentation>())
                    { presentation.Tick(2, presentation.GetComponent<GridEnemy>().Color, 0); presentation.enabled = false; }
                    session.GetComponent<EnemyGridMovement>().enabled = false;
                    player.enabled = false;
                    music.enabled = false; music.Source.Stop();
                    Time.timeScale = 0;
                    Directory.CreateDirectory("TestResults");
                    Present(session, player, music, 0);
                    stage = 2; changed = EditorApplication.timeSinceStartup;
                }
                else if (stage >= 2 && EditorApplication.timeSinceStartup - changed > .3f)
                {
                    int index = stage - 2;
                    ScreenCapture.CaptureScreenshot("TestResults/gameplay-text-" + (phone ? "phone-" : "desktop-") + Frames[index] + ".png");
                    // Let the screenshot finish on an actual rendered frame before changing its state.
                    stage = -stage; changed = EditorApplication.timeSinceStartup;
                }
                else if (stage <= -2 && EditorApplication.timeSinceStartup - changed > .3f)
                {
                    int next = -stage - 1;
                    if (next < Beats.Length)
                    {
                        Present(session, player, music, next);
                        stage = next + 2; changed = EditorApplication.timeSinceStartup;
                    }
                    else
                    {
                        Debug.Log("Gameplay text runtime checks passed: " + (phone ? "simulated phone" : "desktop") +
                            ", Bungee headings, resting/beat frames, charged colors and active magic shot.");
                        Finish(0);
                    }
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void Present(GameSession session, PlayerMovement player, GameplayMusicPlayer music, int index)
        {
            music.PreviewTime = Beats[index] * .5f;
            typeof(PlayerMovement).GetProperty("ReadyColor").SetValue(player, EnemyColor.Green);
            typeof(PlayerMovement).GetProperty("MagicCharges").SetValue(player, index >= 2 ? 1 : 0);
            if (index == 4)
            {
                Check(player.Tongue.TryFire(EnemyColor.Green, 1, true), "Preview starts an active magic shot");
                typeof(PlayerMovement).GetProperty("MagicCharges").SetValue(player, 0);
            }
            player.RefreshPresentation(0);
            player.LifeIcons.Tick(0, music);
            var character = CharacterVisuals.Ensure(player.gameObject);
            character.RefreshBeat(Beats[index], true);
            bool onBeat = Beats[index] == Mathf.Floor(Beats[index]);
            Check((player.LifeIcons.Choreography.Scale > 1) == onBeat &&
                Mathf.Abs(character.BeatScale - (1 + .09f * GameplayMusicPlayer.BeatPulse(Beats[index], true))) < .0001f,
                "Extra lives follow the main beat while the player keeps its separate beat");
            var restingBar = new Rect(0, 0, 100, GameSession.LevelProgressBaseHeight);
            Check((GameSession.LevelProgressSolidRect(restingBar, false, GameplayMusicPlayer.BeatPulse(Beats[index])).height > restingBar.height) == onBeat,
                "Ordinary progress pulses with the extra lives and text");
            var frame = UnityEngine.Object.FindFirstObjectByType<PlayfieldFrame>();
            frame.RefreshBeat(Beats[index]);
            Check(session.GameplayTextColor == player.AccentColor && session.ComboColor == player.DisplayColor,
                "Rendered text uses the current player color");
            if (index >= 2)
            {
                var edge = frame.GetComponent<LineRenderer>().startColor;
                // LineRenderer stores its colors at eight-bit precision.
                Check(Mathf.Abs(edge.r - session.GameplayTextColor.r) < .005f &&
                    Mathf.Abs(edge.g - session.GameplayTextColor.g) < .005f &&
                    Mathf.Abs(edge.b - session.GameplayTextColor.b) < .005f, "Magic text and border share the same hue: frame " +
                    index + ", fleet " + session.Fleet.Count + ", edge " + edge + ", text " + session.GameplayTextColor);
            }
        }

        private static void Finish(int code)
        {
            Time.timeScale = 1;
            GameSession.ForceMobileMenuForTests = false;
            GameplayFraming.ForceIphoneNotchOffsetForTests = false;
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(code);
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Gameplay text runtime check failed: " + message); }
    }
}
