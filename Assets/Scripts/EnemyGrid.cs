using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public sealed class EnemyGrid : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float spacing = 0.75f;
        [SerializeField, Min(0.01f)] private float retreatStepSeconds = 0.15f;
        private double retreatElapsed;
        [SerializeField] private GameObject missilePrefab;
        [SerializeField] private Sprite shieldSprite;
        public void ConfigureAbilities(GameObject missile, Sprite shield)
        { missilePrefab = missile; shieldSprite = shield; }
        public GridModel Model { get; } = new GridModel();
        private readonly Dictionary<int, GridEnemy> views = new Dictionary<int, GridEnemy>();
        private readonly HashSet<EnemyColor> clearedColors = new HashSet<EnemyColor>();
        private readonly HashSet<EnemyColor> seenColors = new HashSet<EnemyColor>();
        public List<EnemyColor> SeenColors()
        {
            var colors = new List<EnemyColor>(seenColors);
            colors.Sort();
            return colors;
        }
        public bool IsColorCleared(EnemyColor color) => clearedColors.Contains(color);
        public void BeginColorCycle()
        {
            clearedColors.Clear();
            seenColors.Clear();
        }
        public event System.Action FleetCleared;
        public event System.Action<EnemyColor> ColorCleared;
        public event System.Action<int, bool> MatchCleared;
        public GridEnemy View(int id) => views.TryGetValue(id, out var enemy) ? enemy : null;
        private void Awake() => RegisterChildren();
        private void Update() => TickRetreat(Time.deltaTime);

        public void TickRetreat(float seconds)
        {
            retreatElapsed += Mathf.Max(0, seconds);
            double interval = Mathf.Max(0.01f, retreatStepSeconds);
            while (retreatElapsed >= interval)
            {
                retreatElapsed -= interval;
                var moved = Model.RetreatStep();
                foreach (var cell in moved)
                {
                    if (!views.TryGetValue(cell.Id, out var enemy)) continue;
                    enemy.SetCell(cell.Column, cell.Row);
                    enemy.transform.localPosition = CellPosition(cell.Column, cell.Row);
                }
                if (moved.Count == 0)
                {
                    retreatElapsed %= interval;
                    break;
                }
            }
        }
        public void RegisterChildren()
        {
            foreach (var enemy in GetComponentsInChildren<GridEnemy>())
                if (enemy.isActiveAndEnabled) Register(enemy);
        }
        public Vector3 CellPosition(int column, int row) => new Vector3((column - (GridModel.Columns - 1) * .5f) * spacing, -row * spacing, 0);

        public bool OccupiedHorizontalBounds(out float left, out float right)
        {
            left = right = 0;
            if (!Model.OccupiedColumns(out int first, out int last)) return false;
            left = transform.TransformPoint(CellPosition(first, 0) + Vector3.left * spacing * 0.5f).x;
            right = transform.TransformPoint(CellPosition(last, 0) + Vector3.right * spacing * 0.5f).x;
            return true;
        }

        public bool AlignForNewRow()
        {
            float worldSpacing = transform.TransformVector(Vector3.right * spacing).x;
            float fullGridLimit = PlayerMovement.HalfWidth - GridModel.Columns * worldSpacing * 0.5f;
            if (worldSpacing <= 0 || fullGridLimit < 0) return false;
            float x = transform.position.x;
            float excess = x - Mathf.Clamp(x, -fullGridLimit, fullGridLimit);
            int shift = Mathf.Abs(excess) <= 0.00001f ? 0 :
                (int)Mathf.Sign(excess) * Mathf.CeilToInt((Mathf.Abs(excess) - 0.00001f) / worldSpacing);
            if (shift == 0) return true;
            if (!Model.TryShiftColumns(shift)) return false;
            // Opposite origin/column shifts preserve every survivor's world position.
            transform.position -= Vector3.right * shift * worldSpacing;
            foreach (var enemy in views.Values)
            {
                enemy.SetCell(enemy.Column + shift, enemy.Row);
                enemy.transform.localPosition = CellPosition(enemy.Column, enemy.Row);
            }
            return true;
        }
        public bool TryDescend()
        {
            if (!Model.TryDescend()) return false;
            foreach (var enemy in views.Values)
            {
                enemy.SetCell(enemy.Column, enemy.Row + 1);
                enemy.transform.localPosition = CellPosition(enemy.Column, enemy.Row);
            }
            return true;
        }
        public bool Register(GridEnemy enemy)
        {
            if (views.ContainsKey(enemy.Id)) return true;
            if (!Model.TryAdd(enemy.Id, enemy.Color, enemy.Column, enemy.Row))
            {
                Debug.LogError("Invalid or occupied enemy cell: " + enemy.name, enemy);
                return false;
            }
            views.Add(enemy.Id, enemy);
            seenColors.Add(enemy.Color);
            enemy.Bind(this);
            enemy.transform.localPosition = CellPosition(enemy.Column, enemy.Row);
            if (missilePrefab != null && shieldSprite != null)
            {
                var abilities = enemy.GetComponent<EnemyAbilities>();
                if (abilities == null) abilities = enemy.gameObject.AddComponent<EnemyAbilities>();
                abilities.Configure(enemy, missilePrefab, shieldSprite);
            }
            return true;
        }
        public void Unregister(GridEnemy enemy)
        {
            Model.Remove(enemy.Id);
            views.Remove(enemy.Id);
            enemy.Bind(null);
        }
        public bool TryMove(int id, int column, int row)
        {
            if (!views.TryGetValue(id, out var enemy) || !Model.TryMove(id, column, row)) return false;
            enemy.SetCell(column, row);
            enemy.transform.localPosition = CellPosition(column, row);
            return true;
        }
        public bool SetColor(int id, EnemyColor color)
        {
            if (!views.TryGetValue(id, out var enemy) || !Model.SetColor(id, color)) return false;
            enemy.SetColor(color);
            seenColors.Add(color);
            var spawner = GetComponent<EnemyRowSpawner>();
            if (spawner != null) spawner.ApplyAppearance(enemy);
            return true;
        }

        public int ClearMatchingChain(int id, EnemyColor color)
        {
            var cleared = Model.ClearMatchingChain(id, color);
            foreach (int clearedId in cleared)
            {
                if (!views.TryGetValue(clearedId, out var enemy)) continue;
                views.Remove(clearedId);
                enemy.Bind(null);
                enemy.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(enemy.gameObject);
                else DestroyImmediate(enemy.gameObject);
            }
            if (cleared.Count > 0)
            {
                if (Model.ColorCount(color) == 0)
                {
                    clearedColors.Add(color);
                    ColorCleared?.Invoke(color);
                }
                MatchCleared?.Invoke(cleared.Count, Model.Count == 0);
                if (Model.Count == 0) FleetCleared?.Invoke();
            }
            return cleared.Count;
        }

        public bool FindMatchingHit(Vector3 origin, float fromLength, float toLength,
            EnemyColor color, float radius, out int id, out float hitLength, bool magic = false)
        {
            id = 0;
            hitLength = float.PositiveInfinity;
            bool found = false;
            foreach (var pair in views)
            {
                var enemy = pair.Value;
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var abilities = enemy.GetComponent<EnemyAbilities>();
                bool blocked = !magic && enemy.Color == EnemyColor.Blue && color != EnemyColor.Blue &&
                    abilities != null && abilities.isActiveAndEnabled && abilities.ShieldActive;
                if (!magic && enemy.Color != color && !blocked) continue;
                var bounds = blocked ? abilities.ShieldBounds : enemy.GetComponent<SpriteRenderer>().bounds;
                if (origin.x + radius < bounds.min.x || origin.x - radius > bounds.max.x) continue;
                float near = bounds.min.y - origin.y;
                float far = bounds.max.y - origin.y;
                if (far < fromLength || near > toLength) continue;
                float contact = Mathf.Max(fromLength, near);
                if (contact >= hitLength) continue;
                found = true;
                id = pair.Key;
                hitLength = contact;
            }
            return found;
        }

        public void ResolveTongueHit(int id, EnemyColor color, bool magic = false)
        {
            if (!views.TryGetValue(id, out var enemy)) return;
            var abilities = enemy.GetComponent<EnemyAbilities>();
            if (!magic && abilities != null && abilities.isActiveAndEnabled && abilities.Absorb(color)) return;
            ClearMatchingChain(id, magic ? enemy.Color : color);
        }
    }
}
