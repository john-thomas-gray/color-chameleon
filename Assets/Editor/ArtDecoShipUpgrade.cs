using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class ArtDecoShipUpgrade
    {
        [MenuItem("Candy Cruisers/Art/Apply Art Deco Ships")]
        public static void Apply()
        {
            var generator = typeof(EnemyPlaceholderArt).GetMethod("DecoTexture");
            if (generator == null)
                throw new InvalidOperationException("The Art Deco generator is no longer installed. Existing artwork was not changed.");
            Directory.CreateDirectory("Assets/Art/Ships");
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
            {
                string prefabPath = "Assets/Prefabs/" + color + " Enemy.prefab";
                var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var visuals = CharacterVisuals.Ensure(prefab);
                    var body = visuals.Body;
                    float size = body.sprite.bounds.size.x;
                    Bounds hitBounds = visuals.HitBounds;
                    string path = "Assets/Art/Ships/" + color + "Ship.png";
                    var texture = (Texture2D)generator.Invoke(null, new object[] { color, false });
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(texture);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 256 / size;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                    body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    body.color = EnemyPalette.Get(color);
                    if (visuals.HitBounds != hitBounds) throw new Exception("Ship art changed collision bounds");
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            AssetDatabase.SaveAssets();
            CapturePreview();
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            for (int col = 0; col < 6; col++)
            {
                var ordinary = ProgressionChecks.Add(grid, (EnemyColor)col, col, 1);
                ordinary.GetComponent<EnemyAbilities>().enabled = false;
                var special = ProgressionChecks.Add(grid, (EnemyColor)col, col, 3);
                special.GetComponent<EnemyAbilities>().enabled = false;
                // Orange has no upgraded tier.
                if ((EnemyColor)col != EnemyColor.Orange)
                {
                    float width = special.Visuals.Body.sprite.bounds.size.x;
                    special.Visuals.Body.sprite = EnemyPlaceholderArt.Triangle;
                    special.Visuals.Root.localScale = Vector3.one * width;
                }
            }
            Directory.CreateDirectory("TestResults");
            GameplayChecks.Capture(900, 1200, "art-deco-ships");
            GameplayChecks.Capture(960, 540, "art-deco-ships-landscape");
        }
    }
}
