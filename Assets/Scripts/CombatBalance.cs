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
                case EnemyColor.Blue: range = new Vector2(3, 10); break;
                case EnemyColor.Green: range = new Vector2(8, 10); break;
                case EnemyColor.Purple: range = new Vector2(8, 12); break;
                case EnemyColor.Yellow: range = new Vector2(2, 5); break;
                case EnemyColor.Orange: range = new Vector2(2, 2); break;
                default: range = new Vector2(3, 10); break;
            }
            range *= .75f;
            range.y = Mathf.Max(range.x + 3, range.y - .25f * Mathf.Max(0, level - 1));
            return range;
        }
    }
}
