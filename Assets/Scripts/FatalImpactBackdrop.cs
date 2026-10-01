using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // Presentation only: one impact envelope, with no camera shake or gameplay time changes.
    public sealed class FatalImpactBackdrop : MonoBehaviour
    {
        public const float Duration = .62f;
        public const int SortingOrder = GameOverBlackout.CoverOrder + 1;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<EnemyColor> particleColors = new List<EnemyColor>();
        private SpriteRenderer[] particles;
        private float[] particleSizes;
        private Mesh mesh;
        private MeshRenderer visual;
        private Vector3 origin;
        private float age;
        private Camera viewCamera;
        private float aspect, depth;
        public bool Active => age >= 0 && age < Duration;

        public static FatalImpactBackdrop Create(Transform owner, Vector3 position, Material material, int level)
        {
            var root = new GameObject("Fatal impact backdrop", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(owner, false);
            var impact = root.AddComponent<FatalImpactBackdrop>();
            impact.origin = position;
            foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
                if (RunProgress.IsUnlocked(color, level)) impact.particleColors.Add(color);
            impact.visual = root.GetComponent<MeshRenderer>();
            impact.visual.sharedMaterial = material;
            impact.visual.sortingOrder = SortingOrder;
            impact.particles = new SpriteRenderer[impact.particleColors.Count * 8];
            impact.particleSizes = new float[impact.particles.Length];
            for (int i = 0; i < impact.particles.Length; i++)
            {
                var particle = new GameObject("Fatal spacedust", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                particle.transform.SetParent(root.transform, false);
                particle.sprite = EnemyPlaceholderArt.SpaceDust;
                particle.sortingOrder = SortingOrder + 1;
                impact.particles[i] = particle;
                impact.particleSizes[i] = EnemyPlaceholderArt.RandomDustSize();
            }
            impact.mesh = new Mesh { name = "Fatal impact streaks and shards" };
            impact.mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = impact.mesh;
            impact.Present(0);
            return impact;
        }

        public void Present(float seconds)
        {
            age = Mathf.Max(0, seconds);
            Refresh();
        }

        public void Refresh()
        {
            if (visual == null) return;
            visual.enabled = Active;
            viewCamera = Camera.main;
            foreach (var particle in particles) particle.enabled = Active && viewCamera != null && age >= PlayerDeathBurst.ShatterSeconds;
            if (!Active || viewCamera == null) return;
            aspect = viewCamera.aspect;
            depth = viewCamera.nearClipPlane + 1;
            Vector2 center = viewCamera.WorldToViewportPoint(origin);
            float launch = Mathf.Clamp01(age / PlayerDeathBurst.ShatterSeconds);
            float fade = 1 - Mathf.SmoothStep(0, 1, (age - .18f) / (Duration - .18f));
            float strength = (.6f + .4f * launch) * fade;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            // Short broken arcs stay close to the hit; no backdrop geometry should span the whole screen.
            for (int arc = 0; arc < 3; arc++)
            {
                float radius = .042f + arc * .019f + launch * .022f + age * .06f;
                float start = .12f + arc * 2.1f;
                Vector2 previous = center + Direction(start) * radius;
                for (int segment = 1; segment <= 18; segment++)
                {
                    float t = segment / 18f;
                    Vector2 point = center + Direction(start + t * 1.65f) * radius;
                    float taper = Mathf.Sin(t * Mathf.PI);
                    Stroke(previous, point, .0007f + .0014f * taper, new Color(1, 1, 1, strength * .8f));
                    previous = point;
                }
            }
            DrawParticles(center, fade);
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
        }

        private Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle) / aspect, Mathf.Sin(angle));

        private void DrawParticles(Vector2 center, float fade)
        {
            float flight = age - PlayerDeathBurst.ShatterSeconds;
            if (flight < 0) return;
            // Analytic motion keeps every unlocked color in the burst without consuming gameplay randomness.
            for (int colorIndex = 0; colorIndex < particleColors.Count; colorIndex++)
            for (int shard = 0; shard < 8; shard++)
            {
                float seed = Mathf.Repeat(shard * .618034f + colorIndex * .381966f, 1);
                float angle = shard * Mathf.PI * 2 / 8 + colorIndex * .47f;
                float distance = flight * (.65f + seed * .7f) * (1 - flight * .6f);
                Vector2 point = center + Direction(angle) * distance;
                int index = colorIndex * 8 + shard;
                var particle = particles[index];
                float shimmer = .5f + .5f * Mathf.Sin(age * (8 + index % 5) + index * 2.399963f);
                float size = particleSizes[index] * .1f * Mathf.Lerp(.9f, 1.1f, shimmer);
                var position = viewCamera.ViewportToWorldPoint(new Vector3(point.x, point.y, depth));
                var edge = viewCamera.ViewportToWorldPoint(new Vector3(point.x, point.y + size, depth));
                particle.transform.position = position;
                particle.transform.rotation = viewCamera.transform.rotation;
                float worldSize = Vector3.Distance(position, edge);
                var parentScale = transform.lossyScale;
                particle.transform.localScale = new Vector3(worldSize / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                    worldSize / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1);
                Color color = EnemyPalette.Get(particleColors[colorIndex]);
                color.a = fade * Mathf.Lerp(.7f, 1, shimmer);
                particle.color = color;
            }
        }

        private void Stroke(Vector2 start, Vector2 end, float width, Color color)
        {
            var delta = new Vector2((end.x - start.x) * aspect, end.y - start.y).normalized;
            var side = new Vector2(-delta.y / aspect, delta.x) * width;
            Triangle(start - side, end - side, end + side, color);
            Triangle(start - side, end + side, start + side, color);
        }

        private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            Add(a, color); Add(b, color); Add(c, color);
        }

        private void Add(Vector2 point, Color color)
        {
            triangles.Add(vertices.Count);
            vertices.Add(transform.InverseTransformPoint(viewCamera.ViewportToWorldPoint(new Vector3(point.x, point.y, depth))));
            colors.Add(color);
        }

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
