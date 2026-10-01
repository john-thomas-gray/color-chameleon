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
            CheckComboMilestonePresentation();
            CheckComboBreakPresentation();
            CheckComboDeathFade();
            CheckComboColor();
            CheckScoreCalculation();
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
            Check(progress.RegisterShotColor(EnemyColor.Red) && progress.ComboStreak == 1 && progress.ActiveComboMultiplier == 2,
                "A new hit color immediately advances the active combo");
            Check(!progress.RegisterShotColor(EnemyColor.Red) && progress.ComboStreak == 1,
                "The same color cannot advance a shot twice");
            Check(progress.RegisterClear(1, false) == 200 && progress.RegisterShotColor(EnemyColor.Blue) &&
                progress.ComboStreak == 2 && progress.ActiveComboMultiplier == 3,
                "The next color scores with the increased multiplier before advancing it again");
            progress.FinishShot(true);
            progress.FinishShot(true);
            Check(progress.ComboStreak == 2 && progress.ComboMultiplier == 3 &&
                !progress.RegisterShotColor(EnemyColor.Green), "Completing a shot never awards an extra streak or leaves it active");
            progress.BeginShot();
            Check(progress.RegisterClear(1, false) == 300 && progress.RegisterShotColor(EnemyColor.Red) &&
                progress.ActiveComboMultiplier == 4, "A new shot can earn another streak for the same color");
            progress.FinishShot(false);
            Check(progress.ComboStreak == 0 && progress.ComboMultiplier == 1 && progress.ActiveComboMultiplier == 1,
                "Miss resets combo multiplier");
        }

        private static void CheckComboMilestonePresentation()
        {
            Check(!GameSession.IsComboMilestone(4) && GameSession.IsComboMilestone(5) &&
                !GameSession.IsComboMilestone(24) && GameSession.IsComboMilestone(25) &&
                !GameSession.IsComboMilestone(50) && !GameSession.IsComboMilestone(74) &&
                GameSession.IsComboMilestone(75) && GameSession.IsComboMilestone(125),
                "Combo milestone thresholds are displayed x5, x25, x75 and every fifty after");
            Check(!GameSession.ShouldDrawComboMultiplier(1) && GameSession.ShouldDrawComboMultiplier(2),
                "Baseline x1 combo stays hidden until the streak raises the multiplier");
            var resting = GameSession.ComboMultiplierRestRect(10, 360, 20);
            Check(Mathf.Abs(resting.xMax - 370) < .001f && resting.yMin > 46 &&
                resting.yMax < 20 + PlayerLifeIcons.ProgressOffset(960),
                "Combo multiplier rests below the level at the top-right, above the progress bar");
            var start = GameSession.ComboMultiplierDisplayRect(resting, 400, 800, 0);
            Check(Close(start.center, new Vector2(200, 400)) && start.width > resting.width * 2,
                "Milestone combo starts enlarged at screen center");
            var pulse = GameSession.ComboMultiplierDisplayRect(resting, 400, 800, GameSession.ComboMilestonePulseSeconds * .25f);
            Check(Close(pulse.center, new Vector2(200, 400)) && Mathf.Abs(pulse.width - start.width) > .1f,
                "Milestone combo pulses before traveling");
            var shrink = GameSession.ComboMultiplierDisplayRect(resting, 400, 800,
                GameSession.ComboMilestonePulseSeconds + (GameSession.ComboMilestoneAnimationSeconds - GameSession.ComboMilestonePulseSeconds) * .5f);
            Check(shrink.center.x > 200 && shrink.center.x < resting.center.x &&
                shrink.center.y < 400 && shrink.center.y > resting.center.y &&
                shrink.width < pulse.width && shrink.width > resting.width,
                "Milestone combo shrinks toward the resting corner");
            Check(Close(GameSession.ComboMultiplierDisplayRect(resting, 400, 800,
                GameSession.ComboMilestoneAnimationSeconds), resting),
                "Milestone combo settles into its usual location");
            CheckGameSessionComboMilestoneTrigger();
        }

        private static void CheckGameSessionComboMilestoneTrigger()
        {
            var root = new GameObject("Combo milestone test session",
                typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            try
            {
                var session = root.AddComponent<GameSession>();
                AdvanceCombo(session, 3);
                Check(!ComboMilestoneActive(session), "Sub-threshold streaks do not trigger combo presentation");
                AdvanceCombo(session, 4);
                Check(ComboMilestoneActive(session) && ComboMilestoneMultiplier(session) == 5,
                    "Displayed x5 triggers the combo multiplier presentation");
                session.Tick(GameSession.ComboMilestoneAnimationSeconds);
                Check(!ComboMilestoneActive(session), "Combo milestone presentation expires");
                AdvanceCombo(session, 23);
                Check(!ComboMilestoneActive(session), "Intermediate streaks stay quiet after the first milestone");
                AdvanceCombo(session, 24);
                Check(ComboMilestoneActive(session) && ComboMilestoneMultiplier(session) == 25,
                    "Displayed x25 triggers the milestone presentation");
                session.Tick(GameSession.ComboMilestoneAnimationSeconds);
                AdvanceCombo(session, 73);
                Check(!ComboMilestoneActive(session), "Displayed x74 stays quiet after x25");
                AdvanceCombo(session, 74);
                Check(ComboMilestoneActive(session) && ComboMilestoneMultiplier(session) == 75,
                    "Displayed x75 triggers the repeating milestone presentation");
                session.Progress.BeginShot();
                session.Progress.FinishShot(false);
                session.Tick(0);
                Check(!ComboMilestoneActive(session), "Misses clear active combo milestone presentation");
                Check(session.ComboBreak.Active && session.ComboBreak.Multiplier == 75,
                    "A miss retains the lost multiplier for its break animation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckComboBreakPresentation()
        {
            foreach (var size in new[] { new Vector2(390, 844), new Vector2(960, 540), new Vector2(320, 320) })
            {
                var resting = GameSession.ComboMultiplierRestRect(10, size.x - 74, 10, (int)size.y);
                Check(resting.yMin >= 26 && resting.yMax < 10 + PlayerLifeIcons.ProgressOffset((int)size.y),
                    "Compact multiplier clears the heading and progress bar");
                var animation = new ComboBreakAnimation();
                var originalColor = EnemyPalette.Get(EnemyColor.Blue);
                animation.Begin(26, originalColor);
                Check(Close(animation.DisplayRect(resting, size.x, size.y), resting), "Broken combo departs from its resting location");
                Check(animation.Tint == originalColor && animation.DrainProgress == 0 && animation.SlashOpacity == 0,
                    "Break departs in the last defeated enemy's color without slashing early");
                animation.Tick(ComboBreakAnimation.TravelSeconds);
                var center = animation.DisplayRect(resting, size.x, size.y);
                Check(Close(center.center, size / 2) && center.width > resting.width && center.xMin >= 0 && center.xMax <= size.x,
                    "Lost multiplier enlarges at screen center and fits portrait and landscape views");
                Check(animation.SplitProgress == 0 && animation.Opacity == 1, "Number remains whole until it reaches the center");
                Check(animation.Tint == originalColor, "Travel retains the original color until centerstage");
                animation.Tick(ComboBreakAnimation.DrainSeconds / 2);
                Check(animation.Tint.r > originalColor.r && animation.Tint.r < 1 && animation.Tint.b >= originalColor.b &&
                    animation.SplitProgress == 0 && animation.SlashOpacity == 0 && animation.Opacity == 1,
                    "Centerstage drains saturation toward white without fading, slashing or separating early");
                animation.Tick(ComboBreakAnimation.DrainSeconds / 2);
                Check(animation.Tint == Color.white && animation.SplitProgress == 0 && animation.Opacity == 1,
                    "The whole combo is stark white before the slash");
                animation.Tick(ComboBreakAnimation.SlashSeconds / 2);
                Check(animation.SlashProgress > 0 && animation.SlashProgress < 1 && animation.SlashOpacity == 1 &&
                    animation.Tint == Color.white && animation.SplitProgress == 0,
                    "A visible slash crosses the white number before the halves separate");
                animation.Tick(ComboBreakAnimation.SlashSeconds / 2 + ComboBreakAnimation.SplitSeconds / 2);
                Check(animation.HalfOffset(center.height, false).x < 0 && animation.HalfOffset(center.height, true).x > 0 &&
                    animation.Opacity > 0 && animation.Opacity < 1 && animation.Tint == Color.white && animation.SlashOpacity == 0,
                    "White halves separate and fade after the slash");
                float age = animation.Age;
                animation.Tick(-1);
                Check(animation.Age == age, "Negative time cannot rewind the break");
                animation.Tick(10);
                Check(!animation.Active && animation.Opacity == 0, "Long frames finish the break cleanly");
                animation.Begin(3, originalColor); animation.Reset();
                Check(!animation.Active, "Run reset removes a pending break");
            }

            var phonePixels = new Vector2(1179, 2556);
            float phoneScale = GameSession.GuiScaleFor(true, phonePixels.x, phonePixels.y);
            var phoneCanvas = phonePixels / phoneScale;
            var phoneResting = GameSession.ComboMultiplierRestRect(10, phoneCanvas.x - 74, 10, Mathf.RoundToInt(phoneCanvas.y));
            var phoneBreak = new ComboBreakAnimation();
            phoneBreak.Begin(99, Color.red);
            phoneBreak.Tick(ComboBreakAnimation.TravelSeconds);
            var logicalCenter = phoneBreak.DisplayRect(phoneResting, phoneCanvas.x, phoneCanvas.y).center * phoneScale;
            var rawScreenCenter = phoneBreak.DisplayRect(phoneResting, phonePixels.x, phonePixels.y).center * phoneScale;
            Check(Close(logicalCenter, phonePixels / 2),
                "Mobile combo break targets the scaled GUI canvas center");
            Check(rawScreenCenter.x >= phonePixels.x && rawScreenCenter.y >= phonePixels.y,
                "Regression guard: raw screen coordinates push the scaled mobile break offscreen");

            var coarse = new ComboBreakAnimation();
            var fine = new ComboBreakAnimation();
            coarse.Begin(75, Color.red); fine.Begin(75, Color.red);
            coarse.Tick(.75f);
            for (int i = 0; i < 75; i++) fine.Tick(.01f);
            Check(Mathf.Abs(coarse.SplitProgress - fine.SplitProgress) < .0001f && coarse.Tint == fine.Tint,
                "Travel, drain and slash timing is stable across long and short frames");
        }

        private static void CheckComboDeathFade()
        {
            foreach (bool shotInFlight in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                try
                {
                    var sentinel = ProgressionChecks.Add(grid, EnemyColor.Red, 4, 0);
                    var target = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                    grid.ClearMatchingChain(target.Id, EnemyColor.Blue);
                    AdvanceCombo(session, 4);
                    Check(ComboMilestoneActive(session), "Fixture interrupts a combo milestone");
                    if (shotInFlight)
                    {
                        player.RefreshColor(true);
                        Check(player.Fire() && tongue.Active, "Damage fixture starts an uncompleted shot");
                    }
                    Check(player.Hit() && player.LifeIcons.Active && !session.ComboBreak.Active &&
                        !ComboMilestoneActive(session) && session.ComboDeathMultiplier == 5 &&
                        session.ComboDeathOpacity == 1 && session.Progress.ComboMultiplier == 1,
                        "Death keeps only the life-loss animation and a fading copy of the old corner combo");
                    Check(session.ComboDeathColor == EnemyPalette.Get(EnemyColor.Blue),
                        "Death fade keeps the last destroyed enemy color");
                    session.Tick(GameSession.ComboDeathFadeSeconds / 2);
                    Check(Mathf.Abs(session.ComboDeathOpacity - .5f) < .001f && !session.ComboBreak.Active,
                        "Corner combo fades smoothly without launching a slash");
                    float opacity = session.ComboDeathOpacity;
                    session.Tick(0); session.Tick(-1);
                    Check(session.ComboDeathOpacity == opacity, "Nonpositive time leaves the fade unchanged");
                    session.Pause();
                    session.Tick(5);
                    Check(session.ComboDeathOpacity == opacity, "Pause freezes the corner fade");
                    session.Resume();
                    grid.ClearMatchingChain(sentinel.Id, EnemyColor.Red);
                    Check(session.ComboColor == EnemyPalette.Get(EnemyColor.Red) &&
                        session.ComboDeathColor == EnemyPalette.Get(EnemyColor.Blue),
                        "Later enemy deaths cannot recolor the fading old combo");
                    session.Tick(GameSession.ComboDeathFadeSeconds);
                    Check(session.ComboDeathOpacity == 0 && !session.ComboBreak.Active,
                        "Fade finishes cleanly without bringing back the combo break");
                    player.TickSurvival(4);
                    Check(player.Hit() && session.ComboDeathOpacity == 0 && !session.ComboBreak.Active,
                        "Dying at the baseline multiplier does not display a fading x1");
                    InvokeLifecycle(session, "ResetComboPresentation");
                    Check(session.ComboDeathMultiplier == 0 && session.ComboDeathOpacity == 0,
                        "Reset clears the retained death-fade multiplier");
                }
                finally
                {
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                }
            });

            foreach (bool fatalHit in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                if (fatalHit)
                    for (int i = 0; i < PlayerMovement.MaxExtraLives; i++)
                    { player.Hit(); player.TickSurvival(4); }
                AdvanceCombo(session, 3);
                if (fatalHit) Check(player.Hit(), "Last-life hit starts fatal defeat");
                else
                {
                    session.Progress.ResetCombo();
                    session.Tick(0);
                    Check(session.ComboBreak.Active, "Fixture starts a normal missed-shot combo break");
                    InvokeLifecycle(session, "BeginPlayerDeath");
                }
                Check(session.State == GameSession.RunState.Dying && !session.ComboBreak.Active &&
                    session.ComboDeathMultiplier == 4 && session.ComboDeathOpacity == 1,
                    "Fatal defeat also replaces any combo break with the corner fade");
                session.Tick(GameSession.ComboDeathFadeSeconds + .01f);
                Check(session.ComboDeathOpacity == 0 && !session.ComboBreak.Active,
                    "Fatal blackout allows the corner combo to finish fading");
            });
        }

        private static void CheckComboColor()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                try
                {
                    Check(session.ComboColor == Color.white && session.ComboFont != null &&
                        session.ComboFont == Resources.Load<Font>("Fonts/Bungee-Regular"),
                        "Combo uses bundled Bungee and begins neutral before any enemy is destroyed");
                    var sentinel = ProgressionChecks.Add(grid, EnemyColor.Red, 4, 5);
                    foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                    {
                        var target = ProgressionChecks.Add(grid, color, 2, 0);
                        grid.ClearMatchingChain(target.Id, color);
                        Check(session.ComboColor == EnemyPalette.Get(color), "Combo follows destroyed " + color + " enemies");
                        player.SetCelebrationColor(Color.gray);
                        Check(session.ComboColor == EnemyPalette.Get(color), "Player color changes do not recolor the combo");
                    }
                    var previous = session.ComboColor;
                    grid.SetColor(sentinel.Id, EnemyColor.Blue);
                    grid.ClearMatchingChain(sentinel.Id, EnemyColor.Red);
                    Check(session.ComboColor == previous, "Conversion and mismatches are not enemy destruction");
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                    var yellow = ProgressionChecks.Add(grid, EnemyColor.Yellow, 2, 1);
                    grid.Model.BeginImitation(yellow.Id, red.Id);
                    grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                    Check(session.ComboColor == EnemyPalette.Get(EnemyColor.Yellow),
                        "A linked mixed-color chain uses its final destroyed enemy, not the shot color");
                    AdvanceCombo(session, 3);
                    session.Progress.FinishShot(false);
                    session.Tick(0);
                    Check(session.ComboBreak.SourceColor == EnemyPalette.Get(EnemyColor.Yellow),
                        "Combo break snapshots the last destroyed color");
                    red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                    grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                    Check(session.ComboColor == EnemyPalette.Get(EnemyColor.Red) &&
                        session.ComboBreak.SourceColor == EnemyPalette.Get(EnemyColor.Yellow),
                        "A new destruction updates the live combo without recoloring an existing break");
                    InvokeLifecycle(session, "ResetComboPresentation");
                    Check(session.ComboColor == Color.white && !session.ComboBreak.Active,
                        "A new run clears the previous enemy color and break");
                }
                finally
                {
                    player.SetCelebrationColor(null);
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                }
            });
        }

        private static void CheckScoreCalculation()
        {
            Check(GameSession.ScoreCalculation(200, 3, 0) == "200 x3 = +600", "Score calculation explicitly multiplies by the combo");
            Check(GameSession.ScoreCalculation(200, 3, 10000) == "200 x3 + " + 10000.ToString("N0") + " = +" + 10600.ToString("N0"),
                "Fleet bonus is added after the multiplier, matching awarded points");
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) InvokeLifecycle(session, "OnEnable");
                try
                {
                    AdvanceCombo(session, 2);
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                    player.RefreshColor();
                    var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 4, 0);
                    ProgressionChecks.Add(grid, EnemyColor.Green, 0, 0);
                    Check(player.Fire(), "Score display fixture accepts the shot");
                    long before = session.Progress.Score;
                    grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                    grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                    long earned = session.Progress.Score - before;
                    Check(earned == 700 && session.ScoreFeedback == "300 + 100 x4 = +700",
                        "Multiple clears in one shot accumulate in one accurate combo equation: earned " +
                        earned + ", feedback " + session.ScoreFeedback);
                    tongue.Cancel();
                }
                finally
                {
                    if (!Application.isPlaying) InvokeLifecycle(session, "OnDisable");
                }
            });
        }

        private static void AdvanceCombo(GameSession session, int streak)
        {
            while (session.Progress.ComboStreak < streak)
            {
                session.Progress.BeginShot();
                session.Progress.RegisterShotColor(EnemyColor.Red);
                session.Progress.FinishShot(true);
                session.Tick(0);
            }
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
                    Check(session.ScoreFeedback == "100 x2 = +200", "Completed shot keeps its actual multiplier after the next combo advances");
                    grid.Unregister(sentinel); UnityEngine.Object.DestroyImmediate(sentinel.gameObject);
                    Check(!player.Fire() && session.Progress.ComboMultiplier == 3,
                        "Rejected empty-field fire leaves combo intact");

                    var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, GridModel.Columns - 1, 5);
                    player.transform.position = new Vector3(-PlayerMovement.HalfWidth + .1f, player.transform.position.y, 0);
                    player.RefreshColor(true);
                    Check(player.Fire() && player.ReadyColor == EnemyColor.Blue, "Miss shot fires with a represented color");
                    tongue.Tick(.01f, grid);
                    Check(!tongue.Retracting && !session.ComboBreak.Active && session.Progress.ComboMultiplier == 3,
                        "The combo remains intact while the missed shot extends");
                    for (int i = 0; i < 200 && !tongue.Retracting; i++) tongue.Tick(.01f, grid);
                    Check(tongue.Retracting && session.ComboBreak.Active && session.ComboBreak.Multiplier == 3 &&
                        session.Progress.ComboStreak == 0, "Combo break starts as soon as the tongue begins returning");
                    session.ComboBreak.Tick(.1f);
                    float breakAge = session.ComboBreak.Age;
                    tongue.Tick(10, grid);
                    Check(session.ComboBreak.Age == breakAge, "Tongue arrival does not restart the combo-break animation");
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
                Check(framing.SolidBlackBackground, "Solid black is the default background");
                framing.SolidBlackBackground = false;
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
                session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated, true);
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
                framing.SolidBlackBackground = true;
                session.Progress.RegisterClear(session.Progress.NextThreshold - session.Progress.Defeated, true);
                framing.TriggerWaveSpawnEffect(); framing.TickBackground(.1f);
                var camera = cameraObject.GetComponent<Camera>();
                Check(camera.clearFlags == CameraClearFlags.SolidColor && camera.backgroundColor == Color.black &&
                    !background.enabled && framing.WavePulseRemaining == 0 && framing.LevelTransitionRemaining == 0,
                    "Black background keeps ordinary starfields hidden without running a level transition");
                foreach (var layer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (layer.name == background.name + " Crossfade")
                        Check(!layer.enabled, "Old starfield crossfade is hidden in black mode");
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
        private static bool Close(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .01f;
        private static bool Close(Rect a, Rect b) =>
            Close(a.position, b.position) && Close(a.size, b.size);

        private static bool ComboMilestoneActive(GameSession session) =>
            ComboMilestoneMultiplier(session) > 1 && ComboMilestoneElapsed(session) < GameSession.ComboMilestoneAnimationSeconds;
        private static int ComboMilestoneMultiplier(GameSession session) => (int)typeof(GameSession)
            .GetField("comboMilestoneMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(session);
        private static float ComboMilestoneElapsed(GameSession session) => (float)typeof(GameSession)
            .GetField("comboMilestoneElapsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(session);

        private static void InvokeLifecycle(GameSession session, string method) => typeof(GameSession)
            .GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(session, null);

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Combo/leaderboard/background check failed: " + message);
        }
    }
}
