using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MagicMultiplierChecks
    {
        public static void Run()
        {
            var numberFont = Resources.Load<Font>("Fonts/Bungee-Regular");
            Check(numberFont != null, "Bungee is bundled with the game");
            numberFont.RequestCharactersInTexture("0123456789", 64, FontStyle.Normal);
            foreach (char digit in "0123456789")
                Check(numberFont.GetCharacterInfo(digit, out var glyph, 64, FontStyle.Normal) && glyph.advance > 0,
                    "Bungee includes a renderable glyph for " + digit);
            foreach (float step in new[] { .016f, .1f, 2f })
            foreach (bool fullClear in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                Populate(grid);
                if (!fullClear) Add(grid, EnemyColor.Purple, 4, 5);
                var before = UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None);
                var progress = new RunProgress();
                var weights = new List<int>();
                grid.MatchCleared += (count, all, weight) =>
                { weights.Add(weight); progress.RegisterClear(count, all, weight); };
                Check(tongue.TryFire(EnemyColor.Yellow, 10, true), "Start magic shot");
                for (int i = 0; tongue.Active && i < 1000; i++) tongue.Tick(step, grid);
                Check(!tongue.Active && tongue.MagicMultiplier == 5 && grid.Model.Count == (fullClear ? 0 : 1),
                    "Magic shot destroys the 3, 1 and 4 enemy chains exactly once");
                Check(weights.SequenceEqual(new[] { 5, 1, 16 }) && progress.Score == 2200 + (fullClear ? 10000 : 0) && progress.Defeated == 8,
                    "Only qualifying chains advance multipliers; singleton awards base points");
                if (Application.isPlaying)
                {
                    var effects = UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None)
                        .Where(effect => !before.Contains(effect)).OrderBy(effect => effect.Multiplier).ToArray();
                    Check(effects.Select(effect => effect.Multiplier).SequenceEqual(new[] { 1, 1, 2, 2, 3, 4, 4, 5 }),
                        "Every displayed multiplier agrees with the scoring sequence");
                    Check(effects.Select(effect => effect.GetComponentInChildren<TextMesh>(true).text)
                        .SequenceEqual(new[] { "", "", "2", "2", "3", "4", "4", "5" }), "Exact requested death labels");
                    Check(effects[4].Depth == 1 && effects[4].Multiplier == 3,
                        "A new chain retains its local animation timing while scoring continues");
                }
                Check(tongue.TryFire(EnemyColor.Blue, 10, true) && tongue.MagicMultiplier == 0, "Next magic shot resets its counter");
                tongue.Cancel();
            });

            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                Populate(grid);
                tongue.TryFire(EnemyColor.Red, 10, true);
                tongue.Tick(.02f, grid);
                Check(tongue.MagicMultiplier == 2 && !tongue.TryFire(EnemyColor.Blue, 10, true) && tongue.MagicMultiplier == 2,
                    "Rejected fire cannot reset an active magic combo");
                tongue.Cancel();
                Check(tongue.TryFire(EnemyColor.Blue, 10, true) && tongue.MagicMultiplier == 0,
                    "Cancellation cannot leak a combo to the next shot");
                tongue.Cancel();
                var source = grid.View(grid.Model.At(2, 3).Id).Visuals.Body;
                var effect = EnemyDeathBurst.Create(source, EnemyColor.Green, 1);
                try { Check(effect.Multiplier == 1 && effect.GetComponentInChildren<TextMesh>(true).text == "",
                    "Ordinary first kills also hide 1 without losing its scoring value"); }
                finally { UnityEngine.Object.DestroyImmediate(effect.gameObject); }
                foreach (int multiplier in new[] { 2, 8, 24, 128 })
                {
                    effect = EnemyDeathBurst.Create(source, EnemyColor.Green, 1, multiplier);
                    try
                    {
                        var label = effect.GetComponentInChildren<TextMesh>(true);
                        Check(label.text == multiplier.ToString() && label.font == numberFont &&
                            label.fontStyle == FontStyle.Normal && label.GetComponent<MeshRenderer>().sharedMaterial == numberFont.material,
                            "Single- and multi-digit destruction labels use Bungee with its original weight and material");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(effect.gameObject); }
                }
            });
            foreach (bool magic in new[] { false, true })
            foreach (int size in new[] { 1, 2 })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                Add(grid, EnemyColor.Red, 2, 5);
                if (size == 2) Add(grid, EnemyColor.Red, 1, 5);
                var before = UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None);
                int weight = 0;
                grid.MatchCleared += (count, all, value) => weight = value;
                int id = grid.Model.At(2, 5).Id;
                if (magic) Check(grid.ClearMagicChain(id, 7) == 0, "Small chains add nothing to an existing magic multiplier");
                else Check(grid.ClearMatchingChain(id, EnemyColor.Red) == size, "Ordinary small chain clears normally");
                Check(weight == size && grid.Model.Count == 0, "Small chains award exactly base points per enemy");
                if (Application.isPlaying)
                {
                    var effects = UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None)
                        .Where(effect => !before.Contains(effect)).ToArray();
                    Check(effects.Length == size && effects.All(effect => effect.Multiplier == 1
                        && effect.GetComponentInChildren<TextMesh>(true).text == ""), "Small chains use unnumbered singleton pops");
                    Check(effects.Select(effect => effect.Depth).OrderBy(depth => depth).SequenceEqual(Enumerable.Range(1, size)),
                        "Small chains retain connection-depth stagger independently of scoring");
                    Check(effects.Count(effect => effect.Exploded) == 1, "Only the first small-chain pop starts immediately");
                    foreach (var effect in effects) effect.Tick(EnemyDeathBurst.RingDelay + .001f);
                    Check(effects.All(effect => effect.Exploded), "The second pop starts after the cascade delay");
                }
            });
            foreach (bool magic in new[] { false, true })
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                // Bend the sixth Red up one cell to fit the five-column field; distances stay identical.
                for (int x = 0; x < 5; x++) Add(grid, EnemyColor.Red, x, 5);
                Add(grid, EnemyColor.Red, 4, 4);
                for (int x = 0; x < 4; x++) Add(grid, EnemyColor.Green, x, 2);
                var ids = Enumerable.Range(0, 5).Select(x => grid.Model.At(x, 5).Id)
                    .Concat(new[] { grid.Model.At(4, 4).Id })
                    .Concat(Enumerable.Range(0, 4).Select(x => grid.Model.At(x, 2).Id)).ToArray();
                var positions = ids.Select(id => grid.View(id).Visuals.Body.transform.position).ToArray();
                var before = UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None);
                var weights = new List<int>();
                grid.MatchCleared += (count, all, weight) => weights.Add(weight);
                if (magic)
                {
                    int peak = grid.ClearMagicChain(ids[1], 0);
                    Check(peak == 5, "First chain peaks at five rather than six kills");
                    peak += grid.ClearMagicChain(ids[7], peak);
                    Check(peak == 8, "Second chain raises the peak to eight");
                }
                else
                {
                    grid.ClearMatchingChain(ids[1], EnemyColor.Red);
                    grid.ClearMatchingChain(ids[7], EnemyColor.Green);
                }
                Check(weights.SequenceEqual(new[] { 17, magic ? 28 : 8 }), "Example score follows distance plus preceding peak");
                if (Application.isPlaying)
                {
                    var effects = UnityEngine.Object.FindObjectsByType<EnemyDeathBurst>(FindObjectsSortMode.None)
                        .Where(effect => !before.Contains(effect)).ToArray();
                    var actual = positions.Select(position => effects.Single(effect =>
                        Vector3.Distance(effect.transform.position, position) < .001f)).ToArray();
                    int[] expected = magic ? new[] { 2, 1, 2, 3, 4, 5, 7, 6, 7, 8 }
                        : new[] { 2, 1, 2, 3, 4, 5, 2, 1, 2, 3 };
                    Check(actual.Select(effect => effect.Multiplier).SequenceEqual(expected), "Spatial multipliers match the example");
                    Check(actual.Select(effect => effect.GetComponentInChildren<TextMesh>(true).text)
                        .SequenceEqual(expected.Select(value => value == 1 ? "" : value.ToString())), "Spatial death labels match scores");
                }
            });
            Debug.Log("Magic multiplier checks passed: branch depths plus cumulative peaks, exact spatial labels, small-chain pops, scores, frame steps and reset.");
        }
        private static void Populate(EnemyGrid grid)
        {
            Add(grid, EnemyColor.Red, 2, 5); Add(grid, EnemyColor.Red, 1, 5); Add(grid, EnemyColor.Red, 3, 5);
            Add(grid, EnemyColor.Green, 2, 3);
            Add(grid, EnemyColor.Blue, 2, 1); Add(grid, EnemyColor.Blue, 1, 1);
            Add(grid, EnemyColor.Blue, 3, 1); Add(grid, EnemyColor.Blue, 3, 0);
        }
        private static void Add(EnemyGrid grid, EnemyColor color, int x, int y) => ProgressionChecks.Add(grid, color, x, y);
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Magic multiplier check failed: " + message); }
    }
}
