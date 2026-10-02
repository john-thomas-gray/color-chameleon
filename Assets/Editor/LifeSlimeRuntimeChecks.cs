using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public static class LifeSlimeRuntimeChecks
    {
        private const string Pending = "CandyCruisers.LifeSlimeChecks";
        private static int stage;
        private static double began, changed;
        static LifeSlimeRuntimeChecks() => EditorApplication.update += Poll;
        public static void Run()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var window = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            window.Show(); window.Focus();
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        public static void RunPortrait()
        { RunSized(440, 820); }
        public static void RunLandscape()
        { RunSized(960, 540); }
        private static void RunSized(int width, int height)
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            foreach (var existing in Resources.FindObjectsOfTypeAll(type)) ((EditorWindow)existing).Close();
            var window = (EditorWindow)ScriptableObject.CreateInstance(type);
            window.ShowUtility(); window.position = new Rect(40, 40, width, height);
            Run();
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            try
            {
                Application.runInBackground = true;
                if (began == 0) began = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - began > 45) throw new Exception("Life slime preview timed out at stage " + stage);
                var session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
                var player = GameObject.Find("Player")?.GetComponent<PlayerMovement>();
                if (session == null || player == null) return;
                if (session.IsPaused) session.Resume();
                if (stage == 0)
                {
                    PlayerLifeIconChecks.Run();
                    PlayerSlimeChecks.Run();
                    SafeRespawnChecks.Run();
                    PlayerDeathChecks.Run();
                    RewardWaveChecks.Run();
                    session.StartRun();
                    stage++;
                }
                else if (stage == 1 && session.State == GameSession.RunState.Playing)
                {
                    player.Hit(); changed = EditorApplication.timeSinceStartup; stage++;
                }
                else if (stage == 2 && EditorApplication.timeSinceStartup - changed > .65f)
                {
                    Require(player.LifeIcons.Active, "Recoverable death has a jumping spare");
                    Require(player.Alive && player.ReplacementFalling && player.Invulnerable && !player.RespawnProtectionActive,
                        "The real falling player takes over invincible without starting the landing timer early");
                    ScreenCapture.CaptureScreenshot("TestResults/life-slime-drop.png");
                    stage++;
                }
                else if (stage == 3 && player.Alive && !player.LifeIcons.Active)
                {
                    var grid = session.GetComponent<EnemyGrid>();
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>()) grid.ClearMagicChain(enemy.Id, 0);
                    Require(player.LifeIcons.Gain.Active, "Cleared full set schedules the new life");
                    stage++;
                }
                else if (stage == 4 && player.LifeIcons.Gain.Visible && player.LifeIcons.Gain.TravelProgress > .2f)
                {
                    ScreenCapture.CaptureScreenshot("TestResults/life-slime-trampoline.png"); stage++;
                }
                else if (stage == 5 && !player.LifeIcons.Gain.Active && session.State == GameSession.RunState.Playing)
                {
                    var grid = session.GetComponent<EnemyGrid>();
                    foreach (var enemy in grid.GetComponentsInChildren<GridEnemy>()) grid.ClearMagicChain(enemy.Id, 0);
                    stage++;
                }
                else if (stage == 6 && player.LifeIcons.Choreography.FlipAngle > 100)
                {
                    ScreenCapture.CaptureScreenshot("TestResults/life-slime-cap-flip.png");
                    changed = EditorApplication.timeSinceStartup; stage++;
                }
                else if (stage == 7 && EditorApplication.timeSinceStartup - changed > .3)
                {
                    Debug.Log("Life slime runtime checks passed: music clocks, real death drop, final-spike entrance and full-cap group flip.");
                    SessionState.SetBool(Pending, false); EditorApplication.Exit(0);
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error); SessionState.SetBool(Pending, false); EditorApplication.Exit(1);
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception("Life slime runtime check failed: " + message); }
    }
}
