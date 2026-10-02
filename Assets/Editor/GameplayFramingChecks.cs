using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class GameplayFramingChecks
    {
        [MenuItem("Candy Cruisers/Run Framing Checks")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            CheckIphoneNotchOffset();
            Debug.Log("Gameplay framing checks passed: iPhone notch framing lifts the playfield without changing gameplay geometry.");
        }

        private static void CheckIphoneNotchOffset()
        {
            var camera = Camera.main;
            var framing = camera.GetComponent<GameplayFraming>();
            var border = GameObject.Find("Playfield Border").GetComponent<LineRenderer>();
            var player = GameObject.Find("Player").GetComponent<PlayerMovement>();
            Vector3 originalCameraPosition = camera.transform.position;
            Vector3 originalPlayerPosition = player.transform.position;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                target = new RenderTexture(390, 844, 24);
                pixels = new Texture2D(390, 844, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                camera.aspect = 390f / 844f;
                GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                framing.Refresh();
                float centeredTop = camera.WorldToViewportPoint(border.transform.TransformPoint(border.GetPosition(2))).y;

                GameplayFraming.ForceIphoneNotchOffsetForTests = true;
                GameplayFraming.SimulatedIphoneTopInsetRatioForTests = GameplayFraming.IphoneNotchDefaultTopInsetRatio;
                framing.Refresh();
                float liftedTop = camera.WorldToViewportPoint(border.transform.TransformPoint(border.GetPosition(2))).y;
                float liftedBottom = camera.WorldToViewportPoint(border.transform.TransformPoint(border.GetPosition(0))).y;
                float expectedTop = Mathf.Min(1f - GameplayFraming.IphoneNotchDefaultTopInsetRatio +
                    GameplayFraming.IphoneNotchTargetOverlapRatio, GameplayFraming.IphoneNotchMaxPlayfieldTopViewportY);

                Check(liftedTop > centeredTop + .04f, "Notched portrait phone moves the playfield up the screen");
                Check(Mathf.Abs(liftedTop - expectedTop) < .002f, "Top rim lands just inside the simulated notch area");
                Check(liftedBottom > .12f && liftedBottom < .25f, "Bottom rim remains comfortably visible after the lift");
                Check(player.transform.position == originalPlayerPosition, "Framing lift does not move the player object");

                Directory.CreateDirectory("TestResults");
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 390, 844), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("TestResults/iphone-notch-framing.png", pixels.EncodeToPNG());
            }
            finally
            {
                GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                GameplayFraming.SimulatedIphoneTopInsetRatioForTests = GameplayFraming.IphoneNotchDefaultTopInsetRatio;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                camera.ResetAspect();
                camera.transform.position = originalCameraPosition;
                player.transform.position = originalPlayerPosition;
                framing.Refresh();
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Gameplay framing check failed: " + message);
        }
    }
}
