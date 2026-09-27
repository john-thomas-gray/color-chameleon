using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class OrangeUpgrade
    {
        public static void Preview()
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            var grid = GameObject.Find("Enemy Grid").GetComponent<EnemyGrid>();
            var orange = ProgressionChecks.Add(grid, EnemyColor.Orange, 1, 2);
            ProgressionChecks.Add(grid, EnemyColor.Red, 2, 2);
            ProgressionChecks.Add(grid, EnemyColor.Red, 4, 0);
            ProgressionChecks.Add(grid, EnemyColor.Green, 3, 1);
            GameplayChecks.Capture(540, 960, "orange-before-swap");
            if (!grid.TryOrangeSwap(orange.Id)) throw new System.Exception("Orange preview swap failed");
            foreach (var effect in grid.GetComponentsInChildren<EnemyPresentation>())
                effect.Tick(.35f, effect.GetComponent<GridEnemy>().Color, 0);
            GameplayChecks.Capture(540, 960, "orange-swap");
            GameplayChecks.Capture(960, 540, "orange-swap-landscape");
            Debug.Log("Orange previews rendered.");
        }

        public static void Apply()
        {
            const string path = "Assets/Prefabs/Orange Enemy.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Yellow Enemy.prefab");
                try
                {
                    root.name = "Orange Enemy";
                    root.GetComponentInChildren<SpriteRenderer>().color = EnemyPalette.Get(EnemyColor.Orange);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            GameObject.Find("Enemy Grid").GetComponent<EnemyRowSpawner>().ConfigureOrange(prefab);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Orange prefab and gameplay scene configured.");
        }
    }
}
