using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class AbilityBeatChecks
    {
        public static void Run()
        {
            var clock = new AbilityBeatClock();
            clock.Advance(0, 12.37f, 1);
            float due = clock.AfterBeats(4);
            Check(due == 4, "Spawn phase aligns deadlines to whole soundtrack beats");
            clock.Advance(50, 15.99f, 1);
            Check(!clock.Reached(due), "Wall time cannot trigger an ability ahead of the soundtrack beat");
            clock.Advance(.001f, 16, 1);
            Check(clock.Reached(due), "Ability becomes due on the exact music beat");
            due = clock.AfterBeats(3);
            clock.Advance(100, 16, 1);
            Check(!clock.Reached(due), "A paused music clock cannot advance cooldowns");
            clock.Advance(0, .73f, 2);
            Check(!clock.Reached(due), "Song changes do not trigger an off-beat ability");
            clock.Advance(0, 2.99f, 2);
            Check(!clock.Reached(due), "Remaining beats survive song changes");
            clock.Advance(0, 3, 2);
            Check(clock.Reached(due), "Pending ability lands on the new song's beat");
            due = clock.AfterBeats(2);
            clock.Advance(0, .25f, 2);
            Check(!clock.Reached(due), "Backward seeks preserve pending intervals");
            clock.Advance(0, 2, 2);
            Check(clock.Reached(due), "Seeked track remains beat aligned");
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
            foreach (bool special in new[] { false, true })
            {
                var beats = CombatBalance.CooldownBeats(color, 1, special);
                Check(beats.x >= 1 && beats.y >= beats.x, "All ability ranges contain positive whole beats");
                Check(CombatBalance.CooldownBeats(color, 50, special).y <= beats.y,
                    "Later levels retain faster maximum cooldowns");
            }
            if (Application.isPlaying) CheckAudioDrivenRed();
            Debug.Log("Ability beat checks passed: phase alignment, pause, track changes, seeks and cooldown ranges.");
        }
        private static void CheckAudioDrivenRed()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var music = grid.gameObject.AddComponent<GameplayMusicPlayer>();
                var clip = AudioClip.Create("Ability beat fixture", 44100 * 30, 1, 44100, false);
                try
                {
                    music.Source.clip = clip;
                    music.Source.timeSamples = 44100;
                    var red = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                    var ability = red.GetComponent<EnemyAbilities>();
                    float due = music.PlaybackSeconds + ability.CooldownRemaining;
                    Check(Mathf.Abs(music.BeatPositionAtTime(due) - Mathf.Round(music.BeatPositionAtTime(due))) < .0001f,
                        "Real enemy deadline is on a soundtrack beat despite a fractional spawn time");
                    int before = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length;
                    music.Source.timeSamples = Mathf.FloorToInt((due - .002f) * clip.frequency);
                    ability.Tick(100);
                    Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == before,
                        "Red cannot fire before its song beat even with a large frame delta");
                    music.Source.timeSamples = Mathf.CeilToInt(due * clip.frequency);
                    ability.Tick(.001f);
                    Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == before + 1,
                        "Red fires when audio playback crosses its scheduled beat");
                    ability.Tick(100);
                    Check(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == before + 1,
                        "Repeated ticks on the same audio beat never duplicate a shot");
                }
                finally { music.Source.clip = null; UnityEngine.Object.DestroyImmediate(clip); }
            });
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Ability beat check failed: " + message); }
    }
}
