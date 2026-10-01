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
                    player.TickSurvival(PlayerLifeIcons.DefaultStartDelay - .01f);
                    Check(!player.Alive && !icons.Started && icons.GrowthScale == 1,
                        "The spent spare waits in its slot until one beat after the hit");
                    player.TickSurvival(.02f);
                    Check(!player.Alive && icons.Started && icons.TravelProgress > 0 && icons.TravelProgress < 1 &&
                        icons.SplitProgress == 0, "The spent spare starts moving on the next beat while the death cue can continue");
                    float age = icons.Age;
                    player.TickSurvival(-10);
                    Check(icons.Age == age, "Negative time cannot reverse the life animation");
                    player.TickSurvival(PlayerLifeIcons.DefaultStartDelay);
                    Check(!player.Alive && icons.TravelProgress == 1 && icons.DrainProgress == 1 &&
                        icons.SlashProgress > 0 && icons.SlashProgress < 1 && icons.SlashOpacity == 1,
                        "The centerstage spare drains white and receives the combo-break slash on the following beat");
                    player.TickSurvival(.5f);
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
                grid.GetComponent<GameplayMusicPlayer>().StopPlayback();
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
                        player.Hit();
                        player.TickSurvival(PlayerLifeIcons.DefaultStartDelay - .01f);
                        Check(!player.Alive && !player.LifeIcons.Started, "Long replacement death still waits one beat before the spare grows");
                        player.TickSurvival(.02f + PlayerLifeIcons.TravelSeconds / 2);
                        Check(!player.Alive && player.LifeIcons.Started && player.LifeIcons.TravelProgress > 0,
                            "Replacement death no longer delays the spare combo-break animation");
                        player.TickSurvival(2);
                        Check(player.Alive && !player.LifeIcons.Active, "Replacement spare break finishes cleanly after respawn");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(custom.gameObject); }
                });

            CheckGainAnimation();
            CheckGainLifecycle();
            PlayerLifeRewardChecks.Run();
            if (Application.isPlaying) CheckBeatTiming();
            Check(PlayerLifeIcons.BeatScale(0) == 1 && Mathf.Abs(PlayerLifeIcons.BeatScale(.5f) - 1.09f) < .0001f,
                "Miniature players pulse on the same opposite beat as the full-size player");
            foreach (var size in new[] { new Vector2(320, 320), new Vector2(390, 844), new Vector2(960, 540), new Vector2(1179, 2556) })
            for (int slot = 0; slot < PlayerMovement.MaxExtraLives; slot++)
            {
                int height = Mathf.RoundToInt(size.y);
                var row = PlayerLifeIcons.RowRect(12, 0, height);
                float textBottom = 26;
                float progressTop = PlayerLifeIcons.ProgressOffset(height);
                var normal = PlayerLifeIcons.IconRect(row, slot);
                var peakPulse = PlayerLifeIcons.IconRect(row, slot, PlayerLifeIcons.BeatScale(.5f));
                Check(peakPulse.yMin > textBottom && peakPulse.yMax < progressTop - 3,
                    "Peak life pulses clear the actual score label and the expanded solid progress bar");
                var departing = PlayerLifeIcons.LossDisplayRect(normal, size.x, size.y, 0);
                var centerstage = PlayerLifeIcons.LossDisplayRect(normal, size.x, size.y, PlayerLifeIcons.TravelSeconds);
                Check(Close(departing, normal), "Spent spare departs from its life-indicator slot");
                Check(Close(centerstage.center, size / 2) && centerstage.xMin >= 0 && centerstage.xMax <= size.x &&
                    centerstage.yMin >= 0 && centerstage.yMax <= size.y,
                    "Spent spare uses the combo-break centerstage target on portrait and landscape canvases");
                Check(normal.yMin > textBottom && normal.yMax < progressTop && normal.xMax < row.x + 122,
                    "Stable icon slots stay between the score and progress bar, away from status text");
                if (slot == 0) Check(Mathf.Abs(normal.center.x - (row.x + row.width / 6f)) < .001f,
                    "The compact life row preserves its first icon's left anchor");
                if (slot > 0)
                {
                    var previous = PlayerLifeIcons.IconRect(row, slot - 1);
                    Check(Mathf.Abs(normal.xMin - previous.xMax - 4) < .001f,
                        "Extra lives have a compact four-pixel gap at every screen size");
                    var pulsing = PlayerLifeIcons.IconRect(row, slot, 1.09f);
                    Check(Close(pulsing.center, normal.center) &&
                        pulsing.xMin > PlayerLifeIcons.IconRect(row, slot - 1, 1.09f).xMax,
                        "Both life icons can pulse without touching or shifting their centers");
                }
                var animation = new PlayerLifeIcons();
                animation.BeginLoss(slot);
                animation.Tick(PlayerLifeIcons.DefaultStartDelay * 2 +
                    PlayerLifeIcons.SlashSeconds + PlayerLifeIcons.SplitSeconds / 2);
                Check(animation.HalfOffset(centerstage.height, false).x < 0 &&
                    animation.HalfOffset(centerstage.height, true).x > 0 && animation.Opacity > 0 && animation.Opacity < 1,
                    "Spent spare halves separate like the combo-break graphic");
            }
            Debug.Log("Player life icon checks passed: beat-timed loss, two-beat life gain, compact tally, reward queues, pause, recovery and restart.");
        }

        private static void CheckGainAnimation()
        {
            float beat = FullSetCelebration.StepDuration;
            foreach (var size in new[] { new Vector2(320, 320), new Vector2(390, 844), new Vector2(960, 540), new Vector2(1179, 2556) })
            for (int slot = 0; slot < PlayerMovement.MaxExtraLives; slot++)
            {
                var resting = PlayerLifeIcons.IconRect(PlayerLifeIcons.RowRect(12, 0, (int)size.y), slot);
                var gain = new PlayerLifeGainAnimation();
                gain.Begin(slot);
                gain.Tick(beat - .001f);
                Check(gain.Active && !gain.Visible, "A reward waits for the first beat before appearing");
                gain.Tick(.001f + beat / 2);
                var appeared = gain.DisplayRect(resting, size.x, size.y);
                var lossCenter = PlayerLifeIcons.LossDisplayRect(resting, size.x, size.y, PlayerLifeIcons.TravelSeconds);
                Check(gain.Visible && gain.Opacity == 1 && Close(appeared.center, lossCenter.center) &&
                    appeared.width > lossCenter.width * 1.19f && gain.TravelProgress == 0,
                    "Beat one both reveals and pulses the full-color man at the life-loss centerstage position");
                gain.Tick(beat);
                var traveling = gain.DisplayRect(resting, size.x, size.y);
                Check(gain.TravelProgress > .49f && gain.TravelProgress < .51f &&
                    traveling.center.y < appeared.center.y && traveling.center.y > resting.center.y &&
                    traveling.width < appeared.width && traveling.width > resting.width &&
                    traveling.xMin >= 0 && traveling.xMax <= size.x && traveling.yMin >= 0,
                    "Beat two moves and shrinks the man into his left-aligned tally slot on every screen size");
                gain.Tick(beat / 2 + .001f);
                Check(!gain.Active && gain.TravelProgress == 1 && Close(gain.DisplayRect(resting, size.x, size.y), resting),
                    "After two visible beats the reward lands exactly in the tally without a size or position jump");
                gain.Begin(slot); gain.Tick(beat * 1.5f);
                float age = gain.Age;
                gain.Tick(-1); gain.Tick(0);
                Check(gain.Age == age, "Nonpositive time cannot advance or rewind the reward");
                gain.Reset();
                Check(!gain.Active && !gain.Visible && gain.Slot == -1, "Reset clears a pending life reward");
            }
        }

        private static void CheckGainLifecycle()
        {
            float beat = FullSetCelebration.StepDuration;
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                player.Music.StopPlayback();
                player.Hit(); player.TickSurvival(4);
                Check(player.GrantLife() && player.Lives == PlayerMovement.MaxLives && player.LifeIcons.Gain.Active &&
                    player.LifeIcons.Gain.Slot == PlayerMovement.MaxExtraLives - 1 &&
                    player.LifeIcons.IsGainPending(PlayerMovement.MaxExtraLives - 1),
                    "Granting a life credits it immediately and reserves the new icon until its flight lands");
                Check(!player.GrantLife(), "A full life tally cannot start a duplicate reward");
                player.TickSurvival(player.Music.BeatDuration * 1.5f);
                Check(player.Alive && player.LifeIcons.Gain.Visible, "Reward animations tick while the player is healthy");
                session.Pause();
                float age = player.LifeIcons.Gain.Age;
                player.TickSurvival(10);
                Check(player.LifeIcons.Gain.Age == age, "Pause freezes an earned-life animation");
                session.Resume();
                player.Music.StopPlayback();
                player.TickSurvival(10);
                Check(player.Lives == PlayerMovement.MaxLives && !player.LifeIcons.Animating &&
                    !player.LifeIcons.IsGainPending(PlayerMovement.MaxExtraLives - 1),
                    "Arrival reveals the tally icon without awarding the same life twice");
                player.Hit(); player.TickSurvival(4);
                player.GrantLife(); player.ResetForRun();
                Check(!player.LifeIcons.Animating && player.ExtraLives == PlayerMovement.MaxExtraLives,
                    "Restart removes in-flight rewards and restores the normal tally");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                for (int i = 0; i < PlayerMovement.MaxExtraLives; i++)
                { player.Hit(); player.TickSurvival(4); }
                Check(player.GrantLife() && player.GrantLife() && player.LifeIcons.Gain.Slot == 0 &&
                    player.LifeIcons.IsGainPending(0) && player.LifeIcons.IsGainPending(1),
                    "Consecutive rewards queue separate arrivals instead of overlapping at centerstage");
                player.TickSurvival(beat * 4 + .01f);
                Check(player.LifeIcons.Gain.Slot == 1 && !player.LifeIcons.IsGainPending(0) && player.LifeIcons.IsGainPending(1),
                    "The first reward lands before the second starts");
                player.TickSurvival(beat * 4 + .01f);
                Check(!player.LifeIcons.Animating && player.Lives == PlayerMovement.MaxLives,
                    "Every queued reward reaches its own tally slot exactly once");
            });

            var icons = new PlayerLifeIcons();
            icons.BeginLoss(1);
            icons.BeginGain(1);
            Check(icons.Active && !icons.Gain.Active && icons.IsGainPending(1),
                "A reward earned during life loss waits for the centerstage loss to finish");
            icons.Tick(10);
            Check(!icons.Active && icons.Gain.Active && icons.Gain.Slot == 1,
                "The queued life enters after the loss without suppressing either animation");
            icons.Reset();
            icons.BeginGain(0); icons.BeginGain(1); icons.BeginLoss(1);
            Check(!icons.Gain.Active && icons.IsGainPending(0) && !icons.IsGainPending(1),
                "Losing an unlanded life cancels that reward and preserves the remaining earned life");
            icons.Tick(10);
            Check(icons.Gain.Active && icons.Gain.Slot == 0, "A surviving reward resumes after the life loss");
            icons.BeginLoss(0);
            Check(!icons.Gain.Active && !icons.IsGainPending(0), "A spent reward cannot later reappear in the tally");
            icons.Reset();
        }

        private static void CheckGainBeatClock(GameplayMusicPlayer music, float startBeat)
        {
            music.Source.Play();
            // Leave room to sample just before the first beat without rewinding past the grant.
            Seek(music, music.SecondsAtBeat(startBeat + .05f));
            float firstBeat = Mathf.Floor(music.BeatPosition) + 1;
            var gain = new PlayerLifeGainAnimation();
            gain.Begin(0, music);
            Seek(music, music.SecondsAtBeat(firstBeat) - .005f);
            gain.Tick(100, music);
            Check(!gain.Visible, "The music clock holds the earned life until beat one despite a long frame");
            Seek(music, music.SecondsAtBeat(firstBeat + .5f));
            gain.Tick(.001f, music);
            Check(gain.Visible && gain.Opacity == 1 && gain.PulseScale > 1.19f && gain.TravelProgress == 0,
                "The reward appears and pulses on the same first music beat, including tempo changes");
            Seek(music, music.SecondsAtBeat(firstBeat + 1.5f));
            gain.Tick(.001f, music);
            Check(Mathf.Abs(gain.TravelProgress - .5f) < .002f,
                "The second music beat drives the full flight into the tally");
            Seek(music, music.SecondsAtBeat(firstBeat + 2) + .005f);
            gain.Tick(.001f, music);
            Check(!gain.Active && gain.TravelProgress == 1, "Arrival ends the two-beat reward sequence");
            gain.Begin(0, music);
            music.StopPlayback(); gain.Tick(10, music);
            Check(!gain.Active, "An interrupted song cannot strand an earned-life animation");
        }
        private static void CheckBeatTiming()
        {
            foreach (float tempo in new[] { 90f, 160f })
            foreach (bool mapped in new[] { false, true })
            foreach (float startBeat in new[] { 2f, 2.65f, 5.8f })
            {
                var root = new GameObject("Life beat clock", typeof(GameplayMusicPlayer));
                var clip = AudioClip.Create("DiscoDescent", 44100 * 20, 1, 44100, false);
                try
                {
                    var music = root.GetComponent<GameplayMusicPlayer>();
                    var settings = new UnityEditor.SerializedObject(music);
                    settings.FindProperty("soundtrack").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("beatsPerMinute").floatValue = tempo;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    music.Source.clip = clip;
                    var cache = (System.Collections.Generic.Dictionary<AudioClip, SongBeatMap>)typeof(GameplayMusicPlayer)
                        .GetField("beatMaps", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(music);
                    cache[clip] = mapped ? new SongBeatMap
                    {
                        durationSeconds = 20,
                        beatTimes = new[] { .1f, .6f, 1.1f, 1.6f, 1.85f, 2.1f, 2.85f, 3.6f, 4.35f, 5.1f, 5.85f }
                    } : null;
                    music.Source.Play();
                    Seek(music, music.SecondsAtBeat(startBeat + .05f));
                    float began = music.PlaybackSeconds;
                    float entryBeat = Mathf.Floor(music.BeatPosition) + 1;
                    float entry = music.SecondsAtBeat(entryBeat);
                    float arrival = entry + PlayerLifeIcons.TravelSeconds;
                    float chop = Mathf.Max(arrival + .0001f, music.SecondsAtBeat(entryBeat + 1));
                    var icons = new PlayerLifeIcons();
                    icons.BeginLoss(0);
                    icons.Tick(0, music);
                    if (entry - began > .005f)
                    {
                        Seek(music, (began + entry) / 2);
                        icons.Tick(100, music);
                        Check(!icons.Started && icons.TravelProgress == 0,
                            "The spent spare waits in its slot until the next beat after the hit");
                    }
                    Seek(music, entry + .005f);
                    icons.Tick(.001f, music);
                    Check(icons.Started && icons.TravelProgress > 0 && icons.TravelProgress < 1 && icons.DrainProgress == 0,
                        "The life moves toward centerstage after entering on the next beat");
                    Seek(music, arrival);
                    icons.Tick(.001f, music);
                    Check(icons.TravelProgress > .999f && icons.SlashProgress == 0,
                        "The life reaches centerstage after the beat entry travel");
                    Seek(music, (arrival + chop) / 2);
                    icons.Tick(.001f, music);
                    float sampleTolerance = Mathf.Max(.002f, 1f / clip.frequency / (chop - arrival));
                    Check(Mathf.Abs(icons.DrainProgress - .5f) < sampleTolerance && icons.SlashOpacity == 0 && icons.SplitProgress == 0,
                        "Color drains across the remaining beat before the chop, including tempo changes");
                    Seek(music, chop - Mathf.Min(.005f, (chop - arrival) / 4));
                    icons.Tick(100, music);
                    Check(icons.SlashProgress == 0 && icons.SplitProgress == 0,
                        "Frame time cannot trigger the chop before the next music beat");
                    Seek(music, chop + .005f);
                    icons.Tick(.001f, music);
                    Check(icons.SlashProgress > 0 && icons.SlashProgress < 1 && icons.SlashOpacity == 1 &&
                        icons.DrainProgress == 1 && icons.LossTint(Color.red) == Color.white,
                        "The chop starts on the immediately following beat with the life fully white");
                    Seek(music, chop + PlayerLifeIcons.SlashSeconds + PlayerLifeIcons.SplitSeconds / 2);
                    icons.Tick(.001f, music);
                    Check(icons.SplitProgress > .49f && icons.SplitProgress < .51f && icons.Opacity > 0,
                        "Split and fade retain their existing motion after the beat-aligned chop");
                    music.StopPlayback();
                    icons.Tick(10, music);
                    Check(!icons.Active, "Losing the music clock finishes the animation without stalling");
                    icons.BeginLoss(0);
                    icons.Tick(PlayerLifeIcons.DefaultStartDelay - .01f);
                    Check(!icons.Started, "Reset and silent fallback wait one default beat before entering");
                    icons.Tick(.01f + PlayerLifeIcons.TravelSeconds / 2);
                    Check(Mathf.Abs(icons.TravelProgress - .5f) < .001f,
                        "Reset and silent fallback do not inherit the previous song's timing");
                    CheckGainBeatClock(music, startBeat);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
            CheckPlayerBeatSequence();
        }

        private static void CheckPlayerBeatSequence()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                var music = player.Music;
                var clip = AudioClip.Create("Life recovery clock", 44100 * 20, 1, 44100, false);
                try
                {
                    music.StopPlayback();
                    var settings = new UnityEditor.SerializedObject(music);
                    settings.FindProperty("track").objectReferenceValue = clip;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    music.UpdatePlayback();
                    Seek(music, 1);
                    Check(player.Hit(), "Player fixture loses one spare life");
                    float now = 1;
                    float deathEnd = 1 + PlayerDeathBurst.Duration;
                    float entryBeat = Mathf.Floor(music.BeatPositionAtTime(now)) + 1;
                    float entry = music.SecondsAtBeat(entryBeat);
                    float chop = music.SecondsAtBeat(entryBeat + 1);
                    Seek(music, entry - .005f);
                    player.TickSurvival(entry - .005f - now);
                    now = entry - .005f;
                    Check(!player.LifeIcons.Started, "Music cannot bring the spare onstage before the next beat after the hit");
                    Seek(music, entry + .005f);
                    player.TickSurvival(.01f);
                    now = entry + .005f;
                    Check(player.LifeIcons.Started && player.LifeIcons.TravelProgress > 0 && player.LifeIcons.TravelProgress < 1,
                        "Player recovery starts the spare animation on the next beat after the hit");
                    session.Pause();
                    float age = player.LifeIcons.Age;
                    player.TickSurvival(10);
                    Check(player.LifeIcons.Age == age, "Pause freezes the music-timed life animation");
                    session.Resume();
                    Seek(music, chop + .005f);
                    player.TickSurvival(chop + .005f - now);
                    now = chop + .005f;
                    Check(player.LifeIcons.TravelProgress == 1 && player.LifeIcons.SlashProgress > 0 &&
                        player.LifeIcons.SlashProgress < 1 && !player.Alive,
                        "The chop remains on the following beat, even before the death cue has finished");
                    Seek(music, deathEnd - .005f);
                    player.TickSurvival(deathEnd - .005f - now);
                    now = deathEnd - .005f;
                    Check(player.LifeIcons.Started, "The spare animation has already begun before the player death cue finishes");
                    Seek(music, Mathf.Max(deathEnd, chop + PlayerLifeIcons.SlashSeconds + PlayerLifeIcons.SplitSeconds) + .01f);
                    player.TickSurvival(2);
                    Check(!player.LifeIcons.Active && player.Alive, "The scheduled life loss completes normally");
                }
                finally
                {
                    music.StopPlayback();
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            });
        }

        private static void Seek(GameplayMusicPlayer music, float seconds) =>
            music.Source.timeSamples = Mathf.RoundToInt(seconds * music.Source.clip.frequency);

        private static bool Close(Rect a, Rect b) => Close(a.position, b.position) && Close(a.size, b.size);
        private static bool Close(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .001f;
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Player life icon check failed: " + message); }
    }
}
