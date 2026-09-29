using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public static class MusicLoudness
    {
        [Serializable] private sealed class Profile
        {
            public int version;
            public Entry[] tracks;
        }
        [Serializable] private sealed class Entry
        {
            public string resourceName;
            public float gainDb;
        }
        private static Dictionary<string, float> gains;

        public static float GainFor(AudioClip clip)
        {
            if (clip == null) return 1;
            if (gains == null) Load();
            return gains.TryGetValue(clip.name, out float gain) ? gain : 1;
        }

        private static void Load()
        {
            gains = new Dictionary<string, float>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("MusicLoudness");
            if (asset == null) return;
            try
            {
                var profile = JsonUtility.FromJson<Profile>(asset.text);
                if (profile == null || profile.version != 1 || profile.tracks == null) return;
                foreach (var entry in profile.tracks)
                {
                    // Attenuation-only gains preserve headroom at every master-volume setting.
                    if (entry == null || string.IsNullOrEmpty(entry.resourceName) ||
                        float.IsNaN(entry.gainDb) || float.IsInfinity(entry.gainDb) || entry.gainDb > 0) continue;
                    gains[entry.resourceName] = Mathf.Pow(10, entry.gainDb / 20);
                }
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("Invalid music loudness profile; using the music volume without normalization.");
            }
        }
    }
}
