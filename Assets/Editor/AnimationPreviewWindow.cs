using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyCruisers.Editor
{
    [InitializeOnLoad]
    public sealed class AnimationPreviewWindow : EditorWindow
    {
        private static readonly float[] Speeds = { .1f, .25f, .5f, 1, 2 };
        private static readonly string[] SpeedNames = { "0.1x", "0.25x", "0.5x", "1x", "2x" };
        private const string Key = AnimationPreviewStage.SettingsKey;
        private static readonly AnimationPreviewStage.Animation[] Choices = Enum.GetValues(typeof(AnimationPreviewStage.Animation))
            .Cast<AnimationPreviewStage.Animation>().OrderBy(choice => choice == AnimationPreviewStage.Animation.StartGame ? 1.5 : (int)choice).ToArray();
        private static string[] choiceNames;
        private static readonly string[] EntranceNames = { "All Three", "Flip Right", "Skid Left", "Tractor Beam" };
        private static readonly MenuIntroAnimation.Entrance[] Entrances = { MenuIntroAnimation.Entrance.AllVariants,
            MenuIntroAnimation.Entrance.FlipRight, MenuIntroAnimation.Entrance.SkidLeft, MenuIntroAnimation.Entrance.TractorBeam };

        static AnimationPreviewWindow() => EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            if (scene.path == AnimationPreviewStage.ScenePath && !Application.isBatchMode) Open();
        };

        [MenuItem("Candy Cruisers/Animation Preview")]
        public static void Open()
        {
            var window = GetWindow<AnimationPreviewWindow>("Animation Preview");
            window.minSize = new Vector2(310, 390);
            window.Show();
        }

        public static void CreateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(AnimationPreviewStage.ScenePath) != null) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Animation Preview", typeof(AnimationPreviewStage));
            EditorSceneManager.SaveScene(scene, AnimationPreviewStage.ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void OpenScene()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScene();
            EditorSceneManager.OpenScene(AnimationPreviewStage.ScenePath);
            Selection.activeGameObject = GameObject.Find("Animation Preview");
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            float width = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 116;
            try { DrawPanel(); }
            finally { EditorGUIUtility.labelWidth = width; }
        }

        private void DrawPanel()
        {
            choiceNames ??= Choices.Select(choice => ObjectNames.NicifyVariableName(choice.ToString())).ToArray();
            var stage = AnimationPreviewStage.Instance;
            bool running = Application.isPlaying && stage != null;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("FolderOpened Icon").image,
                        "Open preview scene"), EditorStyles.toolbarButton, GUILayout.Width(32))) OpenScene();
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying && !running))
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("PlayButton").image,
                        "Play preview"), EditorStyles.toolbarButton, GUILayout.Width(32)))
                    {
                        if (running)
                        {
                            if (stage.Elapsed >= stage.PlaybackSeconds) stage.Replay();
                            else EditorApplication.isPaused = false;
                        }
                        else
                        {
                            OpenScene();
                            if (SceneManager.GetActiveScene().path == AnimationPreviewStage.ScenePath)
                                EditorApplication.EnterPlaymode();
                        }
                    }
                using (new EditorGUI.DisabledScope(!running || stage.Loading))
                {
                    bool paused = GUILayout.Toggle(EditorApplication.isPaused,
                        new GUIContent(EditorGUIUtility.IconContent("PauseButton").image, "Pause preview"),
                        EditorStyles.toolbarButton, GUILayout.Width(32));
                    if (running && paused != EditorApplication.isPaused) EditorApplication.isPaused = paused;
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("StepButton").image,
                        "Advance one frame"), EditorStyles.toolbarButton, GUILayout.Width(32)))
                    { EditorApplication.isPaused = true; EditorApplication.Step(); }
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Refresh").image,
                        "Replay from the beginning"), EditorStyles.toolbarButton, GUILayout.Width(32))) stage.Replay();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label(running ? stage.Loading ? "Loading" : EditorApplication.isPaused ? "Paused" : "Playing" : "Stopped",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(12);
            if (running && stage.Failure != null) EditorGUILayout.HelpBox(stage.Failure, MessageType.Error);
            EditorGUI.BeginChangeCheck();
            var saved = (AnimationPreviewStage.Animation)SessionState.GetInt(Key + "Animation", (int)AnimationPreviewStage.Animation.LifeLoss);
            var selected = Choices[EditorGUILayout.Popup("Animation", Mathf.Max(0, Array.IndexOf(Choices, saved)), choiceNames)];
            var entrance = (MenuIntroAnimation.Entrance)SessionState.GetInt(Key + "MenuEntranceMode", -2);
            if (entrance == MenuIntroAnimation.Entrance.Random) entrance = MenuIntroAnimation.Entrance.AllVariants;
            if (selected == AnimationPreviewStage.Animation.MenuArrival)
                entrance = Entrances[EditorGUILayout.Popup("Character entrance", Mathf.Max(0, Array.IndexOf(Entrances, entrance)), EntranceNames)];
            int color = SessionState.GetInt(Key + "Color", 0);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Color");
                foreach (EnemyColor choice in Enum.GetValues(typeof(EnemyColor)))
                {
                    var previous = GUI.backgroundColor;
                    GUI.backgroundColor = EnemyPalette.Get(choice);
                    if (GUILayout.Toggle(color == (int)choice, new GUIContent("", choice.ToString()), "Button",
                        GUILayout.Width(24), GUILayout.Height(22))) color = (int)choice;
                    GUI.backgroundColor = previous;
                }
            }
            int level = EditorGUILayout.IntSlider("Level", SessionState.GetInt(Key + "Level", 19), 1, 99);
            float tempo = EditorGUILayout.Slider("Beats per minute", SessionState.GetFloat(Key + "Tempo", 120), 40, 240);
            if (EditorGUI.EndChangeCheck())
            {
                SessionState.SetInt(Key + "Animation", (int)selected);
                SessionState.SetInt(Key + "MenuEntranceMode", (int)entrance);
                SessionState.SetInt(Key + "Color", color);
                SessionState.SetInt(Key + "Level", level);
                SessionState.SetFloat(Key + "Tempo", tempo);
                if (running) { stage.ReadSettings(); stage.Replay(); }
            }
            EditorGUILayout.Space(12);
            EditorGUI.BeginChangeCheck();
            int speedIndex = Array.IndexOf(Speeds, SessionState.GetFloat(Key + "Speed", 1));
            float speed = Speeds[EditorGUILayout.Popup("Speed", Mathf.Max(0, speedIndex), SpeedNames)];
            float duration = EditorGUILayout.Slider("Minimum seconds", SessionState.GetFloat(Key + "Duration", 10), 2, 30);
            bool loop = EditorGUILayout.Toggle("Loop", SessionState.GetBool(Key + "Loop", true));
            bool sound = EditorGUILayout.Toggle("Sound effects", SessionState.GetBool(Key + "Sound", false));
            if (EditorGUI.EndChangeCheck())
            {
                SessionState.SetFloat(Key + "Speed", speed);
                SessionState.SetFloat(Key + "Duration", duration);
                SessionState.SetBool(Key + "Loop", loop);
                SessionState.SetBool(Key + "Sound", sound);
                if (running) stage.ReadSettings();
            }
            EditorGUILayout.Space(12);
            var progress = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progress, running ? Mathf.Clamp01(stage.Elapsed / stage.PlaybackSeconds) : 0,
                running ? stage.Elapsed.ToString("F2") + " / " + stage.PlaybackSeconds.ToString("F2") + " s" : "Ready");
            if (running && stage.Ready)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("State", stage.Session.State.ToString());
                EditorGUILayout.LabelField("Beat", (stage.Elapsed * stage.Tempo / 60).ToString("F2"));
            }
        }
    }
}
