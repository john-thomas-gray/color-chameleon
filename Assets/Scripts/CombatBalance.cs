using UnityEngine;

namespace CandyCruisers
{
    public static class CombatBalance
    {
        public static float FleetSpeedMultiplier(int level) => Mathf.Min(2, 1 + .03f * Mathf.Max(0, level - 1));

        public static Vector2 CooldownRange(EnemyColor color, int level)
        {
            Vector2 range;
            switch (color)
            {
                case EnemyColor.Blue: range = new Vector2(35, 75); break;
                case EnemyColor.Green: range = new Vector2(8, 20); break;
                case EnemyColor.Purple: range = new Vector2(18, 50); break;
                case EnemyColor.Yellow: range = new Vector2(18, 40); break;
                default: range = new Vector2(6, 18); break;
            }
            range.y = Mathf.Max(range.x + 4, range.y - .25f * Mathf.Max(0, level - 1));
            return range;
        }
    }
}
