using System;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class MenuBacklightChecks
    {
        public static void Run()
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                MenuBacklight.Begin(new Vector2(256, 256));
                MenuBacklight.Sprite(sprite, new Rect(116, 125, 24, 24));
                var rendered = MenuBacklight.RenderLight(new Rect(124, 190, 8, 20));
                RenderTexture.active = rendered;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                image.Apply();
                System.IO.Directory.CreateDirectory("TestResults");
                System.IO.File.WriteAllBytes("TestResults/menu-light-shadow-check.png", image.EncodeToPNG());
                Require(image.GetPixel(128, 186).a < .02f, "Silhouette casts a shadow away from subtitle");
                Require(image.GetPixel(50, 186).a > .08f, "Light remains visible beside the shadow");
                Require(image.GetPixel(128, 90).a > .3f, "Light between the subtitle and blocker stays bright");
                Require(image.GetPixel(128, 10).a < .02f, "Source primarily shines upward");
                Require(MenuBacklight.Intensity(0) != MenuBacklight.Intensity(.5f), "Light responds to beat");
                // A closed counter must transmit light even though a solid stroke is below it.
                MenuBacklight.Begin(new Vector2(256, 256));
                MenuBacklight.Sprite(sprite, new Rect(108, 115, 40, 5));
                MenuBacklight.Sprite(sprite, new Rect(108, 155, 40, 5));
                MenuBacklight.Sprite(sprite, new Rect(108, 120, 5, 35));
                MenuBacklight.Sprite(sprite, new Rect(143, 120, 5, 35));
                rendered = MenuBacklight.RenderLight(new Rect(124, 190, 8, 20));
                RenderTexture.active = rendered;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                image.Apply();
                Require(image.GetPixel(128, 120).a > .3f, "Enclosed letter hole receives light above a bottom stroke");
                Require(image.GetPixel(128, 186).a > .1f, "Enclosed hole projects light above the letter");

                MenuBacklight.Begin(new Vector2(256, 256));
                rendered = MenuBacklight.RenderLight(new Rect(78, 190, 100, 20));
                RenderTexture.active = rendered;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                image.Apply();
                Require(image.GetPixel(10, 52).a < .01f && image.GetPixel(245, 52).a < .01f,
                    "Both outer wings are dark below the 15-degree boundary");
                Require(image.GetPixel(10, 75).a > .1f && image.GetPixel(245, 75).a > .1f,
                    "Both outer wings light above the 15-degree boundary");

                MenuBacklight.Begin(new Vector2(256, 256));
                rendered = MenuBacklight.RenderLight(new Rect(78, 190, 100, 20), .16f);
                RenderTexture.active = rendered;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                Require(image.GetPixel(128, 75).a > .1f && image.GetPixel(128, 220).a < .01f,
                    "Subtitle tilt starts light near the source before sweeping it upward");

                MenuBacklight.Begin(new Vector2(256, 256));
                MenuBacklight.Sprite(sprite, new Rect(116, 125, 24, 24), Matrix4x4.Translate(new Vector3(45, -20)));
                var silhouette = MenuBacklight.SilhouetteTexture;
                MenuBacklight.RenderLight(new Rect(78, 190, 100, 20));
                RenderTexture.active = silhouette;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                Require(image.GetPixel(173, 139).r > .9f && image.GetPixel(128, 119).r < .01f,
                    "Character silhouettes follow the entrance transform instead of blocking light at the resting position");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(image);
                MenuBacklight.Shutdown();
            }
            Debug.Log("Menu backlight checks passed: silhouette shadow, enclosed counters, 15-degree wings and beat pulse.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new Exception("Menu backlight check failed: " + message); }
    }
}
