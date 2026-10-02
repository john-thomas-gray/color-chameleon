using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    [DefaultExecutionOrder(300)]
    public sealed class PlayerShockVisual : MonoBehaviour
    {
        public const float FlashSeconds = .12f, RecoverySeconds = .7f;
        private PlayerMovement player;
        private SpriteRenderer body;
        private Transform effects;
        private Transform sootRoot;
        public bool OwnsSprite(SpriteRenderer renderer) => cinders.Contains(renderer);
        private LineRenderer flash;
        private readonly List<LineRenderer> smoke = new List<LineRenderer>();
        private readonly List<Transform> features = new List<Transform>();
        private readonly List<Vector3> scales = new List<Vector3>();
        private readonly List<int> featureOrders = new List<int>();
        private readonly List<SpriteRenderer> cinders = new List<SpriteRenderer>();
        private readonly List<Sprite> sprites = new List<Sprite>();
        private readonly List<Vector3> origins = new List<Vector3>();
        private float age, recoveryAge;
        public bool Active { get; private set; }
        public bool Recovering { get; private set; }
        public Color? BodyTint => !Active || Recovering ? (Color?)null : age < FlashSeconds ? Color.white : Color.black;

        public void Begin()
        {
            ResetEffect();
            player = GetComponent<PlayerMovement>();
            var art = CharacterVisuals.Ensure(gameObject);
            body = art.Body;
            if (body == null || body.sprite == null) return;
            foreach (var part in art.Root.GetComponentsInChildren<SpriteRenderer>())
                if (part.name == "Eye" || part.name == "Pupil")
                {
                    features.Add(part.transform); scales.Add(part.transform.localScale); featureOrders.Add(part.sortingOrder);
                    part.sortingOrder = Mathf.Max(part.sortingOrder, body.sortingOrder + (part.name == "Pupil" ? 3 : 2));
                }
            effects = new GameObject("Shield shock aftermath").transform;
            effects.SetParent(transform, false);
            sootRoot = new GameObject("Soot layer").transform;
            sootRoot.SetParent(art.Root, false);
            flash = NewLine("Blue-white impact", .075f, new Color(.75f, .92f, 1), 25);
            flash.loop = true;
            for (int i = 0; i < 3; i++) smoke.Add(NewLine("Smoke " + i, .018f, new Color(.6f, .65f, .7f, .7f), 9));
            var source = body.sprite;
            const int columns = 6, rows = 5;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                var rect = new Rect(source.rect.x + source.rect.width * x / columns,
                    source.rect.y + source.rect.height * y / rows, source.rect.width / columns, source.rect.height / rows);
                var sprite = Sprite.Create(source.texture, rect, Vector2.one * .5f, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites.Add(sprite);
                var piece = new GameObject("Soot cinder", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                piece.transform.SetParent(sootRoot, false);
                piece.sprite = sprite; piece.sharedMaterial = body.sharedMaterial;
                piece.sortingLayerID = body.sortingLayerID; piece.sortingOrder = body.sortingOrder + 1;
                piece.color = Color.black; piece.enabled = false;
                cinders.Add(piece);
                origins.Add(new Vector3(source.bounds.min.x + source.bounds.size.x * (x + .5f) / columns,
                    source.bounds.min.y + source.bounds.size.y * (y + .5f) / rows));
            }
            Active = true; age = recoveryAge = 0;
            UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
            Tick(0);
        }

        public void BeginRecovery()
        {
            if (!Active || Recovering) return;
            Recovering = true; recoveryAge = 0;
            Tick(0);
            UnityEngine.Rendering.SortingGroup.UpdateAllSortingGroups();
        }
        private LineRenderer NewLine(string name, float width, Color color, int points)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(effects, false);
            line.sharedMaterial = body.sharedMaterial; line.useWorldSpace = true;
            line.positionCount = points; line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingLayerID = body.sortingLayerID; line.sortingOrder = body.sortingOrder + 5;
            return line;
        }
        private void LateUpdate() => Tick(Time.deltaTime);
        public void Tick(float seconds)
        {
            if (!Active) return;
            seconds = Mathf.Max(0, seconds); age += seconds;
            if (Recovering) recoveryAge += seconds;
            if (Recovering && recoveryAge >= RecoverySeconds) { ResetEffect(); player.RefreshPresentation(Time.time); return; }
            float release = Recovering ? Mathf.Clamp01((recoveryAge - .12f) / .5f) : 0;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i] == null) continue;
                bool pupil = features[i].name == "Pupil";
                float blink = Recovering ? 1 - .94f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(recoveryAge / .14f)) : 1;
                float size = pupil ? Mathf.Lerp(.3f, 1, release) : 1;
                features[i].localScale = Vector3.Scale(scales[i], new Vector3(size, size * blink, 1));
            }
            var bounds = body.bounds;
            flash.enabled = !Recovering && age < FlashSeconds;
            for (int i = 0; i < flash.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / (flash.positionCount - 1);
                float radius = bounds.size.x * (i % 2 == 0 ? .9f : .6f) * (1 + age / FlashSeconds * .3f);
                flash.SetPosition(i, bounds.center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
            }
            for (int i = 0; i < smoke.Count; i++)
            {
                float phase = Mathf.Repeat(age * .8f + i * .31f, 1);
                var tint = new Color(.62f, .67f, .72f, .65f * (1 - phase) * (1 - release));
                smoke[i].startColor = smoke[i].endColor = tint;
                for (int j = 0; j < smoke[i].positionCount; j++)
                {
                    float t = j / 8f;
                    smoke[i].SetPosition(j, new Vector3(bounds.center.x + (i - 1) * bounds.size.x * .23f +
                        Mathf.Sin(t * 7 + age * 5 + i) * .035f, bounds.max.y + phase * .5f + t * .22f, transform.position.z));
                }
            }
            for (int i = 0; i < cinders.Count; i++)
            {
                var piece = cinders[i]; piece.enabled = Recovering;
                float fall = Mathf.Clamp01((release - (i * 7 % 19) / 40f) * 2);
                piece.transform.position = body.transform.TransformPoint(origins[i]) +
                    new Vector3(Mathf.Sin(i * 13) * fall * .35f, -fall * fall * .7f, 0);
                piece.transform.rotation = body.transform.rotation * Quaternion.Euler(0, 0, fall * (i % 2 == 0 ? 100 : -100));
                var parentScale = sootRoot.lossyScale;
                var worldScale = body.transform.lossyScale * (1 - fall * .5f);
                piece.transform.localScale = new Vector3(worldScale.x / parentScale.x, worldScale.y / parentScale.y, worldScale.z / parentScale.z);
                piece.color = new Color(.025f, .025f, .025f, 1 - fall);
            }
            player.RefreshPresentation(Time.time);
        }
        public void ResetEffect()
        {
            Active = Recovering = false;
            for (int i = 0; i < features.Count; i++) if (features[i] != null)
            {
                features[i].localScale = scales[i];
                features[i].GetComponent<SpriteRenderer>().sortingOrder = featureOrders[i];
            }
            features.Clear(); scales.Clear(); featureOrders.Clear();
            if (effects != null) { effects.gameObject.SetActive(false); Dispose(effects.gameObject); }
            if (sootRoot != null) { sootRoot.gameObject.SetActive(false); Dispose(sootRoot.gameObject); }
            foreach (var sprite in sprites) Dispose(sprite);
            effects = sootRoot = null; sprites.Clear(); cinders.Clear(); origins.Clear(); smoke.Clear();
        }
        private static void Dispose(Object item) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        private void OnDisable() => ResetEffect();
    }
}
