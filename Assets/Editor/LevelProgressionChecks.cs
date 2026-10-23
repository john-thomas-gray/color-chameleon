using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class LevelProgressionChecks
    {
        public const string FixtureJson = @"{
  ""schemaVersion"": 1,
  ""levels"": {
    ""1"": {
      ""enemiesToDefeat"": 13,
      ""numberOfColors"": 3,
      ""possibleColors"": [
        ""Red"",
        ""Blue"",
        ""Green"",
        ""Purple"",
        ""Yellow""
      ],
      ""requiredColors"": [
        ""Green"",
        ""Purple""
      ],
      ""backgroundImage"": """",
      ""possibleSoundtrackTracks"": [
        ""AnotherJoe""
      ],
      ""fleetSpawns"": [
        ""011223/331120/022331/113320"",
        ""031122/223310""
      ],
      ""lineSpawns"": [
        ""022113"",
        ""011332""
      ]
    },
    ""2"": {
      ""enemiesToDefeat"": 37,
      ""numberOfColors"": 1,
      ""possibleColors"": [
        ""Blue""
      ],
      ""requiredColors"": [
        ""Blue""
      ],
      ""backgroundImage"": """",
      ""possibleSoundtrackTracks"": [
        ""Contact""
      ],
      ""fleetSpawns"": [
        ""0bbbbb/0bbbbb/0bbbbb""
      ],
      ""lineSpawns"": [
        ""0bbbbb""
      ]
    }
  }
}";
        [MenuItem("Candy Cruisers/Run Level Map Checks")]
        public static void Run()
        {
            var random = UnityEngine.Random.state;
            bool previousOverride = SpawnOverride.Enabled;
            try
            {
                SpawnOverride.Enabled = false;
                UnityEngine.Random.InitState(673091);
                var catalog = LevelCatalog.Parse(FixtureJson);
                Check(catalog.ForLevel(1) == catalog.levels[0] && catalog.ForLevel(99) == catalog.levels[1], "Numbered map and final-level inheritance");
                CheckValidation();
                CheckDefeatTargets(catalog.levels[0], catalog.levels[1], catalog);
                CheckScripts();
                CheckSequences();
                CheckBackground(catalog, catalog.levels[0], catalog.levels[1]);
                var saved = LevelCatalog.Parse(File.ReadAllText(LevelCatalog.FilePath));
                Check(saved.ValidationErrors().Count == 0, "The actual saved level map validates");
                Debug.Log("Level map checks passed: JSON parsing, invalid settings, sparse fleets, exact letters, random palettes, required colors, round bindings, independent list loops, cleared locks, restart/level resets, immutable plans, defeat targets, progress bars and desktop/phone backgrounds.");
            }
            finally { SpawnOverride.Enabled = previousOverride; UnityEngine.Random.state = random; }
        }

        private static void CheckValidation()
        {
            Invalid(root => root["levels"]["1"]["initialWaveLines"] = 2, "Unknown setting");
            Invalid(root => root["levels"]["1"]["enemiesToDefeat"] = 0, "enemiesToDefeat");
            Invalid(root => root["levels"]["1"]["possibleColors"] = new JArray("Red", "beige"), "unknown color");
            Invalid(root => root["levels"]["1"]["fleetSpawns"] = new JArray("000000"), "at least one enemy");
            Invalid(root => root["levels"]["1"]["fleetSpawns"] = new JArray("00000q"), "unknown character");
            Invalid(root => root["levels"]["1"]["fleetSpawns"] = new JArray("12345/123456"), "same row width");
            Invalid(root => root["levels"]["1"]["lineSpawns"] = new JArray("123123/123123"), "row count");
            Invalid(root => root["levels"]["1"]["numberOfColors"] = 1, "requiredColors");
            Invalid(root => root["levels"]["2"]["fleetSpawns"] = new JArray("123123"), "numbered groups");
            Invalid(root => ((JObject)root["levels"]).Property("1").Remove(), "starting at 1");
            try { LevelCatalog.Parse(FixtureJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1")); throw new Exception("Duplicate keys accepted"); }
            catch (FormatException) { }
        }
        private static void Invalid(Action<JObject> change, string expected)
        {
            var root = JObject.Parse(FixtureJson); change(root);
            try { LevelCatalog.Parse(root.ToString()); throw new Exception("Invalid map accepted: " + expected); }
            catch (FormatException error) { Check(error.Message.Contains(expected), "Actionable validation: " + expected + " got " + error.Message); }
        }

        private static void CheckScripts()
        {
            var level = new LevelDefinition { numberOfColors = 3,
                possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Purple, EnemyColor.Yellow },
                requiredColors = new[] { EnemyColor.Purple }, fleetSpawns = new[] { "312213" }, lineSpawns = new[] { "123123" } };
            Check(level.ValidationErrors().Count == 0, "Random script is valid");
            var assignments = new System.Collections.Generic.HashSet<string>();
            for (int sample = 0; sample < 80; sample++)
            {
                var plan = SpawnScript.Plan(level, "312213"); var cells = plan.CopyCells();
                Check(cells[0] == cells[5] && cells[1] == cells[4] && cells[2] == cells[3] && plan.Colors.Count == 3 &&
                    plan.Colors.Contains(EnemyColor.Purple), "Equal digits agree, different digits differ, and required colors appear");
                var line = SpawnScript.Decode("123123", plan.Bindings);
                Check(line[0] == cells[1] && line[1] == cells[2] && line[2] == cells[0], "Incoming lines reuse the fleet mapping");
                assignments.Add(string.Join(",", cells));
            }
            Check(assignments.Count > 1, "New rounds reroll their assignments");
            level.numberOfColors = 2; level.possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue };
            level.requiredColors = Array.Empty<EnemyColor>(); level.fleetSpawns = new[] { "0R0r0R/b0B0b0" }; level.lineSpawns = new[] { "000000" };
            var sparse = SpawnScript.Plan(level, level.fleetSpawns[0]);
            Check(sparse.Columns == 6 && sparse.Rows == 2 && sparse.CellCount == 6 && sparse.CopyCells()[0] == null &&
                sparse.CopyCells()[7] == null && sparse.PlayerColorWeights().Count == 2 &&
                sparse.PlayerColorWeights()[EnemyColor.Blue] == 6, "Blanks preserve positions, row depth, occupied counts and player weights");
            var copied = sparse.CopyCells(); copied[0] = EnemyColor.Green;
            Check(sparse.CopyCells()[0] == null, "A copied cell array cannot mutate a reserved fleet");
            level.numberOfColors = 6; level.possibleColors = EnemyPalette.Colors.ToArray();
            level.fleetSpawns = new[] { "o11223/334455" }; level.lineSpawns = new[] { "112233" };
            var orange = SpawnScript.Plan(level, level.fleetSpawns[0]);
            Check(orange != null && orange.OccupiedColors().Count(c => c == EnemyColor.Orange) == 1 &&
                !orange.Bindings.Values.Contains(EnemyColor.Orange), "A literal Orange stays unique across random color groups");
        }

        private static void CheckSequences()
        {
            SpecialEnemyChecks.Fixture((grid, player, tongue) =>
            {
                var session = grid.gameObject.AddComponent<GameSession>(); session.Configure(player);
                var first = new LevelDefinition { numberOfColors = 2,
                    possibleColors = new[] { EnemyColor.Red, EnemyColor.Blue, EnemyColor.Green },
                    fleetSpawns = new[] { "101020/200010", "020101" }, lineSpawns = new[] { "100002", "020010" } };
                var second = new LevelDefinition { fleetSpawns = new[] { "0r0b00" }, lineSpawns = new[] { "b0000r" } };
                session.LevelCatalog = new LevelCatalog { levels = new[] { first, second } };
                var spawner = grid.GetComponent<EnemyRowSpawner>();
                var opening = spawner.PlanNextFleet();
                Check(opening.Rows == 2 && opening.CellCount == 5 && spawner.SpawnBatch(opening), "First fleet uses entry zero with exact gaps");
                for (int i = 0; i < 3; i++)
                {
                    Check(spawner.TryAdvance(), "Scripted top row advances");
                    VerifyRow(grid, first.lineSpawns[i % 2], opening);
                }
                Empty(grid);
                var next = spawner.PlanNextFleet();
                Check(next.Rows == 1 && next.CellCount == 3 && spawner.SpawnBatch(next), "The next fleet uses its own row count");
                Check(spawner.TryAdvance(), "Next round has an incoming line");
                VerifyRow(grid, first.lineSpawns[1], next);
                Empty(grid);
                var wrapped = spawner.PlanNextFleet();
                Check(wrapped.Rows == 2 && wrapped.CellCount == 5, "Fleet list wraps to its first entry independently of the line list");
                var copiedBindings = wrapped.Bindings.ToDictionary(pair => pair.Key, pair => pair.Value);
                first.lineSpawns[0] = "000000";
                Check(wrapped.LineSpawns[0] == "100002" && wrapped.Bindings.All(pair => copiedBindings[pair.Key] == pair.Value),
                    "Pending line scripts and mappings survive edits");
                session.Progress.Reset(2);
                var levelTwo = spawner.PlanNextFleet();
                Check(spawner.SpawnBatch(levelTwo) && spawner.TryAdvance(), "Entering a level resets both cursors");
                VerifyRow(grid, "b0000r", levelTwo);
                foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == EnemyColor.Red).ToArray())
                    if (grid.View(enemy.Id) != null) grid.ClearMatchingChain(enemy.Id, enemy.Color);
                Check(grid.IsColorCleared(EnemyColor.Red) && spawner.TryAdvance() && grid.Fleet.ColorCount(EnemyColor.Red) == 0 &&
                    grid.Model.At(5, 0) == null, "Cleared colors leave holes instead of changing their assignments or respawning");
                Empty(grid); session.Progress.Reset(1); spawner.ResetSequences();
                Check(spawner.PlanNextFleet().Rows == 2, "A run reset restarts the fleet sequence");
            });
        }
        private static void VerifyRow(EnemyGrid grid, string pattern, FleetPlan round)
        {
            var expected = SpawnScript.Decode(pattern, round.Bindings);
            for (int column = 0; column < expected.Length; column++)
                Check(grid.Model.At(column, 0)?.Color == expected[column], "Exact top-row positions and round bindings");
        }
        private static void Empty(EnemyGrid grid)
        {
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            { grid.Unregister(enemy); UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        }
        private static void CheckDefeatTargets(LevelDefinition first, LevelDefinition second, LevelCatalog catalog)
        {
            try
            {
                first.enemiesToDefeat = 7; second.enemiesToDefeat = 11;
                catalog.levels = new[] { first, second };
                var progress = new RunProgress { LevelCatalog = catalog };
                progress.Reset();
                Check(progress.PreviousThreshold == 0 && progress.NextThreshold == 7 && progress.EarnedLevel == 1,
                    "Level one uses its configured defeat count");
                progress.RegisterClear(6, false, 600);
                Check(progress.Defeated == 6 && progress.EarnedLevel == 1 &&
                    Mathf.Abs(progress.ActiveLevelProgressFraction - 6f / 7) < .0001f,
                    "Progress counts defeated enemies independently of points and score multipliers");
                progress.RegisterClear(1, false);
                Check(progress.Level == 1 && progress.EarnedLevel == 2 && progress.LevelUpPending &&
                    progress.ActiveLevelProgressFraction == 1 && progress.BankedLevelProgressFraction == 0,
                    "Exactly reaching a custom target banks advancement until the fleet clears");
                progress.RegisterClear(4, false);
                Check(Mathf.Abs(progress.BankedLevelProgressFraction - 4f / 11) < .0001f &&
                    progress.EarnedPreviousThreshold == 7 && progress.EarnedNextThreshold == 18,
                    "Banked progress uses the next level's configured target");
                progress.RegisterClear(1, true);
                Check(progress.Level == 2 && progress.Defeated == 12 && progress.PreviousThreshold == 7 && progress.NextThreshold == 18,
                    "Fleet clear advances with excess defeats carried into the next level");
                progress.RegisterClear(6, true);
                Check(progress.Level == 3 && progress.PreviousThreshold == 18 && progress.NextThreshold == 29,
                    "Levels beyond the catalog repeat its final defeat target");
                progress.RegisterClear(22, false);
                Check(progress.Level == 3 && progress.EarnedLevel == 5, "Multiple custom targets can bank within one fleet");
                progress.RegisterClear(1, true);
                Check(progress.Level == 5 && progress.PreviousThreshold == 40 && progress.NextThreshold == 51,
                    "A fleet clear commits all earned levels using inherited targets");
                progress.Reset(3);
                Check(progress.Level == 3 && progress.Defeated == 18 && progress.Score == 0 &&
                    progress.ActiveLevelProgressFraction == 0, "Starting at a later level begins at its custom cumulative baseline");
                catalog.levels = new[] { second, first };
                progress.Reset(2);
                Check(catalog.ForLevel(1) == second && catalog.ForLevel(2) == first && progress.Defeated == 11 && progress.NextThreshold == 18,
                    "Reordering catalog entries changes their numbers and target sequence together");
                catalog.levels = new[] { first, second }; first.enemiesToDefeat = 1; second.enemiesToDefeat = 1;
                Check(RunProgress.LevelForDefeated(1000000, catalog) == 1000001 &&
                    RunProgress.NextThresholdForLevel(1000000, catalog) == 1000000,
                    "Small inherited targets support distant levels without walking every missing level");
                progress.LevelCatalog = null; progress.Reset(2);
                Check(progress.Defeated == 20 && progress.NextThreshold == 60, "No catalog retains legacy progression");
            }
            finally
            {
                first.enemiesToDefeat = 20; second.enemiesToDefeat = 40;
                catalog.levels = new[] { first, second };
            }
        }

        private static void CheckBackground(LevelCatalog catalog, LevelDefinition first, LevelDefinition second)
        {
            var root = new GameObject("Level background camera", typeof(Camera), typeof(GameplayFraming));
            var background = new GameObject("Level background", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            var sessionRoot = new GameObject("Level background session", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            var texture = new Texture2D(16, 8);
            var image = Sprite.Create(texture, new Rect(0, 0, 16, 8), new Vector2(.1f, .8f), 8);
            try
            {
                var session = sessionRoot.AddComponent<GameSession>(); session.LevelCatalog = catalog;
                var framing = root.GetComponent<GameplayFraming>();
                var camera = root.GetComponent<Camera>(); camera.orthographic = true;
                framing.SetSession(session); framing.SetBackground(background);
                first.backgroundImage = image;
                foreach (var screen in new[] { new Vector2(1920, 1080), new Vector2(540, 960), new Vector2(1206, 2622) })
                {
                    camera.aspect = screen.x / screen.y;
                    GameplayFraming.ForceIphoneNotchOffsetForTests = screen.x == 1206;
                    framing.SetWaveCrunch(1);
                    framing.TickBackground(5);
                    Check(background.enabled && background.sprite == image && background.color == Color.white,
                        "Configured image overrides black mode without tinting");
                    var bounds = background.bounds;
                    Check(camera.WorldToViewportPoint(bounds.min).x <= .0001f && camera.WorldToViewportPoint(bounds.min).y <= .0001f &&
                        camera.WorldToViewportPoint(bounds.max).x >= .9999f && camera.WorldToViewportPoint(bounds.max).y >= .9999f,
                        "Image covers desktop and phone aspect ratios, including a custom pivot and notch offset");
                    var position = background.transform.position;
                    framing.TickBackground(5);
                    Check(background.transform.position == position && background.transform.rotation == Quaternion.identity,
                        "Repeated updates do not drift or rotate configured art");
                    framing.SetWaveCrunch(0);
                }
                session.Progress.Reset(2); framing.TickBackground(0);
                Check(!background.enabled && camera.backgroundColor == Color.black, "Unassigned next-level image restores black");
            }
            finally
            {
                first.backgroundImage = null;
                GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(background.gameObject);
                UnityEngine.Object.DestroyImmediate(sessionRoot); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Level map check failed: " + message); }
    }
}
