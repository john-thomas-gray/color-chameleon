using System;
using System.Reflection;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerLifeRewardChecks
    {
        public static void Run()
        {
            CheckTrajectory(); CheckCancellation();
            foreach (bool full in new[] { false, true })
            foreach (bool room in new[] { false, true }) CheckSession(full, room);
            if (Application.isPlaying) CheckMusicClock();
            Debug.Log("Life reward checks passed: offscreen entrance, final-spike launch, slot arrival, full-cap flip, partial sets, queues, cancellation and soundtrack clock.");
        }

        private static void CheckTrajectory()
        {
            foreach (var screen in new[] { new Vector2(390, 844), new Vector2(960, 540) })
            foreach (bool overlap in new[] { false, true })
            {
                var gain = new PlayerLifeGainAnimation();
                gain.Begin(1, launchDelay: 1, barIndex: 2, barCount: 3, overlappingWave: overlap);
                var resting = PlayerLifeIcons.IconRect(PlayerLifeIcons.RowRect(12, 0, (int)screen.y), 1);
                var bars = new Rect(20, screen.y - 20, screen.x - 40, 5);
                var size = new Vector2(40, 32);
                gain.Tick(gain.EnterAt);
                var stage = PlayerLifeGainAnimation.StageClip(bars, screen.y);
                var entrance = gain.DisplayRect(resting, bars, size, screen.x);
                Check(gain.Visible && entrance.xMin >= stage.xMax && entrance.xMin < stage.xMax + size.x,
                    "New slime begins hidden immediately behind the border");
                gain.Tick(gain.LaunchAt - gain.Age);
                var contact = gain.DisplayRect(resting, bars, size, screen.x);
                var bar = GameSession.ColorClearBarSlotRect(bars.x, bars.y, bars.width, 3, 2);
                Check(bar.Contains(new Vector2(contact.center.x, bar.center.y)) && entrance.center.x - contact.center.x < size.x * 2,
                    "Reward takes a short step onto the near edge of the final bar");
                Near(contact.yMax, bar.yMax - bar.height * FullSetCelebration.WaveScale(FullSetCelebration.SpikePhase, overlap),
                    "Feet touch the rising bar for both single-bar and overlapping waves, independent of the display slot");
                gain.Tick((gain.FinishAt - gain.Age) / 2);
                Check(gain.DisplayRect(resting, bars, size, screen.x).center.y < contact.center.y,
                    "Final spike launches the new life upward");
                Check(gain.Rotation < -180 && gain.Rotation > -360, "Incoming life flips on its upward flight");
                gain.Tick((gain.FinishAt - gain.Age) * .8f);
                Check(gain.DisplayRect(resting, bars, size, screen.x).center.y < resting.center.y,
                    "Incoming life clears the tally ledge before descending onto it");
                gain.Tick(100);
                Check(!gain.Active && Vector2.Distance(gain.DisplayRect(resting, bars, size, screen.x).center, resting.center) < .001f,
                    "Reward lands exactly in the life tally");
            }
        }

        private static void CheckCancellation()
        {
            var icons = new PlayerLifeIcons();
            icons.ScheduleWaveGain(1, null, 2, 1, 2); icons.BeginLoss(1);
            Check(!icons.Gain.Active, "An earned life spent before arrival cannot reappear");
            icons.Reset(); icons.ScheduleWaveGain(0, null, 2, 1, 2); icons.BeginLoss(1);
            Check(icons.Gain.Active, "Losing a different spare does not erase a still-owned incoming life");
            icons.Reset(); icons.BeginLoss(0); icons.BeginGain(0); icons.Tick(2); icons.Tick(3);
            Check(!icons.Animating, "A direct gain queued during loss eventually arrives");
            icons.ScheduleWaveGain(0, null, 2, 0, 1); icons.Reset();
            Check(!icons.Animating, "Restart clears gain, loss and reservations");
        }

        private static void CheckSession(bool full, bool room)
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = Session(grid, player);
                player.Music.StopPlayback();
                if (room) { player.Hit(); player.TickSurvival(4); }
                int lives = player.Lives, arrivals = 0;
                session.WaveSpawned += () => arrivals++;
                Clear(grid, full);
                bool rewarded = full && room;
                Check(player.Lives == lives + (rewarded ? 1 : 0) && player.LifeIcons.Gain.Active == rewarded,
                    "Only a full set with room earns and schedules an incoming life");
                float spike = session.ClearCelebration.FinalSpikeTime;
                if (rewarded)
                {
                    Near(player.LifeIcons.Gain.LaunchAt, spike, "Earned life shares the final bar's spike time");
                    Check(player.LifeIcons.RewardColor == session.ClearCelebration.LastColor &&
                        session.ProgressBarColor == EnemyPalette.Get(player.LifeIcons.RewardColor.Value),
                        "Reward color is reserved before the incoming life begins moving");
                }
                session.Tick(Mathf.Max(0, spike - .001f)); player.TickSurvival(Mathf.Max(0, spike - .001f));
                Check(!player.LifeIcons.Choreography.Flipping, "Full-cap flip does not occur before the last spike");
                if (rewarded) Check(player.LifeIcons.Gain.TravelProgress == 0, "New life cannot launch early");
                session.Tick(.002f);
                Check(player.LifeIcons.Choreography.Flipping == (full && !room),
                    "Only a full set at the existing life cap flips the spare group");
                player.TickSurvival(.002f);
                if (rewarded) Check(player.LifeIcons.Gain.TravelProgress > 0, "Reward bounces on the final spike");
                session.Pause();
                float gainAge = player.LifeIcons.Gain.Age;
                session.Tick(10); player.TickSurvival(10);
                Check(player.LifeIcons.Gain.Age == gainAge && arrivals == 0, "Pause freezes the reward and refill");
                session.Resume(); player.Music.StopPlayback();
                session.Tick(10); player.TickSurvival(10); session.Tick(10); player.TickSurvival(10);
                Check(arrivals == 1 && !player.LifeIcons.Animating && player.Lives == lives + (rewarded ? 1 : 0),
                    "Reward and wave complete once without changing gameplay life counts");
                if (rewarded) Check(player.LifeIcons.RewardColor == null,
                    "The adopted next-wave color releases its temporary reward preview");
            });
        }

        private static void CheckMusicClock()
        {
            foreach (bool mapped in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = Session(grid, player); var music = player.Music;
                music.StopPlayback(); player.Hit(); player.TickSurvival(4);
                var clip = AudioClip.Create("Life trampoline clock", 44100 * 20, 1, 44100, false);
                try
                {
                    music.Source.clip = clip;
                    var cache = (System.Collections.Generic.Dictionary<AudioClip, SongBeatMap>)typeof(GameplayMusicPlayer)
                        .GetField("beatMaps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(music);
                    cache[clip] = mapped ? new SongBeatMap { durationSeconds = 20,
                        beatTimes = new[] { .1f, .6f, 1.1f, 1.6f, 1.85f, 2.1f, 2.35f, 3.1f, 3.85f, 4.6f, 5.35f, 6.1f } } : null;
                    music.Source.Play(); Seek(music, 1.1f);
                    float began = music.PlaybackSeconds;
                    Clear(grid, true);
                    float launch = began + session.ClearCelebration.FinalSpikeTime;
                    var gain = player.LifeIcons.Gain;
                    Seek(music, launch - .01f); session.Tick(100); player.TickSurvival(100);
                    Check(gain.TravelProgress == 0 && gain.Visible, "Frame time cannot launch a reward before the music spike");
                    Seek(music, launch + .01f); session.Tick(.001f); player.TickSurvival(.001f);
                    Check(gain.TravelProgress > 0 && gain.TravelProgress < .1f, "Soundtrack sample clock launches the reward with the spike");
                    Seek(music, began + gain.FinishAt + .01f); player.TickSurvival(.001f);
                    Check(!gain.Active, "Reward completes across tempo changes without extra frames adding delay");
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
        private static void Clear(EnemyGrid grid, bool full)
        {
            var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
            var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
            grid.ClearMatchingChain(red.Id, EnemyColor.Red);
            if (!full) grid.ResetColorClearStreak();
            grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
        }
        private static void Seek(GameplayMusicPlayer music, float seconds) =>
            music.Source.timeSamples = Mathf.RoundToInt(seconds * music.Source.clip.frequency);
        private static void Near(float a, float b, string message) => Check(Mathf.Abs(a - b) < .001f, message);
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Life reward check failed: " + message); }
    }
}
