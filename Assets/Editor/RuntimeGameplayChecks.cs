using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class RuntimeGameplayChecks
    {
        private const string PendingKey = "CandyCruisers.RuntimeChecks";
        private static int phase;
        private static double started;
        private static PlayerMovement player;
        private static TongueShot tongue;
        private static EnemyGrid grid;
        private static Vector3 gridStart;
        private static GridEnemy retreatingEnemy;
        private static double gameOverAt;
        private static bool previewOnly;
        private static bool observedEmptyOpening;
        private static bool observedEmptyRestart;
        private static int matchedCount;

        static RuntimeGameplayChecks() => EditorApplication.update += Poll;

        public static void Run()
        {
            SessionState.SetBool("CandyCruisers.Tests.PreviousSpawnOverride", SpawnOverride.Enabled);
            SpawnOverride.Enabled = false;
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }

        public static void Preview()
        {
            SessionState.SetBool("CandyCruisers.AbilityPreview", true);
            Run();
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            if (Time.timeSinceLevelLoad < 0.1f) return;
            try
            {
                if (phase == 0)
                {
                    grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
                    player = GameObject.Find("Player").GetComponent<PlayerMovement>();
                    tongue = player.GetComponentInChildren<TongueShot>();
                    if (grid.GetComponent<GameSession>().State == GameSession.RunState.Refilling)
                    {
                        Require(grid.Model.Count == 0 && !player.Fire() && !player.ControlsLocked && player.ReadyColor.HasValue,
                            "Opening starts empty, with movement and a planned player color");
                        if (!observedEmptyOpening)
                            ScreenCapture.CaptureScreenshot("TestResults/runtime-empty-opening.png");
                        observedEmptyOpening = true;
                        return;
                    }
                    Require(observedEmptyOpening, "Empty opening is visible before first spawn");
                    foreach (var visual in grid.GetComponentsInChildren<EnemyPresentation>())
                        Require(visual.IsPhasing, "Opening enemies arrive through rifts");
                    if (SessionState.GetBool("CandyCruisers.AbilityPreview", false))
                    {
                        previewOnly = true;
                        SessionState.SetBool("CandyCruisers.AbilityPreview", false);
                        PreparePreview();
                        started = gameOverAt = EditorApplication.timeSinceStartup;
                        phase = 13;
                        return;
                    }
                    Require(grid.Model.Count == 2 * GridModel.Columns && player.ReadyColor.HasValue, "Awake/Start initialize scene");
                    var enemy = grid.GetComponentInChildren<GridEnemy>();
                    enemy.gameObject.SetActive(false);
                    Require(grid.Model.Count == 2 * GridModel.Columns - 1, "Disabled enemy unregisters");
                    enemy.gameObject.SetActive(true);
                    Require(grid.Model.Count == 2 * GridModel.Columns, "Re-enabled enemy registers once");
                    player.transform.position = new Vector3(2.9f, player.transform.position.y, 0);
                    Require(player.Fire() && !player.Fire(), "Runtime shot lock");
                    gridStart = grid.transform.position;
                    started = EditorApplication.timeSinceStartup;
                    phase = 1;
                }
                else if (phase == 1 && EditorApplication.timeSinceStartup - started > 0.2)
                {
                    ScreenCapture.CaptureScreenshot("TestResults/runtime-opening-rifts.png");
                    Require(tongue.Active && tongue.Length > 0, "Update extends tongue");
                    Require(grid.transform.position != gridStart, "Fleet continues moving");
                    phase = 2;
                }
                else if (phase == 2 && !tongue.Active)
                {
                    Require(player.ReadyColor.HasValue && player.Fire(), "Update returns tongue and enables next shot");
                    tongue.Tick(10);
                    grid.GetComponent<EnemyGridMovement>().enabled = false;
                    var target = grid.GetComponentsInChildren<GridEnemy>()
                        .Where(enemy => enemy.Color == player.ReadyColor.Value).OrderByDescending(enemy => enemy.Row).First();
                    matchedCount = grid.GetComponentsInChildren<GridEnemy>().Count(enemy => enemy.Row == target.Row && enemy.Color == target.Color);
                    player.transform.position = new Vector3(target.transform.position.x, player.transform.position.y, 0);
                    Require(player.Fire(), "Aim matching shot at an enemy");
                    phase = 3;
                }
                else if (phase == 3 && !tongue.Active)
                {
                    Require(grid.Model.Count == 2 * GridModel.Columns - matchedCount && grid.GetComponentsInChildren<GridEnemy>().Length == 2 * GridModel.Columns - matchedCount,
                        "Runtime hit clears the contiguous group from scene and model");
                    Require(grid.GetComponent<GameSession>().Progress.Score == 100 * matchedCount &&
                        grid.GetComponent<GameSession>().Progress.Defeated == matchedCount, "Real hits update score and progression once");
                    Require(player.ReadyColor.HasValue && player.Fire(), "Can refire after successful chain");
                    tongue.Tick(10);
                    player.enabled = false;
                    grid.GetComponent<EnemyGridMovement>().enabled = true;
                    phase = 4;
                }
                else if (phase == 4 && grid.Model.Count > 2 * GridModel.Columns - matchedCount)
                {
                    Require(grid.Model.Count == 3 * GridModel.Columns - matchedCount && grid.GetComponentsInChildren<GridEnemy>().Length == 3 * GridModel.Columns - matchedCount,
                        "Right sweep end descends and creates exactly five enemies");
                    for (int column = 0; column < GridModel.Columns; column++)
                        Require(grid.Model.At(column, 0) != null, "New top row is filled");
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().Where(enemy => enemy.Row == 0))
                        Require(enemy.GetComponent<EnemyAbilities>() != null, "Spawned enemies receive abilities");
                    Require(grid.GetComponentsInChildren<GridEnemy>().Count(enemy => enemy.Row > 0) == 2 * GridModel.Columns - matchedCount,
                        "Survivors descend without duplication");
                    phase = 5;
                }
                else if (phase == 5 && grid.Model.Count > 3 * GridModel.Columns - matchedCount)
                {
                    Require(grid.Model.Count == 4 * GridModel.Columns - matchedCount && grid.GetComponentsInChildren<GridEnemy>().Length == 4 * GridModel.Columns - matchedCount,
                        "Left sweep end adds exactly one more row");
                    grid.GetComponent<EnemyGridMovement>().enabled = false;
                    var enemies = grid.GetComponentsInChildren<GridEnemy>();
                    var anchor = enemies.First(enemy => enemy.Column == 0 && enemy.Row == 0);
                    var bridge = enemies.First(enemy => enemy.Column == 0 && enemy.Row == 1);
                    retreatingEnemy = enemies.First(enemy => enemy.Column == 0 && enemy.Row == 2);
                    foreach (var enemy in enemies)
                        if (enemy != anchor && enemy != bridge && enemy != retreatingEnemy) enemy.gameObject.SetActive(false);
                    grid.SetColor(anchor.Id, EnemyColor.Blue);
                    grid.SetColor(bridge.Id, EnemyColor.Red);
                    grid.SetColor(retreatingEnemy.Id, EnemyColor.Blue);
                    Require(grid.ClearMatchingChain(bridge.Id, EnemyColor.Red) == 1, "Clear connecting bridge");
                    phase = 6;
                }
                else if (phase == 6 && retreatingEnemy.Row == 1)
                {
                    Require(grid.Model.Count == 2 && grid.Model.At(0, 1).Id == retreatingEnemy.Id,
                        "Disconnected enemy reconnects without being removed");
                    Require(Vector3.Distance(retreatingEnemy.transform.localPosition, grid.CellPosition(0, 1)) < 0.0001f,
                        "Retreat view follows logical cell");
                    var blueAbility = retreatingEnemy.GetComponent<EnemyAbilities>();
                    Require(blueAbility.ShieldActive && blueAbility.Absorb(EnemyColor.Red), "Live Blue shield absorbs mismatch");
                    blueAbility.Tick(76);
                    Require(blueAbility.ShieldActive, "Live shield recharges");
                    foreach (var missile in UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None))
                    { missile.gameObject.SetActive(false); UnityEngine.Object.Destroy(missile.gameObject); }
                    var shooter = grid.GetComponentsInChildren<GridEnemy>().First(enemy => enemy != retreatingEnemy);
                    grid.SetColor(shooter.Id, EnemyColor.Red);
                    shooter.GetComponent<EnemyAbilities>().Tick(25);
                    var fired = UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None);
                    Require(fired.Length == 1, "Live Red emits a missile");
                    shooter.GetComponent<EnemyAbilities>().enabled = false;
                    player.TickSurvival(10);
                    player.enabled = true;
                    fired[0].transform.position = player.transform.position + Vector3.up;
                    phase = 7;
                }
                else if (phase == 7 && !player.Alive)
                {
                    Require(!player.Fire() && !tongue.Active, "Missile hit disables firing and cancels tongue");
                    phase = 8;
                }
                else if (phase == 8 && player.Alive)
                {
                    Require(player.Invulnerable && !player.Hit() && player.Fire(), "Player respawns protected and can fire");
                    tongue.Cancel();
                    phase = 9;
                }
                else if (phase == 9 && !player.Invulnerable)
                {
                    Require(player.Alive, "Player protection expires normally");
                    var remaining = grid.GetComponentsInChildren<GridEnemy>();
                    foreach (var enemy in remaining) grid.SetColor(enemy.Id, EnemyColor.Red);
                    player.CancelShot();
                    var finalTarget = remaining.OrderByDescending(enemy => enemy.Row).First();
                    player.transform.position = new Vector3(finalTarget.transform.position.x, player.transform.position.y, 0);
                    player.RefreshColor(true);
                    Require(player.Fire(), "Fire the final fleet-clearing shot");
                    float contact = finalTarget.GetComponent<SpriteRenderer>().bounds.min.y - tongue.transform.position.y;
                    tongue.Tick(contact / 14f + .005f, grid);
                    Require(grid.Model.Count == 0 && tongue.Active && tongue.Retracting,
                        "Final live hit clears fleet and keeps the tongue visibly returning");
                    Require(grid.GetComponent<GameSession>().State == GameSession.RunState.Refilling &&
                        !player.Fire() && !grid.GetComponent<EnemyRowSpawner>().TryAdvance(), "Refill pauses firing and ordinary rows");
                    Require(!player.ControlsLocked, "Movement remains unlocked during refill");
                    var beforeRefillMove = player.transform.position;
                    player.Move(1, 0.2f);
                    Require(Mathf.Abs(player.transform.position.x - PlayerMovement.Wrap(beforeRefillMove.x + 1f)) < 0.001f,
                        "Player moves and wraps during refill");
                    var pointer = Camera.main.WorldToScreenPoint(new Vector3(0, player.transform.position.y, 0));
                    player.BeginPointer(pointer);
                    player.UpdatePointer(pointer);
                    phase = 10;
                }
                else if (phase == 10 && grid.GetComponent<GameSession>().State == GameSession.RunState.Playing)
                {
                    Require(grid.Model.Count == 3 * GridModel.Columns && grid.GetComponentsInChildren<GridEnemy>().Length == 3 * GridModel.Columns,
                        "Delayed refill creates three five-enemy rows");
                    Require(player.ReadyColor.HasValue && grid.Model.ColorCount(player.ReadyColor.Value) > 0 &&
                        player.DisplayColor != Color.gray && !tongue.Active, "Returned player color exists in the live next batch");
                    Require(!grid.IsColorCleared(EnemyColor.Red) && !grid.IsColorCleared(EnemyColor.Blue), "Live refill resets color locks");
                    Require(Mathf.Abs(grid.transform.position.x) < 0.1f && !player.ControlsLocked,
                        "Refill resets fleet and restores controls");
                    Require(Mathf.Abs(player.transform.position.x) < 0.1f, "Pointer movement continues during refill");
                    player.EndPointer(Camera.main.WorldToScreenPoint(player.transform.position), true);
                    Require(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == 0,
                        "Old missiles do not survive refill");
                    var spawner = grid.GetComponent<EnemyRowSpawner>();
                    for (int i = 0; i < 7; i++) Require(spawner.TryAdvance(), "Fill new fleet");
                    Require(!spawner.TryAdvance() && grid.GetComponent<GameSession>().State == GameSession.RunState.GameOver,
                        "Bottom boundary ends game");
                    Require(grid.Model.Count == GridModel.Columns * GridModel.Rows && !player.Fire() && !grid.enabled && !grid.GetComponent<EnemyGridMovement>().enabled,
                        "Game over freezes play without losing enemies");
                    foreach (var ability in grid.GetComponentsInChildren<EnemyAbilities>())
                        Require(ability.Suspended, "Game over suspends enemy abilities");
                    Require(!spawner.TryAdvance() && grid.Model.Count == GridModel.Columns * GridModel.Rows, "Game over rejects more rows");
                    ScreenCapture.CaptureScreenshot("TestResults/game-over.png");
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 11;
                }
                else if (phase == 11 && EditorApplication.timeSinceStartup - gameOverAt > 0.5)
                {
                    grid.GetComponent<GameSession>().Restart();
                    phase = 12;
                }
                else if (phase == 12)
                {
                    grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
                    player = GameObject.Find("Player").GetComponent<PlayerMovement>();
                    tongue = player.GetComponentInChildren<TongueShot>();
                    if (grid.GetComponent<GameSession>().State == GameSession.RunState.Refilling)
                    {
                        Require(grid.Model.Count == 0, "Restart also begins empty");
                        observedEmptyRestart = true;
                        return;
                    }
                    Require(observedEmptyRestart, "Restart repeats the empty opening");
                    Require(grid.Model.Count == 2 * GridModel.Columns && grid.GetComponent<GameSession>().State == GameSession.RunState.Playing,
                        "Restart restores opening fleet and session");
                    Require(grid.GetComponent<GameSession>().Progress.Score == 0 && grid.GetComponent<GameSession>().Progress.Level == 1,
                        "Restart resets score and level");
                    Require(player.MagicCharges == 0 && Mathf.Abs(grid.GetComponent<EnemyGridMovement>().CurrentSpeed - .3f) < .001f,
                        "Restart resets magic and fleet speed");
                    Require(player.Alive && !player.Invulnerable && !player.ControlsLocked && player.Fire(),
                        "Restart restores player and firing");
                    Require(grid.enabled && grid.GetComponent<EnemyGridMovement>().enabled && grid.GetComponent<EnemyRowSpawner>().enabled,
                        "Restart restores movement and spawning");
                    Require(UnityEngine.Object.FindObjectsByType<EnemyMissile>(FindObjectsSortMode.None).Length == 0,
                        "Restart clears projectiles");
                    player.StopActions();
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
                        if (enemy.Column != 2) enemy.gameObject.SetActive(false);
                    var sparseMovement = grid.GetComponent<EnemyGridMovement>();
                    sparseMovement.ResetSweep();
                    sparseMovement.Tick(2.5);
                    Require(grid.Model.Count == 2 && sparseMovement.Direction == 1,
                        "Empty leading columns cause no descent at former full-fleet boundary");
                    sparseMovement.Tick(6.25);
                    Require(grid.Model.Count == 2 + GridModel.Columns && sparseMovement.Direction == -1,
                        "Occupied column contact triggers exactly one live descent and row");
                    foreach (var survivor in grid.GetComponentsInChildren<GridEnemy>().Where(enemy => enemy.Row > 0))
                        Require(survivor.Column == GridModel.Columns - 1 && Mathf.Abs(survivor.transform.position.x - 2.625f) < 0.001f,
                            "Live reindex preserves survivor contact position");
                    grid.OccupiedHorizontalBounds(out float fleetLeft, out float fleetRight);
                    Require(fleetLeft >= -3.0001f && fleetRight <= 3.0001f, "New live row stays inside border");
                    PreparePreview();
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 13;
                }
                else if (phase == 13 && EditorApplication.timeSinceStartup - gameOverAt > .15)
                {
                    ScreenCapture.CaptureScreenshot("TestResults/ability-feedback.png");
                    gameOverAt = EditorApplication.timeSinceStartup;
                    phase = 14;
                }
                else if (phase == 14 && EditorApplication.timeSinceStartup - gameOverAt > .5)
                {
                    SessionState.SetBool(PendingKey, false);
                    SpawnOverride.Enabled = SessionState.GetBool("CandyCruisers.Tests.PreviousSpawnOverride", false);
                    Debug.Log(previewOnly ? "Ability preview captured." :
                        "Runtime gameplay checks passed: core gameplay, abilities, restart, score and occupied-column descent with safe row alignment.");
                    EditorApplication.Exit(0);
                }
                if (phase > 0 && EditorApplication.timeSinceStartup - started > 35)
                    throw new Exception("Runtime shot failed to return within timeout.");
            }
            catch (Exception error)
            {
                SessionState.SetBool(PendingKey, false);
                SpawnOverride.Enabled = SessionState.GetBool("CandyCruisers.Tests.PreviousSpawnOverride", false);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void PreparePreview()
        {
            var movement = grid.GetComponent<EnemyGridMovement>();
            movement.ResetSweep();
            movement.enabled = false;
            grid.enabled = false;
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>())
            { enemy.gameObject.SetActive(false); UnityEngine.Object.Destroy(enemy.gameObject); }
            var session = grid.GetComponent<GameSession>();
            session.Progress.RegisterClear(269, false);
            var colors = new[] { EnemyColor.Blue, EnemyColor.Blue, EnemyColor.Green, EnemyColor.Red, EnemyColor.Purple, EnemyColor.Yellow };
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < GridModel.Columns; column++)
                ProgressionChecks.Add(grid, colors[(column + row * 2) % 6], column, row);
            var single = grid.View(grid.Model.At(2, 0).Id);
            grid.ClearMatchingChain(single.Id, EnemyColor.Green);
            Require(session.Progress.Level == 6, "Live clear crosses Yellow unlock threshold");
            Require(Mathf.Abs(movement.CurrentSpeed - .345f) < .001f, "Live level increase scales base speed");
            if (previewOnly) session.Progress.RegisterClear(30, false);
            var enemies = grid.GetComponentsInChildren<GridEnemy>();
            var yellow = enemies.First(e => e.Color == EnemyColor.Yellow && e.Row == 1);
            var neighbor = grid.View(grid.Model.Neighbors(yellow.Column, yellow.Row).First(n => n.Color != EnemyColor.Yellow).Id);
            var oldSprite = yellow.GetComponent<SpriteRenderer>().sprite;
            grid.SetColor(yellow.Id, neighbor.Color);
            yellow.GetComponent<EnemyAbilities>().Tick(0);
            yellow.GetComponent<EnemyPresentation>().BeginImitation(neighbor, oldSprite);
            foreach (var enemy in enemies)
            {
                var visual = enemy.GetComponent<EnemyPresentation>();
                if (enemy.Row == 2) visual.SpeedShift(1);
                if (enemy.Color == EnemyColor.Red && enemy.Row == 0) visual.PhaseIn(enemy.Color);
                visual.Tick(enemy == yellow ? .65f : .15f, enemy.Color, enemy.Color == EnemyColor.Purple || enemy.Color == EnemyColor.Green ? 1 : 0);
                enemy.GetComponent<EnemyAbilities>().Suspended = true;
            }
            player.RefreshColor(true);
            foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>().Where(e => e.Color == EnemyColor.Yellow))
                grid.ClearMatchingChain(enemy.Id, EnemyColor.Yellow);
            Require(player.MagicCharges == 1, "Live last-color clear awards magic");
            Require(grid.IsColorCleared(EnemyColor.Yellow) && grid.SeenColors().Count == 5,
                "Live color clear earns one of five encountered-color segments");
            player.transform.position = new Vector3(0, -4.6f, 0);
            Require(player.Fire() && tongue.IsMagic && player.MagicCharges == 0, "Live next shot consumes magic");
            tongue.Tick(.125f, grid);
            player.RefreshPresentation(.81f);
            Require(session.ProgressBarColor == player.DisplayColor && player.DisplayColor == EnemyPalette.Get(EnemyColor.Yellow),
                "Progress fill follows the magic player's displayed flash color");
            player.enabled = false;
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception("Runtime gameplay check failed: " + message); }
    }
}
