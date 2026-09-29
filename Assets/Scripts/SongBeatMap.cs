using System;
using UnityEngine;

namespace CandyCruisers
{
    [Serializable]
    public sealed class SongBeatMap
    {
        public int version = 1;
        public float durationSeconds;
        public float[] beatTimes;
        public float FirstBeatTime => beatTimes[0];

        public bool IsValid(float clipDuration)
        {
            if (version != 1 || beatTimes == null || beatTimes.Length < 2 || !Finite(durationSeconds) ||
                !Finite(clipDuration) || durationSeconds <= 0 || Mathf.Abs(durationSeconds - clipDuration) > .1f) return false;
            float previous = -1;
            foreach (float beat in beatTimes)
            {
                if (!Finite(beat) || beat < 0 || beat <= previous || beat > durationSeconds) return false;
                previous = beat;
            }
            return true;
        }

        public static bool TryParse(string json, float clipDuration, out SongBeatMap map)
        {
            map = null;
            try
            {
                var parsed = JsonUtility.FromJson<SongBeatMap>(json);
                if (parsed == null || !parsed.IsValid(clipDuration)) return false;
                map = parsed;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private int IntervalAtTime(float seconds)
        {
            int index = Array.BinarySearch(beatTimes, seconds);
            if (index < 0) index = ~index - 1;
            return Mathf.Clamp(index, 0, beatTimes.Length - 2);
        }

        public float BeatAtTime(float seconds)
        {
            int index = IntervalAtTime(seconds);
            return index + (seconds - beatTimes[index]) / (beatTimes[index + 1] - beatTimes[index]);
        }

        public float TimeAtBeat(float beat)
        {
            int index = Mathf.Clamp(Mathf.FloorToInt(beat), 0, beatTimes.Length - 2);
            return beatTimes[index] + (beat - index) * (beatTimes[index + 1] - beatTimes[index]);
        }

        public float DurationAtTime(float seconds)
        {
            int index = IntervalAtTime(seconds);
            return beatTimes[index + 1] - beatTimes[index];
        }
    }
}
