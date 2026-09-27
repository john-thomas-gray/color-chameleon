using System.Collections.Generic;
using UnityEngine;

namespace CandyCruisers
{
    public static class LocalLeaderboard
    {
        public const int MaxEntries = 5;
        private const string Prefix = "CandyCruisers.Leaderboard.";

        public struct Entry
        {
            public long Score { get; }
            public int Level { get; }
            public string Date { get; }

            public Entry(long score, int level, string date)
            {
                Score = score;
                Level = level;
                Date = date;
            }
        }

        public static List<Entry> Entries()
        {
            int count = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Count", 0), 0, MaxEntries);
            var entries = new List<Entry>();
            for (int i = 0; i < count; i++)
            {
                string scoreText = PlayerPrefs.GetString(Prefix + "Score." + i, "0");
                long score;
                if (!long.TryParse(scoreText, out score)) score = 0;
                entries.Add(new Entry(score, PlayerPrefs.GetInt(Prefix + "Level." + i, 1),
                    PlayerPrefs.GetString(Prefix + "Date." + i, "")));
            }
            return entries;
        }

        public static int Submit(long score, int level)
        {
            if (score <= 0) return 0;
            var entries = Entries();
            var entry = new Entry(score, Mathf.Max(1, level), System.DateTime.Now.ToString("yyyy-MM-dd"));
            entries.Add(entry);
            entries.Sort((left, right) =>
            {
                int byScore = right.Score.CompareTo(left.Score);
                return byScore != 0 ? byScore : right.Level.CompareTo(left.Level);
            });
            int rank = entries.IndexOf(entry) + 1;
            while (entries.Count > MaxEntries) entries.RemoveAt(entries.Count - 1);
            Save(entries);
            return rank <= MaxEntries ? rank : 0;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Prefix + "Count");
            for (int i = 0; i < MaxEntries; i++)
            {
                PlayerPrefs.DeleteKey(Prefix + "Score." + i);
                PlayerPrefs.DeleteKey(Prefix + "Level." + i);
                PlayerPrefs.DeleteKey(Prefix + "Date." + i);
            }
            PlayerPrefs.Save();
        }

        public static void Replace(IEnumerable<Entry> entries)
        {
            var copy = new List<Entry>(entries);
            copy.Sort((left, right) =>
            {
                int byScore = right.Score.CompareTo(left.Score);
                return byScore != 0 ? byScore : right.Level.CompareTo(left.Level);
            });
            while (copy.Count > MaxEntries) copy.RemoveAt(copy.Count - 1);
            Save(copy);
        }

        private static void Save(List<Entry> entries)
        {
            PlayerPrefs.SetInt(Prefix + "Count", entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                PlayerPrefs.SetString(Prefix + "Score." + i, entries[i].Score.ToString());
                PlayerPrefs.SetInt(Prefix + "Level." + i, entries[i].Level);
                PlayerPrefs.SetString(Prefix + "Date." + i, entries[i].Date);
            }
            PlayerPrefs.Save();
        }
    }
}
