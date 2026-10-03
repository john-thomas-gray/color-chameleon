using System;
using UnityEngine;

namespace CandyCruisers
{
    // Copy real sprite pieces in neutral local space; never inherit the player's current pose.
    public sealed class LifeSlimeArtwork
    {
        private struct Piece
        {
            public Sprite Sprite;
            public Rect Rect;
            public Color Tint;
            public bool Body, FlipX, FlipY;
            public float FaceHeight;
        }
        private Piece[] pieces;
        private Transform sourceRoot;
        private Sprite sourceBody;
        private bool jelly;
        private Rect bodyRect;
        public Rect Bounds { get; private set; }
        public int PartCount => pieces != null ? pieces.Length : 0;
        public Sprite PartSprite(int index) => pieces[index].Sprite;
        public Rect PartRect(int index) => pieces[index].Rect;
        private Texture2D dome;
        private readonly Color32[] pixels = new Color32[128 * 128];
        private float lastLean = float.NaN;

        public void Bind(CharacterVisuals visuals)
        {
            if (visuals == null || visuals.Body == null || visuals.Body.sprite == null) return;
            if (sourceRoot == visuals.Root && sourceBody == visuals.Body.sprite && pieces != null) return;
            sourceRoot = visuals.Root; sourceBody = visuals.Body.sprite;
            var slime = visuals.GetComponent<PlayerSlimeVisual>();
            var shock = visuals.GetComponent<PlayerShockVisual>();
            jelly = slime != null && slime.PlaceholderSprite == sourceBody;
            var renderers = sourceRoot.GetComponentsInChildren<SpriteRenderer>(true);
            Array.Sort(renderers, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            var result = new System.Collections.Generic.List<Piece>();
            Vector2 allMin = Vector2.one * float.PositiveInfinity, allMax = Vector2.one * float.NegativeInfinity;
            foreach (var part in renderers)
            {
                if (part.sprite == null || shock != null && shock.OwnsSprite(part)) continue;
                var matrix = sourceRoot.worldToLocalMatrix * part.transform.localToWorldMatrix;
                var displacement = slime != null ? slime.FaceDisplacement(part.transform, sourceRoot) : Vector3.zero;
                var bounds = part.sprite.bounds;
                Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
                for (int corner = 0; corner < 4; corner++)
                {
                    var point = matrix.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y, 0)) - displacement;
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                var rect = new Rect(min, max - min);
                bool isBody = part == visuals.Body;
                if (isBody) bodyRect = rect;
                result.Add(new Piece { Sprite = part.sprite, Rect = rect, Body = isBody, Tint = part.color,
                    FlipX = part.flipX, FlipY = part.flipY, FaceHeight = slime != null ? slime.FaceHeight(part.transform) : 0 });
                allMin = Vector2.Min(allMin, min); allMax = Vector2.Max(allMax, max);
            }
            pieces = result.ToArray(); Bounds = new Rect(allMin, allMax - allMin);
        }

        public Rect FromBodyRect(Rect projectedBody)
        {
            if (bodyRect.width <= 0 || bodyRect.height <= 0) return projectedBody;
            float xScale = projectedBody.width / bodyRect.width, yScale = projectedBody.height / bodyRect.height;
            return new Rect(projectedBody.x + (Bounds.xMin - bodyRect.xMin) * xScale,
                projectedBody.y - (Bounds.yMax - bodyRect.yMax) * yScale,
                Bounds.width * xScale, Bounds.height * yScale);
        }

        public Rect Fit(Rect frame)
        {
            float scale = Mathf.Min(frame.width / Mathf.Max(.0001f, Bounds.width), frame.height / Mathf.Max(.0001f, Bounds.height));
            var size = Bounds.size * scale;
            return new Rect(frame.center - size / 2, size);
        }

        public void Draw(Rect frame, Color color, float opacity, float lean = 0, float rotation = 0, float verticalScale = 1)
        {
            if (pieces == null || pieces.Length == 0) return;
            if (jelly) EnsureDome(lean);
            var rect = Fit(frame);
            var matrix = GUI.matrix; var tint = GUI.color;
            GUI.matrix = AnimatedGuiMatrix(matrix, rect, rotation, verticalScale);
            try
            {
                foreach (var piece in pieces)
                {
                    var part = piece.Rect;
                    if (!piece.Body && jelly) part.x += PlayerSlimeVisual.JellyOffset(piece.FaceHeight, lean) * bodyRect.width * .5f;
                    var target = new Rect(rect.x + (part.x - Bounds.x) / Bounds.width * rect.width,
                        rect.y + (Bounds.yMax - part.yMax) / Bounds.height * rect.height,
                        part.width / Bounds.width * rect.width, part.height / Bounds.height * rect.height);
                    var texture = piece.Body && jelly ? dome : piece.Sprite.texture;
                    var uv = piece.Body && jelly ? new Rect(0, 0, 1, 1) : new Rect(piece.Sprite.rect.x / texture.width,
                        piece.Sprite.rect.y / texture.height, piece.Sprite.rect.width / texture.width, piece.Sprite.rect.height / texture.height);
                    if (piece.FlipX) { uv.x += uv.width; uv.width = -uv.width; }
                    if (piece.FlipY) { uv.y += uv.height; uv.height = -uv.height; }
                    var partTint = piece.Body ? color : piece.Tint;
                    partTint.a *= opacity; GUI.color = partTint;
                    GUI.DrawTextureWithTexCoords(target, texture, uv, true);
                }
            }
            finally { GUI.matrix = matrix; GUI.color = tint; }
        }

        public static Matrix4x4 AnimatedGuiMatrix(Matrix4x4 baseMatrix, Rect rect, float rotation, float verticalScale)
        {
            float safeScale = Mathf.Max(.0001f, verticalScale);
            var center = new Vector3(rect.center.x, rect.center.y, 0);
            var feet = new Vector3(rect.center.x, rect.yMax, 0);
            var rotate = Matrix4x4.Translate(center) * Matrix4x4.Rotate(Quaternion.Euler(0, 0, rotation)) *
                Matrix4x4.Translate(-center);
            var scale = Matrix4x4.Translate(feet) *
                Matrix4x4.Scale(new Vector3(1 / Mathf.Sqrt(safeScale), safeScale, 1)) *
                Matrix4x4.Translate(-feet);
            return baseMatrix * rotate * scale;
        }

        private void EnsureDome(float lean)
        {
            if (dome == null)
                dome = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = "Independent player copy", hideFlags = HideFlags.DontSave };
            if (!float.IsNaN(lastLean) && Mathf.Abs(lean - lastLean) < .002f) return;
            lastLean = lean; PlayerSlimeVisual.PaintDome(pixels, 128, lean);
            dome.SetPixels32(pixels); dome.Apply(false);
        }
        public void Dispose()
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(dome); else UnityEngine.Object.DestroyImmediate(dome);
            dome = null; lastLean = float.NaN; pieces = null; sourceRoot = null; sourceBody = null;
        }
    }
}
