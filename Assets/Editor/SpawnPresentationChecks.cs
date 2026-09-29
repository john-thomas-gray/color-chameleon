using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class SpawnPresentationChecks
    {
        public static void Run()
        {
            var root = new GameObject("Spawn presentation fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            bool enabled = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            var random = UnityEngine.Random.state;
            try
            {
                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                grid.ConfigureAbilities(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Missile.prefab"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ShieldArc.png"));
                spawner.Configure(Prefab(EnemyColor.Blue), Prefab(EnemyColor.Red));
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow), Prefab(EnemyColor.Orange));
                SpawnOverride.Enabled = true;
                foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                {
                    if (color == EnemyColor.Orange) continue; // Orange isolation needs a sparse fixture; summon tint tests still cover it.
                    SpawnOverride.Types = 1 << (int)color;
                    Check(spawner.SpawnBatch(1), "Batch created");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                    {
                        VerifyArrival(enemy, color);
                        var ability = enemy.GetComponent<EnemyAbilities>();
                        ability.Tick(.4f);
                        Check(Mathf.Abs(enemy.GetComponentInChildren<SpriteRenderer>().color.a - .5f) < .001f, "Sprite phases in gradually");
                        if (color == EnemyColor.Blue)
                            Check(!enemy.Visuals.Root.Find("Shield").GetComponentInChildren<SpriteRenderer>().enabled && !ability.ShieldActive,
                                "Shield waits until its owner's arrival finishes");
                        ability.Tick(.4f);
                        Check(!enemy.GetComponent<EnemyPresentation>().IsPhasing && enemy.GetComponentInChildren<SpriteRenderer>().color.a == 1 &&
                            !enemy.Visuals.Root.Find("Summoning rift").GetComponent<LineRenderer>().enabled, "Phase effect completes cleanly");
                        if (color == EnemyColor.Blue)
                        {
                            Check(!ability.ShieldActive && !enemy.Visuals.Root.Find("Shield").GetComponentInChildren<SpriteRenderer>().enabled,
                                "Ordinary Blue remains unshielded after arrival");
                            ability.Tick(ability.CooldownRemaining);
                            ability.Tick(.35f);
                            Check(ability.ShieldActive, "Shield becomes protective after power-up");
                        }
                    }
                    Check(spawner.TryAdvance(), "New row created");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                        if (enemy.Row == 0) VerifyArrival(enemy, color);
                    var removed = grid.View(grid.Model.At(2, 0).Id);
                    grid.Unregister(removed); UnityEngine.Object.DestroyImmediate(removed.gameObject);
                    var summoned = spawner.TrySummon(grid.View(grid.Model.At(0, 0).Id));
                    Check(summoned != null, "Summon created");
                    VerifyArrival(summoned, color, true);
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                    { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
                    var opening = spawner.PlanOpening();
                    Check(opening.Length == 2 * RunProgress.StandardRowWidth, "Opening is two rows");
                    foreach (var entry in opening) Check(entry == color, "Opening respects spawn override");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                SpawnOverride.Enabled = enabled;
                SpawnOverride.Types = types;
                UnityEngine.Random.state = random;
            }
            RunSummonTint();
            Debug.Log("Spawn presentation checks passed: every color, rows, batches, summons, opening overrides, immediate transparency and shield fade.");
        }
        public static void RunSummonTint()
        {
            bool enabled = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            try
            {
                SpawnOverride.Enabled = true;
                foreach (bool special in new[] { false, true })
                foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    SpawnOverride.Types = 1 << (int)color;
                    var purple = ProgressionChecks.Add(grid, EnemyColor.Purple, 2, 1);
                    if (special)
                    {
                        ProgressionChecks.Add(grid, EnemyColor.Purple, 1, 1);
                        ProgressionChecks.Add(grid, EnemyColor.Purple, 2, 0);
                        grid.RefreshSpecials();
                    }
                    Check(purple.IsSpecial == special, "Summoner has the expected tier");
                    var summoned = grid.GetComponent<EnemyRowSpawner>().TrySummon(purple);
                    for (int attempt = 0; summoned == null && color == EnemyColor.Orange && attempt < 1000; attempt++)
                        summoned = grid.GetComponent<EnemyRowSpawner>().TrySummon(purple);
                    Check(summoned != null && summoned.Color == color && grid.Model.At(summoned.Column, summoned.Row).Color == color,
                        "Portal tint does not change actual enemy color");
                    var ability = summoned.GetComponent<EnemyAbilities>();
                    VerifyArrival(summoned, special ? EnemyColor.Purple : color, true);
                    var rift = summoned.Visuals.Root.Find("Summoning rift").GetComponent<LineRenderer>();
                    var expected = (Color32)Color.Lerp(EnemyPalette.Get(special ? EnemyColor.Purple : color), Color.white, .25f);
                    for (int i = 0; i < 3; i++)
                    {
                        ability.Tick(.2f);
                        var actual = (Color32)rift.startColor;
                        Check(rift.enabled && actual.r == expected.r && actual.g == expected.g && actual.b == expected.b,
                            "Summon tint persists throughout the phase animation");
                        var body = summoned.Visuals.Body.color;
                        var tint = EnemyPalette.Get(color);
                        Check(Vector3.Distance(new Vector3(body.r, body.g, body.b), new Vector3(tint.r, tint.g, tint.b)) < .001f,
                            "Arriving body retains its own color");
                    }
                    ability.Tick(.21f);
                    Check(!rift.enabled && !summoned.GetComponent<EnemyPresentation>().IsPhasing,
                        "Purple-tinted portal completes normally");
                    if (color == EnemyColor.Blue) Check(!ability.ShieldActive, "Summoned Blue still waits for its shield cooldown");
                });
            }
            finally { SpawnOverride.Enabled = enabled; SpawnOverride.Types = types; }
            Debug.Log("Summon tint checks passed: all colors, both Purple tiers, persistent portal tint, body identity and completion.");
        }
        private static void VerifyArrival(GridEnemy enemy, EnemyColor color, bool warp = false)
        {
            Check(enemy.GetComponent<EnemyPresentation>().IsPhasing && enemy.GetComponentInChildren<SpriteRenderer>().color.a == 0,
                "Spawn begins transparent without a full-opacity frame");
            var rift = enemy.Visuals.Root.Find("Summoning rift").GetComponent<LineRenderer>();
            if (!warp)
            {
                Check(!rift.enabled && !enemy.GetComponent<EnemyPresentation>().IsWarping &&
                    enemy.Visuals.Body.transform.localScale.sqrMagnitude < .000001f,
                    "Regular arrivals start at zero scale with no portal");
                return;
            }
            // LineRenderer stores gradient colors at byte precision.
            Color expected = (Color32)Color.Lerp(EnemyPalette.Get(color), Color.white, .25f);
            Check(rift.enabled && Vector3.Distance(new Vector3(rift.startColor.r, rift.startColor.g, rift.startColor.b),
                new Vector3(expected.r, expected.g, expected.b)) < .001f,
                $"Rift uses expected effect color: {color}, actual {rift.startColor}, expected {expected}, enabled {rift.enabled}");
        }
        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Spawn presentation check failed: " + message); }
    }
}
