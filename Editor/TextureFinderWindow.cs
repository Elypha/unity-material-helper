using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public TextureImporter Importer;
    }

    [MenuItem("Elypha/Texture Finder")]
    public static void ShowWindow()
    {
        var window = GetWindow<TextureFinderWindow>("Texture Finder");
        window.minSize = new Vector2(500, 400);
    }

    private Vector2 scrollPosition;
    private bool isResizing = false;
    private float nameColumnWidth = 200f;
    private const float actionColumnWidth = 60f;
    private GUIStyle pathTextStyle;

    private DefaultAsset targetFolder;
    private FilterType currentFilter = FilterType.UsedCrunchCompression;

    private List<TextureResult> reportData = new List<TextureResult>();

    private void OnEnable()
    {
        pathTextStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = false,
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


        // Buttons Section
        // --------------------------------
        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh", GUILayout.Height(24)))
        {
            AnalyzeTextures();
        }

        EditorGUI.BeginDisabledGroup(reportData == null || reportData.Count == 0);
        if (GUILayout.Button("Select All", GUILayout.Height(24)))
        {
            SelectAllResults();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        Services.Separator();

        // Report Header
        // --------------------------------
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // 1. Action Header
        GUILayout.Label("Action", GUILayout.Width(actionColumnWidth));

        // 2. Texture Asset Header
        GUILayout.Label("Texture Asset", GUILayout.Width(nameColumnWidth));

        // Resize Handle
        Rect resizeRect = GUILayoutUtility.GetLastRect();
        resizeRect.x += resizeRect.width;
        resizeRect.width = 5f;

        // 3. Path Header
        GUILayout.Label("Asset Path");
        EditorGUILayout.EndHorizontal();

        HandleResize(resizeRect);

        // Report Content
        // --------------------------------
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (reportData != null && reportData.Count > 0)
        {
            foreach (var item in reportData)
            {
                EditorGUILayout.BeginHorizontal("box");

                // Col 1: Action (Button or None)
                // -----------------------------
                GUILayout.BeginVertical(GUILayout.Width(actionColumnWidth));
                if (currentFilter == FilterType.IsNormalMap)
                {
                    bool isEligible = IsEligibleForBC5(item.Importer);

                    EditorGUI.BeginDisabledGroup(!isEligible);
                    if (GUILayout.Button("BC5", GUILayout.Height(18)))
                    {
                        ApplyBC5Settings(item.Importer);
                    }
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    GUILayout.Label("None", EditorStyles.miniLabel);
                }
                GUILayout.EndVertical();

                // Col 2: Clickable Object
                // -----------------------------
                EditorGUILayout.ObjectField(item.TextureObject, typeof(Texture), false, GUILayout.Width(nameColumnWidth));

                GUILayout.Space(10);

                // Col 3: Path text
                // -----------------------------
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

    private bool IsEligibleForBC5(TextureImporter importer)
    {
        if (importer == null) return false;

        // 1. Check if Standalone (Windows/Mac/Linux) settings are overridden
        var settings = importer.GetPlatformTextureSettings("Standalone");
        if (settings.overridden) return false;

        // 2. Check if default settings are "Default"
        // Compressed = Compression @ Normal Quality
        // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/TextureImporterCompression.html
        bool isDefaultCompression = importer.textureCompression == TextureImporterCompression.Compressed;
        bool isNotCrunched = !importer.crunchedCompression;

        return isDefaultCompression && isNotCrunched;
    }

    private void ApplyBC5Settings(TextureImporter importer)
    {
        if (importer == null) return;

        // Get Standalone settings
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Standalone");

        // Set Override
        settings.overridden = true;
        settings.name = "Standalone";

        // Set parameters
        settings.maxTextureSize = importer.maxTextureSize; // Keep the same size as Default
        settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
        settings.format = TextureImporterFormat.BC5;

        // Apply settings
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();
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
            nameColumnWidth = Event.current.mousePosition.x - actionColumnWidth; // 减去第一列的宽度
            nameColumnWidth = Mathf.Clamp(nameColumnWidth, 100f, position.width - 100f - actionColumnWidth);
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

        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning("Selected object is not a folder.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { folderPath });

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string guid = guids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (i % 50 == 0)
                    EditorUtility.DisplayProgressBar("Scanning Textures", path, (float)i / guids.Length);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    bool isMatch = false;

                    switch (currentFilter)
                    {
                        case FilterType.UsedCrunchCompression:
                            isMatch = importer.crunchedCompression;
                            break;

                        case FilterType.IsNormalMap:
                            isMatch = importer.textureType == TextureImporterType.NormalMap;
                            break;
                    }

                    if (isMatch)
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
                        var relativePath = path.Replace(folderPath, ".");
                        reportData.Add(new TextureResult { TextureObject = tex, Path = relativePath, Importer = importer });
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void SelectAllResults()
    {
        if (reportData == null || reportData.Count == 0) return;

        Object[] objectsToSelect = reportData
            .Where(x => x.TextureObject != null)
            .Select(x => x.TextureObject as Object)
            .ToArray();

        Selection.objects = objectsToSelect;
        EditorUtility.FocusProjectWindow();
    }
}
