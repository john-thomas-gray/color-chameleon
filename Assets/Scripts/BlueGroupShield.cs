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
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
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
            for (int i = 0; i < edges.Count; i++)
            {
                if (i == lines.Count)
                {
                    var child = new GameObject("Rigid Blue perimeter", typeof(LineRenderer));
                    child.transform.SetParent(transform, false);
                    var line = child.GetComponent<LineRenderer>();
                    line.sharedMaterial = grid.View(edges[i].Owner).Visuals.Body.sharedMaterial;
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.startWidth = line.endWidth = .035f;
                    line.startColor = line.endColor = new Color(.65f, .93f, 1);
                    line.sortingOrder = 14;
                    lines.Add(line);
                }
                lines[i].enabled = true;
                lines[i].SetPosition(0, edges[i].A);
                lines[i].SetPosition(1, edges[i].B);
            }
            for (int i = edges.Count; i < lines.Count; i++) lines[i].enabled = false;
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
