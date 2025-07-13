using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Elypha.Helper;
using Elypha.I18N;


public class SoftDisableEditor : EditorWindow
{
    private Vector2 scrollPosition;

    private AnimationClip targetClip;
    private GameObject animatorRootObject;
    private readonly List<GameObject> objectsToDisable = new() { null };

    private bool showAdvancedSettings = false;
    private static PluginLanguage language = PluginLanguage.English;
    private TemplateI18N i18n = new(language);
    private readonly GuiMessage guiMessage = new();


    [MenuItem("Tools/Elypha Toolkit/Soft Disable")]
    public static void ShowWindow()
    {
        var window = GetWindow<SoftDisableEditor>("Soft Disable");
        window.minSize = new Vector2(300, 400);
    }

    private void OnGUI()
    {

        DrawAdvancedSettings();

        UnityHelper.DrawTitle1(i18n.Localise("Settings"));

        EditorGUILayout.LabelField("Target Animation Clip", EditorStyles.boldLabel);
        targetClip = (AnimationClip)EditorGUILayout.ObjectField("Write curves to", targetClip, typeof(AnimationClip), false);
        animatorRootObject = (GameObject)EditorGUILayout.ObjectField("Path relative to", animatorRootObject, typeof(GameObject), true);


        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Skinned Mesh Renderers to disable", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawGameObjectList();

        EditorGUILayout.EndScrollView();

        UnityHelper.Separator(Color.grey, 1, 0, 4);

        guiMessage.Draw(10, Repaint);


        GUI.enabled = IsAllInputsValid();
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Generate Disable Animation", GUILayout.Height(40)))
        {
            ProcessAnimation();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private void DrawGameObjectList()
    {
        for (int i = 0; i < objectsToDisable.Count; i++)
        {
            EditorGUI.BeginChangeCheck();
            GameObject newObj = (GameObject)EditorGUILayout.ObjectField(objectsToDisable[i], typeof(GameObject), true);

            if (EditorGUI.EndChangeCheck())
            {
                if (newObj != null && newObj.GetComponent<SkinnedMeshRenderer>() == null)
                {
                    Debug.LogError($"错误: '{newObj.name}' 不包含 SkinnedMeshRenderer 组件，无法添加。");
                }
                else
                {
                    objectsToDisable[i] = newObj;
                }
            }
        }

        if (objectsToDisable.Count == 0 || objectsToDisable[objectsToDisable.Count - 1] != null)
        {
            objectsToDisable.Add(null);
            Repaint();
        }
    }

    private void ProcessAnimation()
    {
        List<GameObject> validObjects = objectsToDisable.Where(obj => obj != null).ToList();

        // Register the clip for an undo operation. This single call covers all subsequent modifications.
        Undo.RecordObject(targetClip, "Generate Soft Disable Animation");

        targetClip.ClearCurves();

        Transform rootTransform = animatorRootObject != null ? animatorRootObject.transform : null;

        foreach (GameObject go in validObjects)
        {
            string path = AnimationUtility.CalculateTransformPath(go.transform, rootTransform);

            EditorCurveBinding binding = new()
            {
                path = path,
                type = typeof(SkinnedMeshRenderer),
                propertyName = "m_Enabled" // The internal property name for enabling/disabling a component.
            };

            // We set tangents to create a "stepped" curve.
            Keyframe key = new(time: 0f, value: 0f, inTangent: float.PositiveInfinity, outTangent: float.PositiveInfinity);
            AnimationCurve curve = new(key);

            // Note: For performance, it's better to build a list and call SetEditorCurves once,
            // but for this tool, applying one by one is fine and conceptually simpler.
            AnimationUtility.SetEditorCurve(targetClip, binding, curve);
        }

        EditorUtility.SetDirty(targetClip);

        // string successMessage = $"成功为 {} 个对象在动画剪辑 '{}' 中生成了禁用关键帧。";
        string successMessage =$"Done! Wrote disable keyframes for {validObjects.Count} objects in '{targetClip.name}'.";
        guiMessage.Show(successMessage, 3);
        Debug.Log(successMessage);
    }

    private bool IsAllInputsValid()
    {
        return targetClip != null && objectsToDisable.Any(obj => obj != null);
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
                i18n = new TemplateI18N(language);
            }
            GUILayout.EndHorizontal();
        }
    }
}