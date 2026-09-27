using System;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class PlayerColorAssistChecks
    {
        public static void Run()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                Add(grid, EnemyColor.Red, 0, 2);
                player.RefreshColor();
                Add(grid, EnemyColor.Red, 1, 2);
                Add(grid, EnemyColor.Blue, 0, 3);
                Add(grid, EnemyColor.Blue, 1, 3);
                var blue = Add(grid, EnemyColor.Blue, 2, 3);
                var green = Add(grid, EnemyColor.Green, 4, 1);
                grid.RefreshSpecials();
                Check(grid.SelectablePlayerColors().SequenceEqual(new[] { EnemyColor.Blue, EnemyColor.Green }),
                    "Every Red immediately above special Blues is excluded; exposed Green remains eligible");
                player.RefreshColor();
                Check(player.ReadyColor == EnemyColor.Red, "Existing ready color is not changed retroactively");
                Check(player.Fire(), "Existing color can still fire");
                player.RefreshColor(true);
                Check(player.ReadyColor == EnemyColor.Red && tongue.ShotColor == EnemyColor.Red,
                    "Assistance never changes an in-flight shot");
                tongue.Cancel();
                for (int i = 0; i < 80; i++)
                { player.RefreshColor(true); Check(player.ReadyColor != EnemyColor.Red, "Rerolls exclude blocked-only colors"); }

                var exposed = Add(grid, EnemyColor.Red, 3, 0);
                Check(grid.SelectablePlayerColors().Contains(EnemyColor.Red), "One unblocked enemy restores the entire color");
                Remove(grid, exposed);
                Remove(grid, green);
                grid.TryMove(blue.Id, 2, 6);
                Check(grid.SelectablePlayerColors().SequenceEqual(new[] { EnemyColor.Blue }),
                    "Only Blue remains when every other enemy is directly blocked");
                for (int i = 0; i < 20; i++)
                { player.RefreshColor(true); Check(player.ReadyColor == EnemyColor.Blue, "Blocked-only colors cannot be selected"); }
                Add(grid, EnemyColor.Green, 4, 0);
                grid.SetColor(grid.Model.At(0, 3).Id, EnemyColor.Purple);
                Check(grid.SelectablePlayerColors().Contains(EnemyColor.Red), "Changing a blocking Blue releases that color immediately");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                Add(grid, EnemyColor.Blue, 0, 3);
                Add(grid, EnemyColor.Red, 0, 2);
                Check(grid.SelectablePlayerColors().Contains(EnemyColor.Red), "An ordinary Blue does not block color selection");
                Add(grid, EnemyColor.Blue, 1, 3);
                Add(grid, EnemyColor.Blue, 2, 3);
                Add(grid, EnemyColor.Green, 4, 0);
                grid.RefreshSpecials();
                grid.TryMove(grid.Model.At(0, 2).Id, 0, 1);
                Check(grid.SelectablePlayerColors().Contains(EnemyColor.Red), "A gap above the Blue is not directly behind it");
                var yellow = Add(grid, EnemyColor.Yellow, 3, 0);
                Check(grid.Model.BeginImitation(yellow.Id, grid.Model.At(4, 0).Id), "Yellow starts transforming");
                Check(!grid.SelectablePlayerColors().Contains(EnemyColor.Yellow), "Existing transforming-Yellow exclusion is preserved");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                Check(grid.SelectablePlayerColors().Count == 0, "Empty fleets do not invent Blue");
                player.PrepareNextWave(new[] { EnemyColor.Red });
                Add(grid, EnemyColor.Red, 0, 0);
                Add(grid, EnemyColor.Blue, 0, 1);
                Add(grid, EnemyColor.Blue, 1, 1);
                Add(grid, EnemyColor.Blue, 2, 1);
                grid.RefreshSpecials();
                player.RefreshColor();
                Check(player.ReadyColor == EnemyColor.Blue, "Reserved next-wave color is rechecked against the arriving front line");
            });
            CheckBarriers();
            CheckWeights();
            CheckPlannedWeights();
            Debug.Log("Player color assistance checks passed: connected Blue barriers, diagonal/vertical paths, gaps, exposed targets, count/row weights, Blue bias, rerolls and refill selection.");
        }

        private static void CheckBarriers()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var barrier = AddSpecialBlues(grid, Enumerable.Range(0, 6).Select(x => new Vector2Int(x, 4)).ToArray());
                Add(grid, EnemyColor.Red, 2, 1);
                Add(grid, EnemyColor.Purple, 0, 1);
                Add(grid, EnemyColor.Purple, 0, 6);
                Add(grid, EnemyColor.Green, 4, 7);
                var weights = grid.PlayerColorWeights();
                Check(grid.SelectablePlayerColors().SequenceEqual(new[] { EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple }),
                    "A full Blue barrier excludes colors only above it, even with enemies in front");
                Check(weights[EnemyColor.Blue] == 60 && weights[EnemyColor.Purple] == 9 && weights[EnemyColor.Green] == 8,
                    "Barrier Blues get double weight; all on-screen enemies of an eligible color add weight");
                for (int i = 0; i < 100; i++)
                { player.RefreshColor(true); Check(player.ReadyColor != EnemyColor.Red, "Barrier filtering reaches actual player rerolls"); }
                Remove(grid, barrier[2]);
                Check(grid.SelectablePlayerColors().Contains(EnemyColor.Red) && grid.PlayerColorWeights()[EnemyColor.Blue] == 25,
                    "Breaking the wall restores distant colors and removes the Blue bonus");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                AddSpecialBlues(grid, Enumerable.Range(0, 6).Select(x => new Vector2Int(x, x + 2)).ToArray());
                Add(grid, EnemyColor.Red, 4, 0);
                Add(grid, EnemyColor.Green, 0, 3);
                Add(grid, EnemyColor.Purple, 5, 8);
                Check(grid.SelectablePlayerColors().SequenceEqual(new[] { EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple }),
                    "Diagonal contact forms a wall; below means below the wall in that enemy's column");
                grid.SetColor(grid.Model.At(3, 5).Id, EnemyColor.Yellow);
                Check(grid.SelectablePlayerColors().Contains(EnemyColor.Red), "A converted barrier member opens the diagonal wall immediately");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                AddSpecialBlues(grid, new[] {
                    new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(1, 3), new Vector2Int(1, 4),
                    new Vector2Int(2, 5), new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 5)
                });
                Add(grid, EnemyColor.Red, 2, 0);
                Add(grid, EnemyColor.Green, 0, 3);
                Add(grid, EnemyColor.Yellow, 5, 6);
                Check(grid.SelectablePlayerColors().SequenceEqual(new[] { EnemyColor.Blue, EnemyColor.Green, EnemyColor.Yellow }),
                    "Horizontal, vertical and diagonal segments can form one continuous barrier");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                AddSpecialBlues(grid, Enumerable.Range(0, 6).Select(x => new Vector2Int(x, x % 2 == 0 ? 2 : 6)).ToArray());
                Add(grid, EnemyColor.Red, 0, 0);
                Check(grid.SelectablePlayerColors().Contains(EnemyColor.Red) && grid.PlayerColorWeights()[EnemyColor.Blue] == 30,
                    "Disconnected special Blues in every column are not a continuous barrier");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                for (int x = 0; x < 6; x++) Add(grid, EnemyColor.Blue, x, 4);
                Add(grid, EnemyColor.Red, 2, 0);
                Check(grid.SelectablePlayerColors().Contains(EnemyColor.Red) && grid.PlayerColorWeights()[EnemyColor.Blue] == 30,
                    "Ordinary Blues neither create a wall nor receive the barrier bonus");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                AddSpecialBlues(grid, Enumerable.Range(1, 5).Select(x => new Vector2Int(x, 4)).ToArray());
                Add(grid, EnemyColor.Red, 2, 0);
                Add(grid, EnemyColor.Green, 4, 6);
                Check(grid.SelectablePlayerColors().SequenceEqual(new[] { EnemyColor.Blue, EnemyColor.Green }),
                    "A shifted five-column fleet uses its occupied width, not unused edge columns");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var positions = Enumerable.Range(0, 6).Select(x => new Vector2Int(x, 3))
                    .Concat(Enumerable.Range(0, 6).Select(x => new Vector2Int(x, 6))).ToArray();
                AddSpecialBlues(grid, positions);
                Add(grid, EnemyColor.Red, 0, 4);
                Add(grid, EnemyColor.Green, 0, 7);
                Check(grid.SelectablePlayerColors().SequenceEqual(new[] { EnemyColor.Blue, EnemyColor.Green }),
                    "With two barriers, only enemies beneath the nearer wall are eligible");
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                AddSpecialBlues(grid, Enumerable.Range(0, 6).Select(x => new Vector2Int(x, 4)).ToArray());
                var yellow = Add(grid, EnemyColor.Yellow, 1, 6);
                var target = Add(grid, EnemyColor.Red, 2, 6);
                Add(grid, EnemyColor.Yellow, 3, 0);
                Check(grid.Model.BeginImitation(yellow.Id, target.Id), "Exposed Yellow begins transforming");
                Check(!grid.SelectablePlayerColors().Contains(EnemyColor.Yellow),
                    "Hidden ready Yellows do not make an exposed transforming Yellow selectable");
            });
        }

        private static void CheckWeights()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                Add(grid, EnemyColor.Red, 0, 0);
                Add(grid, EnemyColor.Red, 1, 0);
                Add(grid, EnemyColor.Red, 2, 0);
                Add(grid, EnemyColor.Blue, 5, 0);
                var weights = grid.PlayerColorWeights();
                Check(weights[EnemyColor.Red] == 3 && weights[EnemyColor.Blue] == 1, "Enemy count changes color weight");
                UnityEngine.Random.InitState(8421);
                int reds = 0;
                for (int i = 0; i < 2000; i++)
                { player.RefreshColor(true); if (player.ReadyColor == EnemyColor.Red) reds++; }
                Check(reds > 1400 && reds < 1600, "Actual player selections follow the three-to-one population ratio");
                var blue = grid.View(grid.Model.At(5, 0).Id);
                grid.TryMove(blue.Id, 5, 5);
                weights = grid.PlayerColorWeights();
                Check(weights[EnemyColor.Blue] == 6, "Moving an enemy to a lower row immediately increases its weight");
                int blues = 0;
                for (int i = 0; i < 2000; i++)
                { player.RefreshColor(true); if (player.ReadyColor == EnemyColor.Blue) blues++; }
                Check(blues > 1230 && blues < 1430, "A lower enemy receives more selection weight than three top-row enemies");
                Remove(grid, blue);
                Check(!grid.PlayerColorWeights().ContainsKey(EnemyColor.Blue), "Removed colors have zero weight");
            });
        }

        private static void CheckPlannedWeights()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var plan = Enumerable.Repeat(EnemyColor.Red, 5).Concat(Enumerable.Repeat(EnemyColor.Blue, 5)).ToArray();
                UnityEngine.Random.InitState(3982);
                int blues = 0;
                for (int i = 0; i < 1200; i++)
                {
                    player.ResetForRun();
                    player.PrepareNextWave(plan);
                    if (player.ReadyColor == EnemyColor.Blue) blues++;
                }
                Check(blues > 740 && blues < 860, "Reserved colors also weight the lower planned row more heavily");
                player.PrepareNextWave(new[] { EnemyColor.Red });
                player.PrepareNextWave(plan);
                Check(player.ReadyColor == EnemyColor.Blue, "Refill still prefers a different color when one is available");
            });
        }

        private static GridEnemy[] AddSpecialBlues(EnemyGrid grid, Vector2Int[] positions)
        {
            var blues = new GridEnemy[positions.Length];
            for (int i = 0; i < blues.Length; i++) blues[i] = Add(grid, EnemyColor.Blue, i % 6, i / 6);
            grid.RefreshSpecials();
            for (int i = 0; i < blues.Length; i++)
                Check(blues[i].IsSpecial && grid.TryMove(blues[i].Id, positions[i].x, positions[i].y), "Place a previously promoted Blue");
            return blues;
        }
        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int x, int y) => ProgressionChecks.Add(grid, color, x, y);
        private static void Remove(EnemyGrid grid, GridEnemy enemy)
        { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Player color assistance check failed: " + message); }
    }
}
