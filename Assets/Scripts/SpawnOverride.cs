#if UNITY_EDITOR
using UnityEditor;

namespace CandyCruisers
{
    // Editor-session storage survives Play mode domain reloads, but never enters a player build.
    public static class SpawnOverride
    {
        public const int AllTypes = (1 << 6) - 1;
        public static bool Enabled
        {
            get => SessionState.GetBool("CandyCruisers.SpawnOverride.Enabled", false);
            set => SessionState.SetBool("CandyCruisers.SpawnOverride.Enabled", value);
        }
        public static int Types
        {
            get => SessionState.GetInt("CandyCruisers.SpawnOverride.Types", AllTypes);
            set
            {
                if (value == 0 || (value & ~AllTypes) != 0)
                    throw new System.ArgumentOutOfRangeException(nameof(value), "Select at least one valid enemy type.");
                SessionState.SetInt("CandyCruisers.SpawnOverride.Types", value);
            }
        }
        public static bool Allows(EnemyColor color) => (Types & (1 << (int)color)) != 0;
    }
}
#endif
