using UnityEngine;

namespace CandyCruisers
{
    public static class MenuBacklight
    {
        public const float LowerEdgeAngle = 15;
        private static Material maskMaterial, lightMaterial;
        private static RenderTexture mask, light;
        private static RenderTexture previousTarget;
        private static Vector2 canvas;
        private static float blockerTop, blockerBottom;
        public static RenderTexture SilhouetteTexture => mask;

        public static float Intensity(float beatPosition) =>
            .8f + .2f * GameplayMusicPlayer.BeatPulse(beatPosition);

        public static void Begin(Vector2 size)
        {
            canvas = size;
            blockerTop = size.y;
            blockerBottom = 0;
            if (maskMaterial == null) maskMaterial = new Material(Resources.Load<Shader>("MenuLightMask"))
                { hideFlags = HideFlags.HideAndDontSave };
            if (lightMaterial == null) lightMaterial = new Material(Resources.Load<Shader>("MenuLight"))
                { hideFlags = HideFlags.HideAndDontSave };
            int width = Mathf.Min(1024, Mathf.CeilToInt(size.x));
            int height = Mathf.CeilToInt(width * size.y / size.x);
            if (mask == null || mask.width != width || mask.height != height)
            {
                Release();
                mask = CreateTarget(width, height, "Menu silhouettes");
                light = CreateTarget(width, height, "Subtitle light");
            }
            previousTarget = RenderTexture.active;
            RenderTexture.active = mask;
            GL.Clear(true, true, Color.clear);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, size.x, size.y, 0);
        }

        private static RenderTexture CreateTarget(int width, int height, string name)
        {
            var result = new RenderTexture(width, height, 0)
            { name = name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            result.Create();
            return result;
        }

        public static void Release()
        {
            foreach (var target in new[] { mask, light })
            {
                if (target == null) continue;
                target.Release();
                if (Application.isPlaying) Object.Destroy(target); else Object.DestroyImmediate(target);
            }
            mask = light = null;
        }

        public static void Shutdown()
        {
            Release();
            foreach (var material in new[] { maskMaterial, lightMaterial })
            {
                if (material == null) continue;
                if (Application.isPlaying) Object.Destroy(material); else Object.DestroyImmediate(material);
            }
            maskMaterial = lightMaterial = null;
        }

        public static void Sprite(Sprite sprite, Rect rect)
            => Sprite(sprite, rect, Matrix4x4.identity);

        public static void Sprite(Sprite sprite, Rect rect, Matrix4x4 transform)
        {
            if (sprite == null) return;
            var uv = sprite.textureRect;
            uv = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height,
                uv.width / sprite.texture.width, uv.height / sprite.texture.height);
            Quad(sprite.texture, rect, new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax),
                new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMin, uv.yMin), 1, transform);
        }

        public static void Word(string text, Rect rect, GUIStyle style, float opacity)
        {
            Font font = style.font;
            font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);
            float scale = style.fontSize / (float)font.fontSize;
            float baseline = rect.center.y + (font.ascent - font.lineHeight * .5f) * scale;
            float x = rect.x;
            foreach (char c in text)
            {
                if (!font.GetCharacterInfo(c, out var glyph, style.fontSize, style.fontStyle)) continue;
                var bounds = new Rect(x + glyph.minX, baseline - glyph.maxY, glyph.glyphWidth, glyph.glyphHeight);
                Quad(font.material.mainTexture, bounds, glyph.uvTopLeft, glyph.uvTopRight,
                    glyph.uvBottomRight, glyph.uvBottomLeft, opacity);
                x += glyph.advance;
            }
        }

        public static Rect TextInkBounds(string text, Rect line, GUIStyle style)
        {
            Font font = style.font;
            font.RequestCharactersInTexture(text, style.fontSize, style.fontStyle);
            float scale = style.fontSize / (float)font.fontSize;
            float baseline = line.center.y + (font.ascent - font.lineHeight * .5f) * scale;
            float x = line.center.x - style.CalcSize(new GUIContent(text)).x / 2;
            Rect result = default;
            bool found = false;
            foreach (char c in text)
            {
                if (!font.GetCharacterInfo(c, out var glyph, style.fontSize, style.fontStyle)) continue;
                if (glyph.glyphWidth > 0 && glyph.glyphHeight > 0)
                {
                    var bounds = new Rect(x + glyph.minX, baseline - glyph.maxY, glyph.glyphWidth, glyph.glyphHeight);
                    result = !found ? bounds : Rect.MinMaxRect(Mathf.Min(result.xMin, bounds.xMin),
                        Mathf.Min(result.yMin, bounds.yMin), Mathf.Max(result.xMax, bounds.xMax), Mathf.Max(result.yMax, bounds.yMax));
                    found = true;
                }
                x += glyph.advance;
            }
            return found ? result : line;
        }

        private static void Quad(Texture texture, Rect rect, Vector2 a, Vector2 b, Vector2 c, Vector2 d, float opacity,
            Matrix4x4? transform = null)
        {
            var matrix = transform ?? Matrix4x4.identity;
            var topLeft = matrix.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMin));
            var topRight = matrix.MultiplyPoint3x4(new Vector3(rect.xMax, rect.yMin));
            var bottomRight = matrix.MultiplyPoint3x4(new Vector3(rect.xMax, rect.yMax));
            var bottomLeft = matrix.MultiplyPoint3x4(new Vector3(rect.xMin, rect.yMax));
            blockerTop = Mathf.Min(blockerTop, Mathf.Min(Mathf.Min(topLeft.y, topRight.y), Mathf.Min(bottomLeft.y, bottomRight.y)));
            blockerBottom = Mathf.Max(blockerBottom, Mathf.Max(Mathf.Max(topLeft.y, topRight.y), Mathf.Max(bottomLeft.y, bottomRight.y)));
            maskMaterial.mainTexture = texture;
            maskMaterial.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(new Color(1, 1, 1, opacity));
            GL.TexCoord(a); GL.Vertex(topLeft);
            GL.TexCoord(b); GL.Vertex(topRight);
            GL.TexCoord(c); GL.Vertex(bottomRight);
            GL.TexCoord(d); GL.Vertex(bottomLeft);
            GL.End();
        }

        public static RenderTexture RenderLight(Rect subtitle, float reveal = 1)
        {
            GL.PopMatrix();
            RenderTexture.active = previousTarget;
            lightMaterial.SetVector("_Source", new Vector4(subtitle.center.x / canvas.x,
                1 - subtitle.center.y / canvas.y, canvas.x / canvas.y, subtitle.width / canvas.y));
            lightMaterial.SetVector("_BlockRange", new Vector4(1 - blockerBottom / canvas.y,
                1 - blockerTop / canvas.y, 0, 0));
            lightMaterial.SetVector("_LowerEdge", new Vector4(1 - subtitle.yMax / canvas.y,
                Mathf.Tan(LowerEdgeAngle * Mathf.Deg2Rad), 0, 0));
            lightMaterial.SetFloat("_Reveal", Mathf.Clamp01(reveal));
            Graphics.Blit(mask, light, lightMaterial);
            RenderTexture.active = previousTarget;
            return light;
        }

        public static void End(Rect subtitle, Color color, float beat, float opacity, float reveal = 1)
        {
            RenderLight(subtitle, reveal);
            var previous = GUI.color;
            color.a = opacity * Intensity(beat);
            GUI.color = color;
            GUI.DrawTexture(new Rect(Vector2.zero, canvas), light);
            GUI.color = previous;
        }
    }
}
