using System.Collections.Generic;
using System.Linq;
using Elypha.Common;
using UnityEditor;
using UnityEngine;

public class MaterialMatcher : EditorWindow
{
    [MenuItem("Elypha/Material Matcher")]
    public static void ShowWindow()
    {
        var window = GetWindow<MaterialMatcher>("Material Matcher");
        window.minSize = new Vector2(400, 400);
    }

    private GameObject referenceObject;
    private GameObject targetObject;

    private readonly List<string> referenceUnusedReport = new();
    private readonly List<string> targetUnsetReport = new();
    private int matchCount = 0;

    private Vector2 scrollPosition;

    private void OnGUI()
    {
        referenceObject = (GameObject)EditorGUILayout.ObjectField("Reference Object", referenceObject, typeof(GameObject), true);
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        EditorGUILayout.Space();
        GUI.enabled = referenceObject && targetObject;
        if (GUILayout.Button("Apply Materials"))
        {
            ApplyMaterials();
        }
        GUI.enabled = true;

        Services.Separator();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (matchCount > 0 || referenceUnusedReport.Any() || targetUnsetReport.Any())
        {
            EditorGUILayout.HelpBox($"Matched: {matchCount}, Reference Unused: {referenceUnusedReport.Count}, Target Unset: {targetUnsetReport.Count}", MessageType.Info);
        }

        EditorGUILayout.LabelField("Reference Unused:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (referenceUnusedReport.Any())
        {
            foreach (var line in referenceUnusedReport)
            {
                EditorGUILayout.LabelField(line);
            }
        }
        else
        {
            EditorGUILayout.LabelField("All reference materials used");
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Target Unset:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (targetUnsetReport.Any())
        {
            foreach (var line in targetUnsetReport)
            {
                EditorGUILayout.LabelField(line);
            }
        }
        else
        {
            EditorGUILayout.LabelField("All target renderers matched");
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.EndScrollView();
    }

    private void ApplyMaterials()
    {
        referenceUnusedReport.Clear();
        targetUnsetReport.Clear();
        matchCount = 0;

        Undo.SetCurrentGroupName("Match Materials");
        int group = Undo.GetCurrentGroup();

        var refRenderers = referenceObject.GetComponentsInChildren<Renderer>(true);
        var refDataMap = new Dictionary<string, Material[]>();

        foreach (var r in refRenderers)
        {
            if (IsValidRenderer(r))
            {
                string path = r.transform.GetRelativePath(referenceObject.transform);
                refDataMap[path] = r.sharedMaterials;
            }
        }

        var targetRenderers = targetObject.GetComponentsInChildren<Renderer>(true);
        var targetPathsProcessed = new HashSet<string>();

        foreach (var tRenderer in targetRenderers)
        {
            if (!IsValidRenderer(tRenderer)) continue;

            string path = tRenderer.transform.GetRelativePath(targetObject.transform);
            targetPathsProcessed.Add(path);

            if (refDataMap.TryGetValue(path, out Material[] mats))
            {
                Undo.RecordObject(tRenderer, "Apply Material Match");
                tRenderer.sharedMaterials = mats;
                matchCount++;
            }
            else
            {
                targetUnsetReport.Add(path);
            }
        }

        foreach (var kvp in refDataMap)
        {
            if (!targetPathsProcessed.Contains(kvp.Key))
            {
                referenceUnusedReport.Add(kvp.Key);
            }
        }

        Undo.CollapseUndoOperations(group);

        string logMsg = $"<b>[Material Matcher]</b> Completed.\nMatched: {matchCount}\nRef Unused: {referenceUnusedReport.Count}\nTarget Unset: {targetUnsetReport.Count}";
        Debug.Log(logMsg);

        if (referenceUnusedReport.Count > 0)
        {
            Debug.LogWarning("[Ref Unused Paths]:\n" + string.Join("\n", referenceUnusedReport));
        }
        if (targetUnsetReport.Count > 0)
        {
            Debug.LogWarning("[Target Unset Paths]:\n" + string.Join("\n", targetUnsetReport));
        }
    }

    private bool IsValidRenderer(Renderer r)
    {
        return r is MeshRenderer || r is SkinnedMeshRenderer;
    }
}