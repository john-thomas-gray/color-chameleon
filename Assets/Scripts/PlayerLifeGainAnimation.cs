using UnityEngine;

namespace CandyCruisers
{
    public sealed class PlayerLifeGainAnimation
    {
        public int Slot { get; private set; } = -1;
        public int BarIndex { get; private set; }
        public int BarCount { get; private set; } = 1;
        public float Age { get; private set; }
        public float LaunchAt { get; private set; }
        public float FinishAt { get; private set; }
        public float EnterAt { get; private set; }
        private AudioClip musicClip;
        private float musicSeconds;
        private bool overlappingWave;
        public bool Active => Slot >= 0 && Age < FinishAt;
        public bool Visible => Active && Age >= EnterAt;
        public float EntranceProgress => Mathf.Clamp01((Age - EnterAt) / Mathf.Max(.0001f, LaunchAt - EnterAt));
        public float TravelProgress => Mathf.Clamp01((Age - LaunchAt) / Mathf.Max(.0001f, FinishAt - LaunchAt));
        public float Rotation => -360 * Mathf.SmoothStep(0, 1, Mathf.Clamp01(TravelProgress / .85f));
        public static Rect StageClip(Rect bars, float screenHeight) => new Rect(bars.x - 8, 0, bars.width + 16, screenHeight);

        public void Begin(int slot, GameplayMusicPlayer music = null, float launchDelay = -1,
            int barIndex = 0, int barCount = 1, bool overlappingWave = false)
        {
            Reset();
            if (slot < 0 || slot >= PlayerMovement.MaxExtraLives) return;
            Slot = slot; BarCount = Mathf.Max(1, barCount); BarIndex = Mathf.Clamp(barIndex, 0, BarCount - 1);
            this.overlappingWave = overlappingWave;
            float beat = music != null ? music.BeatDuration : FullSetCelebration.StepDuration;
            LaunchAt = launchDelay >= 0 ? launchDelay : beat;
            EnterAt = Mathf.Max(0, LaunchAt - beat * .65f);
            FinishAt = LaunchAt + 2 * beat;
            if (music == null || !music.BeatClockRunning || music.Source.clip == null) return;
            musicClip = music.Source.clip;
            musicSeconds = music.PlaybackSeconds;
            float launchSeconds = musicSeconds + LaunchAt;
            float launchBeat = music.BeatPositionAtTime(launchSeconds);
            FinishAt = music.SecondsAtBeat(launchBeat + 2) - musicSeconds;
        }

        public void Reset() { Slot = -1; Age = 0; musicClip = null; overlappingWave = false; }
        public void Tick(float seconds, GameplayMusicPlayer music = null)
        {
            if (!Active) return;
            seconds = Mathf.Max(0, seconds);
            if (musicClip != null)
            {
                if (music != null && music.Source.clip == musicClip && music.BeatClockRunning && music.PlaybackSeconds >= musicSeconds)
                {
                    float now = music.PlaybackSeconds;
                    seconds = now - musicSeconds; musicSeconds = now;
                }
                else musicClip = null;
            }
            Age = Mathf.Min(FinishAt, Age + seconds);
        }

        public Rect DisplayRect(Rect resting, Rect bars, Vector2 incomingSize, float screenWidth)
        {
            var bar = GameSession.ColorClearBarSlotRect(bars.x, bars.y, bars.width, BarCount, BarIndex);
            float launchTop = bar.yMax - bar.height * FullSetCelebration.WaveScale(FullSetCelebration.SpikePhase, overlappingWave);
            var stage = StageClip(bars, 1);
            bool fromRight = stage.xMax - bar.xMax <= bar.xMin - stage.xMin;
            float inset = Mathf.Min(bar.width / 2, incomingSize.x * .6f);
            var contact = new Vector2(fromRight ? bar.xMax - inset : bar.xMin + inset, launchTop - incomingSize.y / 2);
            Vector2 center, size;
            if (Age < LaunchAt)
            {
                float edge = fromRight ? stage.xMax + incomingSize.x * .55f : stage.xMin - incomingSize.x * .55f;
                center = Vector2.Lerp(new Vector2(edge, bar.yMin - incomingSize.y / 2), contact,
                    Mathf.SmoothStep(0, 1, EntranceProgress));
                size = incomingSize;
            }
            else
            {
                float t = TravelProgress;
                float rise = Mathf.Abs(contact.y - resting.center.y);
                var lift = new Vector2(Mathf.Lerp(contact.x, resting.center.x, .25f), contact.y - rise * .9f);
                var overLedge = new Vector2(resting.center.x, resting.center.y - Mathf.Clamp(rise * .2f, 30, 100));
                float remaining = 1 - t;
                center = remaining * remaining * remaining * contact + 3 * remaining * remaining * t * lift +
                    3 * remaining * t * t * overLedge + t * t * t * resting.center;
                size = Vector2.Lerp(incomingSize, resting.size, Mathf.SmoothStep(0, 1, t));
            }
            return new Rect(center - size / 2, size);
        }
    }
}
