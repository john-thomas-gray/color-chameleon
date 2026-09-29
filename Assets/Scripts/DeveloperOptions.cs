using UnityEngine;

namespace CandyCruisers
{
    public static class DeveloperOptions
    {
        private static bool playerInvincible;
        public static bool Available => Application.isEditor || Debug.isDebugBuild;
        public static bool PlayerInvincible
        {
            get => Available && playerInvincible;
            set => playerInvincible = Available && value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            playerInvincible = false;
        }
    }
}
