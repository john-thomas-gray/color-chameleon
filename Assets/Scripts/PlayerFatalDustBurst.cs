using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // Detached game-over artwork; its owning cue controls time and cleanup.
    public sealed class PlayerFatalDustBurst : MonoBehaviour
    {
        public const float Duration = 2.4f;
        public const int CoreGrains = 640;
        public const int SprayGrains = 320;
        public const int GrainCount = CoreGrains + SprayGrains;
        private readonly Vector3[] vertices = new Vector3[GrainCount * 4];
        private readonly Color[] colors = new Color[GrainCount * 4];
        private readonly Vector3[] directions = new Vector3[GrainCount];
        private readonly float[] speeds = new float[GrainCount];
        private readonly float[] sizes = new float[GrainCount];
        private readonly Color[] tints = new Color[GrainCount];
        private Mesh mesh;
        private MeshRenderer visual;
        private Material material;
        private SpriteRenderer flash;

        public static PlayerFatalDustBurst Create(SpriteRenderer source, Transform parent, int level)
        {
            var root = new GameObject("Player fatal dust burst", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<PlayerFatalDustBurst>();
            var palette = new List<Color>();
            foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                if (RunProgress.IsUnlocked(color, level)) palette.Add(EnemyPalette.Get(color));
            if (palette.Count == 0) palette.Add(EnemyPalette.Get(EnemyColor.Red));
            var bounds = source.bounds;
            var visuals = source.GetComponentInParent<CharacterVisuals>();
            if (visuals != null)
                foreach (var sprite in visuals.Root.GetComponentsInChildren<SpriteRenderer>())
                    if (sprite != null && sprite.sprite != null) bounds.Encapsulate(sprite.bounds);
            root.transform.position = bounds.center;

            // One textured mesh keeps the dense powder spray to one draw, even at opening levels.
            burst.material = new Material(Shader.Find("Sprites/Default"))
                { name = "Fatal multicolored powder", mainTexture = EnemyPlaceholderArt.SpaceDust.texture };
            burst.visual = root.GetComponent<MeshRenderer>();
            burst.visual.sharedMaterial = burst.material;
            burst.visual.sortingLayerID = source.sortingLayerID;
            burst.visual.sortingOrder = source.sortingOrder + 28;
            burst.mesh = new Mesh { name = "Fatal powder spray" };
            burst.mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = burst.mesh;
            var coordinates = new Vector2[GrainCount * 4];
            var triangles = new int[GrainCount * 6];
            var random = new System.Random(7319);
            for (int i = 0; i < GrainCount; i++)
            {
                float seed = (float)random.NextDouble();
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                // Uneven lobes carry the spray diagonally across the shockwave's plane.
                float fan = .68f + .32f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle - .45f)), 3);
                burst.directions[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                burst.speeds[i] = (i < CoreGrains ? Mathf.Lerp(.25f, 2.8f, seed * seed) :
                    Mathf.Lerp(2.8f, 5.8f, seed)) * fan;
                burst.sizes[i] = Mathf.Lerp(.03f, .11f, Mathf.Pow((float)random.NextDouble(), 2));
                burst.tints[i] = palette[i % palette.Count];
                int vertex = i * 4, triangle = i * 6;
                coordinates[vertex] = Vector2.zero;
                coordinates[vertex + 1] = Vector2.right;
                coordinates[vertex + 2] = Vector2.one;
                coordinates[vertex + 3] = Vector2.up;
                triangles[triangle] = vertex; triangles[triangle + 1] = vertex + 2; triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex; triangles[triangle + 4] = vertex + 3; triangles[triangle + 5] = vertex + 2;
            }
            burst.mesh.vertices = burst.vertices;
            burst.mesh.uv = coordinates;
            burst.mesh.triangles = triangles;
            burst.flash = new GameObject("Fatal detonation flash", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            burst.flash.transform.SetParent(root.transform, false);
            burst.flash.sprite = EnemyPlaceholderArt.SpaceDust;
            burst.flash.sharedMaterial = source.sharedMaterial;
            burst.flash.sortingOrder = source.sortingOrder + 31;
            burst.Present(0);
            return burst;
        }

        public void Present(float age)
        {
            float flight = Mathf.Max(0, age - PlayerDeathBurst.ShatterSeconds);
            float t = Mathf.Clamp01(flight / (Duration - PlayerDeathBurst.ShatterSeconds));
            float gather = Mathf.Clamp01(age / PlayerDeathBurst.ShatterSeconds);
            var camera = Camera.main;
            float viewScale = camera != null && camera.orthographic ?
                camera.orthographicSize * 2 * Mathf.Min(1, camera.aspect) / 6.4f : 1;
            visual.enabled = age >= 0 && age < Duration;
            flash.enabled = age >= PlayerDeathBurst.ShatterSeconds && flight < .12f;
            flash.transform.localScale = Vector3.one * Mathf.Lerp(.85f, 1.6f, flight / .12f) * viewScale;
            flash.color = new Color(1, 1, 1, 1 - Mathf.Clamp01(flight / .12f));
            for (int i = 0; i < GrainCount; i++)
            {
                float seed = Mathf.Repeat(i * .618034f, 1);
                float perspective = 1 / (1 - t * Mathf.Lerp(.12f, .62f, seed));
                float travel = flight * speeds[i] * perspective;
                Vector3 sideways = new Vector3(-directions[i].y, directions[i].x, 0) *
                    Mathf.Sin(flight * 1.7f + i) * .08f * t;
                Vector3 position = (directions[i] * (travel + .14f * seed * (1 - gather)) + sideways) * viewScale;
                float size = sizes[i] * perspective * viewScale;
                int vertex = i * 4;
                vertices[vertex] = position + new Vector3(-size, -size);
                vertices[vertex + 1] = position + new Vector3(size, -size);
                vertices[vertex + 2] = position + new Vector3(size, size);
                vertices[vertex + 3] = position + new Vector3(-size, size);
                var tint = Color.Lerp(Color.white, tints[i], Mathf.Clamp01(flight / .1f));
                tint.a = (1 - Mathf.SmoothStep(0, 1, (t - .63f) / .37f)) * Mathf.Lerp(.55f, 1, seed);
                for (int corner = 0; corner < 4; corner++) colors[vertex + corner] = tint;
            }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying) { Destroy(mesh); Destroy(material); }
            else { DestroyImmediate(mesh); DestroyImmediate(material); }
        }
    }
}
