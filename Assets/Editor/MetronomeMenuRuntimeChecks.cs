using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class MetronomeMenuRuntimeChecks
    {
        private const string Pending = "CandyCruisers.MetronomeMenuChecks";
        private static readonly MethodInfo Touch = typeof(GameSession).GetMethod("ActivateMenuTouch",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static int stage;
        private static double began, changed;
        static MetronomeMenuRuntimeChecks() => EditorApplication.update += Poll;
        public static void RunDesktop() => RunSized(960, 640, false);
        public static void RunPhone() => RunSized(440, 900, true);
        private static void RunSized(int width, int height, bool phone)
        {
            MetronomeCheckSettings.BeginRuntime(Pending);
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
                Check(EditorApplication.timeSinceStartup - began < 45, "Options playback timed out at stage " + stage);
                bool phone = SessionState.GetBool(Pending + ".Phone", false);
                GameSession.ForceMobileMenuForTests = phone;
                GameplayFraming.ForceIphoneNotchOffsetForTests = phone;
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                if (session == null) return;
                if (session.IsPaused && (stage == 0 || stage == 1 || stage == 2 || stage == 5 || stage == 8)) session.Resume();
                var music = session.GetComponent<GameplayMusicPlayer>();
                var screen = new Vector2(Screen.width, Screen.height);
                float scale = GameSession.GuiScaleFor(phone, screen.x, screen.y);
                var entry = GameSession.MetronomeMenuButtonRect(screen, Screen.safeArea, scale, phone);
                var panel = GameSession.MetronomeMenuRect(screen, Screen.safeArea, scale, phone);
                string prefix = "TestResults/metronome-menu-" + (phone ? "phone-" : "desktop-");
                if (stage == 0)
                {
                    if (session.State != GameSession.RunState.MainMenu) return;
                    session.LevelCatalog = null;
                    if (session.OpeningWarningActive) session.HandleMenuKey(KeyCode.Space);
                    if (!music.Source.isPlaying || session.MenuTitleIntroElapsed < GameSession.MenuWordIntroDuration) return;
                    DeveloperOptions.PlayerInvincible = true;
                    Check(!MetronomeSettings.VoiceEnabled && !MetronomeSettings.VisualEnabled && music.ContainsTrack(music.Source.clip.name),
                        "Fresh playback uses regular music with both metronomes off");
                    Directory.CreateDirectory("TestResults");
                    ScreenCapture.CaptureScreenshot(prefix + "main.png");
                    Next(1);
                }
                else if (stage == 1 && Ready())
                {
                    Check(!session.MetronomeMenuAvailable && !session.MetronomeMenuOpen &&
                        !session.HandleMenuKey(KeyCode.F9) && !session.HandleMenuKey(KeyCode.F10),
                        "Main menu has neither visible metronome controls nor active shortcuts");
                    session.StartRun();
                    Next(2);
                }
                else if (stage == 2 && session.State == GameSession.RunState.Playing)
                {
                    foreach (var presentation in session.GetComponentsInChildren<EnemyPresentation>())
                        if (presentation.IsPhasing) return;
                    session.Pause();
                    Tap(session, entry, screen, scale);
                    Check(session.MetronomeMenuOpen && session.IsPaused, "Pause opens the metronome options");
                    ScreenCapture.CaptureScreenshot(prefix + "off.png");
                    Next(3);
                }
                else if (stage == 3 && Ready())
                {
                    Tap(session, GameSession.MetronomeMenuRowRect(panel, 0), screen, scale);
                    Tap(session, GameSession.MetronomeMenuRowRect(panel, 1), screen, scale);
                    Check(session.IsPaused && MetronomeSettings.VoiceEnabled && MetronomeSettings.VisualEnabled && !music.ShouldPlayMusic,
                        "Both options can be enabled while preserving pause");
                    ScreenCapture.CaptureScreenshot(prefix + "on.png");
                    Next(4);
                }
                else if (stage == 4 && Ready())
                {
                    Tap(session, GameSession.MetronomeMenuRowRect(panel, 2), screen, scale);
                    Check(session.IsPaused && !session.MetronomeMenuOpen, "Back returns to pause without resuming");
                    session.Resume();
                    Next(5);
                }
                else if (stage == 5 && Ready() && music.Source.isPlaying)
                {
                    Check(session.VisualMetronomeVisible && music.Source.loop &&
                        music.Source.clip.name == GameplayMusicPlayer.CountingResourceName && !session.MetronomeMenuAvailable,
                        "The enabled voice and visual metronomes play after resume");
                    ScreenCapture.CaptureScreenshot(prefix + "game.png");
                    Next(6);
                }
                else if (stage == 6 && Ready())
                {
                    session.Pause();
                    Tap(session, entry, screen, scale);
                    Check(session.MetronomeMenuOpen && session.IsPaused, "Pause reopens the saved options");
                    Tap(session, GameSession.MetronomeMenuRowRect(panel, 0), screen, scale);
                    Tap(session, GameSession.MetronomeMenuRowRect(panel, 1), screen, scale);
                    Check(session.IsPaused && !music.ShouldPlayMusic && !MetronomeSettings.VoiceEnabled && !MetronomeSettings.VisualEnabled,
                        "Disabling the metronomes preserves pause");
                    ScreenCapture.CaptureScreenshot(prefix + "pause-off.png");
                    Next(7);
                }
                else if (stage == 7 && Ready())
                {
                    Tap(session, GameSession.MetronomeMenuRowRect(panel, 2), screen, scale);
                    session.Resume();
                    Next(8);
                }
                else if (stage == 8 && Ready() && music.Source.isPlaying)
                {
                    Check(music.ContainsTrack(music.Source.clip.name) && music.Source.clip.name != GameplayMusicPlayer.CountingResourceName &&
                        !session.VisualMetronomeVisible && !session.IsPaused,
                        "Turning the options off restores regular playback and hides the counter");
                    Debug.Log("Metronome menu runtime checks passed: " + (phone ? "phone" : "desktop") +
                        ", clean main menu, pause-only controls, voice playback, visual counter, saved switches and restored music.");
                    Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static bool Ready() => EditorApplication.timeSinceStartup - changed > .35;
        private static void Next(int next) { stage = next; changed = EditorApplication.timeSinceStartup; }
        private static void Tap(GameSession session, Rect rect, Vector2 screen, float scale) => Touch.Invoke(session,
            new object[] { new Vector2(rect.center.x * scale, screen.y - rect.center.y * scale) });
        private static void Finish(int code)
        {
            MetronomeCheckSettings.EndRuntime(Pending);
            SessionState.SetBool(Pending, false);
            EditorApplication.Exit(code);
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Metronome menu runtime check failed: " + message); }
    }
}
