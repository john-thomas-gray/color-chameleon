using System;
using System.Collections.Generic;

namespace CandyCruisers
{
    public sealed class RunProgress
    {
        public const int MinLevel = 1;
        public const int MaxMenuStartLevel = 99;
        public int Level { get; private set; } = 1;
        public int Defeated { get; private set; }
        public long Score { get; private set; }
        public int ComboStreak { get; private set; }
        public int ComboMultiplier => Math.Max(1, ComboStreak + 1);
        public int ActiveComboMultiplier { get; private set; } = 1;
        private bool shotActive;
        private readonly HashSet<EnemyColor> shotColors = new HashSet<EnemyColor>();
        public int ScoringComboMultiplier => shotActive ? ActiveComboMultiplier : 1;
        public int NextThreshold => 9 * Level * (Level + 1);
        public int PreviousThreshold => 9 * (Level - 1) * Level;
        public const int StandardRowWidth = 5;
        public const int WideRowWidth = 6;
        public const int WideRowsStartLevel = 7;
        public int RowWidth => RowWidthForLevel(Level);
        public int BatchRows => Level <= 2 ? 2 : Level == 3 ? 3 : Level < 4 ? 4 : Level < 7 ? 5 : 6;
        public int BatchEnemies => BatchRows * RowWidth;
        public static int RowWidthForLevel(int level) => level >= WideRowsStartLevel ? WideRowWidth : StandardRowWidth;
        public static int UnlockLevel(EnemyColor color) =>
            color == EnemyColor.Green ? 2 : color == EnemyColor.Yellow ? 4 : color == EnemyColor.Purple ? 6 : color == EnemyColor.Orange ? 9 : 1;
        public static bool IsUnlocked(EnemyColor color, int level) => level >= UnlockLevel(color);
        public static int ClampMenuStartLevel(int level) => Math.Max(MinLevel, Math.Min(MaxMenuStartLevel, level));

        public void Reset(int level = MinLevel)
        {
            Level = Math.Max(MinLevel, level);
            Defeated = PreviousThreshold;
            Score = 0;
            ResetCombo();
        }

        public long RegisterClear(int count, bool fleetCleared, int? scoreWeight = null, bool applyCombo = true)
        {
            if (count <= 0) return 0;
            long earned = (100L + 10L * (Level - 1)) * (scoreWeight ?? count) * (applyCombo ? ScoringComboMultiplier : 1);
            if (fleetCleared) earned += 10000L * Level;
            Score += earned;
            Defeated += count;
            while (Defeated >= NextThreshold) Level++;
            return earned;
        }

        public void BeginShot()
        {
            shotColors.Clear();
            ActiveComboMultiplier = ComboMultiplier;
            shotActive = true;
        }

        public bool RegisterShotColor(EnemyColor color)
        {
            if (!shotActive || !shotColors.Add(color)) return false;
            ComboStreak++;
            ActiveComboMultiplier = ComboMultiplier;
            return true;
        }

        public void FinishShot(bool hit)
        {
            if (!hit) ResetCombo();
            shotActive = false;
            shotColors.Clear();
            ActiveComboMultiplier = ComboMultiplier;
        }

        public void ResetCombo()
        {
            ComboStreak = 0;
            ActiveComboMultiplier = 1;
            shotActive = false;
            shotColors.Clear();
        }
    }
}
