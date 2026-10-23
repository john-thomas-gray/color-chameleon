using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class FullSetCelebration
    {
        public enum BarOrder { Wave, Random, Varied }
        public const float StepDuration = 60f / GameplayMusicPlayer.DefaultBeatsPerMinute;
        public const int MaxRewardBars = 6;
        public const float Duration = StepDuration * MaxRewardBars;
        public const float SpikePhase = .22f;
        public static float SpikeScale(float phase)
        {
            float spike = Mathf.Sin(Mathf.Clamp01(phase) * Mathf.PI);
            return 1 + 9 * spike * spike;
        }
        public static float WaveScale(float phase, bool overlap)
        {
            float riseStart = overlap ? -.35f : 0;
            float envelope = phase < .5f ? .5f * Mathf.InverseLerp(riseStart, .5f, phase) : phase;
            return SpikeScale(envelope);
        }
        public float FinalSpikeTime
        {
            get
            {
                if (!Active) return 0;
                float start = colors.Count == 1 ? holdSeconds : stepEnds[colors.Count - 2];
                return Mathf.Lerp(start, PlaybackDuration, SpikePhase);
            }
        }
        private readonly List<EnemyColor> colors = new List<EnemyColor>();
        private float age;
        private float holdSeconds;
        private int startedBars;
        private bool simultaneousPowerDown;
        private bool randomOrder;
        public bool FinalBarOverlaps => Active && !simultaneousPowerDown && !randomOrder && colors.Count > 1;
        private readonly List<float> stepEnds = new List<float>();
        public float PlaybackDuration => Active ? stepEnds[stepEnds.Count - 1] : 0;
        public static float DurationForBars(float barBeats, float beatDuration) =>
            Mathf.Max(0, barBeats) * Mathf.Max(.0001f, beatDuration);
        private static float BeatDurationForTotal(float duration, int bars) =>
            Mathf.Max(.0001f, Mathf.Max(.0001f, duration) / Mathf.Max(1, bars));
        private readonly System.Random random = new System.Random();
        public event System.Action<EnemyColor> ColorRemoved;
        public event System.Action<EnemyColor> BarStarted;
        public event System.Action<EnemyColor, float> BarSpiked;
        public EnemyColor? LastColor => Active ? colors[colors.Count - 1] : (EnemyColor?)null;
        public bool Active => colors.Count > 0;
        public bool Waiting => Active && age < holdSeconds;
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
        public Color Color => Active && simultaneousPowerDown ? Finished ? EnemyPalette.Get(LastColor.Value) : UnityEngine.Color.white :
            Active && (Finished || StepProgress >= .5f) ? EnemyPalette.Get(colors[ColorIndex]) : UnityEngine.Color.white;
        public bool SimultaneousPowerDown => Active && simultaneousPowerDown;
        public float PowerDownProgress => SimultaneousPowerDown ?
            Mathf.Clamp01((age - holdSeconds) / Mathf.Max(.0001f, PlaybackDuration - holdSeconds)) : 0;
        public float PowerDownProgressFor(EnemyColor color)
        {
            int index = colors.IndexOf(color);
            if (!Active || index < 0) return 0;
            if (SimultaneousPowerDown) return PowerDownProgress;
            float start = index == 0 ? holdSeconds : stepEnds[index - 1];
            float peak = Mathf.Lerp(start, stepEnds[index], .5f);
            return Mathf.Clamp01((age - peak) / Mathf.Max(.0001f, stepEnds[index] - peak));
        }
        public void Begin(List<EnemyColor> earned, EnemyColor[] plan = null, float duration = -1,
            float[] beatDurations = null, float holdDuration = 0, bool simultaneousPowerDown = false,
            BarOrder order = BarOrder.Wave)
        {
            colors.Clear(); colors.AddRange(earned); age = 0; startedBars = 0;
            holdSeconds = Mathf.Max(0, holdDuration);
            this.simultaneousPowerDown = simultaneousPowerDown;
            randomOrder = !simultaneousPowerDown && colors.Count > 1 &&
                (order == BarOrder.Random || order == BarOrder.Varied && random.Next(2) == 0);
            float totalDuration = duration >= 0 ? duration : DurationForBars(colors.Count, StepDuration);
            float fallback = BeatDurationForTotal(totalDuration - holdSeconds, colors.Count);
            stepEnds.Clear();
            float end = holdSeconds;
            for (int i = 0; i < colors.Count; i++)
            {
                if (simultaneousPowerDown)
                {
                    stepEnds.Add(Mathf.Max(.0001f, totalDuration));
                    continue;
                }
                float step = beatDurations != null && beatDurations.Length == colors.Count ? beatDurations[i] : fallback;
                end += Mathf.Max(.0001f, step);
                stepEnds.Add(end);
            }
            colors.Sort();
            if (!simultaneousPowerDown && !randomOrder)
            {
                // Match the left-to-right slot order; the session reserves the final color in the next fleet.
                return;
            }
            for (int i = colors.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var swap = colors[i]; colors[i] = colors[j]; colors[j] = swap;
            }
            if (randomOrder)
            {
                // A random variation should visibly differ from the regular left-to-right wave.
                bool sorted = true;
                for (int i = 1; i < colors.Count; i++)
                    if (colors[i - 1].CompareTo(colors[i]) > 0) { sorted = false; break; }
                if (sorted)
                { var swap = colors[0]; colors[0] = colors[1]; colors[1] = swap; }
                return;
            }
            // Prefer a final color already in the reserved fleet without consuming gameplay random numbers.
            if (plan != null)
                for (int i = colors.Count - 1; i >= 0; i--)
                    if (System.Array.IndexOf(plan, colors[i]) >= 0)
                    { var swap = colors[colors.Count - 1]; colors[colors.Count - 1] = colors[i]; colors[i] = swap; break; }
        }
        public void Reset() { colors.Clear(); stepEnds.Clear(); age = 0; holdSeconds = 0; startedBars = 0; simultaneousPowerDown = randomOrder = false; }
        public void Tick(float seconds)
        {
            float previous = age;
            age += Mathf.Max(0, seconds);
            // Walk chronological removal boundaries, including those crossed by a long frame.
            for (int index = 0; Active && index < colors.Count; index++)
            {
                float removalTime = stepEnds[index];
                float start = index == 0 ? holdSeconds : stepEnds[index - 1];
                if (!simultaneousPowerDown && index >= startedBars && age >= start)
                {
                    startedBars = index + 1;
                    BarStarted?.Invoke(colors[index]);
                }
                float spikeTime = start + (removalTime - start) * SpikePhase;
                if (!simultaneousPowerDown && previous < spikeTime && age >= spikeTime)
                    BarSpiked?.Invoke(colors[index], removalTime - start);
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
            if (!Active || index < 0) return resting;
            if (simultaneousPowerDown)
            {
                float progress = Mathf.Clamp01((age - holdSeconds) / Mathf.Max(.0001f, PlaybackDuration - holdSeconds));
                float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / .18f));
                float fall = Mathf.SmoothStep(0, 1, Mathf.Clamp01((progress - .18f) / .82f));
                float spike = rise * (1 - fall);
                float spikeHeight = resting.height * Mathf.Lerp(1, 5.4f, spike);
                return new Rect(resting.x, resting.yMax - spikeHeight, resting.width, spikeHeight);
            }
            float start = index == 0 ? holdSeconds : stepEnds[index - 1];
            float end = stepEnds[index];
            // Anticipate the next rise without moving any beat-counted removal or reward.
            float phase = (age - start) / Mathf.Max(.0001f, end - start);
            float height = resting.height * WaveScale(phase, !randomOrder && index > 0);
            return new Rect(resting.x, resting.yMax - height, resting.width, height);
        }
    }

    public sealed class ColorClearBarAnimation
    {
        public const float Duration = .65f;
        private readonly Dictionary<EnemyColor, float> ages = new Dictionary<EnemyColor, float>();
        public void Reset() => ages.Clear();
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
