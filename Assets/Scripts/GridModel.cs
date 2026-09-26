using System;
using System.Collections.Generic;

namespace CandyCruisers
{
    public enum EnemyColor { Red, Blue, Green, Purple, Yellow }

    public sealed class GridModel
    {
        public const int Columns = 5;
        public const int Rows = 10;
        public sealed class Occupant
        {
            public int Id { get; }
            public EnemyColor Color { get; internal set; }
            public int Column { get; internal set; }
            public int Row { get; internal set; }
            internal Occupant(int id, EnemyColor color, int column, int row)
            { Id = id; Color = color; Column = column; Row = row; }
        }

        private readonly Occupant[,] cells = new Occupant[Columns, Rows];
        private readonly Dictionary<int, Occupant> enemies = new Dictionary<int, Occupant>();
        public int Count => enemies.Count;
        public static bool InBounds(int column, int row) => column >= 0 && column < Columns && row >= 0 && row < Rows;
        public Occupant At(int column, int row) => InBounds(column, row) ? cells[column, row] : null;

        public bool OccupiedColumns(out int first, out int last)
        {
            first = Columns;
            last = -1;
            foreach (var enemy in enemies.Values)
            {
                first = Math.Min(first, enemy.Column);
                last = Math.Max(last, enemy.Column);
            }
            return last >= 0;
        }

        public bool TryShiftColumns(int offset)
        {
            foreach (var enemy in enemies.Values)
                if (!InBounds(enemy.Column + offset, enemy.Row)) return false;
            Array.Clear(cells, 0, cells.Length);
            foreach (var enemy in enemies.Values)
            {
                enemy.Column += offset;
                cells[enemy.Column, enemy.Row] = enemy;
            }
            return true;
        }

        public bool TryAdd(int id, EnemyColor color, int column, int row)
        {
            if (!InBounds(column, row) || cells[column, row] != null || enemies.ContainsKey(id) || !Enum.IsDefined(typeof(EnemyColor), color)) return false;
            var enemy = new Occupant(id, color, column, row);
            enemies.Add(id, enemy);
            cells[column, row] = enemy;
            return true;
        }

        public bool TryMove(int id, int column, int row)
        {
            if (!enemies.TryGetValue(id, out var enemy) || !InBounds(column, row)) return false;
            if (cells[column, row] == enemy) return true;
            if (cells[column, row] != null) return false;
            cells[enemy.Column, enemy.Row] = null;
            cells[column, row] = enemy;
            enemy.Column = column;
            enemy.Row = row;
            return true;
        }

        public bool Remove(int id)
        {
            if (!enemies.TryGetValue(id, out var enemy)) return false;
            cells[enemy.Column, enemy.Row] = null;
            return enemies.Remove(id);
        }

        public bool CanDescend
        {
            get
            {
                for (int column = 0; column < Columns; column++)
                    if (cells[column, Rows - 1] != null) return false;
                return true;
            }
        }

        public List<Occupant> RetreatStep()
        {
            var anchored = new HashSet<int>();
            var pending = new Queue<Occupant>();
            for (int column = 0; column < Columns; column++)
            {
                var enemy = cells[column, 0];
                if (enemy != null) { anchored.Add(enemy.Id); pending.Enqueue(enemy); }
            }
            while (pending.Count > 0)
            {
                var enemy = pending.Dequeue();
                foreach (var neighbor in Neighbors(enemy.Column, enemy.Row))
                    if (anchored.Add(neighbor.Id)) pending.Enqueue(neighbor);
            }

            // Use one connectivity snapshot so disconnected groups keep their shape.
            // Top-to-bottom movement vacates each destination before the next member moves.
            var moved = new List<Occupant>();
            for (int row = 1; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                var enemy = cells[column, row];
                if (enemy == null || anchored.Contains(enemy.Id)) continue;
                cells[column, row - 1] = enemy;
                cells[column, row] = null;
                enemy.Row = row - 1;
                moved.Add(enemy);
            }
            return moved;
        }

        public bool TryDescend()
        {
            if (!CanDescend) return false;
            for (int row = Rows - 2; row >= 0; row--)
            for (int column = 0; column < Columns; column++)
            {
                var enemy = cells[column, row];
                cells[column, row + 1] = enemy;
                cells[column, row] = null;
                if (enemy != null) enemy.Row = row + 1;
            }
            return true;
        }

        public List<int> ClearMatchingChain(int id, EnemyColor shotColor)
        {
            var cleared = new List<int>();
            if (!enemies.TryGetValue(id, out var first) || first.Color != shotColor) return cleared;
            var visited = new HashSet<int> { id };
            var pending = new Queue<Occupant>();
            pending.Enqueue(first);
            while (pending.Count > 0)
            {
                var enemy = pending.Dequeue();
                cleared.Add(enemy.Id);
                foreach (var neighbor in Neighbors(enemy.Column, enemy.Row))
                    if (neighbor.Color == shotColor && visited.Add(neighbor.Id)) pending.Enqueue(neighbor);
            }
            // Discover the whole group before removal changes its connectivity.
            foreach (int clearedId in cleared) Remove(clearedId);
            return cleared;
        }

        public bool SetColor(int id, EnemyColor color)
        {
            if (!enemies.TryGetValue(id, out var enemy) || !Enum.IsDefined(typeof(EnemyColor), color)) return false;
            enemy.Color = color;
            return true;
        }

        public int ColorCount(EnemyColor color)
        {
            int count = 0;
            foreach (var enemy in enemies.Values) if (enemy.Color == color) count++;
            return count;
        }

        public List<EnemyColor> AvailableColors()
        {
            var result = new List<EnemyColor>();
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                if (ColorCount(color) > 0) result.Add(color);
            return result;
        }

        public IEnumerable<Occupant> Neighbors(int column, int row)
        {
            if (!InBounds(column, row)) yield break;
            var left = At(column - 1, row); if (left != null) yield return left;
            var right = At(column + 1, row); if (right != null) yield return right;
            var above = At(column, row - 1); if (above != null) yield return above;
            var below = At(column, row + 1); if (below != null) yield return below;
        }
    }
}
