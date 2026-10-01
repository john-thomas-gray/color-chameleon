using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class CountingMetronomeChecks
    {
        public static void Run()
        {
            var root = new GameObject("Counting metronome fixture", typeof(GameplayMusicPlayer));
            try
            {
                var music = root.GetComponent<GameplayMusicPlayer>();
                var clip = Resources.Load<AudioClip>(GameplayMusicPlayer.CountingResourceName);
                Check(clip != null && music.ContainsTrack(clip.name), "Counting track is installed in the playlist");
                music.Source.clip = clip;
                Check(Mathf.Abs(music.CurrentTempo - GameplayMusicPlayer.DefaultBeatsPerMinute) < .001f,
                    "Counting tempo matches Disco Descent");
                Check(Mathf.Abs(clip.length - (.08f + 400 * 60f / music.CurrentTempo)) < .002f,
                    "Recording contains exactly 100 four-beat measures after the onset offset");
                Check(music.CurrentDownbeatOffsetBeats == 0, "The spoken one is explicitly the first beat");
                for (int measure = 0; measure < 100; measure++)
                {
                    float one = .08f + measure * 4 * 60f / music.CurrentTempo;
                    Check(Mathf.Abs(music.BeatPositionAtTime(one) - measure * 4) < .001f,
                        "Every spoken one stays aligned throughout the full track");
                }
                foreach (bool full in new[] { false, true })
                for (int bars = 1; bars <= FullSetCelebration.MaxRewardBars; bars++)
                for (float beat = 0; beat < 12; beat += .25f)
                {
                    float minimumBarBeats = GameSession.MinimumColorClearBarSequenceBeats(bars);
                    Check(GameSession.RefillDownbeat(beat, minimumBarBeats, full, music.CurrentDownbeatOffsetBeats) % 4 == 0,
                        "Metronome refills target one, not two or four");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                var music = grid.GetComponent<GameplayMusicPlayer>() ?? grid.gameObject.AddComponent<GameplayMusicPlayer>();
                music.Configure(session);
                Check(music.PlayCountingMetronome() && music.Source.clip.name == GameplayMusicPlayer.CountingResourceName,
                    "Developer selection starts the counting track immediately");
                Check(music.CurrentDownbeatOffsetBeats == 0, "Developer selection applies the spoken-one downbeat");
                Check(music.SkipCurrentSong() && music.Source.clip.name != GameplayMusicPlayer.CountingResourceName,
                    "Normal developer skip leaves the calibration track");
            });
            MusicLoudnessChecks.Run();
            SoundEffectsChecks.Run();
            WaveTransitionChecks.RunRefillDelayEnvelope();
            Debug.Log("Counting metronome checks passed: 100 measures, tempo, downbeats, loudness, playlist and refill scheduling.");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Counting metronome check failed: " + message); }
    }
}
