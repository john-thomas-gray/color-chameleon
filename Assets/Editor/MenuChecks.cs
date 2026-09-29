using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MenuChecks
    {
        public static void Run()
        {
            Check(GameSession.GuiScaleFor(false, 1179, 2556) == 1, "Desktop builds keep the original interface scale");
            Check(GameSession.GuiScaleFor(true, 390, 844) == GameSession.SmallPhoneGuiScale,
                "Compact phone screens still enlarge the interface");
            Check(GameSession.GuiScaleFor(true, 750, 1334) == GameSession.MediumPhoneGuiScale,
                "Mid-density phone screens use the medium interface scale");
            Check(GameSession.GuiScaleFor(true, 1179, 2556) == GameSession.LargePhoneGuiScale,
                "Dense iPhone-class screens use the large interface scale");
            CheckMainMenuTitle();
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
                    Check(session.HandleMenuKey(KeyCode.P), "P activates pause");
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
                    Check(!session.HandleMenuKey(KeyCode.M) && !session.HandleMenuKey(KeyCode.LeftArrow) &&
                        !session.HandleMenuKey(KeyCode.Alpha4), "Menu-only hotkeys do not consume gameplay movement or open menus");
                    session.HandleMenuKey(KeyCode.Escape);
                    Check(session.IsPaused && session.HandleMenuKey(KeyCode.KeypadEnter) && !session.IsPaused,
                        "Escape pause and keypad Enter resume work");
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
                    Check(session.HandleMenuKey(KeyCode.Alpha9) && session.StartLevel == 9, "Typing one digit selects that start level");
                    Check(session.HandleMenuKey(KeyCode.Alpha9) && session.StartLevel == 99, "Typing two digits selects level ninety-nine");
                    Check(session.HandleMenuKey(KeyCode.Alpha0) && session.StartLevel == RunProgress.MinLevel,
                        "Typing after two digits starts a fresh capped menu entry");
                    Check(session.HandleMenuKey(KeyCode.Keypad7) && session.StartLevel == 7, "Numeric keypad digits continue typed level entry");
                    session.StartLevel = 12;
                    Check(session.HandleMenuKey(KeyCode.RightArrow) && session.StartLevel == 13, "Right increases starting level");
                    Check(session.HandleMenuKey(KeyCode.LeftArrow) && session.StartLevel == 12, "Left decreases starting level");
                    Check(!session.HandleMenuKey(KeyCode.R) && !session.HandleMenuKey(KeyCode.M), "Unavailable menu actions are ignored");
                    Check(session.HandleMenuKey(KeyCode.Return), "Enter starts the selected level");
                    Check(session.Progress.Level == 12 && session.Progress.Defeated == session.Progress.PreviousThreshold &&
                        session.Progress.Score == 0, "Play starts at the selected level without score");
                    Check(session.State == GameSession.RunState.Refilling && grid.Model.Count == 0,
                        "Selected level start keeps the empty musical opening beat");
                    session.Tick(grid.GetComponent<GameplayMusicPlayer>().BeatDuration + .001f);
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
        private static void CheckMainMenuTitle()
        {
            Check(GameSession.MainMenuTitle == "BASS INVADERS",
                "Main menu title uses the requested top line");
            Check(GameSession.MainMenuSubtitle == "THE RHYTHM IS OUT THERE",
                "Main menu subtitle uses the requested second line");
            var font = Resources.Load<Font>("Fonts/Bungee-Regular");
            Check(font != null, "Main menu title font is bundled");
            CheckMainMenuTitleLayout(new Vector2(540, 960), false, font);
            CheckMainMenuTitleLayout(new Vector2(390, 844), true, font);
            CheckMainMenuTitleLayout(new Vector2(1179, 2556), true, font);
        }

        private static void CheckMainMenuTitleLayout(Vector2 screen, bool mobile, Font font)
        {
            float scale = GameSession.GuiScaleFor(mobile, screen.x, screen.y);
            var canvas = screen / scale;
            float anchor = GameSession.MenuAnchorYFor(canvas.y, true);
            var rect = GameSession.MainMenuTitleRect(canvas.x, canvas.y, anchor);
            var titleStyle = new GUIStyle
            {
                font = font,
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset()
            };
            titleStyle.normal.textColor = Color.white;
            var subtitleStyle = new GUIStyle(titleStyle) { fontSize = 18 };
            var titleRect = GameSession.MainMenuTitleLineRect(rect);
            var subtitleRect = GameSession.MainMenuSubtitleLineRect(rect);
            int titleSize = GameSession.FitMainMenuTitle(titleStyle, titleRect);
            int subtitleSize = GameSession.FitMainMenuSubtitle(subtitleStyle, subtitleRect);
            var titleContent = new GUIContent(GameSession.MainMenuTitle);
            var subtitleContent = new GUIContent(GameSession.MainMenuSubtitle);
            float titleHeight = titleStyle.CalcHeight(titleContent, titleRect.width);
            float subtitleHeight = subtitleStyle.CalcHeight(subtitleContent, subtitleRect.width);
            Check(titleStyle.font == font && titleStyle.normal.textColor == Color.white &&
                subtitleStyle.font == font && subtitleStyle.normal.textColor == Color.white,
                "Main menu title uses the combo number font in white");
            Check(rect.xMin >= 0 && rect.xMax <= canvas.x && rect.yMin >= 0 && rect.yMax <= canvas.y,
                "Main menu title stays on the visible menu canvas");
            Check(titleStyle.CalcSize(titleContent).x <= titleRect.width + .01f &&
                titleHeight <= titleRect.height + .01f && titleSize >= 20,
                "Main menu title fits on one large top line");
            Check(subtitleStyle.CalcSize(subtitleContent).x <= subtitleRect.width + .01f &&
                subtitleHeight <= subtitleRect.height + .01f && subtitleSize >= 11,
                "Main menu subtitle fits on one smaller second line");
            Check(titleStyle.wordWrap == false && subtitleStyle.wordWrap == false && titleSize > subtitleSize,
                "Main menu title is larger than the subtitle and neither line wraps");
            Check(titleRect.yMax <= subtitleRect.yMin + .01f && subtitleRect.yMax <= rect.yMax + .01f,
                "Main menu subtitle sits under the title within the title block");
            Check(rect.yMax <= anchor - 144 + .01f,
                "Main menu title clears the enemy preview row");
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Menu check failed: " + message); }
        private static void InvokeLifecycle(GameSession session, string method) => typeof(GameSession)
            .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(session, null);
    }
}
