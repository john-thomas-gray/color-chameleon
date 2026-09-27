using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class WaveTransitionChecks
    {
        public static void Run()
        {
            var root = new GameObject("Transition fixture", typeof(EnemyGrid), typeof(EnemyGridMovement), typeof(EnemyRowSpawner));
            var playerObject = new GameObject("Transition player", typeof(PlayerMovement), typeof(SpriteRenderer));
            var mouth = new GameObject("Transition tongue", typeof(LineRenderer), typeof(TongueShot));
            bool enabled = SpawnOverride.Enabled;
            int types = SpawnOverride.Types;
            var random = UnityEngine.Random.state;
            try
            {
                var line = mouth.GetComponent<LineRenderer>();
                line.startWidth = .075f; line.endWidth = .11f;
                var tongue = mouth.GetComponent<TongueShot>();
                tongue.TryFire(EnemyColor.Red, 10);
                tongue.Tick(.1f);
                Near(tongue.Length, 1.4f, "Normal extension speed");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 10, true);
                tongue.Tick(.1f);
                Check(line.enabled && !tongue.TryFire(EnemyColor.Blue, 10), "Magic fires immediately and blocks repeated fire");
                Near(tongue.Length, 5.6f, "Magic extension is immediately four times normal speed");
                Near(line.startWidth, .15f, "Magic base is twice as thick");
                Near(line.endWidth, .22f, "Magic tip is twice as thick");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 2, true);
                tongue.Tick(2f / 56);
                tongue.Tick(.02f);
                Near(tongue.Length, .4f, "Magic retracts at eighty units per second");
                tongue.Cancel();
                tongue.TryFire(EnemyColor.Red, 2);
                Near(line.endWidth, .11f, "Normal width restored after magic");
                tongue.Cancel();

                var grid = root.GetComponent<EnemyGrid>();
                var spawner = root.GetComponent<EnemyRowSpawner>();
                spawner.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Blue Enemy.prefab"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Red Enemy.prefab"));
                var player = playerObject.GetComponent<PlayerMovement>();
                playerObject.GetComponentInChildren<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PlayerPlaceholder.png");
                mouth.transform.SetParent(playerObject.transform, false);
                player.Configure(grid, tongue, playerObject.GetComponentInChildren<SpriteRenderer>());
                var session = root.AddComponent<GameSession>();
                session.Configure(player);
                typeof(GameSession).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(session, null);
                SpawnOverride.Enabled = true;
                SpawnOverride.Types = 1 << (int)EnemyColor.Blue;
                var target = ProgressionChecks.Add(grid, EnemyColor.Red, 2, 0);
                playerObject.transform.position = new Vector3(target.transform.position.x, -4.6f, 0);
                Check(player.Fire(), "Fire at last enemy");
                tongue.Tick(.33f, grid);
                Check(session.State == GameSession.RunState.Refilling && tongue.Active && tongue.Retracting,
                    "Last normal hit retracts instead of disappearing");
                Check(player.ReadyColor == EnemyColor.Red, "Outgoing color stays until return");
                float length = tongue.Length;
                session.Tick(10);
                Check(grid.Model.Count == 0 && tongue.Active, "Refill waits for returning tongue");
                tongue.Tick(.01f, grid);
                Check(tongue.Length < length && tongue.Length > 0, "Retraction remains animated during refill");
                SpawnOverride.Types = 1 << (int)EnemyColor.Red;
                tongue.Tick(10, grid);
                player.RefreshColor(true);
                Check(player.ReadyColor == EnemyColor.Blue && player.DisplayColor == EnemyPalette.Get(EnemyColor.Blue) && !player.Fire(),
                    "Return switches straight to planned color without allowing empty-field shots");
                session.Tick(0);
                Check(grid.Model.ColorCount(EnemyColor.Blue) == session.Progress.BatchEnemies && player.ReadyColor == EnemyColor.Blue,
                    "Reserved next-wave color really spawns even if override changes during the pause");

                tongue.TryFire(EnemyColor.Red, 10, true);
                var bottom = grid.View(grid.Model.At(2, session.Progress.BatchRows - 1).Id).GetComponentInChildren<SpriteRenderer>().bounds.min.y;
                tongue.Tick((bottom - tongue.transform.position.y) / 56 + .001f, grid);
                Check(grid.Model.Count == 0 && tongue.Active && tongue.Retracting, "Last magic hit also returns visibly");
                session.Tick(10);
                Check(grid.Model.Count == 0, "Magic return cannot hit the next batch");
                tongue.Tick(10, grid);
                player.RefreshColor();
                session.Tick(0);
                Check(player.ReadyColor == EnemyColor.Red && grid.Model.ColorCount(EnemyColor.Red) > 0,
                    "Following wave uses newly selected override and matching player color");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(playerObject);
                SpawnOverride.Enabled = enabled;
                SpawnOverride.Types = types;
                UnityEngine.Random.state = random;
            }
            Debug.Log("Wave transition checks passed: magic width/speed, visible final-hit returns, non-grey player and guaranteed next-batch color.");
        }
        private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message);
        private static void Check(bool condition, string message)
        { if (!condition) throw new Exception("Wave transition check failed: " + message); }
    }
}
