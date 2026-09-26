using System;
using UnityEditor;
using UnityEngine;

namespace CandyCruisers.Editor
{
    public sealed class SpawnOverrideWindow : EditorWindow
    {
        [MenuItem("Candy Cruisers/Spawn Overrides")]
        public static void Open()
        {
            var window = GetWindow<SpawnOverrideWindow>("Spawn Overrides");
            window.minSize = new Vector2(240, 230);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            SpawnOverride.Enabled = EditorGUILayout.ToggleLeft("Override level eligibility", SpawnOverride.Enabled);
            EditorGUILayout.Space(6);
            foreach (EnemyColor color in Enum.GetValues(typeof(EnemyColor)))
            {
                int bit = 1 << (int)color;
                bool selected = SpawnOverride.Allows(color);
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect swatch = GUILayoutUtility.GetRect(12, 18, GUILayout.Width(12));
                    EditorGUI.DrawRect(new Rect(swatch.x, swatch.y + 3, 12, 12), EnemyPalette.Get(color));
                    using (new EditorGUI.DisabledScope(selected && SpawnOverride.Types == bit))
                    {
                        bool next = EditorGUILayout.ToggleLeft(color.ToString(), selected);
                        if (next != selected) SpawnOverride.Types = next ? SpawnOverride.Types | bit : SpawnOverride.Types & ~bit;
                    }
                }
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(SpawnOverride.Enabled ? "Override active" : "Normal level unlocks", EditorStyles.boldLabel);
        }
    }
}
