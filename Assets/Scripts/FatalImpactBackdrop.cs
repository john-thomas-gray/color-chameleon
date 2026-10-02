using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    // Presentation only: one impact envelope, with no camera shake or gameplay time changes.
    public sealed class FatalImpactBackdrop : MonoBehaviour
    {
        public const float Duration = PlayerFatalDustBurst.Duration;
        public const int SortingOrder = GameOverBlackout.CoverOrder + 1;
        private const int RingSegments = 160;
        private static readonly float[] RingProfile = { -3, -1.3f, -.35f, 0, .35f, 1.3f, 3 };
        private static readonly float[] RingOpacity = { 0, .12f, .65f, 1, .65f, .12f, 0 };
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
            impact.mesh = new Mesh { name = "Fatal tilted shockwave crests" };
            impact.mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = impact.mesh;
            impact.Present(0);
            return impact;
        }

        public void Present(float seconds)
        {
            age = seconds;
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
            float flight = age - PlayerDeathBurst.ShatterSeconds;
            float fade = 1 - Mathf.SmoothStep(0, 1, (age - 1.45f) / (Duration - 1.45f));
            vertices.Clear(); colors.Clear(); triangles.Clear();
            if (flight >= 0)
            {
                // The reference wave is a nearly edge-on, upright disc, not a horizontal belt.
                float radius = .035f + flight * .8f + flight * flight * 1.1f;
                DrawShockwave(center, radius, .003f + flight * .012f, fade);
                if (flight > .025f)
                    DrawShockwave(center, radius * .89f, .0015f + flight * .004f, fade * .48f);
            }
            DrawParticles(center, fade);
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
        }

        private Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle) / aspect, Mathf.Sin(angle));

        private void DrawShockwave(Vector2 center, float radius, float width, float opacity)
        {
            int first = vertices.Count;
            float shortSide = Mathf.Min(1, aspect);
            var rotation = Quaternion.Euler(0, 0, 8);
            for (int segment = 0; segment <= RingSegments; segment++)
            {
                float angle = segment * Mathf.PI * 2 / RingSegments;
                float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
                float ripple = 1 + .0015f * Mathf.Sin(angle * 19 + age * 13) +
                    .0008f * Mathf.Sin(angle * 37 - age * 9);
                Vector2 point = rotation * new Vector3(cosine * .16f, sine, 0) * (radius * ripple);
                Vector2 normal = rotation * new Vector3(cosine, sine * .16f, 0).normalized;
                float nearSide = .5f - .5f * cosine;
                for (int band = 0; band < RingProfile.Length; band++)
                {
                    Vector2 offset = (point + normal * (RingProfile[band] * width * Mathf.Lerp(.45f, 1.8f, nearSide))) * shortSide;
                    offset.x /= aspect;
                    vertices.Add(transform.InverseTransformPoint(viewCamera.ViewportToWorldPoint(
                        new Vector3(center.x + offset.x, center.y + offset.y, depth))));
                    colors.Add(new Color(1, 1, 1, opacity * Mathf.Lerp(.62f, 1, nearSide) * RingOpacity[band]));
                }
            }
            for (int segment = 0; segment < RingSegments; segment++)
            for (int band = 0; band < RingProfile.Length - 1; band++)
            {
                int a = first + segment * RingProfile.Length + band;
                int b = a + RingProfile.Length;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }
        }

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
                float distance = flight * (.55f + seed * .75f) / (1 + flight * 1.4f) * Mathf.Min(1, aspect);
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

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
