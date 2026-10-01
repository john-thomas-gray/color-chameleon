using UnityEngine;

namespace CandyCruisers
{
    public sealed class PlayerLifeGainAnimation
    {
        public int Slot { get; private set; } = -1;
        public float Age { get; private set; }
        private AudioClip musicClip;
        private float musicSeconds;
        private float appearAt, travelAt, finishAt;
        public bool Active => Slot >= 0 && Age < finishAt;
        public bool Visible => Active && Age >= appearAt;
        public float AppearanceProgress => Mathf.Clamp01((Age - appearAt) / Mathf.Max(.0001f, (travelAt - appearAt) * .25f));
        public float PulseProgress => Mathf.Clamp01((Age - appearAt) / Mathf.Max(.0001f, travelAt - appearAt));
        public float PulseScale => 1 + .2f * Mathf.Sin(PulseProgress * Mathf.PI);
        public float TravelProgress => Mathf.Clamp01((Age - travelAt) / Mathf.Max(.0001f, finishAt - travelAt));
        public float Opacity => Visible ? Mathf.SmoothStep(0, 1, AppearanceProgress) : 0;

        public void Begin(int slot, GameplayMusicPlayer music = null, float? appearanceBeat = null)
        {
            Reset();
            if (slot < 0 || slot >= PlayerMovement.MaxExtraLives) return;
            Slot = slot;
            float beat = music != null ? music.BeatDuration : FullSetCelebration.StepDuration;
            appearAt = beat;
            travelAt = appearAt + beat;
            finishAt = travelAt + beat;
            if (music == null || !music.Source.isPlaying || music.Source.clip == null) return;
            musicClip = music.Source.clip;
            musicSeconds = music.PlaybackSeconds;
            float firstBeat = appearanceBeat ?? Mathf.Floor(music.BeatPositionAtTime(musicSeconds)) + 1;
            appearAt = music.SecondsAtBeat(firstBeat) - musicSeconds;
            travelAt = music.SecondsAtBeat(firstBeat + 1) - musicSeconds;
            finishAt = music.SecondsAtBeat(firstBeat + 2) - musicSeconds;
        }

        public void Reset() { Slot = -1; Age = 0; musicClip = null; }

        public void Tick(float seconds, GameplayMusicPlayer music = null)
        {
            if (!Active) return;
            seconds = Mathf.Max(0, seconds);
            if (musicClip != null)
            {
                if (music != null && music.Source.clip == musicClip && music.Source.isPlaying &&
                    music.PlaybackSeconds >= musicSeconds)
                {
                    float now = music.PlaybackSeconds;
                    seconds = now - musicSeconds;
                    musicSeconds = now;
                }
                else musicClip = null;
            }
            Age = Mathf.Min(finishAt, Age + seconds);
        }

        public Rect DisplayRect(Rect resting, float screenWidth, float screenHeight)
        {
            var centerstage = PlayerLifeIcons.LossDisplayRect(resting, screenWidth, screenHeight, PlayerLifeIcons.TravelSeconds);
            float appear = Mathf.SmoothStep(0, 1, AppearanceProgress);
            float travel = Mathf.SmoothStep(0, 1, TravelProgress);
            var size = Vector2.Lerp(centerstage.size * Mathf.Lerp(.65f, 1, appear) * PulseScale, resting.size, travel);
            var center = Vector2.Lerp(centerstage.center, resting.center, travel);
            return new Rect(center - size / 2, size);
        }
    }
}
