using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class ColorCycleChecks
    {
        public static void Run()
        {
            bool previous = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            var random = UnityEngine.Random.state;
            var root = new GameObject("Color cycle fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow));
                SpawnOverride.Enabled = false;
                Check(spawner.UnlockedColors().Count == 2 && grid.SeenColors().Count == 0, "Unlocks alone do not create indicator slots");
                SpawnOverride.Enabled = true;
                SpawnOverride.Types = (1 << (int)EnemyColor.Red) | (1 << (int)EnemyColor.Blue) | (1 << (int)EnemyColor.Purple);
                UnityEngine.Random.InitState(500);
                Check(spawner.UnlockedColors().Count == 3 && grid.SeenColors().Count == 0, "Override selection alone does not create indicator slots");
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                Check(grid.SeenColors().SequenceEqual(new[] { EnemyColor.Red }), "First actual color gives one full-width slot");
                ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                Check(grid.SeenColors().Count == 2, "Second actual color gives two half-width slots");
                var purple = ProgressionChecks.Add(grid, EnemyColor.Purple, 4, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(grid.IsColorCleared(EnemyColor.Red) && grid.SeenColors().Count == 3,
                    "Clear locks color without shrinking indicator denominator");
                Check(spawner.TryAdvance() && grid.Model.ColorCount(EnemyColor.Red) == 0, "Rows cannot restore a cleared color");
                var summon = spawner.TrySummon(purple);
                Check(summon != null && summon.Color != EnemyColor.Red, "Purple cannot restore a cleared color");
                SpawnOverride.Types = 1 << (int)EnemyColor.Red;
                int count = grid.Model.Count, row = purple.Row;
                Check(spawner.TryAdvance() && grid.Model.Count == count && purple.Row == row + 1,
                    "All-locked override descends without adding forbidden enemies");
                Check(spawner.TrySummon(purple) == null && grid.IsColorCleared(EnemyColor.Red), "Override cannot bypass lock");
                SpawnOverride.Types = SpawnOverride.AllTypes;
                Check(spawner.UnlockedColors().Count == 5 && grid.SeenColors().Count == 3 && grid.IsColorCleared(EnemyColor.Red),
                    "Unseen override colors cannot shrink earned segments");
                foreach (var color in new[] { EnemyColor.Blue, EnemyColor.Purple })
                    while (grid.Model.ColorCount(color) > 0)
                        grid.ClearMatchingChain(grid.GetComponentsInChildren<GridEnemy>().First(e => e.Color == color).Id, color);
                Check(grid.Model.Count == 0 && grid.IsColorCleared(EnemyColor.Red) && grid.IsColorCleared(EnemyColor.Blue) &&
                    grid.IsColorCleared(EnemyColor.Purple), "Locks persist through the empty-fleet pause");
                Check(!spawner.SpawnBatch(0) && grid.IsColorCleared(EnemyColor.Red), "Rejected batch cannot reset the cycle");
                Check(spawner.SpawnBatch(3), "Next batch starts a fresh cycle");
                foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                    Check(!grid.IsColorCleared(color), "Successful refill restores every color");
                Check(grid.SeenColors().SequenceEqual(grid.Model.AvailableColors()), "New batch remembers only colors actually spawned");
                while (grid.Model.Count > 0)
                {
                    var enemy = grid.GetComponentsInChildren<GridEnemy>()[0];
                    grid.Unregister(enemy);
                    UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                }
                grid.BeginColorCycle();
                var converted = ProgressionChecks.Add(grid, EnemyColor.Yellow, 0, 0);
                grid.SetColor(converted.Id, EnemyColor.Green);
                Check(grid.SeenColors().SequenceEqual(new[] { EnemyColor.Green, EnemyColor.Yellow }),
                    "Conversion records its displayed color without forgetting the previous one");
                grid.ClearMatchingChain(converted.Id, EnemyColor.Green);
                Check(grid.SeenColors().Count == 2, "Seen history survives clearing and the refill pause");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                SpawnOverride.Enabled = previous;
                SpawnOverride.Types = types;
                UnityEngine.Random.state = random;
            }
            Debug.Log("Color cycle checks passed: row/summon exclusion, override locks, stable eligibility counts and refill reset.");
        }
        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Color cycle check failed: " + message); }
    }
}
