using UnityEngine;

namespace CandyCruisers
{
    [RequireComponent(typeof(EnemyGrid), typeof(EnemyGridMovement))]
    public sealed class EnemyRowSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject bluePrefab;
        [SerializeField] private GameObject redPrefab;
        [SerializeField] private GameObject greenPrefab;
        [SerializeField] private GameObject purplePrefab;
        [SerializeField] private GameObject yellowPrefab;
        private EnemyGrid grid;
        private EnemyGridMovement movement;
        public event System.Action BottomReached;

        public void Configure(GameObject blue, GameObject red)
        { bluePrefab = blue; redPrefab = red; }
        public void ConfigureNewTypes(GameObject green, GameObject purple, GameObject yellow)
        { greenPrefab = green; purplePrefab = purple; yellowPrefab = yellow; }
        public GameObject Prefab(EnemyColor color) => color == EnemyColor.Blue ? bluePrefab :
            color == EnemyColor.Red ? redPrefab : color == EnemyColor.Green ? greenPrefab :
            color == EnemyColor.Purple ? purplePrefab : yellowPrefab;
        public int Level => GetComponent<GameSession>()?.Progress.Level ?? 1;
        public void ApplyAppearance(GridEnemy enemy)
        {
            var prefab = Prefab(enemy.Color);
            if (prefab == null) return;
            enemy.GetComponent<SpriteRenderer>().sprite = prefab.GetComponent<SpriteRenderer>().sprite;
            enemy.transform.localScale = prefab.transform.localScale;
        }

        public System.Collections.Generic.List<EnemyColor> UnlockedColors()
        {
            var eligible = new System.Collections.Generic.List<EnemyColor>();
            foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
            {
                bool allowed = RunProgress.IsUnlocked(color, Level);
#if UNITY_EDITOR
                if (SpawnOverride.Enabled) allowed = SpawnOverride.Allows(color);
#endif
                if (allowed) eligible.Add(color);
            }
            return eligible;
        }

        private System.Collections.Generic.List<EnemyColor> SpawnColors(bool newCycle = false)
        {
            if (grid == null) grid = GetComponent<EnemyGrid>();
            var colors = UnlockedColors();
            colors.RemoveAll(color => (!newCycle && grid.IsColorCleared(color)) || Prefab(color) == null ||
                Prefab(color).GetComponent<SpriteRenderer>() == null);
            return colors;
        }

        public GridEnemy TrySummon(GridEnemy source)
        {
            if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled) return null;
            if (grid == null) grid = GetComponent<EnemyGrid>();
            if (grid.View(source.Id) != source) return null;
            var session = GetComponent<GameSession>();
            if (session != null && session.State != GameSession.RunState.Playing) return null;
            var colors = SpawnColors();
            if (colors.Count == 0) return null;
            var candidates = SummonCandidates(grid.Model);
            // Match the original two-stage random choice: anchor, then neighboring gap.
            var anchors = new System.Collections.Generic.List<int>(candidates.Keys);
            if (anchors.Count == 0) return null;
            var gaps = candidates[anchors[Random.Range(0, anchors.Count)]];
            var cell = gaps[Random.Range(0, gaps.Count)];
            return Spawn(colors[Random.Range(0, colors.Count)], cell.x, cell.y);
        }

        public System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Vector2Int>> SummonCandidates(GridModel model)
        {
            var candidates = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<Vector2Int>>();
            var offsets = new[] { Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up };
            int farthest = -1;
            for (int row = 0; row < GridModel.Rows; row++)
            for (int column = 0; column < GridModel.Columns; column++)
            {
                if (model.At(column, row) == null) continue;
                var gaps = new System.Collections.Generic.List<Vector2Int>();
                foreach (var offset in offsets)
                {
                    var cell = new Vector2Int(column, row) + offset;
                    if (GridModel.InBounds(cell.x, cell.y) && model.At(cell.x, cell.y) == null) gaps.Add(cell);
                }
                if (gaps.Count == 0) continue;
                farthest = row * GridModel.Columns + column;
                candidates.Add(farthest, gaps);
            }
            foreach (int anchor in new System.Collections.Generic.List<int>(candidates.Keys))
            {
                int row = anchor / GridModel.Columns, column = anchor % GridModel.Columns;
                candidates[anchor].RemoveAll(cell =>
                    (row == farthest / GridModel.Columns && (cell.y > row || anchor == farthest && cell.x > column)) ||
                    Mathf.Abs(transform.TransformPoint(GetComponent<EnemyGrid>().CellPosition(cell.x, cell.y)).x) + .375f > PlayerMovement.HalfWidth + .00001f);
                if (candidates[anchor].Count == 0) candidates.Remove(anchor);
            }
            return candidates;
        }

        private void OnEnable()
        {
            grid = GetComponent<EnemyGrid>();
            movement = GetComponent<EnemyGridMovement>();
            movement.SweepEnded += OnSweepEnded;
        }
        private void OnDisable()
        {
            if (movement != null) movement.SweepEnded -= OnSweepEnded;
        }
        private void OnSweepEnded() => TryAdvance();

        public bool TryAdvance()
        {
            var session = GetComponent<GameSession>();
            if (session != null && session.State != GameSession.RunState.Playing) return false;
            if (grid == null) grid = GetComponent<EnemyGrid>();
            var colors = SpawnColors();
            if (colors.Count == 0 && SpawnColors(true).Count == 0)
            {
                Debug.LogWarning("No configured enemy prefabs match the allowed spawn types.", this);
                return false;
            }
            if (!grid.Model.CanDescend) { BottomReached?.Invoke(); return false; }
            if (!grid.AlignForNewRow())
            {
                Debug.LogError("Cannot fit the new row within the playfield.", this);
                return false;
            }
            if (!grid.TryDescend()) return false;
            if (colors.Count > 0) SpawnRow(0, colors);
            return true;
        }

        public bool SpawnBatch(int rows)
        {
            return SpawnBatch(PlanBatch(rows));
        }

        public EnemyColor[] PlanOpening()
        {
            var colors = SpawnColors(true);
            if (colors.Count != 2 || !colors.Contains(EnemyColor.Red) || !colors.Contains(EnemyColor.Blue))
                return PlanBatch(2);
            var plan = new EnemyColor[2 * GridModel.Columns];
            for (int i = 0; i < plan.Length; i++)
                plan[i] = ((i % GridModel.Columns < (GridModel.Columns + 1) / 2) == (i / GridModel.Columns == 0))
                    ? EnemyColor.Blue : EnemyColor.Red;
            return plan;
        }

        public EnemyColor[] PlanBatch(int rows)
        {
            var colors = SpawnColors(true);
            if (rows < 1 || rows > GridModel.Rows || colors.Count == 0) return null;
            var plan = new EnemyColor[rows * GridModel.Columns];
            for (int row = 0; row < rows; row++)
                System.Array.Copy(PlanRow(colors), 0, plan, row * GridModel.Columns, GridModel.Columns);
            return plan;
        }

        private static EnemyColor[] PlanRow(System.Collections.Generic.List<EnemyColor> eligible)
        {
            var colors = new System.Collections.Generic.List<EnemyColor>(eligible);
            int groups = Mathf.Min(3, colors.Count, GridModel.Columns);
            var sizes = new int[groups];
            // Sample distinct colors without replacement, then allocate contiguous runs.
            for (int i = 0; i < groups; i++)
            {
                int selected = Random.Range(i, colors.Count);
                var swap = colors[i]; colors[i] = colors[selected]; colors[selected] = swap;
                sizes[i] = 1;
            }
            for (int i = groups; i < GridModel.Columns; i++) sizes[Random.Range(0, groups)]++;
            var row = new EnemyColor[GridModel.Columns];
            int column = 0;
            for (int group = 0; group < groups; group++)
                for (int i = 0; i < sizes[group]; i++) row[column++] = colors[group];
            return row;
        }

        public bool SpawnBatch(EnemyColor[] plan)
        {
            if (grid == null) grid = GetComponent<EnemyGrid>();
            if (grid.Model.Count != 0 || plan == null || plan.Length == 0 || plan.Length > GridModel.Rows * GridModel.Columns ||
                plan.Length % GridModel.Columns != 0) return false;
            foreach (var color in plan)
                if (!System.Enum.IsDefined(typeof(EnemyColor), color) || Prefab(color) == null || Prefab(color).GetComponent<SpriteRenderer>() == null) return false;
            grid.BeginColorCycle();
            for (int i = 0; i < plan.Length; i++) Spawn(plan[i], i % GridModel.Columns, i / GridModel.Columns);
            return true;
        }

        private void SpawnRow(int row, System.Collections.Generic.List<EnemyColor> colors)
        {
            var plan = PlanRow(colors);
            for (int column = 0; column < GridModel.Columns; column++)
                Spawn(plan[column], column, row);
        }

        private GridEnemy Spawn(EnemyColor color, int column, int row)
        {
                // Configure while inactive and outside the grid, before OnEnable can register it.
                var instance = Instantiate(Prefab(color));
                instance.SetActive(false);
                var enemy = instance.GetComponent<GridEnemy>();
                if (enemy == null) enemy = instance.AddComponent<GridEnemy>();
                enemy.Configure(color, column, row);
                instance.name = color + " Enemy";
                instance.transform.SetParent(transform, false);
                instance.transform.localPosition = grid.CellPosition(column, row);
                instance.SetActive(true);
                grid.Register(enemy);
                instance.GetComponent<EnemyAbilities>()?.BeginSpawnEffect();
                return enemy;
        }
    }
}
