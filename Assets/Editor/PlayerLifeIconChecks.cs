using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerLifeIconChecks
    {
        public static void Run()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                player.RefreshColor();
                Check(player.Lives == PlayerMovement.MaxLives && player.ExtraLives == PlayerMovement.MaxExtraLives && !player.GrantLife(),
                    "Two spares plus the active player, capped at three total lives");
                for (int hit = 1; hit <= PlayerMovement.MaxExtraLives; hit++)
                {
                    Check(player.Hit() && player.Lives == PlayerMovement.MaxLives - hit &&
                        player.ExtraLives == PlayerMovement.MaxExtraLives - hit && !player.FatallyDefeated,
                        "Each spare-life death spends one spare and permits recovery");
                    var icons = player.LifeIcons;
                    Check(icons.Active && icons.ConsumedSlot == PlayerMovement.MaxExtraLives - hit && !icons.Started,
                        "The rightmost remaining icon waits for player death");
                    Check(!player.Hit() && icons.ConsumedSlot == PlayerMovement.MaxExtraLives - hit,
                        "Repeated damage never spends a second icon");
                    player.TickSurvival(PlayerDeathBurst.Duration - .01f);
                    Check(!player.Alive && !icons.Started && icons.GrowthScale == 1, "Icons remain intact during the player's death");
                    player.TickSurvival(.01f + PlayerLifeIcons.TravelSeconds / 2);
                    Check(!player.Alive && icons.Started && icons.TravelProgress > 0 && icons.TravelProgress < 1 &&
                        icons.SplitProgress == 0, "The spent spare starts the combo-break travel after the player death");
                    float age = icons.Age;
                    player.TickSurvival(-10);
                    Check(icons.Age == age, "Negative time cannot reverse the life animation");
                    player.TickSurvival(PlayerLifeIcons.TravelSeconds / 2 + PlayerLifeIcons.DrainSeconds + PlayerLifeIcons.SlashSeconds / 2 + .01f);
                    Check(player.Alive && player.Invulnerable && icons.TravelProgress == 1 && icons.DrainProgress == 1 &&
                        icons.SlashProgress > 0 && icons.SlashProgress < 1 && icons.SlashOpacity == 1,
                        "The centerstage spare drains white and receives the combo-break slash during protected respawn");
                    player.TickSurvival(PlayerLifeIcons.SlashSeconds / 2 + PlayerLifeIcons.SplitSeconds / 2);
                    Check(player.Alive && player.Invulnerable && icons.SplitProgress > 0 && icons.SplitProgress < 1 &&
                        icons.Opacity > 0 && icons.Opacity < 1,
                        "The slashed spare splits and fades without delaying respawn");
                    player.TickSurvival(4);
                    Check(player.Alive && !player.Invulnerable && !icons.Active, "The consumed icon disappears and recovery completes");
                }
                Check(player.Alive && player.ExtraLives == 0 && player.Fire(), "The final active player can still play with no spare icons");
                Check(player.Hit() && player.FatallyDefeated && player.Lives == 0 && !player.LifeIcons.Active,
                    "Only the third death ends the run, without inventing a spare-icon animation");
                Check(!player.GrantLife(), "A finished run cannot be revived by a late reward");
                player.ResetForRun();
                Check(player.Lives == PlayerMovement.MaxLives && player.ExtraLives == PlayerMovement.MaxExtraLives &&
                    !player.LifeIcons.Active, "Restart restores two intact icons");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                player.Hit();
                player.TickSurvival(PlayerDeathBurst.Duration + .08f);
                session.Pause();
                float age = player.LifeIcons.Age;
                player.TickSurvival(10);
                Check(player.LifeIcons.Age == age && !player.Alive, "Pause freezes the spare-life animation and recovery");
                session.Resume();
                Check(player.GrantLife() && player.ExtraLives == PlayerMovement.MaxExtraLives && !player.GrantLife(),
                    "Rewards replenish one spare, even during recovery, without exceeding the cap");
                player.TickSurvival(4);
                Check(player.Alive && player.ExtraLives == PlayerMovement.MaxExtraLives && !player.LifeIcons.Active,
                    "A replenished spare remains after the consumed icon finishes");
                player.Hit();
                player.ResetForRun();
                Check(!player.LifeIcons.Active && player.ExtraLives == PlayerMovement.MaxExtraLives,
                    "Reset cancels a pending icon loss");
            });

            if (Application.isPlaying)
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var custom = new GameObject("Long life-icon death", typeof(PresentationCue)).GetComponent<PresentationCue>();
                    try
                    {
                        var settings = new UnityEditor.SerializedObject(custom);
                        settings.FindProperty("duration").floatValue = 2;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        CharacterVisuals.Ensure(player.gameObject).SetCuePrefabs(null, null, custom);
                        player.Hit(); player.TickSurvival(1.5f);
                        Check(!player.Alive && !player.LifeIcons.Started, "Long replacement death finishes before the spare grows");
                        player.TickSurvival(.5f + PlayerLifeIcons.TravelSeconds / 2);
                        Check(player.Alive && player.Invulnerable && player.LifeIcons.Started && player.LifeIcons.TravelProgress > 0,
                            "Replacement death still leads into the spare combo-break animation without delaying respawn");
                        player.TickSurvival(PlayerLifeIcons.Duration - PlayerLifeIcons.TravelSeconds / 2 + .01f);
                        Check(player.Alive && !player.LifeIcons.Active, "Replacement spare break finishes cleanly after respawn");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(custom.gameObject); }
                });

            Check(PlayerLifeIcons.BeatScale(0) == 1 && Mathf.Abs(PlayerLifeIcons.BeatScale(.5f) - 1.09f) < .0001f,
                "Miniature players pulse on the same opposite beat as the full-size player");
            foreach (var size in new[] { new Vector2(320, 320), new Vector2(390, 844), new Vector2(960, 540), new Vector2(1179, 2556) })
            for (int slot = 0; slot < PlayerMovement.MaxExtraLives; slot++)
            {
                int height = Mathf.RoundToInt(size.y);
                var row = PlayerLifeIcons.RowRect(12, 0, height);
                float textBottom = height < 400 ? 16 : height < 600 ? 18 : 26;
                float progressTop = PlayerLifeIcons.ProgressOffset(height);
                var normal = PlayerLifeIcons.IconRect(row, slot);
                var departing = PlayerLifeIcons.LossDisplayRect(normal, size.x, size.y, 0);
                var centerstage = PlayerLifeIcons.LossDisplayRect(normal, size.x, size.y, PlayerLifeIcons.TravelSeconds);
                Check(Close(departing, normal), "Spent spare departs from its life-indicator slot");
                Check(Close(centerstage.center, size / 2) && centerstage.xMin >= 0 && centerstage.xMax <= size.x &&
                    centerstage.yMin >= 0 && centerstage.yMax <= size.y,
                    "Spent spare uses the combo-break centerstage target on portrait and landscape canvases");
                Check(normal.yMin > textBottom && normal.yMax < progressTop && normal.xMax < row.x + 122,
                    "Stable icon slots stay between the score and progress bar, away from status text");
                if (slot > 0) Check(normal.xMin > PlayerLifeIcons.IconRect(row, slot - 1, 1.09f).xMax,
                    "The animated spare leaves neighboring idle spares untouched");
                var animation = new PlayerLifeIcons();
                animation.BeginLoss(slot);
                animation.Tick(PlayerLifeIcons.TravelSeconds + PlayerLifeIcons.DrainSeconds +
                    PlayerLifeIcons.SlashSeconds + PlayerLifeIcons.SplitSeconds / 2);
                Check(animation.HalfOffset(centerstage.height, false).x < 0 &&
                    animation.HalfOffset(centerstage.height, true).x > 0 && animation.Opacity > 0 && animation.Opacity < 1,
                    "Spent spare halves separate like the combo-break graphic");
            }
            Debug.Log("Player life icon checks passed: two spare players, third-death game over, combo-break spare loss, pause, long frames, replacement cues, rewards and restart.");
        }
        private static bool Close(Rect a, Rect b) => Close(a.position, b.position) && Close(a.size, b.size);
        private static bool Close(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .001f;
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Player life icon check failed: " + message); }
    }
}
