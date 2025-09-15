using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elypha.Common;
using UnityEditor;
using UnityEngine;


public class BatchPackageImporter : EditorWindow
{
    private Vector2 scrollPosition;


    private List<string> validPaths = new();
    private List<string> acceptedPaths = new();

    [MenuItem("Elypha/Editor/Batch Package Importer", false, 0)]
    public static void ShowWindow()
    {
        GetWindow<BatchPackageImporter>("Batch Package Importer");
    }

    private void OnGUI()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 100.0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
        GUI.Box(dropArea, "Drag and Drop .unitypackage files here", new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter }); // 可视化拖放区域

        Event currentEvent = Event.current;
        if (dropArea.Contains(currentEvent.mousePosition))
        {
            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    validPaths.Clear();
                    foreach (string path in DragAndDrop.paths)
                    {
                        if (Path.GetExtension(path).ToLower() == ".unitypackage")
                        {
                            validPaths.Add(path);
                        }
                    }
                    DragAndDrop.visualMode = validPaths.Any() ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

                    // Mark the event as used to prevent further processing
                    currentEvent.Use();
                    break;

                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();

                    acceptedPaths = DragAndDrop.paths
                        .Where(path => Path.GetExtension(path).ToLower() == ".unitypackage")
                        .ToList();

                    currentEvent.Use();
                    break;
            }
        }


        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawPackageList();

        EditorGUILayout.EndScrollView();


        Services.Separator();
        GUI.enabled = IsAllInputsValid();
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Import All", GUILayout.Height(40)))
        {
            try
            {
                ImportPackages(acceptedPaths);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private void DrawPackageList()
    {
        Services.LabelBoldColored($"Found {acceptedPaths.Count} packages.", Services.ColourTitle2);
        foreach (string path in GetPathSimpleNames(acceptedPaths))
        {
            EditorGUILayout.LabelField($"・ {path}");
        }
    }

    private List<string> GetPathSimpleNames(List<string> paths)
    {
        return paths.Select(path => Path.GetFileName(path)).ToList();
    }

    private void ImportPackages(List<string> paths)
    {
        try
        {
            // Stop asset database updates to prevent multiple refreshes
            AssetDatabase.StartAssetEditing();
            foreach (string path in paths)
            {
                Debug.Log($"[BatchPackageImporter] Importing: {path}");
                AssetDatabase.ImportPackage(path, false);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            // (Optional) Manually trigger a refresh to ensure everything is updated
            AssetDatabase.Refresh();
        }
    }

    private bool IsAllInputsValid()
    {
        return acceptedPaths.Any();
    }
}
