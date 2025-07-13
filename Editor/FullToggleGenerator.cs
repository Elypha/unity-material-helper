using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Elypha.Helper;
using Elypha.I18N;
using System;


public class FullToggleGeneratorWindow : EditorWindow
{
    private Vector2 scrollPosition;

    private GameObject assumedRootObject;
    private AnimationClip targetClip;
    private readonly List<GameObjectGroup> objectGroups = new();
    private void ObjectGroupsAddNew() => objectGroups.Add(new GameObjectGroup() { groupName = $"Group {objectGroups.Count + 1}" });

    private bool showAdvancedSettings = false;
    private static PluginLanguage language = PluginLanguage.English;
    private FullToggleGeneratorI18N i18n = new(language);
    private readonly GuiMessage guiMessage = new();


    [MenuItem("Tools/Elypha Toolkit/Full Toggle Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<FullToggleGeneratorWindow>("Full Toggle Generator");
        window.minSize = new Vector2(300, 400);
        if (window.objectGroups.Count == 0)
        {
            window.ObjectGroupsAddNew();
        }
    }


    private void OnGUI()
    {
        DrawAdvancedSettings();

        UnityHelper.DrawTitle1(i18n.Localise("Settings"));

        GUILayout.Label("Target Animation Clip", EditorStyles.boldLabel);

        targetClip = (AnimationClip)EditorGUILayout.ObjectField("Write curves to", targetClip, typeof(AnimationClip), false);
        assumedRootObject = (GameObject)EditorGUILayout.ObjectField("Path relative to", assumedRootObject, typeof(GameObject), true, UnityHelper.LayoutExpanded);

        EditorGUILayout.Space(8);

        DrawObjectGroupsHeader();

        EditorGUILayout.Space(2);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawObjectGroups();

        EditorGUILayout.EndScrollView();

        UnityHelper.Separator(Color.grey, 1, 0, 4);

        guiMessage.Draw(10, Repaint);

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button(i18n.Localise("Generate Full Toggle Animation"), GUILayout.Height(40)))
        {
            try
            {
                GenerateFullToggleAnimation();
            }
            finally
            {
                // make sure to clear the progress bar even if an error occurs
                EditorUtility.ClearProgressBar();
            }
        }
        GUI.backgroundColor = Color.white;
    }


    private void DrawObjectGroupsHeader()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Object Groups", EditorStyles.boldLabel);

        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("＋", new GUILayoutOption[] { GUILayout.Width(40), GUILayout.Height(20) }))
        {
            ObjectGroupsAddNew();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }


    private void DrawObjectGroups()
    {
        for (int i = 0; i < objectGroups.Count; i++)
        {
            var group = objectGroups[i];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            group.groupName = EditorGUILayout.TextField(group.groupName, new GUIStyle()
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = UnityHelper.ColourTitle2 }
            });

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("－", GUILayout.Width(25)))
            {
                objectGroups.RemoveAt(i);
                Repaint();
                return;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            for (int j = 0; j < group.gameObjects.Count; j++)
            {
                EditorGUI.BeginChangeCheck();
                group.gameObjects[j] = (GameObject)EditorGUILayout.ObjectField(group.gameObjects[j], typeof(GameObject), true);

                if (EditorGUI.EndChangeCheck())
                {
                    if (j == group.gameObjects.Count - 1 && group.gameObjects[j] != null)
                    {
                        group.gameObjects.Add(null);
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }
    }

    private void GenerateFullToggleAnimation()
    {
        if (targetClip == null)
        {
            EditorUtility.DisplayDialog("Error", "You must select a target Animation Clip.", "OK");
            return;
        }

        var validGroups = objectGroups.Where(g => g.gameObjects != null && g.gameObjects.Any(go => go != null)).ToList();

        if (validGroups.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "You must add at least one valid object group with assigned GameObjects.", "OK");
            return;
        }

        Undo.RecordObject(targetClip, "Generate Full Toggle Animation");

        targetClip.ClearCurves();

        int frame = 0;

        // a list to hold all possible states
        List<List<bool>> allStates = new()
        {
            // frame 0: all groups enabled
            Enumerable.Repeat(true, validGroups.Count).ToList()
        };

        // frame 1~: generate combinations of groups to disable
        for (int numToDisable = 1; numToDisable <= validGroups.Count; numToDisable++)
        {
            var combinations = GetCombinations(Enumerable.Range(0, validGroups.Count).ToList(), numToDisable);

            foreach (var combination in combinations)
            {
                List<bool> currentStates = Enumerable.Repeat(true, validGroups.Count).ToList();
                foreach (int indexToDisable in combination)
                {
                    currentStates[indexToDisable] = false;
                }
                allStates.Add(currentStates);
            }
        }

        // frame -1: all groups disabled
        // Enumerable.Repeat(false, validGroups.Count).ToList().ForEach(state => allStates.Add(state));

        // Save in a dictionary to avoid multiple calls to AnimationUtility.SetEditorCurve
        Dictionary<EditorCurveBinding, AnimationCurve> curves = new();

        // Loop through all states and set the curves
        foreach (var states in allStates)
        {
            // Calculate the time for this frame every frame
            float time = (float)frame / targetClip.frameRate;

            EditorUtility.DisplayProgressBar(
                "Generating Animation",
                $"Processing frame {frame}...",
                (float)frame / allStates.Count);

            SetGroupStates(validGroups, states, time, curves);
            frame++;
        }

        // After all states are set, write the curves to the AnimationClip
        foreach (var a in curves)
        {
            AnimationUtility.SetEditorCurve(targetClip, a.Key, a.Value);
        }

        EditorUtility.ClearProgressBar();
        guiMessage.Show($"Done! {frame} frames written to '{targetClip.name}'.", 3.0);

        EditorUtility.SetDirty(targetClip);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void SetGroupStates(List<GameObjectGroup> groups, List<bool> states, float time, Dictionary<EditorCurveBinding, AnimationCurve> curves)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            foreach (var go in groups[i].gameObjects)
            {
                if (go == null) continue;

                string path = assumedRootObject != null
                    ? AnimationUtility.CalculateTransformPath(go.transform, assumedRootObject.transform)
                    : AnimationUtility.CalculateTransformPath(go.transform, null);

                EditorCurveBinding binding = new()
                {
                    path = path,
                    type = typeof(GameObject),
                    propertyName = "m_IsActive"
                };

                if (!curves.ContainsKey(binding))
                {
                    curves[binding] = new AnimationCurve();
                }

                var curve = curves[binding];
                float value = states[i] ? 1f : 0f;

                int keyIndex = curve.AddKey(time, value);
            }
        }

        // Set the tangent modes to constant to ensure the state remains constant
        // IMPORTANT: This needs to be done *after* all keys are added
        foreach (AnimationCurve curve in curves.Values)
        {
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
        }
    }

    private IEnumerable<IEnumerable<T>> GetCombinations<T>(IEnumerable<T> list, int k)
    {
        if (k == 0)
            return new[] { new T[0] };

        return list.SelectMany((e, i) =>
            GetCombinations(list.Skip(i + 1), k - 1).Select(c => (new[] { e }).Concat(c)));
    }



    private void DrawAdvancedSettings()
    {
        showAdvancedSettings = EditorGUILayout.Foldout(
            showAdvancedSettings,
            "Advanced Settings",
            true,
            new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            }
        );

        if (showAdvancedSettings)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Language", GUILayout.Width(100));
            language = (PluginLanguage)EditorGUILayout.EnumPopup(language, GUILayout.Width(200));
            if (language != i18n.language)
            {
                i18n = new FullToggleGeneratorI18N(language);
            }
            GUILayout.EndHorizontal();
        }
    }
}

[System.Serializable]
public class GameObjectGroup
{
    public List<GameObject> gameObjects = new() { null };
    public string groupName = "Group";
}
