using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Elypha.Common;
using UnityEditor;
using UnityEngine;

public class SdkBuildLogViewer : EditorWindow
{
    private Vector2 scrollPos;

    public BuildReport buildReport = new();
    private static readonly Color[] barColors = new Color[] {
        new(0.27f, 0.51f, 0.93f), // blue
        new(0.93f, 0.43f, 0.27f), // orange
        new(0.53f, 0.73f, 0.33f), // green
        new(0.93f, 0.73f, 0.27f), // yellow
        new(0.47f, 0.73f, 0.93f), // light blue
        new(0.93f, 0.47f, 0.73f)  // pink
    };


    [MenuItem("Elypha/Editor/SDK Build Log Viewer", false, 1)]
    public static void ShowWindow()
    {
        var window = GetWindow(typeof(SdkBuildLogViewer));
        window.titleContent = new GUIContent("SDK Build Log Viewer");
        window.minSize = new Vector2(600, 400);
    }


    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Analyse Last Build", GUILayout.Height(40)))
        {
            buildReport = GetLastBuildReport();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();

        if (buildReport.isReady)
            DrawBuildReport();

        EditorGUILayout.EndScrollView();
    }


    private void DrawBuildReport()
    {
        if (buildReport.compressedSize != null && buildReport.UncompressedCategoryList.Count > 0)
        {
            Services.LabelBoldColored($"Build Type: {buildReport.buildType}", Services.ColourTitle1);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"Size Compressed    {buildReport.compressedSize,8}", Utils.MonoStyle);
            EditorGUILayout.LabelField($"Size Uncompressed  {buildReport.uncompressedSize ?? "Unknown",8}", Utils.MonoStyle);
            EditorGUI.indentLevel--;
            Services.LabelBoldColored("Uncompressed Build Size Breakdown:", Services.ColourTitle1);

            var maxCategoryLengthInCharSafe = buildReport.UncompressedCategoryList.Select(c => c.category.Length).Max() + 2;
            var _contentForWidth = new GUIContent(new string(' ', maxCategoryLengthInCharSafe + 2 + 8 + 2 + 7));
            var _labelWidthSafe = Utils.MonoStyle.CalcSize(_contentForWidth).x + 26;

            var accumulatedPercent = 0f;
            var colorIndex = 0;
            EditorGUI.indentLevel++;
            foreach (var category in buildReport.UncompressedCategoryList)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(
                    $"{category.category.PadRight(maxCategoryLengthInCharSafe)}  {category.size,8}  {category.percent,7}",
                    Utils.MonoStyle,
                    new GUILayoutOption[] { GUILayout.ExpandWidth(false), GUILayout.Width(_labelWidthSafe) }
                );

                var lineRect = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
                if (float.TryParse(category.percent.TrimEnd('%'), out float currentPercent))
                {
                    currentPercent /= 100f;
                    EditorGUI.DrawRect(new Rect(lineRect.x, lineRect.y, lineRect.width, lineRect.height), new Color(0.1f, 0.1f, 0.1f, 0.5f));

                    var startX = lineRect.x + lineRect.width * accumulatedPercent;
                    var segmentWidth = lineRect.width * currentPercent;
                    var segmentRect = new Rect(startX, lineRect.y, segmentWidth, lineRect.height);
                    EditorGUI.DrawRect(segmentRect, barColors[colorIndex % barColors.Length]);

                    accumulatedPercent += currentPercent;
                    colorIndex++;
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        if (buildReport.buildAssetList.Count > 0)
        {
            Services.LabelBoldColored("Assets and Files from the Resources Folder:", Services.ColourTitle1);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Path", EditorStyles.toolbarButton);
            EditorGUILayout.LabelField("Size", EditorStyles.toolbarButton, GUILayout.Width(80));
            EditorGUILayout.LabelField("%", EditorStyles.toolbarButton, GUILayout.Width(50));
            EditorGUILayout.LabelField("Locate", EditorStyles.toolbarButton, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            foreach (BuildAsset buildAsset in buildReport.buildAssetList)
            {
                var isAssetPath = buildAsset.path.StartsWith("Assets/");
                var isSelectable = buildAsset.path.StartsWith("Assets/") || buildAsset.path.StartsWith("Packages/");
                EditorGUILayout.BeginHorizontal();
                if (!isAssetPath)
                    GUI.color = Services.ColourLightBlue;
                EditorGUILayout.LabelField(buildAsset.path, Utils.MonoStyle); // Add a tooltip with the full path
                EditorGUILayout.LabelField(buildAsset.size, Utils.MonoStyle, GUILayout.Width(80));
                EditorGUILayout.LabelField(buildAsset.percent, Utils.MonoStyle, GUILayout.Width(50));
                if (!isAssetPath)
                    GUI.color = Color.white;

                GUI.enabled = isSelectable;
                if (GUILayout.Button(">", GUILayout.Width(30)))
                {
                    Object obj = AssetDatabase.LoadAssetAtPath<Object>(buildAsset.path);
                    if (obj != null)
                    {
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }

        }
    }


    private BuildReport GetLastBuildReport()
    {
        var AppDataLocalPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        var editorLogPath = Path.Combine(AppDataLocalPath, "Unity", "Editor", "Editor.log");
        var editorLogCopyPath = Path.Combine(AppDataLocalPath, "Unity", "Editor", "Editor.SdkLogViewer.log");
        var BuildReport = new BuildReport();

        try
        {
            if (!File.Exists(editorLogPath))
            {
                Debug.LogWarning("Editor log not found at: " + editorLogPath);
                return BuildReport;
            }
            File.Copy(editorLogPath, editorLogCopyPath, true);

            string fileContent;
            using (StreamReader reader = new(editorLogCopyPath))
            {
                fileContent = reader.ReadToEnd();
            }

            // Find the start of the last build report.
            // format 1: Bundle Name: prefab-id-v1_avtr_827a679b-31db-4871-9215-5be579254d2b_8578641685.prefab.unity3d
            // format 2: scene-.*.vrcw
            var _avatarBuildReportIndex = Utils.FindLastIndexOf(fileContent, new Regex(@"avtr_[\w-]+\.prefab\.unity3d"));
            var _worldBuildReportIndex = Utils.FindLastIndexOf(fileContent, new Regex(@"scene-.+\.vrcw"));
            var buildReportIndex = Mathf.Max(_avatarBuildReportIndex, _worldBuildReportIndex);
            if (buildReportIndex == -1)
            {
                Debug.LogWarning("No build report found in the Editor log.");
                return BuildReport;
            }
            BuildReport.buildType = buildReportIndex == _avatarBuildReportIndex ? BuildType.Avatar : BuildType.World;

            // Find the start of the asset list after that report.
            // format: Used Assets and files from the Resources folder, sorted by uncompressed size:
            var assetListIndex = fileContent.IndexOf("Used Assets and files from the Resources folder", buildReportIndex, System.StringComparison.Ordinal);
            if (assetListIndex == -1)
            {
                Debug.LogWarning($"No asset list found in the build report {BuildReport.buildType} at index {buildReportIndex}.");
                return BuildReport;
            }

            string line;
            using (StringReader stringReader = new(fileContent[buildReportIndex..]))
            {
                // Read until "Compressed Size"
                while ((line = stringReader.ReadLine()) != null && !line.Contains("Compressed Size")) { }
                if (line != null) BuildReport.compressedSize = line.Split(':')[1].Trim();

                // Read breakdown
                // Textures               55.3 mb	 57.5%
                // Meshes                 38.0 mb	 39.5%
                // Animations             1.9 mb	 2.0%
                // ...
                // Complete build size    96.1 mb
                var categoryRegex = new Regex(@"(?<category>^[\w\s]+?)\s{2,}(?<size>\d+\.\d\s\w+)\t\s*(?<percent>\d+\.\d%)");
                while ((line = stringReader.ReadLine()) != null)
                {
                    if (line.StartsWith("Complete build size"))
                    {
                        BuildReport.uncompressedSize = line.Replace("Complete build size", "").Trim();
                        break;
                    }
                    var match = categoryRegex.Match(line);
                    if (match.Success)
                    {
                        var category = new UncompressedCategory
                        {
                            category = match.Groups["category"].Value.Trim(),
                            size = match.Groups["size"].Value.Trim(),
                            percent = match.Groups["percent"].Value.Trim()
                        };
                        BuildReport.UncompressedCategoryList.Add(category);
                    }
                }

                // Find detailed asset list
                //  37.9 mb	 39.5% Assets/Avatar/Shinano/FBX/Shinano.fbx
                //  21.3 mb	 22.2% Assets/Avatar/Shinano/Texture/Shinano_hair.png
                var assetListRegex = new Regex(@"^\s*(?<size>\d+\.\d+\s\w+)\t\s+(?<percent>\d+\.\d+%)\s+(?<path>.+)$");
                while ((line = stringReader.ReadLine()) != null)
                {
                    if (line.StartsWith("-----------------------------"))
                        break;
                    var assetMatch = assetListRegex.Match(line);
                    if (assetMatch.Success)
                    {
                        var buildAsset = new BuildAsset
                        {
                            size = assetMatch.Groups["size"].Value.Trim(),
                            percent = assetMatch.Groups["percent"].Value.Trim(),
                            path = assetMatch.Groups["path"].Value.Trim()
                        };
                        BuildReport.buildAssetList.Add(buildAsset);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to read or parse the build log: " + e.Message);
            return BuildReport;
        }
        finally
        {
            if (File.Exists(editorLogCopyPath)) File.Delete(editorLogCopyPath);
        }

        BuildReport.isReady = true;
        return BuildReport;
    }

    public class BuildReport
    {
        public bool isReady = false;
        public BuildType buildType;
        public string compressedSize;
        public string uncompressedSize;
        public List<UncompressedCategory> UncompressedCategoryList = new();
        public List<BuildAsset> buildAssetList = new();
    }

    public enum BuildType
    {
        Avatar,
        World
    }

    public class UncompressedCategory
    {
        public string category;
        public string size;
        public string percent;
    }

    public class BuildAsset
    {
        public string size;
        public string percent;
        public string path;
    }

}