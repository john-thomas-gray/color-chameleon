using UnityEngine;

namespace CandyCruisers
{
    public static class EnemyPlaceholderArt
    {
        private static Sprite triangle;
        private static readonly System.Random dustRandom = new System.Random();
        public static float RandomDustSize(float sizeBias = 1.7f) => Mathf.Lerp(.055f, .16f,
            Mathf.Pow((float)dustRandom.NextDouble(), sizeBias));
        private static Sprite spaceDust;
        public static Sprite SpaceDust
        {
            get
            {
                if (spaceDust != null) return spaceDust;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Soft spacedust glint", wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2((x + .5f) / size * 2 - 1,
                        (y + .5f) / size * 2 - 1).magnitude;
                    float halo = Mathf.Exp(-radius * radius * 5) * .4f;
                    float core = Mathf.Exp(-radius * radius * 60);
                    float alpha = Mathf.Clamp01(halo + core) *
                        (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 1, radius)));
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                texture.Apply();
                spaceDust = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
                spaceDust.name = texture.name;
                return spaceDust;
            }
        }
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
