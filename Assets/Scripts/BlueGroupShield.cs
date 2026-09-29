using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class BlueGroupShield : MonoBehaviour
    {
        private struct Edge
        {
            public Vector3 A, B;
            public int Owner;
        }
        private readonly List<Edge> edges = new List<Edge>();
        private Mesh band;
        private MeshRenderer bandRenderer;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private readonly HashSet<int> protectedIds = new HashSet<int>();
        public int EdgeCount => edges.Count;
        public bool Protects(int id) => protectedIds.Contains(id);

        public void Refresh(EnemyGrid grid)
        {
            edges.Clear();
            protectedIds.Clear();
            var visited = new HashSet<int>();
            for (int row = 0; row < GridModel.Rows; row++)
            for (int column = 0; column < GridModel.Columns; column++)
            {
                var cell = grid.Model.At(column, row);
                if (cell == null || cell.Color != EnemyColor.Blue || !visited.Add(cell.Id)) continue;
                var group = grid.Model.ColorGroup(cell.Id);
                int source = 0;
                foreach (var member in group)
                {
                    visited.Add(member.Id);
                    var enemy = grid.View(member.Id);
                    var ability = enemy.GetComponent<EnemyAbilities>();
                    if (enemy.IsSpecial && ability != null && ability.isActiveAndEnabled && ability.ShieldActive) source = member.Id;
                }
                if (source == 0) continue;
                foreach (var member in group)
                {
                    protectedIds.Add(member.Id);
                    var center = grid.CellPosition(member.Column, member.Row);
                    float half = grid.Spacing / 2;
                    var tl = center + new Vector3(-half, half, 0);
                    var tr = center + new Vector3(half, half, 0);
                    var bl = center + new Vector3(-half, -half, 0);
                    var br = center + new Vector3(half, -half, 0);
                    bool BlueAt(int x, int y) => grid.Model.At(x, y)?.Color == EnemyColor.Blue;
                    if (!BlueAt(member.Column, member.Row - 1)) AddEdge(tl, tr, source);
                    if (!BlueAt(member.Column + 1, member.Row)) AddEdge(tr, br, source);
                    if (!BlueAt(member.Column, member.Row + 1)) AddEdge(br, bl, source);
                    if (!BlueAt(member.Column - 1, member.Row)) AddEdge(bl, tl, source);
                }
            }
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                enemy.GetComponent<EnemyAbilities>()?.SetGroupShielded(Protects(enemy.Id));
            if (band == null && edges.Count > 0)
            {
                var child = new GameObject("Rigid Blue perimeter", typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(transform, false);
                band = new Mesh { name = "Inward-fading Blue perimeter" };
                band.MarkDynamic();
                child.GetComponent<MeshFilter>().sharedMesh = band;
                bandRenderer = child.GetComponent<MeshRenderer>();
                bandRenderer.sharedMaterial = grid.View(edges[0].Owner).Visuals.Body.sharedMaterial;
                bandRenderer.sortingOrder = 14;
            }
            if (band == null) return;
            vertices.Clear(); colors.Clear(); triangles.Clear();
            foreach (var edge in edges)
            {
                var direction = (edge.B - edge.A).normalized;
                var inward = new Vector3(direction.y, -direction.x, 0);
                int start = vertices.Count;
                for (int strip = 0; strip <= 8; strip++)
                {
                    float t = strip / 8f;
                    var offset = inward * Mathf.Lerp(-.0175f, .21f, t);
                    vertices.Add(edge.A - direction * .0175f + offset);
                    vertices.Add(edge.B + direction * .0175f + offset);
                    var tint = new Color(.82f, 1, 1, 1 - Mathf.SmoothStep(0, 1, t));
                    colors.Add(tint); colors.Add(tint);
                    if (strip == 8) continue;
                    int v = start + strip * 2;
                    triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                    triangles.Add(v + 1); triangles.Add(v + 3); triangles.Add(v + 2);
                }
            }
            band.Clear();
            band.SetVertices(vertices); band.SetColors(colors); band.SetTriangles(triangles, 0);
            band.RecalculateBounds();
            bandRenderer.enabled = edges.Count > 0;
        }

        private void OnDestroy()
        {
            if (band == null) return;
            if (Application.isPlaying) Destroy(band); else DestroyImmediate(band);
        }

        private void AddEdge(Vector3 a, Vector3 b, int owner) => edges.Add(new Edge { A = a, B = b, Owner = owner });

        public bool FindHit(Vector3 origin, float from, float to, float radius, out int id, out float distance)
        {
            id = 0;
            distance = float.PositiveInfinity;
            foreach (var edge in edges)
            {
                Vector3 a = transform.TransformPoint(edge.A), b = transform.TransformPoint(edge.B);
                if (origin.x + radius < Mathf.Min(a.x, b.x) - .0175f || origin.x - radius > Mathf.Max(a.x, b.x) + .0175f) continue;
                float near = Mathf.Min(a.y, b.y) - .0175f - origin.y;
                float far = Mathf.Max(a.y, b.y) + .0175f - origin.y;
                if (far < from || near > to) continue;
                float contact = Mathf.Max(from, near);
                if (contact >= distance) continue;
                id = edge.Owner;
                distance = contact;
            }
            return id != 0;
        }
    }
}
