using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elypha.Common;
using UnityEditor;
using UnityEngine;

public class TextureManager : EditorWindow
{
    private Vector2 scrollPosition;

    private DefaultAsset searchFolder;
    private Dictionary<string, List<TextureInfo>> texturesByFolder = new();
    private readonly HashSet<Texture> referencedTextures = new();
    private static readonly string[] validOverridePlatforms = { "Standalone", "Android", "iPhone" };
    private static readonly string defaultOverridePlatform = "Standalone";

    private readonly GUILayoutOption[] textureLabelOptions = new GUILayoutOption[]
    {
        GUILayout.Width(200),
        GUILayout.ExpandWidth(true),
    };

    private class TextureInfo
    {
        public Texture Texture;
        public int NativeWidth;
        public int ConfiguredWidth;
        public string CompressionFormat;
        public string CompressionQuality;
        public bool IsCrunched;
        public bool HasPlatformOverrides;
    }

    [MenuItem("Elypha/Texture Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<TextureManager>("Texture Manager");
        window.minSize = new Vector2(400, 400);
    }

    private void OnGUI()
    {
        searchFolder = (DefaultAsset)EditorGUILayout.ObjectField("Search Folder", searchFolder, typeof(DefaultAsset), false);
        Services.Separator();


        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (texturesByFolder.Count > 0)
        {
            foreach (var folderEntry in texturesByFolder)
            {
                EditorGUILayout.LabelField(
                    Path.GetRelativePath(AssetDatabase.GetAssetPath(searchFolder), folderEntry.Key),
                    new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Services.ColourTitle1 } }
                );
                EditorGUILayout.Space(2);

                foreach (var texInfo in folderEntry.Value)
                {
                    bool isReferenced = referencedTextures.Contains(texInfo.Texture);
                    Color originalBgColor = GUI.backgroundColor;
                    Color originalContentColor = GUI.contentColor;

                    if (!isReferenced)
                        GUI.backgroundColor = Color.yellow;

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                    if (texInfo.HasPlatformOverrides)
                        GUI.contentColor = new Color(0.6f, 0.8f, 1f);

                    string infoLabel = $"{texInfo.NativeWidth,4}  {texInfo.ConfiguredWidth,4}  {texInfo.CompressionQuality,3}_{texInfo.CompressionFormat} {(texInfo.IsCrunched ? "Crunched" : "")}";
                    EditorGUILayout.LabelField(infoLabel, Utils.MonoStyle, textureLabelOptions);

                    GUI.contentColor = originalContentColor;

                    EditorGUILayout.ObjectField(texInfo.Texture, typeof(Texture), false);

                    EditorGUILayout.EndHorizontal();
                    GUI.backgroundColor = originalBgColor;
                }

                EditorGUILayout.Space();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Click the button to start.");
        }

        EditorGUILayout.EndScrollView();

        Services.Separator();

        GUI.enabled = IsAllInputsValid();
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Analyse Textures", GUILayout.Height(40)))
        {
            FindAssetsAndTheirDetails();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    private bool IsAllInputsValid() => searchFolder != null;


    private void FindAssetsAndTheirDetails()
    {
        texturesByFolder.Clear();
        referencedTextures.Clear();

        string folderPath = AssetDatabase.GetAssetPath(searchFolder);
        if (string.IsNullOrEmpty(folderPath)) return;

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture", new[] { folderPath });
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });
        var allFoundTextures = new HashSet<Texture>();

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;

            foreach (string propName in material.GetTexturePropertyNames())
            {
                Texture texture = material.GetTexture(propName);
                if (texture == null) continue;

                referencedTextures.Add(texture);
            }
        }

        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (texture == null) continue;

            allFoundTextures.Add(texture);

            var texInfo = GetTextureDetails(texture);

            string directory = Path.GetDirectoryName(path).Replace("\\", "/");
            if (!texturesByFolder.ContainsKey(directory))
                texturesByFolder[directory] = new List<TextureInfo>();

            texturesByFolder[directory].Add(texInfo);
        }

        referencedTextures.IntersectWith(allFoundTextures);

        texturesByFolder = texturesByFolder.OrderBy(kvp => kvp.Key)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private TextureInfo GetTextureDetails(Texture texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        var info = new TextureInfo
        {
            Texture = texture,
            ConfiguredWidth = texture.width, // the current in-editor width
        };

        if (importer != null)
        {
            // get native (original file) width using reflection
            object[] args = new object[2] { 0, 0 };
            var getSourceDimensions = typeof(TextureImporter).GetMethod("GetWidthAndHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            getSourceDimensions.Invoke(importer, args);
            info.NativeWidth = (int)args[0];

            info.HasPlatformOverrides = validOverridePlatforms.Any(platform => importer.GetPlatformTextureSettings(platform).overridden);

            var platformSettings = importer.GetPlatformTextureSettings(defaultOverridePlatform);
            info.CompressionFormat = platformSettings.overridden ? platformSettings.format.ToString() : importer.textureCompression.ToString();
            info.CompressionQuality = platformSettings.overridden ? platformSettings.compressionQuality.ToString() : importer.compressionQuality.ToString();
            info.IsCrunched = platformSettings.crunchedCompression;
        }
        else
        {
            info.NativeWidth = texture.width; // fallback for non-importer textures
            info.CompressionFormat = "NA";
        }

        return info;
    }
}