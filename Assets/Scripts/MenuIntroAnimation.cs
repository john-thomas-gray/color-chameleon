using UnityEngine;

namespace CandyCruisers
{
    public static class MenuIntroAnimation
    {
        public enum Entrance { AllVariants = -2, Random = -1, FlipRight, SkidLeft, TractorBeam }
        public const float TitleBeatSeconds = 60f / 140f;
        public const float TitleFirstBeatSeconds = .1f;
        public const float TitleSeconds = TitleFirstBeatSeconds + TitleBeatSeconds * 8f;
        public const float SubtitleSeconds = .75f;
        public const float CharacterSeconds = 1.7f;
        public const float CharacterStart = TitleSeconds + SubtitleSeconds;
        public const float TractorLandingProgress = .85f;
        public const float BeamCloseStart = CharacterStart + CharacterSeconds * TractorLandingProgress;
        public const float BeamCloseSeconds = .42f;
        public const float BeamZipSeconds = .32f;
        public const float Duration = BeamCloseStart + BeamCloseSeconds + BeamZipSeconds;
        private static Texture2D beamTexture, ringTexture;
        private static readonly float[] TurnTimes = {
            TitleFirstBeatSeconds,
            TitleFirstBeatSeconds + TitleBeatSeconds,
            TitleFirstBeatSeconds + TitleBeatSeconds * 3,
            TitleFirstBeatSeconds + TitleBeatSeconds * 4,
            TitleFirstBeatSeconds + TitleBeatSeconds * 6,
            TitleFirstBeatSeconds + TitleBeatSeconds * 7,
            TitleSeconds
        };
        private static readonly Vector2[] Turns = {
            new Vector2(1, 0), new Vector2(1, .22f), new Vector2(-1, .22f),
            new Vector2(-1, .49f), new Vector2(1, .49f), new Vector2(1, 1), new Vector2(0, 1)
        };
        public static int TitleTurnCount => TurnTimes.Length;
        public static float TitleTurnTime(int index) => TurnTimes[Mathf.Clamp(index, 0, TurnTimes.Length - 1)];

        public readonly struct Pose
        {
            public readonly Rect Body;
            public readonly float Rotation;
            public readonly bool Visible;
            public Matrix4x4 Transform => Matrix4x4.Translate(Body.center) *
                Matrix4x4.Rotate(Quaternion.Euler(0, 0, Rotation)) * Matrix4x4.Translate(-Body.center);
            public Pose(Rect body, float rotation = 0, bool visible = true)
            { Body = body; Rotation = rotation; Visible = visible; }
        }

        public static Rect TitleRect(Rect resting, float canvasWidth, float elapsed)
        {
            elapsed = Mathf.Clamp(elapsed, 0, TitleSeconds);
            if (elapsed >= TitleSeconds) return resting;
            int leg = 0;
            while (leg < TurnTimes.Length - 2 && elapsed > TurnTimes[leg + 1]) leg++;
            var path = Vector2.Lerp(Turns[leg], Turns[leg + 1],
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(TurnTimes[leg], TurnTimes[leg + 1], elapsed)));
            float travel = Mathf.Max(0, (canvasWidth - resting.width) * .5f - 6);
            resting.x += path.x * travel;
            resting.y = Mathf.Lerp(-resting.height - 12, resting.y, path.y);
            return resting;
        }

        public static float SubtitleProgress(float elapsed) =>
            Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - TitleSeconds) / SubtitleSeconds));

        public static Matrix4x4 SubtitleTransform(Rect resting, float elapsed)
        {
            float progress = SubtitleProgress(elapsed);
            var pivot = new Vector3(resting.center.x, resting.yMax);
            return Matrix4x4.Translate(pivot) * Matrix4x4.Scale(new Vector3(
                Mathf.Lerp(.84f, 1, progress), Mathf.Sin(progress * Mathf.PI * .5f), 1)) *
                Matrix4x4.Translate(-pivot);
        }

        public static Rect TransformRect(Rect rect, Matrix4x4 matrix)
        {
            var a = matrix.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMin));
            var b = matrix.MultiplyPoint3x4(new Vector3(rect.xMax, rect.yMax));
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        public static Pose CharacterPose(Rect resting, float canvasWidth, float elapsed, Entrance entrance)
        {
            if (elapsed < CharacterStart) return new Pose(resting, visible: false);
            float t = Mathf.Clamp01((elapsed - CharacterStart) / CharacterSeconds);
            if (t >= 1) return new Pose(resting);
            var center = resting.center;
            var size = resting.size;
            float angle = 0;
            float arc = Mathf.Max(60, resting.height * 3.2f);
            if (entrance == Entrance.TractorBeam)
            {
                float fall = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, TractorLandingProgress, t));
                center.y = Mathf.Lerp(-resting.height, center.y, fall);
                center.x += Mathf.Sin(t * Mathf.PI * 6) * resting.width * .12f * Mathf.Sin(fall * Mathf.PI);
                angle = Mathf.Sin(t * Mathf.PI * 4) * 8 * Mathf.Sin(fall * Mathf.PI);
            }
            else if (entrance == Entrance.SkidLeft)
            {
                float overshoot = Mathf.Min(canvasWidth - resting.xMax - 8, resting.width * 2.2f);
                if (t < .43f)
                {
                    float fly = 1 - Mathf.Pow(1 - t / .43f, 2);
                    center.x = Mathf.Lerp(-resting.width, resting.center.x + overshoot * .65f, fly);
                    center.y -= Mathf.Sin((1 - fly) * Mathf.PI * .5f) * arc * .5f;
                    angle = -18 * fly;
                }
                else if (t < .65f)
                {
                    float slide = Mathf.SmoothStep(0, 1, (t - .43f) / .22f);
                    center.x += Mathf.Lerp(overshoot * .65f, overshoot, slide);
                    angle = Mathf.Lerp(-18, 12, slide);
                    size = Vector2.Scale(size, new Vector2(1 + .18f * Mathf.Sin(slide * Mathf.PI),
                        1 - .15f * Mathf.Sin(slide * Mathf.PI)));
                }
                else
                {
                    float flip = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, .94f, t));
                    center.x += overshoot * (1 - flip);
                    center.y -= Mathf.Sin(flip * Mathf.PI) * arc * .65f;
                    angle = Mathf.Lerp(12, -360, flip);
                }
            }
            else
            {
                float flip = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .88f));
                center.x = Mathf.Lerp(canvasWidth + resting.width, center.x, flip);
                center.y -= Mathf.Sin(flip * Mathf.PI) * arc;
                angle = -360 * (1 - flip);
            }
            float landingAt = entrance == Entrance.SkidLeft ? .94f : entrance == Entrance.TractorBeam ? TractorLandingProgress : .88f;
            float squash = Mathf.Sin(Mathf.InverseLerp(landingAt, 1, t) * Mathf.PI) * .14f;
            size = Vector2.Scale(size, new Vector2(1 + squash, 1 - squash));
            center.y += (resting.height - size.y) * .5f;
            return new Pose(new Rect(center - size / 2, size), angle);
        }

        public static float BeamOpacity(float elapsed)
        {
            float t = (elapsed - CharacterStart) / CharacterSeconds;
            if (t <= 0 || elapsed >= Duration) return 0;
            return Mathf.SmoothStep(0, 1, t / .12f);
        }

        public static float BeamCloseProgress(float elapsed) =>
            Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - BeamCloseStart) / BeamCloseSeconds));

        public static Rect BeamBounds(Rect landing, float elapsed)
        {
            float zip = Mathf.Clamp01((elapsed - BeamCloseStart - BeamCloseSeconds) / BeamZipSeconds);
            float width = Mathf.Lerp(Mathf.Max(80, landing.width * 3.8f), 2, BeamCloseProgress(elapsed));
            return new Rect(landing.center.x - width / 2, -12, width, (landing.yMax + 12) * (1 - zip * zip));
        }

        public static float BeamWidthAt(Rect beam, float y) =>
            beam.width * Mathf.Lerp(.6f, .98f, Mathf.InverseLerp(beam.yMin, beam.yMax, y));

        public static void DrawBeam(Rect landing, float elapsed)
        {
            float opacity = BeamOpacity(elapsed);
            if (opacity <= 0) return;
            EnsureBeamTextures();
            var previous = GUI.color;
            var cone = BeamBounds(landing, elapsed);
            float close = BeamCloseProgress(elapsed);
            GUI.color = new Color(1, 1, 1, opacity * (.85f + .15f * Mathf.Sin(elapsed * 22)));
            GUI.DrawTexture(cone, beamTexture);
            for (int i = 0; i < 4; i++)
            {
                float drop = Mathf.Repeat(elapsed * .65f + i * .25f, 1);
                float ringWidth = BeamWidthAt(cone, cone.y + cone.height * drop);
                GUI.color = new Color(1, 1, 1, opacity * (1 - close) * Mathf.Sin(drop * Mathf.PI) * .28f);
                GUI.DrawTexture(new Rect(landing.center.x - ringWidth / 2, cone.y + cone.height * drop,
                    ringWidth, Mathf.Max(4, ringWidth * .09f)), ringTexture);
            }
            // The last fan of light coalesces into a bright filament before retracting upward.
            GUI.color = new Color(1, 1, 1, opacity * close * close * .8f);
            GUI.DrawTexture(new Rect(landing.center.x - 1, cone.y, 2, cone.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void EnsureBeamTextures()
        {
            if (beamTexture != null) return;
            beamTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
                { name = "Menu tractor cone", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            ringTexture = new Texture2D(128, 32, TextureFormat.RGBA32, false)
                { name = "Menu tractor rings", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var cone = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float down = 1 - y / 127f;
                float across = Mathf.Abs(x / 127f - .5f);
                float halfWidth = Mathf.Lerp(.3f, .49f, down);
                float edge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(halfWidth - .045f, halfWidth, across));
                float alpha = edge * (.10f + .2f * across / halfWidth + .035f * Mathf.Cos(across * 110));
                cone[y * 128 + x] = new Color(1, 1, 1, alpha);
            }
            beamTexture.SetPixels(cone); beamTexture.Apply();
            var rings = new Color[128 * 32];
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 128; x++)
            {
                float radius = new Vector2((x / 127f - .5f) * 2, (y / 31f - .5f) * 2).magnitude;
                rings[y * 128 + x] = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, Mathf.Abs(radius - .8f) / .14f));
            }
            ringTexture.SetPixels(rings); ringTexture.Apply();
        }

        public static void Shutdown()
        {
            foreach (var texture in new[] { beamTexture, ringTexture })
                if (texture != null)
                { if (Application.isPlaying) Object.Destroy(texture); else Object.DestroyImmediate(texture); }
            beamTexture = ringTexture = null;
        }
    }
}
