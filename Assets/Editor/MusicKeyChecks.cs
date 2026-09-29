using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MusicKeyChecks
    {
        public static void RunWithSoundChecks() { Run(); SoundEffectsChecks.Run(); }
        public static void RunInPlayMode()
        {
            SessionState.SetBool("CandyCruisers.MusicKeyChecks", true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        private static void RestorePlayModeCheck()
        {
            if (SessionState.GetBool("CandyCruisers.MusicKeyChecks", false))
                EditorApplication.playModeStateChanged += OnPlayMode;
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            SessionState.SetBool("CandyCruisers.MusicKeyChecks", false);
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void Run()
        {
            int[] scale = { 0, 2, 4, 5, 7, 9, 11 };
            for (int tonic = 0; tonic < 12; tonic++)
            for (int shift = -12; shift <= 19; shift++)
            for (int frequency = 100; frequency < 1800; frequency += 37)
            {
                float tuned = ArcadeSoundClips.KeyFrequency(frequency, tonic, shift);
                float note = 69 + 12 * Mathf.Log(tuned / 440, 2);
                int degree = (Mathf.RoundToInt(note) - tonic) % 12;
                if (degree < 0) degree += 12;
                Check(Mathf.Abs(note - Mathf.Round(note)) < .001f && Array.IndexOf(scale, degree) >= 0,
                    "Every generated sweep and combo pitch lands in the selected scale");
            }
            var root = new GameObject("Key tuning fixture", typeof(SoundEffects), typeof(GameplayMusicPlayer));
            var anotherJoe = AudioClip.Create("AnotherJoe", 4410, 1, 44100, false);
            var gameplayMusic = AudioClip.Create("GameplayMusic", 4410, 1, 44100, false);
            var replacement = AudioClip.Create("Custom key test", 4410, 1, 44100, false);
            try
            {
                var music = root.GetComponent<GameplayMusicPlayer>();
                var sounds = root.GetComponent<SoundEffects>();
                var voice = sounds.CreateVoice(root.transform);
                music.Source.clip = anotherJoe;
                Check(music.CurrentRelativeMajorTonic == 2, "Actual menu playback uses D major");
                music.Source.clip = gameplayMusic;
                Check(music.CurrentRelativeMajorTonic == 8, "Original track uses F minor's relative Ab major");
                music.Source.clip = replacement;
                var serialized = new SerializedObject(music);
                foreach (int tonic in new[] { 0, 2, 5, 8 })
                {
                    serialized.FindProperty("customTrackRelativeMajorTonic").intValue = tonic;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var clip = sounds.GetClip(SoundEffect.EnemyDefeat);
                    Check(clip == sounds.GetClip(SoundEffect.EnemyDefeat), "Keyed clips are cached");
                    var data = new float[clip.samples]; clip.GetData(data, 0);
                    int crossings = 0;
                    for (int i = 1; i < data.Length; i++) if (data[i - 1] <= 0 && data[i] > 0) crossings++;
                    Check(Mathf.Abs(crossings / clip.length - ArcadeSoundClips.KeyFrequency(261.6256f, tonic)) < 12,
                        "Rendered defeat audio has the expected tuned fundamental");
                    Check(sounds.Play(SoundEffect.BarPowerUp, voice, SoundEffects.DefeatPitch(6)) && voice.pitch == 1 &&
                        Mathf.Abs(voice.clip.length - .28f) < .001f, "Baked tuning preserves effect duration even for high combo pitches");
                }
                var gameOver = sounds.GetClip(SoundEffect.GameOver);
                serialized.FindProperty("customTrackRelativeMajorTonic").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Check(gameOver != sounds.GetClip(SoundEffect.GameOver) &&
                    gameOver.length == sounds.GetClip(SoundEffect.GameOver).length, "Tonal game-over cadence follows the key without changing its duration");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(anotherJoe);
                UnityEngine.Object.DestroyImmediate(gameplayMusic);
                UnityEngine.Object.DestroyImmediate(replacement);
            }
            Debug.Log("Music key checks passed: per-song keys, diatonic sweeps, combo notes, rendered fundamentals, caching and unchanged durations.");
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Music key check failed: " + message); }
    }
}
