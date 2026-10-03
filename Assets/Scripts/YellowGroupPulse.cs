using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class YellowGroupPulse : MonoBehaviour
    {
        private struct Edge
        {
            public Vector3 A, B;
        }

        private sealed class Pulse
        {
            public readonly HashSet<int> Ids;
            public readonly GameObject Root;
            public readonly Mesh Mesh;
            public readonly MeshRenderer Renderer;
            public readonly float ArrivalSeconds;
            public readonly float FadeSeconds;
            public float Age;
            public int EdgeCount;
            public float MaxAlpha;

            public Pulse(HashSet<int> ids, GameObject root, Mesh mesh, MeshRenderer renderer,
                float arrivalSeconds, float fadeSeconds)
            {
                Ids = ids;
                Root = root;
                Mesh = mesh;
                Renderer = renderer;
                ArrivalSeconds = Mathf.Max(.01f, arrivalSeconds);
                FadeSeconds = Mathf.Max(.01f, fadeSeconds);
            }

            public float Duration => ArrivalSeconds + FadeSeconds;
        }

        private readonly List<Pulse> pulses = new List<Pulse>();
        private readonly List<Edge> edges = new List<Edge>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        public int ActivePulseCount => pulses.Count;
        public int EdgeCount { get; private set; }
        public float MaxAlpha { get; private set; }

        public static YellowGroupPulse Ensure(EnemyGrid grid)
        {
            var pulse = grid.GetComponent<YellowGroupPulse>();
            return pulse != null ? pulse : grid.gameObject.AddComponent<YellowGroupPulse>();
        }

        public void Begin(EnemyGrid grid, IEnumerable<int> groupIds, float arrivalSeconds, float fadeSeconds)
        {
            var ids = new HashSet<int>();
            SpriteRenderer source = null;
            foreach (int id in groupIds)
            {
                var enemy = grid.View(id);
                if (enemy == null) continue;
                ids.Add(id);
                if (source == null) source = enemy.Visuals.Body;
            }
            if (ids.Count == 0 || source == null) return;

            var root = new GameObject("Yellow group energy pulse", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(grid.transform, false);
            var mesh = new Mesh { name = "Yellow group energy pulse" };
            mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingOrder = source.sortingOrder + 8;

            var pulse = new Pulse(ids, root, mesh, renderer, arrivalSeconds, fadeSeconds);
            pulses.Add(pulse);
            Present(grid, pulse);
            RefreshDebugState();
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float seconds)
        {
            var grid = GetComponent<EnemyGrid>();
            if (grid == null) return;
            seconds = Mathf.Max(0, seconds);
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                var pulse = pulses[i];
                pulse.Age += seconds;
                Present(grid, pulse);
                if (pulse.Age >= pulse.Duration || pulse.EdgeCount == 0)
                {
                    Dispose(pulse);
                    pulses.RemoveAt(i);
                }
            }
            RefreshDebugState();
        }

        private void Present(EnemyGrid grid, Pulse pulse)
        {
            RebuildEdges(grid, pulse.Ids);
            pulse.EdgeCount = edges.Count;
            if (edges.Count == 0)
            {
                pulse.Renderer.enabled = false;
                pulse.MaxAlpha = 0;
                return;
            }

            float arrival = Mathf.SmoothStep(0, 1, Mathf.Clamp01(pulse.Age / pulse.ArrivalSeconds));
            float fadeProgress = Mathf.Clamp01((pulse.Age - pulse.ArrivalSeconds) / pulse.FadeSeconds);
            float alpha = pulse.Age <= pulse.ArrivalSeconds ? arrival : 1 - Mathf.SmoothStep(0, 1, fadeProgress);
            pulse.MaxAlpha = alpha;
            pulse.Renderer.enabled = alpha > .001f;

            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            Color yellow = EnemyPalette.Get(EnemyColor.Yellow);
            Color core = Color.Lerp(yellow, Color.white, .35f);
            float outside = Mathf.Lerp(.32f, .055f, arrival);
            float inside = Mathf.Lerp(.10f, .22f, arrival);
            foreach (var edge in edges)
            {
                var direction = (edge.B - edge.A).normalized;
                var inward = new Vector3(direction.y, -direction.x, 0);
                int start = vertices.Count;
                for (int strip = 0; strip <= 8; strip++)
                {
                    float t = strip / 8f;
                    var offset = inward * Mathf.Lerp(-outside, inside, t);
                    vertices.Add(edge.A - direction * .02f + offset);
                    vertices.Add(edge.B + direction * .02f + offset);
                    float rim = Mathf.Pow(1 - Mathf.Abs(t - .22f) / .78f, 2);
                    float glow = 1 - Mathf.SmoothStep(.15f, 1, t);
                    var tint = Color.Lerp(yellow, core, Mathf.Clamp01(rim));
                    tint.a = alpha * Mathf.Clamp01(.12f + glow * .55f + rim * .45f);
                    colors.Add(tint);
                    colors.Add(tint);
                    if (strip == 8) continue;
                    int v = start + strip * 2;
                    triangles.Add(v);
                    triangles.Add(v + 1);
                    triangles.Add(v + 2);
                    triangles.Add(v + 1);
                    triangles.Add(v + 3);
                    triangles.Add(v + 2);
                }
            }

            pulse.Mesh.Clear();
            pulse.Mesh.SetVertices(vertices);
            pulse.Mesh.SetColors(colors);
            pulse.Mesh.SetTriangles(triangles, 0);
            pulse.Mesh.RecalculateBounds();
        }

        private void RebuildEdges(EnemyGrid grid, HashSet<int> ids)
        {
            edges.Clear();
            foreach (int id in ids)
            {
                var enemy = grid.View(id);
                if (enemy == null) continue;
                int column = enemy.Column;
                int row = enemy.Row;
                Vector3 center = grid.CellPosition(column, row);
                float half = grid.Spacing * .5f;
                var tl = center + new Vector3(-half, half, 0);
                var tr = center + new Vector3(half, half, 0);
                var bl = center + new Vector3(-half, -half, 0);
                var br = center + new Vector3(half, -half, 0);
                bool InPulse(int x, int y)
                {
                    var occupant = grid.Model.At(x, y);
                    return occupant != null && ids.Contains(occupant.Id);
                }
                if (!InPulse(column, row - 1)) edges.Add(new Edge { A = tl, B = tr });
                if (!InPulse(column + 1, row)) edges.Add(new Edge { A = tr, B = br });
                if (!InPulse(column, row + 1)) edges.Add(new Edge { A = br, B = bl });
                if (!InPulse(column - 1, row)) edges.Add(new Edge { A = bl, B = tl });
            }
        }

        private void RefreshDebugState()
        {
            EdgeCount = 0;
            MaxAlpha = 0;
            foreach (var pulse in pulses)
            {
                EdgeCount += pulse.EdgeCount;
                MaxAlpha = Mathf.Max(MaxAlpha, pulse.MaxAlpha);
            }
        }

        private static void Dispose(Pulse pulse)
        {
            if (pulse.Mesh != null)
            {
                if (Application.isPlaying) Destroy(pulse.Mesh);
                else DestroyImmediate(pulse.Mesh);
            }
            if (pulse.Root != null)
            {
                if (Application.isPlaying) Destroy(pulse.Root);
                else DestroyImmediate(pulse.Root);
            }
        }

        private void OnDestroy()
        {
            foreach (var pulse in pulses) Dispose(pulse);
            pulses.Clear();
        }
    }
}
