using UnityEngine;

namespace CandyCruisers
{
    public sealed class LifeSlimeChoreography
    {
        public bool Swaying { get; private set; }
        public float Beat { get; private set; }
        private int measure, track;
        private bool initialized;
        private float phase, flipAge, flipDuration;
        public bool Flipping => flipDuration > 0 && flipAge < flipDuration;
        public float FlipProgress => Flipping ? Mathf.Clamp01(flipAge / flipDuration) : 0;
        public float FlipAngle => Flipping ? 360 * Mathf.SmoothStep(0, 1, FlipProgress) : 0;
        public float FlipHop => Flipping ? Mathf.Sin(Mathf.PI * FlipProgress) : 0;
        public float FlipVerticalScale => Flipping ? PlayerLifeIcons.LossVerticalScale(FlipProgress) : 1;
        public float Lean => Swaying ? 16 * Mathf.Sin(phase * Mathf.PI) * Mathf.Pow(Mathf.Sin(phase * Mathf.PI / 4), 2) : 0;
        public float Scale => PlayerLifeIcons.BeatScale(Beat);

        public void Reset() { initialized = false; Swaying = false; Beat = phase = flipAge = flipDuration = 0; }
        public void BeginFlip(float seconds) { flipAge = 0; flipDuration = Mathf.Max(.001f, seconds); }

        public void Tick(float seconds, GameplayMusicPlayer music)
        {
            seconds = Mathf.Max(0, seconds);
            bool playing = music != null && music.BeatClockRunning && music.Source.clip != null;
            float beat = playing ? music.BeatPosition : Beat + seconds / FullSetCelebration.StepDuration;
            int offset = music != null ? music.CurrentDownbeatOffsetBeats : 0;
            int id = playing ? music.Source.clip.GetInstanceID() : 0;
            SetBeat(beat, offset, id);
            flipAge = Mathf.Min(flipDuration, flipAge + seconds);
        }

        public void SetBeat(float beat, int downbeatOffset, int trackId)
        {
            int nextMeasure = Mathf.FloorToInt((beat - downbeatOffset) / GameplayMusicPlayer.BeatsPerMeasure);
            // Song changes and seeks preserve the routine until the next measure boundary.
            if (initialized && track == trackId && beat >= Beat && nextMeasure != measure)
                Swaying = ((nextMeasure % 5) + 5) % 5 == 3;
            initialized = true;
            track = trackId;
            measure = nextMeasure;
            Beat = beat;
            phase = Mathf.Repeat(beat - downbeatOffset, GameplayMusicPlayer.BeatsPerMeasure);
        }
    }
}
