using UnityEngine;

namespace CandyCruisers
{
    public sealed class GridEnemy : MonoBehaviour
    {
        [SerializeField] private EnemyColor color;
        [SerializeField] private int column;
        [SerializeField] private int row;
        private EnemyGrid owner;
        public int Id => GetInstanceID();
        public EnemyColor Color => color;
        public int Column => column;
        public int Row => row;

        public void Configure(EnemyColor newColor, int newColumn, int newRow)
        {
            if (owner != null) throw new System.InvalidOperationException("Use EnemyGrid to modify registered enemies.");
            color = newColor; column = newColumn; row = newRow;
        }

        internal void Bind(EnemyGrid grid) => owner = grid;
        internal void SetCell(int newColumn, int newRow) { column = newColumn; row = newRow; }
        internal void SetColor(EnemyColor newColor)
        {
            color = newColor;
            GetComponent<SpriteRenderer>().color = EnemyPalette.Get(color);
        }
        private void OnEnable()
        {
            var grid = GetComponentInParent<EnemyGrid>();
            if (grid != null) grid.Register(this);
        }
        private void OnDisable()
        {
            if (owner != null) owner.Unregister(this);
        }
    }

    public static class EnemyPalette
    {
        public static Color Get(EnemyColor color)
        {
            switch (color)
            {
                case EnemyColor.Red: return new Color(1f, 0.26f, 0.33f);
                case EnemyColor.Blue: return new Color(0.22f, 0.64f, 1f);
                case EnemyColor.Green: return new Color(0.35f, 0.9f, 0.45f);
                case EnemyColor.Purple: return new Color(0.75f, 0.4f, 1f);
                default: return new Color(1f, 0.85f, 0.25f);
            }
        }
    }
}
