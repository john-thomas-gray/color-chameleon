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
            Debug.Log("Gameplay framing checks passed: iPhone safe-area clearance, desktop framing and unchanged gameplay geometry.");
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
                target = new RenderTexture(1206, 2622, 24);
                pixels = new Texture2D(1206, 2622, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                camera.aspect = 1206f / 2622f;
                GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                framing.Refresh();
                float centeredTop = camera.WorldToViewportPoint(border.transform.TransformPoint(border.GetPosition(2))).y;

                foreach (var screen in new[] { new Vector3(390, 844, .055f), new Vector3(1179, 2556, .07f),
                    new Vector3(1206, 2622, .07f) })
                {
                    camera.aspect = screen.x / screen.y;
                    GameplayFraming.ForceIphoneNotchOffsetForTests = true;
                    GameplayFraming.SimulatedIphoneTopInsetRatioForTests = screen.z;
                    framing.Refresh();
                    float safeTop = 1 - screen.z;
                    float outerTop = camera.WorldToViewportPoint(new Vector3(0,
                        PlayfieldFrame.OuterTopY + PlayfieldFrame.OutlineLineWidth / 2)).y;
                    float bottom = camera.WorldToViewportPoint(new Vector3(0, -PlayfieldFrame.OuterTopY)).y;
                    Check(outerTop < safeTop && (safeTop - outerTop) * 2 * camera.orthographicSize >=
                        PlayfieldFrame.BoundaryLineWidth - .0001f, "Entire outer rim clears the notch with a border-width margin");
                    Check(bottom > .1f, "Lower rim leaves room above the phone home indicator");
                    var position = camera.transform.position;
                    framing.SetWaveCrunch(1);
                    Check(camera.WorldToViewportPoint(new Vector3(0, PlayfieldFrame.OuterTopY)).y < safeTop,
                        "Wave compression keeps the frame below the notch");
                    framing.SetWaveCrunch(0);
                    framing.Refresh();
                    Check(Vector3.Distance(position, camera.transform.position) < .0001f,
                        "Repeated framing and animation do not accumulate camera drift");
                    Check(player.transform.position == originalPlayerPosition, "Safe-area framing does not move the player object");
                }

                Directory.CreateDirectory("TestResults");
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1206, 2622), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("TestResults/iphone-notch-framing.png", pixels.EncodeToPNG());
                GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                framing.Refresh();
                Check(Mathf.Abs(camera.WorldToViewportPoint(border.transform.TransformPoint(border.GetPosition(2))).y - centeredTop) < .001f,
                    "Desktop framing returns to its original centered position");
                GameplayFraming.ForceIphoneNotchOffsetForTests = true;
                foreach (var screen in new[] { new Vector3(750, 1334, 0), new Vector3(844, 390, .055f) })
                {
                    camera.aspect = screen.x / screen.y;
                    GameplayFraming.SimulatedIphoneTopInsetRatioForTests = screen.z;
                    framing.Refresh();
                    Check(Mathf.Abs(framing.PlayfieldViewportLiftForIphoneNotch()) < .001f,
                        "Non-notched and landscape displays keep their existing framing");
                }
            }
            finally
            {
                GameplayFraming.ForceIphoneNotchOffsetForTests = false;
                framing.SetWaveCrunch(0);
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
