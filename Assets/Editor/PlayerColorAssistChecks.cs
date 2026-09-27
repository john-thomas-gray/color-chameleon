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
                    "Ragged special-Blue front forces Blue, ignoring empty columns");
                for (int i = 0; i < 20; i++)
                { player.RefreshColor(true); Check(player.ReadyColor == EnemyColor.Blue, "All-special-Blue front forces the next player color"); }
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
            Debug.Log("Player color assistance checks passed: special-Blue fronts, adjacent blocked colors, exposed alternatives, rerolls, flight preservation and refill selection.");
        }
        private static GridEnemy Add(EnemyGrid grid, EnemyColor color, int x, int y) => ProgressionChecks.Add(grid, color, x, y);
        private static void Remove(EnemyGrid grid, GridEnemy enemy)
        { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Player color assistance check failed: " + message); }
    }
}
