using UnityEngine;

namespace CandyCruisers
{
    // A detached visual snapshot: no occupancy, ability, scoring or collision behavior.
    public sealed class EnemyDeathBurst : MonoBehaviour
    {
        public const float RingDelay = .065f;
        public const float BurstSeconds = .5f;
        private SpriteRenderer snapshot;
        private TextMesh digit;
        private SpriteRenderer[] shards;
        private Color tint;
        private float age;
        public int Depth { get; private set; }
        public int Multiplier { get; private set; }
        public bool Exploded => age >= (Depth - 1) * RingDelay;
        public bool Finished { get; private set; }

        public static EnemyDeathBurst Create(SpriteRenderer source, EnemyColor color, int depth, int? multiplier = null)
        {
            var root = new GameObject("Enemy death " + depth);
            root.transform.position = source.transform.position;
            var effect = root.AddComponent<EnemyDeathBurst>();
            effect.Depth = Mathf.Max(1, depth);
            effect.Multiplier = Mathf.Max(1, multiplier ?? effect.Depth);
            effect.tint = EnemyPalette.Get(color);
            var copy = new GameObject("Dying body", typeof(SpriteRenderer));
            copy.transform.SetParent(root.transform, false);
            copy.transform.localScale = source.transform.lossyScale;
            copy.transform.rotation = source.transform.rotation;
            effect.snapshot = copy.GetComponent<SpriteRenderer>();
            effect.snapshot.sprite = source.sprite;
            effect.snapshot.sharedMaterial = source.sharedMaterial;
            effect.snapshot.sortingOrder = source.sortingOrder;
            effect.snapshot.color = effect.tint;
            var label = new GameObject("Connection depth", typeof(TextMesh));
            label.transform.SetParent(root.transform, false);
            effect.digit = label.GetComponent<TextMesh>();
            effect.digit.text = effect.Multiplier == 1 ? "" : effect.Multiplier.ToString();
            effect.digit.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            effect.digit.fontSize = 64;
            effect.digit.characterSize = .08f;
            effect.digit.fontStyle = FontStyle.Bold;
            effect.digit.anchor = TextAnchor.MiddleCenter;
            effect.digit.alignment = TextAlignment.Center;
            effect.digit.color = effect.tint;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = effect.digit.font.material;
            renderer.sortingOrder = source.sortingOrder + 6;
            effect.shards = new SpriteRenderer[7];
            for (int i = 0; i < effect.shards.Length; i++)
            {
                var shard = new GameObject("Shard", typeof(SpriteRenderer));
                shard.transform.SetParent(root.transform, false);
                var sprite = shard.GetComponent<SpriteRenderer>();
                sprite.sprite = EnemyPlaceholderArt.Triangle;
                sprite.sharedMaterial = source.sharedMaterial;
                sprite.sortingOrder = source.sortingOrder + 5;
                effect.shards[i] = sprite;
            }
            effect.Tick(0);
            return effect;
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (Finished) return;
            age += Mathf.Max(0, seconds);
            float burstAge = age - (Depth - 1) * RingDelay;
            snapshot.enabled = burstAge < 0;
            digit.gameObject.SetActive(burstAge >= 0);
            float progress = Mathf.Clamp01(burstAge / BurstSeconds);
            digit.color = new Color(tint.r, tint.g, tint.b, 1 - progress * progress);
            digit.transform.localScale = Vector3.one * Mathf.Lerp(.5f, 1.15f, Mathf.Min(1, progress * 5));
            digit.transform.localPosition = Vector3.up * progress * .12f;
            for (int i = 0; i < shards.Length; i++)
            {
                var shard = shards[i];
                shard.enabled = burstAge >= 0;
                float angle = i * Mathf.PI * 2 / shards.Length;
                shard.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (.06f + progress * .3f);
                shard.transform.localScale = Vector3.one * (.07f * (1 - progress));
                shard.transform.localRotation = Quaternion.Euler(0, 0, i * 51 + progress * 120);
                shard.color = new Color(tint.r, tint.g, tint.b, 1 - progress);
            }
            if (burstAge < BurstSeconds) return;
            Finished = true;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
        }
    }
}
