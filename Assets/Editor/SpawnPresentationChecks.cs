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
                spawner.ConfigureNewTypes(Prefab(EnemyColor.Green), Prefab(EnemyColor.Purple), Prefab(EnemyColor.Yellow));
                SpawnOverride.Enabled = true;
                foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
                {
                    SpawnOverride.Types = 1 << (int)color;
                    Check(spawner.SpawnBatch(1), "Batch created");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                    {
                        VerifyArrival(enemy, color);
                        var ability = enemy.GetComponent<EnemyAbilities>();
                        ability.Tick(.4f);
                        Check(Mathf.Abs(enemy.GetComponent<SpriteRenderer>().color.a - .5f) < .001f, "Sprite phases in gradually");
                        if (color == EnemyColor.Blue)
                            Check(Mathf.Abs(enemy.transform.Find("Shield").GetComponent<SpriteRenderer>().color.a - .45f) < .001f,
                                "Shield phases in with its owner");
                        ability.Tick(.4f);
                        Check(!enemy.GetComponent<EnemyPresentation>().IsPhasing && enemy.GetComponent<SpriteRenderer>().color.a == 1 &&
                            !enemy.transform.Find("Summoning rift").GetComponent<LineRenderer>().enabled, "Phase effect completes cleanly");
                    }
                    Check(spawner.TryAdvance(), "New row created");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                        if (enemy.Row == 0) VerifyArrival(enemy, color);
                    var removed = grid.View(grid.Model.At(2, 0).Id);
                    grid.Unregister(removed); UnityEngine.Object.DestroyImmediate(removed.gameObject);
                    var summoned = spawner.TrySummon(grid.View(grid.Model.At(0, 0).Id));
                    Check(summoned != null, "Summon created");
                    VerifyArrival(summoned, color);
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                    { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
                    var opening = spawner.PlanOpening();
                    Check(opening.Length == 2 * GridModel.Columns, "Opening is two rows");
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
            Debug.Log("Spawn presentation checks passed: every color, rows, batches, summons, opening overrides, immediate transparency and shield fade.");
        }
        private static void VerifyArrival(GridEnemy enemy, EnemyColor color)
        {
            Check(enemy.GetComponent<EnemyPresentation>().IsPhasing && enemy.GetComponent<SpriteRenderer>().color.a == 0,
                "Spawn begins transparent without a full-opacity frame");
            var rift = enemy.transform.Find("Summoning rift").GetComponent<LineRenderer>();
            // LineRenderer stores gradient colors at byte precision.
            Color expected = (Color32)Color.Lerp(EnemyPalette.Get(color), Color.white, .25f);
            Check(rift.enabled && Vector3.Distance(new Vector3(rift.startColor.r, rift.startColor.g, rift.startColor.b),
                new Vector3(expected.r, expected.g, expected.b)) < .001f,
                $"Rift uses arriving enemy's color: {color}, actual {rift.startColor}, expected {expected}, enabled {rift.enabled}");
        }
        private static GameObject Prefab(EnemyColor color) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + color + " Enemy.prefab");
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Spawn presentation check failed: " + message); }
    }
}
