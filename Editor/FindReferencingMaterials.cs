using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Elypha.Helper;
using Elypha.I18N;


public class FindReferencingMaterials : EditorWindow
{
    private Vector2 _scrollPosition;
    private Vector2 _textureListScrollPosition;

    private List<Texture> _inputTextures = new();
    private readonly Dictionary<string, List<Material>> _groupedResults = new();

    private const float TextureAreaHeight = 125f;

    private bool showAdvancedSettings = false;
    private static PluginLanguage language = PluginLanguage.English;
    private readonly TemplateI18N i18n = new(language);
    private readonly GuiMessage guiMessage = new();

    [MenuItem("Tools/Elypha Toolkit/Find Referencing Materials")]
    public static void ShowWindow()
    {
        var window = GetWindow<FindReferencingMaterials>("Find Referencing Materials");
        window.minSize = new Vector2(400, 300);
    }



    void OnGUI()
    {
        UnityHelper.DrawAdvancedSettings(ref showAdvancedSettings, ref language, i18n);

        UnityHelper.DrawTitle1(i18n.Localise("Settings"));

        DrawAddTexture();

        GUI.enabled = IsAllInputsValid();
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Find Unique Referencing Materials", GUILayout.Height(40)))
        {
            FindMaterials();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        DrawResults();
    }

    private void DrawAddTexture()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField("Add Textures", EditorStyles.boldLabel);

        string textureCountLabel = $"{_inputTextures.Count} texture(s) loaded.";
        var labelRect = GUILayoutUtility.GetRect(new GUIContent(textureCountLabel), EditorStyles.label);
        labelRect.x = TextureAreaHeight + 4;
        labelRect.y += 2;
        EditorGUI.LabelField(labelRect, textureCountLabel, EditorStyles.label);
        EditorGUILayout.EndHorizontal();


        EditorGUILayout.BeginHorizontal(GUILayout.Height(TextureAreaHeight));

        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 0.0f, GUILayout.Width(TextureAreaHeight), GUILayout.Height(TextureAreaHeight));
        GUIStyle dropAreaStyle = new(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            normal = { textColor = UnityHelper.ColourTitle1 * 0.95f },
        };
        GUI.Box(dropArea, "Drag Here", dropAreaStyle);
        HandleDragAndDrop(dropArea);


        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        _textureListScrollPosition = EditorGUILayout.BeginScrollView(_textureListScrollPosition, GUILayout.ExpandWidth(true), GUILayout.Height(TextureAreaHeight));
        if (_inputTextures.Count > 0)
        {
            foreach (var tex in _inputTextures)
            {
                EditorGUILayout.ObjectField(tex, typeof(Texture), false);
            }
        }
        else
        {
            EditorGUILayout.LabelField("Drop textures on the left.", EditorStyles.centeredGreyMiniLabel);
        }
        EditorGUILayout.EndScrollView();


        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event currentEvent = Event.current;
        if (!dropArea.Contains(currentEvent.mousePosition)) return;

        if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                var draggedTextures = DragAndDrop.objectReferences.OfType<Texture>();

                // clear existing textures if not holding Shift
                if (!currentEvent.shift)
                {
                    _inputTextures.Clear();
                }

                // Add new textures without creating duplicates
                var currentTextureSet = new HashSet<Texture>(_inputTextures);
                currentTextureSet.UnionWith(draggedTextures);
                _inputTextures = currentTextureSet.ToList();

                Repaint();
            }
            currentEvent.Use();
        }
    }

    private void DrawResults()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
        if (_groupedResults.Count == 0)
        {
            EditorGUILayout.HelpBox("No materials found referencing the specified textures.", MessageType.Info);
        }
        else
        {
            foreach (var group in _groupedResults)
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (Material mat in group.Value)
                {
                    if (mat != null)
                    {
                        EditorGUILayout.ObjectField(mat, typeof(Material), false);
                    }
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Separator();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void FindMaterials()
    {
        _groupedResults.Clear();
        var searchTextures = new HashSet<Texture>(_inputTextures.Where(t => t != null));
        if (searchTextures.Count == 0) return;

        string[] allMaterialGUIDs = AssetDatabase.FindAssets("t:Material");
        var foundMaterials = new List<Material>();

        foreach (string guid in allMaterialGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null) continue;

            int propertyCount = ShaderUtil.GetPropertyCount(material.shader);
            for (int i = 0; i < propertyCount; i++)
            {
                if (ShaderUtil.GetPropertyType(material.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    string propertyName = ShaderUtil.GetPropertyName(material.shader, i);
                    if (material.GetTexture(propertyName) is Texture textureInMaterial && searchTextures.Contains(textureInMaterial))
                    {
                        foundMaterials.Add(material);
                        break;
                    }
                }
            }
        }

        foreach (Material mat in foundMaterials.Distinct())
        {
            string path = AssetDatabase.GetAssetPath(mat);
            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (directory == null) continue;

            if (!_groupedResults.ContainsKey(directory))
            {
                _groupedResults[directory] = new List<Material>();
            }
            _groupedResults[directory].Add(mat);
        }
        Repaint();
    }

    private bool IsAllInputsValid()
    {
        return _inputTextures.Count > 0 && _inputTextures.All(t => t != null);
    }
}