using UnityEngine;

namespace CandyCruisers
{
    public sealed class GridEnemy : MonoBehaviour
    {
        [SerializeField] private EnemyColor color;
        [SerializeField] private int column;
        [SerializeField] private int row;
        private EnemyGrid owner;
        private CharacterVisuals visuals;
        public CharacterVisuals Visuals => visuals != null ? visuals : visuals = CharacterVisuals.Ensure(gameObject);
        public Bounds HitBounds => Visuals.HitBounds;
        public int Id => GetInstanceID();
        public EnemyColor Color => color;
        public int Column => column;
        public int Row => row;
        public bool IsSpecial { get; private set; }
        public int Tier => IsSpecial ? 2 : 1;
        internal void Promote() { if (color != EnemyColor.Orange) IsSpecial = true; }

        public void Configure(EnemyColor newColor, int newColumn, int newRow)
        {
            if (owner != null) throw new System.InvalidOperationException("Use EnemyGrid to modify registered enemies.");
            color = newColor; column = newColumn; row = newRow;
            IsSpecial = false;
        }

        internal void Bind(EnemyGrid grid) => owner = grid;
        internal void SetCell(int newColumn, int newRow) { column = newColumn; row = newRow; }
        internal void SetColor(EnemyColor newColor)
        {
            if (color != newColor) IsSpecial = false;
            color = newColor;
            Visuals.Body.color = EnemyPalette.Get(color);
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
                case EnemyColor.Orange: return new Color(1f, 0.48f, 0.08f);
                default: return new Color(1f, 0.85f, 0.25f);
            }
        }
    }
}
