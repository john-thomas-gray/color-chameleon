using UnityEngine;

namespace CandyCruisers
{
    // Deadlines remain whole beats, including across track changes and seeks.
    public sealed class AbilityBeatClock
    {
        public const float DefaultBeatSeconds = 60f / GameplayMusicPlayer.DefaultBeatsPerMinute;
        public float Position { get; private set; }
        private float lastSongBeat;
        private float offset;
        private int? songId;
        public void Advance(float seconds, float? songBeat = null, int trackId = 0)
        {
            if (songBeat.HasValue)
            {
                float beat = songBeat.Value;
                if (songId != trackId || beat < lastSongBeat - .001f)
                    offset = Mathf.Floor(Position) - Mathf.Floor(beat);
                Position = beat + offset;
                lastSongBeat = beat;
                songId = trackId;
            }
            else
            {
                songId = null;
                Position += Mathf.Max(0, seconds) / DefaultBeatSeconds;
            }
        }
        public float AfterBeats(int beats) => Mathf.Floor(Position + .00001f) + Mathf.Max(1, beats);
        public bool Reached(float deadline) => Position + .00001f >= deadline;
        public float SecondsUntil(float deadline, GameplayMusicPlayer music = null) => music != null && songId.HasValue
            ? music.SecondsAtBeat(deadline - offset) - music.PlaybackSeconds
            : (deadline - Position) * DefaultBeatSeconds;
    }
}
