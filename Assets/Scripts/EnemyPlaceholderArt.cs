using UnityEngine;

namespace CandyCruisers
{
    public static class EnemyPlaceholderArt
    {
        private static Sprite triangle;
        public static Sprite Triangle
        {
            get
            {
                if (triangle != null) return triangle;
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "Tier two triangle placeholder";
                texture.wrapMode = TextureWrapMode.Clamp;
                float height = Mathf.Sqrt(3) / 2;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float py = (y + .5f) / size - .5f;
                    float px = Mathf.Abs((x + .5f) / size - .5f);
                    float halfWidth = (py + height / 2) / Mathf.Sqrt(3);
                    float alpha = Mathf.Clamp01(Mathf.Min(height / 2 - py, halfWidth - px) * size);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                texture.Apply();
                triangle = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
                triangle.name = texture.name;
                return triangle;
            }
        }
    }
}
