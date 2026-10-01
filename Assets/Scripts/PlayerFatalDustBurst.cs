using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // Detached game-over artwork; its owning cue controls time and cleanup.
    public sealed class PlayerFatalDustBurst : MonoBehaviour
    {
        public const float Duration = PlayerDeathBurst.Duration;
        public const int CoreGrainsPerColor = 10;
        public const int ShockRingGrainsPerColor = 8;
        public const int GrainsPerColor = CoreGrainsPerColor + ShockRingGrainsPerColor;
        private SpriteRenderer[] grains;
        private EnemyColor[] grainColors;
        private bool[] shockRing;
        private Vector3[] origins;
        private Vector3[] directions;
        private float[] speeds;
        private float[] sizes;

        public static PlayerFatalDustBurst Create(SpriteRenderer source, Transform parent, int level)
        {
            var root = new GameObject("Player fatal dust burst");
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<PlayerFatalDustBurst>();
            var colors = UnlockedColors(level);
            int count = colors.Count * GrainsPerColor;
            burst.grains = new SpriteRenderer[count];
            burst.grainColors = new EnemyColor[count];
            burst.shockRing = new bool[count];
            burst.origins = new Vector3[count];
            burst.directions = new Vector3[count];
            burst.speeds = new float[count];
            burst.sizes = new float[count];

            var bounds = source.bounds;
            var visuals = source.GetComponentInParent<CharacterVisuals>();
            if (visuals != null)
                foreach (var sprite in visuals.Root.GetComponentsInChildren<SpriteRenderer>())
                    if (sprite != null && sprite.sprite != null) bounds.Encapsulate(sprite.bounds);
            root.transform.position = bounds.center;

            int sortingOrder = source.sortingOrder + 28;
            for (int colorIndex = 0; colorIndex < colors.Count; colorIndex++)
            for (int grain = 0; grain < GrainsPerColor; grain++)
            {
                int index = colorIndex * GrainsPerColor + grain;
                float seed = Mathf.Repeat(index * .618034f + colorIndex * .173f, 1);
                bool ring = grain >= CoreGrainsPerColor;
                int ringIndex = grain - CoreGrainsPerColor;
                float angle = ring ? ringIndex * Mathf.PI * 2 / ShockRingGrainsPerColor + colorIndex * .29f :
                    index * 2.399963f + colorIndex * .41f;
                var direction = ring ? new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .14f, 0) :
                    new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0).normalized;
                var renderer = new GameObject("Player fatal dust", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                renderer.transform.SetParent(root.transform, false);
                renderer.sprite = EnemyPlaceholderArt.SpaceDust;
                renderer.sharedMaterial = source.sharedMaterial;
                renderer.sortingOrder = sortingOrder + (ring ? 2 : 0);
                burst.grains[index] = renderer;
                burst.grainColors[index] = colors[colorIndex];
                burst.shockRing[index] = ring;
                burst.origins[index] = ring ?
                    direction * Mathf.Lerp(.01f, .035f, seed) + Vector3.up * Mathf.Lerp(-.008f, .008f, Mathf.Repeat(seed * 1.73f, 1)) :
                    direction * Mathf.Lerp(.015f, .14f, seed) + Vector3.up * Mathf.Lerp(-.07f, .09f, Mathf.Repeat(seed * 1.73f, 1));
                burst.directions[index] = direction;
                burst.speeds[index] = ring ? Mathf.Lerp(1.7f, 2.95f, Mathf.Repeat(seed * 2.31f, 1)) :
                    Mathf.Lerp(.45f, 1.45f, Mathf.Repeat(seed * 2.31f, 1));
                burst.sizes[index] = EnemyPlaceholderArt.RandomDustSize(ring ? .35f : .5f);
            }
            burst.Present(0);
            return burst;
        }

        private static List<EnemyColor> UnlockedColors(int level)
        {
            var colors = new List<EnemyColor>();
            foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                if (RunProgress.IsUnlocked(color, level)) colors.Add(color);
            if (colors.Count == 0) colors.Add(EnemyColor.Red);
            return colors;
        }

        public void Present(float age)
        {
            float t = Mathf.Clamp01(age / Duration);
            float drift = 1 - Mathf.Pow(1 - t, 3);
            for (int i = 0; i < grains.Length; i++)
            {
                var grain = grains[i];
                grain.enabled = age >= 0 && age < Duration;
                float shimmer = .5f + .5f * Mathf.Sin(age * (7 + i % 6) + i * 2.399963f);
                if (shockRing[i])
                {
                    float ring = Mathf.Clamp01((age - PlayerDeathBurst.ShatterSeconds * .55f) / (Duration - PlayerDeathBurst.ShatterSeconds * .55f));
                    float ringDrift = 1 - Mathf.Pow(1 - ring, 4);
                    float ripple = Mathf.Sin(age * 16 + i * 1.7f) * .012f * (1 - ring * .35f);
                    grain.transform.localPosition = origins[i] + directions[i] * (.08f + speeds[i] * ringDrift) +
                        Vector3.up * ripple;
                    grain.transform.localScale = Vector3.one * sizes[i] * Mathf.Lerp(1.45f, .66f, ring) *
                        Mathf.Lerp(.88f, 1.18f, shimmer);
                    var color = Color.Lerp(Color.white, EnemyPalette.Get(grainColors[i]), Mathf.Clamp01(ring * 4.5f));
                    color.a = Mathf.SmoothStep(0, 1, ring * 6) * (1 - ring * ring) * Mathf.Lerp(.78f, 1, shimmer);
                    grain.color = color;
                    continue;
                }
                Vector3 sideways = new Vector3(-directions[i].y, directions[i].x, 0) *
                    Mathf.Sin(age * (1.1f + i % 4 * .17f) + i) * .035f * t;
                grain.transform.localPosition = origins[i] + directions[i] * (.08f + speeds[i] * drift) +
                    sideways + Vector3.down * t * t * .26f;
                float flash = Mathf.Max(0, 1 - Mathf.Abs(age - PlayerDeathBurst.ShatterSeconds) / .16f);
                grain.transform.localScale = Vector3.one * sizes[i] * Mathf.Lerp(1.3f + flash * .65f, .78f, t) *
                    Mathf.Lerp(.9f, 1.14f, shimmer);
                var coreColor = Color.Lerp(Color.white, EnemyPalette.Get(grainColors[i]), Mathf.Clamp01(t * 3.2f));
                coreColor.a = (1 - t * t) * Mathf.Lerp(.72f, 1, shimmer);
                grain.color = coreColor;
            }
        }
    }
}
