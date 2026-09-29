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
            CheckLifeReward();
            CheckHitLosesBars();
            bool previous = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            var random = UnityEngine.Random.state;
            var root = new GameObject("Color cycle fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow), Prefab(EnemyColor.Orange));
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
                Check(spawner.UnlockedColors().Count == 6 && grid.SeenColors().Count == 3 && grid.IsColorCleared(EnemyColor.Red),
                    "Unseen override colors cannot shrink earned segments");
                foreach (var color in new[] { EnemyColor.Blue, EnemyColor.Purple })
                    while (grid.Model.ColorCount(color) > 0)
                        grid.ClearMatchingChain(grid.GetComponentsInChildren<GridEnemy>().First(e => e.Color == color).Id, color);
                Check(grid.Model.Count == 0 && grid.IsColorCleared(EnemyColor.Red) && grid.IsColorCleared(EnemyColor.Blue) &&
                    grid.IsColorCleared(EnemyColor.Purple) && grid.AllColorClearBarsFilled,
                    "Locks persist through the empty-fleet pause and all bars report filled");
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

        private static void CheckHitLosesBars()
        {
            foreach (bool fatal in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                if (fatal)
                    for (int i = 0; i < PlayerMovement.MaxLives - 1; i++)
                    { player.Hit(); player.TickSurvival(4); }
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                ProgressionChecks.Add(grid, EnemyColor.Green, 4, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(grid.HasColorClearBar(EnemyColor.Red) && grid.HasColorClearBar(EnemyColor.Blue), "Fixture earns multiple bars");
                player.ControlsLocked = true;
                Check(!player.Hit() && grid.HasColorClearBar(EnemyColor.Red), "Rejected hit preserves bars");
                player.ControlsLocked = false;
                Check(player.Hit() && grid.SeenColors().All(c => !grid.HasColorClearBar(c)), "Accepted hit clears every earned bar, including fatal hits");
                Check(grid.SeenColors().Count == 3 && !grid.IsColorCleared(EnemyColor.Red) && !grid.IsColorCleared(EnemyColor.Blue),
                    "Hit removes spawn locks without resetting seen-color history");
                if (fatal) return;
                player.TickSurvival(2);
                red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(player.Invulnerable && !player.Hit() && grid.HasColorClearBar(EnemyColor.Red), "Invulnerability rejection preserves newly earned bars");
                player.BeginFatalDefeat();
                Check(!grid.HasColorClearBar(EnemyColor.Red), "Direct fatal contact also removes bars");
            });
        }

        private static void CheckLifeReward()
        {
            foreach (bool damaged in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>();
                session.Configure(player);
                if (!Application.isPlaying) typeof(GameSession).GetMethod("OnEnable",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(session, null);
                if (damaged)
                {
                    Check(player.Hit() && player.Lives == PlayerMovement.MaxLives - 1, "Fixture spends one life");
                    player.TickSurvival(3.1f);
                }
                var red = ProgressionChecks.Add(grid, EnemyColor.Red, 0, 0);
                var blue = ProgressionChecks.Add(grid, EnemyColor.Blue, 2, 0);
                grid.ClearMatchingChain(red.Id, EnemyColor.Red);
                Check(player.Lives == (damaged ? PlayerMovement.MaxLives - 1 : PlayerMovement.MaxLives),
                    "Partial bar set does not grant a life");
                grid.ClearMatchingChain(blue.Id, EnemyColor.Blue);
                Check(grid.AllColorClearBarsFilled && player.Lives == PlayerMovement.MaxLives,
                    damaged ? "Completing every color bar restores one life" : "Completing every color bar respects the life cap");
            });
        }

        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Color cycle check failed: " + message); }
    }
}
