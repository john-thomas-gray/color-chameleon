using System.Reflection;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class EpilepsyWarningChecks
    {
        public static void Run()
        {
            Check(GameSession.EpilepsyWarningSeconds >= 3 &&
                GameSession.EpilepsyWarningTitle.Contains("WARNING") &&
                GameSession.EpilepsyWarningMessage.Contains("photosensitive epilepsy") &&
                GameSession.EpilepsyWarningMessage.Contains("consult a doctor"),
                "Opening warning uses standard photosensitive-epilepsy language");
            foreach (var size in new[] { new Vector2(237, 356), new Vector2(289, 625), new Vector2(625, 289),
                new Vector2(540, 960), new Vector2(1280, 720), new Vector2(1920, 1080) })
            {
                var warning = GameSession.EpilepsyWarningRect(size.x, size.y);
                var title = GameSession.EpilepsyWarningTitleRect(size.x, size.y);
                var message = GameSession.EpilepsyWarningMessageRect(size.x, size.y);
                var skip = GameSession.EpilepsyWarningSkipRect(size.x, size.y);
                Check(warning.xMin >= 0 && warning.xMax <= size.x && warning.yMin >= 0 && warning.yMax <= size.y &&
                    title.x == warning.x && title.width == warning.width && title.y == warning.y &&
                    message.x == warning.x && message.width == warning.width && message.yMin > title.yMax &&
                    message.yMax < skip.yMin && skip.yMax == warning.yMax &&
                    skip.x == warning.x && skip.width == warning.width,
                    "Opening warning stays centered and fits portrait and landscape canvases: " + size);
            }
            Debug.Log("Epilepsy warning checks passed: standard text and skip hint fit portrait and landscape canvases.");
        }

        public static void RunRuntime(GameSession session)
        {
            Check(Application.isPlaying && session.State == GameSession.RunState.MainMenu,
                "Skip checks run in the live opening menu");
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var elapsed = typeof(GameSession).GetField("epilepsyWarningElapsed", flags);
            var shown = typeof(GameSession).GetField("epilepsyWarningShown", BindingFlags.NonPublic | BindingFlags.Static);
            var pointer = typeof(GameSession).GetMethod("ActivateMenuTouch", flags);
            var music = session.GetComponent<GameplayMusicPlayer>();
            float menuElapsed = session.MenuTitleIntroElapsed;
            void ResetWarning()
            {
                elapsed.SetValue(session, 0f);
                shown.SetValue(null, false);
                Check(session.OpeningWarningActive && !music.ShouldPlayMusic, "Warning blocks playback before dismissal");
            }
            void CheckDismissed()
            {
                Check(!session.OpeningWarningActive && session.State == GameSession.RunState.MainMenu &&
                    !session.MenuArriving && session.MenuTitleIntroElapsed == menuElapsed && session.BlocksGameplayInput &&
                    music.ShouldPlayMusic, "Dismissal releases music without starting the menu or gameplay");
            }
            foreach (var key in new[] { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Escape })
            {
                ResetWarning();
                Check(!session.HandleMenuKey(KeyCode.P) && !session.HandleMenuKey(KeyCode.R) &&
                    session.OpeningWarningActive, "Other menu shortcuts leave the warning visible");
                session.Tick(.1f);
                Check(session.HandleMenuKey(key), "Skip key is consumed: " + key);
                CheckDismissed();
            }
            foreach (var point in new[] { Vector2.zero, new Vector2(Screen.width / 2f, Screen.height / 2f),
                new Vector2(Screen.width - 1, Screen.height - 1) })
            {
                ResetWarning();
                Check((bool)pointer.Invoke(session, new object[] { point }), "Tap or click is consumed anywhere on screen");
                CheckDismissed();
            }
            ResetWarning();
            session.Tick(GameSession.EpilepsyWarningSeconds - .01f);
            Check(session.OpeningWarningActive, "Automatic dismissal still waits the full four seconds");
            session.Tick(.02f);
            CheckDismissed();
            session.Tick(0);
            Check(!session.OpeningWarningActive, "Completed warning stays dismissed");
            Debug.Log("Epilepsy warning runtime checks passed: skip keys, pointer dismissal, input isolation, music release and automatic timeout.");
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new System.Exception("Epilepsy warning check failed: " + message); }
    }
}
