using System.Collections.Generic;
using System.IO;
using Elypha.Common;
using UnityEditor;
using UnityEngine;

public class TextureFinderWindow : EditorWindow
{
    public enum FilterType
    {
        UsedCrunchCompression,
        IsNormalMap,
    }

    private struct TextureResult
    {
        public Texture TextureObject;
        public string Path;
    }

    [MenuItem("Elypha/Texture Finder")]
    public static void ShowWindow()
    {
        var window = GetWindow<TextureFinderWindow>("Texture Finder");
        window.minSize = new Vector2(400, 400);
    }

    private Vector2 scrollPosition;
    private bool isResizing = false;
    private float nameColumnWidth = 200f; // Initial width of the name column
    private GUIStyle pathTextStyle;

    private DefaultAsset targetFolder; // Input folder
    private FilterType currentFilter = FilterType.UsedCrunchCompression; // Currently selected filter

    private List<TextureResult> reportData = new List<TextureResult>();

    private void OnEnable()
    {
        pathTextStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = false, // Paths are usually long, so it's better not to wrap text to keep it tidy
            richText = true,
            alignment = TextAnchor.MiddleLeft
        };
    }

    private void OnGUI()
    {
        // Input Section
        // --------------------------------
        var originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 120f;

        EditorGUI.BeginChangeCheck();

        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);
        currentFilter = (FilterType)EditorGUILayout.EnumPopup("Filter", currentFilter);

        if (EditorGUI.EndChangeCheck())
        {
            AnalyzeTextures();
        }

        EditorGUIUtility.labelWidth = originalLabelWidth;

        Services.Separator();

        // Report Header
        // --------------------------------
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Texture Asset", GUILayout.Width(nameColumnWidth));

        // Draw resize handle area
        Rect resizeRect = GUILayoutUtility.GetLastRect();
        resizeRect.x += resizeRect.width;
        resizeRect.width = 5f;

        GUILayout.Label("Asset Path");
        EditorGUILayout.EndHorizontal();

        // handle column resizing
        HandleResize(resizeRect);

        // Report Content
        // --------------------------------
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (reportData != null && reportData.Count > 0)
        {
            foreach (var item in reportData)
            {
                EditorGUILayout.BeginHorizontal("box");

                // First column: clickable object
                EditorGUILayout.ObjectField(item.TextureObject, typeof(Texture), false, GUILayout.Width(nameColumnWidth));

                GUILayout.Space(10);

                // Second column: path text
                EditorGUILayout.LabelField(item.Path, pathTextStyle);

                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            if (targetFolder == null)
            {
                EditorGUILayout.LabelField("Please select a Target Folder to analyze.");
            }
            else
            {
                EditorGUILayout.LabelField($"No textures found matching criteria: {currentFilter}");
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void HandleResize(Rect resizeRect)
    {
        EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);

        if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
        {
            isResizing = true;
        }

        if (isResizing)
        {
            nameColumnWidth = Event.current.mousePosition.x;
            nameColumnWidth = Mathf.Clamp(nameColumnWidth, 100f, position.width - 100f);
            Repaint();
        }

        if (Event.current.type == EventType.MouseUp)
        {
            isResizing = false;
        }
    }

    private void AnalyzeTextures()
    {
        reportData.Clear();

        if (targetFolder == null) return;

        string folderPath = AssetDatabase.GetAssetPath(targetFolder);

        // Ensure the selected object is a folder
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning("Selected object is not a folder.");
            return;
        }

        // Find all GUIDs of texture types
        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { folderPath });

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Display progress bar because it may lag when there are many files
                if (i % 50 == 0) // Update UI every 50 to avoid slowdown due to too frequent refresh
                    EditorUtility.DisplayProgressBar("Scanning Textures", path, (float)i / guids.Length);

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    bool isMatch = false;

                    switch (currentFilter)
                    {
                        case FilterType.UsedCrunchCompression:
                            // Check if Crunch compression is enabled
                            isMatch = importer.crunchedCompression;
                            break;

                        case FilterType.IsNormalMap:
                            // Check if marked as a normal map
                            isMatch = importer.textureType == TextureImporterType.NormalMap;
                            break;
                    }

                    if (isMatch)
                    {
                        Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
                        var relativePath = path.Replace(folderPath, ".");
                        reportData.Add(new TextureResult { TextureObject = tex, Path = relativePath });
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
