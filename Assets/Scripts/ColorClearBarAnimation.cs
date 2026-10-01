using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class FullSetCelebration
    {
        public const float StepDuration = 60f / GameplayMusicPlayer.DefaultBeatsPerMinute;
        public const int MaxRewardBars = 6;
        public const float Duration = StepDuration * MaxRewardBars;
        private readonly List<EnemyColor> colors = new List<EnemyColor>();
        private float age;
        private float holdSeconds;
        private readonly List<float> stepEnds = new List<float>();
        public float PlaybackDuration => Active ? stepEnds[stepEnds.Count - 1] : 0;
        public static float DurationForBars(float barBeats, float beatDuration) =>
            Mathf.Max(0, barBeats) * Mathf.Max(.0001f, beatDuration);
        private static float BeatDurationForTotal(float duration, int bars) =>
            Mathf.Max(.0001f, Mathf.Max(.0001f, duration) / Mathf.Max(1, bars));
        private readonly System.Random random = new System.Random();
        public event System.Action<EnemyColor> ColorRemoved;
        public EnemyColor? LastColor => Active ? colors[colors.Count - 1] : (EnemyColor?)null;
        public bool Active => colors.Count > 0;
        public bool Finished => Active && age >= PlaybackDuration;
        private int Step
        {
            get
            {
                int step = 0;
                while (step < colors.Count - 1 && age >= stepEnds[step]) step++;
                return step;
            }
        }
        private float StepProgress
        {
            get
            {
                if (!Active) return 0;
                int step = Step;
                float start = step == 0 ? holdSeconds : stepEnds[step - 1];
                return Mathf.Clamp01((age - start) / (stepEnds[step] - start));
            }
        }
        private int ColorIndex => Mathf.Clamp(Step, 0, Mathf.Max(0, colors.Count - 1));
        public Color Color => Active && (Finished || StepProgress >= .5f) ?
            EnemyPalette.Get(colors[ColorIndex]) : UnityEngine.Color.white;
        public void Begin(List<EnemyColor> earned, EnemyColor[] plan = null, float duration = -1,
            float[] beatDurations = null, float holdDuration = 0)
        {
            colors.Clear(); colors.AddRange(earned); age = 0;
            holdSeconds = Mathf.Max(0, holdDuration);
            float totalDuration = duration >= 0 ? duration : DurationForBars(colors.Count, StepDuration);
            float fallback = BeatDurationForTotal(totalDuration - holdSeconds, colors.Count);
            stepEnds.Clear();
            float end = holdSeconds;
            for (int i = 0; i < colors.Count; i++)
            {
                float step = beatDurations != null && beatDurations.Length == colors.Count ? beatDurations[i] : fallback;
                end += Mathf.Max(.0001f, step);
                stepEnds.Add(end);
            }
            for (int i = colors.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var swap = colors[i]; colors[i] = colors[j]; colors[j] = swap;
            }
            // Prefer a final color already in the reserved fleet without consuming gameplay random numbers.
            if (plan != null)
                for (int i = colors.Count - 1; i >= 0; i--)
                    if (System.Array.IndexOf(plan, colors[i]) >= 0)
                    { var swap = colors[colors.Count - 1]; colors[colors.Count - 1] = colors[i]; colors[i] = swap; break; }
        }
        public void Reset() { colors.Clear(); stepEnds.Clear(); age = 0; holdSeconds = 0; }
        public void Tick(float seconds)
        {
            float previous = age;
            age += Mathf.Max(0, seconds);
            // Walk chronological removal boundaries, including those crossed by a long frame.
            for (int index = 0; Active && index < colors.Count; index++)
            {
                float removalTime = stepEnds[index];
                if (previous < removalTime && age >= removalTime) ColorRemoved?.Invoke(colors[index]);
            }
        }
        public bool Visible(EnemyColor color)
        {
            int index = colors.IndexOf(color);
            return !Active || index >= 0 && age < stepEnds[index];
        }
        public Rect Present(EnemyColor color, Rect resting)
        {
            int index = colors.IndexOf(color);
            if (!Active || index != Step) return resting;
            float beat = StepProgress;
            float t = Mathf.Clamp01((beat - .5f) * 2);
            float growth = Mathf.SmoothStep(0, 1, t);
            float width = resting.width * Mathf.Lerp(1, 1.25f, growth);
            float height = resting.height * Mathf.Lerp(1, 5, growth);
            return new Rect(resting.center.x - width / 2, resting.yMax - height, width, height);
        }
    }

    public sealed class ColorClearBarAnimation
    {
        public const float Duration = .65f;
        private readonly Dictionary<EnemyColor, float> ages = new Dictionary<EnemyColor, float>();
        public void Begin(EnemyColor color) => ages[color] = 0;
        public void Tick(float seconds)
        {
            foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                if (ages.ContainsKey(color)) ages[color] = Mathf.Min(Duration, ages[color] + Mathf.Max(0, seconds));
        }
        private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9 };
        public static float TonePitch(int bars) => Mathf.Pow(2, MajorScale[Mathf.Clamp(bars - 1, 0, 5)] / 12f);
        public Rect Present(EnemyColor color, Rect resting)
        {
            if (!ages.TryGetValue(color, out float age) || age >= Duration) return resting;
            float t = age / Duration;
            float ease = 1 - Mathf.Pow(1 - t, 3);
            float width = resting.width * Mathf.Lerp(1.25f, 1, ease);
            float height = resting.height * Mathf.Lerp(4, 1, ease);
            var center = resting.center + Vector2.up * Mathf.Lerp(-32, 0, ease);
            return new Rect(center.x - width / 2, center.y - height / 2, width, height);
        }
    }
}
