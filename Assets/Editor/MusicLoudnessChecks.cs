using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MusicLoudnessChecks
    {
        private static readonly string[] ActiveSoundtrack =
            { "AnotherJoe", "PotentialForAnything", "DiscoDescent", "GameplayMusic", "Skanska",
                "TheThirdKind", "DownToEarthPart1", "UntilICollapse", "WarOnActivism",
                "IntergalacticEmotionalBreakdown", "ShootingRobotsInSpace", "VertexStage1",
                "CountingMetronome" };

        [Serializable] private sealed class Profile
        {
            public int version;
            public float targetLufs;
            public float truePeakCeilingDbtp;
            public Entry[] tracks;
        }
        [Serializable] private sealed class Entry
        {
            public string resourceName;
            public string sourceSha256;
            public float integratedLufs;
            public float truePeakDbtp;
            public float gainDb;
        }

        public static void Run()
        {
            var asset = Resources.Load<TextAsset>("MusicLoudness");
            Check(asset != null, "The measured profile ships as a runtime resource");
            var profile = JsonUtility.FromJson<Profile>(asset.text);
            Check(profile.version == 1 && profile.tracks != null, "The loudness profile is versioned and readable");
            var profileNames = profile.tracks.Select(entry => entry.resourceName).ToArray();
            Check(ActiveSoundtrack.All(name => profileNames.Contains(name)),
                "Every active soundtrack recording has a loudness measurement");
            foreach (var entry in profile.tracks)
            {
                var clip = Resources.Load<AudioClip>(entry.resourceName);
                if (clip == null) continue;
                using (var hash = SHA256.Create())
                {
                    string fingerprint = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(AssetDatabase.GetAssetPath(clip))))
                        .Replace("-", "").ToLowerInvariant();
                    Check(fingerprint == entry.sourceSha256, "The profile matches the current recording: " + entry.resourceName);
                }
                float gain = MusicLoudness.GainFor(clip);
                Check(gain > 0 && gain <= 1, "Normalization only attenuates, leaving full master-volume headroom");
                Near(entry.integratedLufs + 20 * Mathf.Log10(gain), profile.targetLufs,
                    "Every recording reaches the common measured loudness");
                Check(entry.truePeakDbtp + entry.gainDb <= profile.truePeakCeilingDbtp,
                    "Normalized true peaks remain below the clipping ceiling");
                Near(gain, MusicLoudness.GainFor(clip), "Repeated lookups retain stable gain");
            }
            var resources = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources" });
            Check(resources.Length <= profile.tracks.Length &&
                resources.Select(guid => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)).name)
                    .All(name => profileNames.Contains(name)),
                "Any local music resource has a measured profile, while clean clones can stay music-free");
            var unknown = AudioClip.Create("Unmeasured replacement music", 4410, 1, 44100, false);
            try
            {
                Near(MusicLoudness.GainFor(null), 1, "No track has neutral gain");
                Near(MusicLoudness.GainFor(unknown), 1, "Unmeasured custom tracks retain the configured volume");
            }
            finally { UnityEngine.Object.DestroyImmediate(unknown); }
            CheckPlayback();
            Debug.Log("Music loudness checks passed: every recording, source fingerprints, equal loudness, peak headroom, menu continuity, track changes, volume, mute, pause and game over.");
        }

        private static void CheckPlayback()
        {
            var menuClip = AudioClip.Create(GameplayMusicPlayer.MenuResourceName, 44100 * 15, 1, 44100, false);
            try
            {
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var session = grid.gameObject.AddComponent<GameSession>();
                    session.Configure(player);
                    if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                    typeof(GameSession).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(session, null);
                    var music = grid.GetComponent<GameplayMusicPlayer>();
                    var settings = new SerializedObject(music);
                    settings.FindProperty("menuTrack").objectReferenceValue = menuClip;
                    settings.FindProperty("volume").floatValue = .4f;
                    settings.FindProperty("muted").boolValue = true;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    music.UpdatePlayback();
                    var menu = music.Source.clip;
                    Near(music.Source.volume, .4f * MusicLoudness.GainFor(menu), "Menu music uses the measured gain");
                    float menuVolume = music.Source.volume;
                    session.StartRun();
                    Check(music.Source.clip == menu, "Normalization does not replace the carried menu song");
                    Near(music.Source.volume, menuVolume, "Starting gameplay does not cause a loudness jump");
                    if (HasBundledSoundtrack())
                    {
                        for (int i = 0; i < music.TrackCount; i++)
                        {
                            Near(music.Source.volume, .4f * MusicLoudness.GainFor(music.Source.clip), "Each shuffled song uses its own normalization gain");
                            Check(music.Source.mute, "Normalization preserves the music mute setting");
                            Check(music.SkipCurrentSong(), "Development skipping advances to another normalized song");
                        }
                    }
                    else Check(!music.SkipCurrentSong(), "Development skipping stays unavailable without local music resources");
                    foreach (float master in new[] { 0f, .15f, 1f })
                    {
                        settings.Update();
                        settings.FindProperty("volume").floatValue = master;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                        music.UpdatePlayback();
                        Near(music.Source.volume, master * MusicLoudness.GainFor(music.Source.clip),
                            "Normalization composes with zero, default and maximum music volume");
                    }
                    var paused = music.Source.clip;
                    float pausedVolume = music.Source.volume;
                    session.Pause();
                    session.Resume();
                    Check(music.Source.clip == paused, "Pause retains the current normalized song");
                    Near(music.Source.volume, pausedVolume, "Pause and resume retain the same normalized level");
                    typeof(GameSession).GetMethod("EndGame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(session, null);
                    Check(music.Source.volume == 0 && !music.Source.isPlaying, "Game over still cuts normalized music immediately");
                });
            }
            finally { UnityEngine.Object.DestroyImmediate(menuClip); }
        }

        private static bool HasBundledSoundtrack() =>
            ActiveSoundtrack.All(name => Resources.Load<AudioClip>(name) != null);

        private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .0001f, message);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Music loudness check failed: " + message); }
    }
}
