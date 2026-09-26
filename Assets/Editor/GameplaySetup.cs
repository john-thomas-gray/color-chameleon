using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class GameplaySetup
    {
        public const string ScenePath = "Assets/Scenes/Gameplay.unity";

        [MenuItem("Candy Cruisers/Create Gameplay Scene")]
        public static void Create()
        {
            if (File.Exists(ScenePath))
                throw new System.InvalidOperationException("Gameplay scene already exists; edit it directly.");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs");
            var stars = ImportSprite("Assets/Art/galaxy.jpeg");
            var blue = CreateEnemy("Blue Enemy", ImportSprite("Assets/Art/blueberry.png"),
                new Color(0.22f, 0.64f, 1f));
            var red = CreateEnemy("Red Enemy", ImportSprite("Assets/Art/Apple.png"),
                new Color(1f, 0.26f, 0.33f));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(GameplayFraming));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            var background = new GameObject("Star Background", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            background.sprite = stars;
            background.sortingOrder = -100;
            cameraObject.GetComponent<GameplayFraming>().SetBackground(background);

            var grid = new GameObject("Enemy Grid", typeof(EnemyGridMovement));
            grid.transform.position = new Vector3(0, 3f, 0);

            PlayerSettings.companyName = "John Gray";
            PlayerSettings.productName = "Candy Cruisers";
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        private static Sprite ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject CreateEnemy(string name, Sprite sprite, Color color)
        {
            var instance = new GameObject(name, typeof(SpriteRenderer));
            var renderer = instance.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 10;
            instance.transform.localScale = Vector3.one * (0.58f / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, $"Assets/Prefabs/{name}.prefab");
            Object.DestroyImmediate(instance);
            return prefab;
        }
    }
}
