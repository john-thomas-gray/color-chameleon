using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class TouchControlChecks
    {
        public static void RunInPlayMode()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            UnityEditor.SessionState.SetBool("CandyCruisers.TouchControlChecks", true);
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayMode;
            UnityEditor.EditorApplication.isPlaying = true;
        }
        [UnityEditor.InitializeOnLoadMethod]
        private static void RestoreCheck()
        {
            if (UnityEditor.SessionState.GetBool("CandyCruisers.TouchControlChecks", false))
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayMode;
        }
        private static void OnPlayMode(UnityEditor.PlayModeStateChange state)
        {
            if (state != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayMode;
            UnityEditor.SessionState.SetBool("CandyCruisers.TouchControlChecks", false);
            try { Run(); UnityEditor.EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }
        public static void Run()
        {
            foreach (var size in new[] { new Vector2(390, 844), new Vector2(844, 390), new Vector2(540, 960) })
            {
                Vector2 center = new Vector2(size.x * .5f, size.y * .3f);
                float radius = Mathf.Min(size.x, size.y) * .12f;
                float spawnY = center.y + radius * 4;
                Check(PlayerMovement.IsTouchShootPosition(center + Vector2.right * radius, center, radius, spawnY), "Zone includes its side boundary");
                Check(PlayerMovement.IsTouchShootPosition(new Vector2(center.x + radius * .8f, 0), center, radius, spawnY),
                    "The shooting zone extends to the bottom of the screen");
                Check(PlayerMovement.IsTouchShootPosition(new Vector2(center.x, spawnY), center, radius, spawnY),
                    "Vertical distance does not block in-column shots below the spawn line");
                Check(!PlayerMovement.IsTouchShootPosition(new Vector2(center.x + radius + 1, 0), center, radius, spawnY),
                    "Below-player taps outside the column remain movement taps");
                Check(!PlayerMovement.IsTouchShootPosition(new Vector2(center.x, spawnY + 1), center, radius, spawnY),
                    "Only taps above the enemy spawn line are too high to shoot");
            }
            GameObject cameraRoot = null;
            if (Camera.main == null)
            {
                cameraRoot = new GameObject("Touch test camera", typeof(Camera));
                cameraRoot.tag = "MainCamera";
                cameraRoot.transform.position = Vector3.back * 10;
                cameraRoot.GetComponent<Camera>().orthographic = true;
                cameraRoot.GetComponent<Camera>().orthographicSize = 6;
            }
            try
            {
                SpecialEnemyChecks.Fixture((grid, player, tongue) =>
                {
                    var clickedEnemy = ProgressionChecks.Add(grid, EnemyColor.Red, 1, 0);
                    player.RefreshColor();
                    int shots = 0;
                    player.ShotAccepted += () => shots++;
                    var camera = Camera.main;
                    Vector2 ScreenPoint(float x) => camera.WorldToScreenPoint(new Vector3(x, player.transform.position.y, 0));
                    player.transform.position = new Vector3(0, -4.6f, 0);
                    var original = player.transform.position;
                    Vector2 near = ScreenPoint(.1f);
                    player.BeginPointer(near, true);
                    player.TickPointerMovement(.1f);
                    player.EndPointer(near, false);
                    player.TickPointerMovement(1);
                    Check(shots == 1 && player.transform.position == original, "Near tap fires without nudging the player");
                    tongue.Cancel();
                    Vector2 below = new Vector2(ScreenPoint(.1f).x, 0);
                    player.BeginPointer(below, true);
                    player.EndPointer(below, false);
                    player.TickPointerMovement(1);
                    Check(shots == 2 && player.transform.position == original, "Tap beneath the character shoots without moving");
                    tongue.Cancel();
                    float spawnY = camera.WorldToScreenPoint(grid.transform.TransformPoint(grid.CellPosition(0, 0))).y;
                    Vector2 high = new Vector2(ScreenPoint(.1f).x, spawnY - 1);
                    player.BeginPointer(high, true);
                    player.EndPointer(high, false);
                    player.TickPointerMovement(1);
                    Check(shots == 3 && Mathf.Abs(player.transform.position.x - .1f) < .001f,
                        "Tall in-column tap aligns to the tap below the enemy spawn line");
                    tongue.Cancel();
                    Vector2 tooHigh = new Vector2(ScreenPoint(0).x, spawnY + 2);
                    player.BeginPointer(tooHigh, true);
                    player.EndPointer(tooHigh, false);
                    player.TickPointerMovement(1);
                    Check(shots == 3, "In-column tap above the enemy spawn line is too high to fire");
                    Vector2 enemyTap = camera.WorldToScreenPoint(clickedEnemy.HitBounds.center);
                    player.BeginPointer(enemyTap, true);
                    player.EndPointer(enemyTap, false);
                    player.TickPointerMovement(1);
                    Check(shots == 4 && Mathf.Abs(player.transform.position.x - clickedEnemy.HitBounds.center.x) < .001f,
                        "Tapping an enemy aligns the player with that enemy and fires");
                    tongue.Cancel();
                    Vector2 above = camera.WorldToScreenPoint(new Vector3(-1.25f, player.transform.position.y + 1.2f, 0));
                    player.BeginPointer(above, true);
                    player.EndPointer(above, false);
                    player.TickPointerMovement(1);
                    Check(shots == 5 && Mathf.Abs(player.transform.position.x + 1.25f) < .001f,
                        "Tapping above the player aligns with the tap and fires");
                    tongue.Cancel();
                    Vector2 far = ScreenPoint(2);
                    player.BeginPointer(far, true);
                    player.EndPointer(far, false);
                    player.TickPointerMovement(.02f);
                    Check(Mathf.Abs(PlayerMovement.Wrap(player.transform.position.x + 1.25f)) > .01f && shots == 5,
                        "Far tap moves after release without firing");
                    player.TickPointerMovement(1);
                    Check(Mathf.Abs(player.transform.position.x - 2) < .001f, "Tap movement reaches its destination");
                    var start = ScreenPoint(2);
                    player.BeginPointer(start, true);
                    player.UpdatePointer(ScreenPoint(.5f));
                    player.TickPointerMovement(1);
                    Check(Mathf.Abs(player.transform.position.x - .5f) < .001f, "Dragging from the shooting zone repositions the player");
                    player.EndPointer(ScreenPoint(.5f), false);
                    Check(shots == 5, "A drag never fires on release");
                    player.BeginPointer(ScreenPoint(-2), true);
                    player.EndPointer(ScreenPoint(-2), true);
                    original = player.transform.position;
                    player.TickPointerMovement(1);
                    Check(player.transform.position == original && shots == 5, "Canceled touches neither move nor fire");
                    Vector2 mouseAbove = camera.WorldToScreenPoint(new Vector3(-2, player.transform.position.y + 1, 0));
                    player.BeginPointer(mouseAbove);
                    player.EndPointer(mouseAbove, false);
                    Check(shots == 6 && Mathf.Abs(player.transform.position.x + 2) < .001f,
                        "Desktop mouse taps above the player align and fire");
                    tongue.Cancel();
                    player.BeginPointer(ScreenPoint(-2), true);
                    player.CancelPointer();
                    player.EndPointer(ScreenPoint(-2), false);
                    player.TickPointerMovement(1);
                    Check(shots == 6, "Canceled gestures cannot resume or fire later");
                });
            }
            finally { if (cameraRoot != null) UnityEngine.Object.DestroyImmediate(cameraRoot); }
            Debug.Log("Touch control checks passed: spawn-height shooting, enemy/above-player tap firing, distant movement, drag, cancellation and mouse compatibility.");
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("Touch control check failed: " + message); }
    }
}
