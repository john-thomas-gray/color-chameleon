using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public static class VisualUpgrade
    {
        [MenuItem("Candy Cruisers/Upgrade Visual Children")]
        public static void Apply()
        {
            foreach (EnemyColor color in System.Enum.GetValues(typeof(EnemyColor)))
            {
                string path = "Assets/Prefabs/" + color + " Enemy.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Upgrade(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            Upgrade(GameObject.Find("Player"));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        public static void Upgrade(GameObject actor)
        {
            var visual = actor.transform.Find("Visuals");
            if (visual == null)
            {
                visual = new GameObject("Visuals").transform;
                visual.SetParent(actor.transform, false);
            }
            var rootBody = actor.GetComponent<SpriteRenderer>();
            if (rootBody != null)
            {
                var body = new GameObject("Body", typeof(SpriteRenderer));
                body.transform.SetParent(visual, false);
                EditorUtility.CopySerialized(rootBody, body.GetComponent<SpriteRenderer>());
                Object.DestroyImmediate(rootBody);
            }
            foreach (var sprite in actor.GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.transform.parent == actor.transform) sprite.transform.SetParent(visual, true);
            var adapter = CharacterVisuals.Ensure(actor);
            adapter.Configure(visual.GetComponentInChildren<SpriteRenderer>(), visual);
            var relay = visual.GetComponent<VisualAnimationEvents>();
            if (relay == null) relay = visual.gameObject.AddComponent<VisualAnimationEvents>();
            relay.Configure(adapter);
            EditorUtility.SetDirty(adapter);
        }
    }
}
