using System;
using System.Reflection;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerLifeRewardChecks
    {
        public static void Run()
        {
            CheckReservations();
            foreach (bool fullSet in new[] { false, true })
            foreach (bool room in new[] { false, true })
            foreach (bool longFrame in new[] { false, true })
                CheckSilentSequence(fullSet, room, longFrame);
            if (Application.isPlaying)
                foreach (bool mapped in new[] { false, true })
                foreach (bool lateFrame in new[] { false, true })
                foreach (bool counting in new[] { false, true })
                    CheckMusicSequence(mapped, lateFrame, counting);
            Debug.Log("Life reward checks passed: post-bar appearance/pulse, following-beat flight and downbeat spawn, cap, partial sets, pause and missed frames.");
        }

        private static void CheckReservations()
        {
            var icons = new PlayerLifeIcons();
            icons.ReserveGain(1); icons.Tick(100);
            Check(icons.IsGainPending(1) && !icons.Gain.Active,
                "An earned icon stays hidden throughout any length bar celebration");
            icons.ReleaseGain(1); icons.Tick(FullSetCelebration.StepDuration * 1.5f);
            Check(icons.Gain.Visible && icons.Gain.PulseScale > 1.19f,
                "Releasing the reward starts appearance and pulse on the same next beat");
            icons.Reset(); icons.ReserveGain(1); icons.BeginLoss(1); icons.ReleaseGain(1); icons.Tick(100);
            Check(!icons.Animating, "A reserved but spent life cannot reappear after death");
            icons.ReserveGain(0); icons.BeginLoss(1); icons.ReleaseGain(0); icons.Tick(100);
            Check(icons.Gain.Active && icons.Gain.Slot == 0, "A still-owned reward waits for an active loss to finish");
            icons.Reset(); icons.ReserveGain(1); icons.Reset(); icons.ReleaseGain(1);
            Check(!icons.Animating, "Restart clears every pending reward");
        }

        private static void CheckSilentSequence(bool fullSet, bool room, bool longFrame)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = Session(grid, player);
                var music = player.Music;
                music.StopPlayback();
                if (room) { player.Hit(); player.TickSurvival(4); }
                int lives = player.Lives, arrivals = 0;
                bool rewarded = fullSet && room;
                session.WaveSpawned += () => arrivals++;
                ClearBars(grid, fullSet);
                var gain = player.LifeIcons.Gain;
                float beat = music.BeatDuration, duration = session.ClearCelebration.PlaybackDuration;
                Check(player.Lives == lives + (rewarded ? 1 : 0) && !gain.Active &&
                    player.LifeIcons.IsGainPending(player.ExtraLives - 1) == rewarded,
                    "Only an actual life reward reserves an icon; nothing appears while bars power down");
                player.TickSurvival(duration + 100);
                session.Tick(duration - .001f);
                Check(!gain.Active && arrivals == 0, "A long player frame cannot show the reward before all bars finish");
                session.Tick(longFrame ? 100 : .002f);
                Check(gain.Active == rewarded && !gain.Visible && arrivals == 0,
                    "The final power-down frame is empty even after a long frame");
                session.Pause(); session.Tick(10); player.TickSurvival(10);
                Check(gain.Age == 0 && arrivals == 0, "Pause holds both the reward and the post-bar spawn delay");
                session.Resume(); music.StopPlayback();
                session.Tick(beat - .001f); player.TickSurvival(beat - .001f);
                Check(!gain.Visible && arrivals == 0, "The first post-bar beat has not started early");
                session.Tick(.002f); player.TickSurvival(.002f);
                Check(rewarded ? gain.Visible && arrivals == 0 : !gain.Active && arrivals == 1,
                    "The next beat shows a rewarded life; capped and partial clears spawn on their original beat");
                if (rewarded)
                {
                    session.Tick(beat / 2); player.TickSurvival(beat / 2);
                    Check(gain.PulseScale > 1.19f && gain.TravelProgress == 0 && arrivals == 0,
                        "Appearance and pulse occupy one beat while the field remains empty");
                    session.Tick(beat / 2); player.TickSurvival(beat / 2);
                    Check(arrivals == 1 && gain.TravelProgress > 0 && gain.TravelProgress < .01f,
                        "Exactly one additional beat precedes the fleet and the flight to the tally");
                }
                player.TickSurvival(10); session.Tick(10);
                Check(arrivals == 1 && !player.LifeIcons.Animating && player.Lives == lives + (rewarded ? 1 : 0),
                    "The reward lands once without duplicating lives or waves");
            });
        }

        private static void CheckMusicSequence(bool mapped, bool lateFrame, bool counting)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = Session(grid, player);
                var music = player.Music;
                music.StopPlayback();
                player.Hit(); player.TickSurvival(4);
                var clip = AudioClip.Create(counting ? GameplayMusicPlayer.CountingResourceName : "DiscoDescent", 44100 * 20, 1, 44100, false);
                try
                {
                    music.Source.clip = clip;
                    var cache = (System.Collections.Generic.Dictionary<AudioClip, SongBeatMap>)typeof(GameplayMusicPlayer)
                        .GetField("beatMaps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(music);
                    cache[clip] = mapped ? new SongBeatMap { durationSeconds = 20,
                        beatTimes = new[] { .1f, .6f, 1.1f, 1.6f, 1.85f, 2.1f, 2.35f, 3.1f, 3.85f, 4.6f, 5.35f, 6.1f, 6.85f } } : null;
                    music.Source.Play();
                    Seek(music, 2.125f);
                    float began = music.PlaybackSeconds;
                    float clearBeat = music.BeatPosition;
                    float minimumBarBeats = GameSession.MinimumColorClearBarSequenceBeats(2);
                    float downbeat = GameSession.RefillDownbeat(clearBeat, minimumBarBeats, true,
                        music.CurrentDownbeatOffsetBeats, true);
                    int arrivals = 0;
                    session.WaveSpawned += () => arrivals++;
                    ClearBars(grid, true);
                    var gain = player.LifeIcons.Gain;
                    Near(session.ClearCelebration.PlaybackDuration, music.SecondsAtBeat(downbeat - 2) - began,
                        "The dynamic bar schedule reserves the life beat before the earliest fitting downbeat");
                    Seek(music, downbeat - 2.01f); session.Tick(100); player.TickSurvival(100);
                    Check(!gain.Active && arrivals == 0, "Music holds the reserved life until the final bar is gone");
                    Seek(music, downbeat - (lateFrame ? .8f : 1.99f)); session.Tick(.001f); player.TickSurvival(.001f);
                    if (lateFrame)
                        Check(gain.Visible && gain.PulseProgress > .19f && gain.PulseProgress < .21f,
                            "A missed bar-removal frame catches up to the planned reward beat without postponing it");
                    else
                    {
                        Check(gain.Active && !gain.Visible && arrivals == 0, "The reward waits one actual beat after final removal");
                        Seek(music, downbeat - 1.01f); session.Tick(100); player.TickSurvival(100);
                        Check(!gain.Visible && arrivals == 0, "Frame duration cannot bring the life in before its music beat");
                    }
                    Seek(music, downbeat - .5f); session.Tick(.001f); player.TickSurvival(.001f);
                    Check(gain.Visible && gain.PulseScale > 1.19f && gain.TravelProgress == 0 && arrivals == 0,
                        "The intervening beat reveals and pulses the life even through tempo changes");
                    Seek(music, downbeat - .01f); session.Tick(100); player.TickSurvival(100);
                    Check(arrivals == 0 && gain.TravelProgress == 0, "Both flight and spawn wait for the downbeat");
                    Seek(music, downbeat + .01f); session.Tick(.001f); player.TickSurvival(.001f);
                    Check(arrivals == 1 && gain.TravelProgress > 0 && gain.TravelProgress < .03f,
                        "The corrected song downbeat starts the fleet and the life flight together");
                    Seek(music, downbeat + 1.01f); player.TickSurvival(.001f); session.Tick(.001f);
                    Check(!gain.Active && arrivals == 1 && !player.LifeIcons.IsGainPending(player.ExtraLives - 1),
                        "The next beat completes the flight exactly once");
                }
                finally { music.StopPlayback(); UnityEngine.Object.DestroyImmediate(clip); }
            });
        }

        private static GameSession Session(EnemyGrid grid, PlayerMovement player)
        {
            var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
            if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
            return session;
        }
        private static void ClearBars(EnemyGrid grid, bool full)
        {
            var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
            var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
            grid.ClearMatchingChain(red.Id, EnemyColor.Red);
            if (!full) grid.ResetColorClearStreak();
            grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
        }
        private static void Seek(GameplayMusicPlayer music, float beat) =>
            music.Source.timeSamples = Mathf.RoundToInt(music.SecondsAtBeat(beat) * music.Source.clip.frequency);
        private static void Near(float a, float b, string message) => Check(Mathf.Abs(a - b) < .001f, message);
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Life reward check failed: " + message); }
    }
}
