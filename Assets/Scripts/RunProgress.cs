using System;

namespace CandyCruisers
{
    public sealed class RunProgress
    {
        public int Level { get; private set; } = 1;
        public int Defeated { get; private set; }
        public long Score { get; private set; }
        public int NextThreshold => 9 * Level * (Level + 1);
        public int PreviousThreshold => 9 * (Level - 1) * Level;
        public int BatchRows => Level == 1 ? 3 : Level < 4 ? 4 : Level < 7 ? 5 : 6;
        public static int UnlockLevel(EnemyColor color) =>
            color == EnemyColor.Green ? 2 : color == EnemyColor.Purple ? 4 : color == EnemyColor.Yellow ? 6 : 1;
        public static bool IsUnlocked(EnemyColor color, int level) => level >= UnlockLevel(color);

        public long RegisterClear(int count, bool fleetCleared)
        {
            if (count <= 0) return 0;
            long earned = (100L + 10L * (Level - 1)) * count;
            if (fleetCleared) earned += 10000L * Level;
            Score += earned;
            Defeated += count;
            while (Defeated >= NextThreshold) Level++;
            return earned;
        }
    }
}
