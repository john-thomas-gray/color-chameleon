using UnityEngine;

namespace CandyCruisers
{
    // Detached placeholder artwork; its owning cue controls time and cleanup.
    public sealed class PlayerDeathBurst : MonoBehaviour
    {
        public const float Duration = .9f;
        private SpriteRenderer[] pieces;
        private Vector3[] origins, scales;
        private Color[] colors;
        private SpriteRenderer[] fragments;
        private Color tint;

        public static PlayerDeathBurst Create(SpriteRenderer source, Transform parent, Color color)
        {
            var root = new GameObject("Player death burst");
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<PlayerDeathBurst>();
            burst.tint = color;
            var visuals = source.GetComponentInParent<CharacterVisuals>();
            var sources = visuals != null ? visuals.Root.GetComponentsInChildren<SpriteRenderer>() : new[] { source };
            burst.pieces = new SpriteRenderer[sources.Length];
            burst.origins = new Vector3[sources.Length];
            burst.scales = new Vector3[sources.Length];
            burst.colors = new Color[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                var original = sources[i];
                var copy = new GameObject(original.name + " snapshot", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                copy.transform.SetParent(root.transform, false);
                copy.transform.position = original.transform.position;
                copy.transform.rotation = original.transform.rotation;
                copy.transform.localScale = original.transform.lossyScale;
                copy.sprite = original.sprite;
                copy.sharedMaterial = original.sharedMaterial;
                copy.sortingOrder = original.sortingOrder + 20;
                burst.pieces[i] = copy;
                burst.origins[i] = copy.transform.localPosition;
                burst.scales[i] = copy.transform.localScale;
                burst.colors[i] = original.color;
            }
            burst.fragments = new SpriteRenderer[14];
            for (int i = 0; i < burst.fragments.Length; i++)
            {
                var fragment = new GameObject("Player fragment", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                fragment.transform.SetParent(root.transform, false);
                fragment.sprite = EnemyPlaceholderArt.Triangle;
                fragment.sharedMaterial = source.sharedMaterial;
                fragment.sortingOrder = source.sortingOrder + 24;
                burst.fragments[i] = fragment;
            }
            burst.Present(0);
            return burst;
        }

        public void Present(float age)
        {
            float flash = Mathf.Clamp01(age / .14f);
            float scatter = Mathf.Clamp01((age - .14f) / .22f);
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                piece.enabled = age < .36f;
                piece.color = Color.Lerp(colors[i], Color.white, flash);
                piece.transform.localScale = scales[i] * (1 + flash * .22f) * (1 - scatter);
                piece.transform.localPosition = origins[i] * (1 + scatter * 2);
            }
            float t = Mathf.Clamp01((age - .14f) / (Duration - .14f));
            for (int i = 0; i < fragments.Length; i++)
            {
                var fragment = fragments[i];
                fragment.enabled = age >= .14f && age < Duration;
                float angle = i * Mathf.PI * 2 / fragments.Length;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                fragment.transform.localPosition = direction * (.1f + t * .8f) + Vector3.down * t * t * .18f;
                fragment.transform.localScale = Vector3.one * (.11f * (1 - t));
                fragment.transform.localRotation = Quaternion.Euler(0, 0, i * 26 + t * 220);
                fragment.color = Color.Lerp(Color.white, tint, Mathf.Min(1, t * 4));
                var color = fragment.color; color.a = 1 - t * t; fragment.color = color;
            }
        }
    }
}
