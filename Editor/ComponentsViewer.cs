using System.Collections.Generic;
using System.Linq;
using Elypha.Common;
using UnityEditor;
using UnityEngine;

public class ComponentsViewer : EditorWindow
{
    [MenuItem("Elypha/Components Viewer")]
    public static void ShowWindow()
    {
        var window = GetWindow<ComponentsViewer>("Components Viewer");
        window.minSize = new Vector2(400, 400);
    }
    private Vector2 scrollPosition;
    private bool isResizing = false;
    private float nameColumnWidth = 200f;  // Initial width of the name column
    private GUIStyle componentTextStyle;


    private GameObject targetObject;
    // last object
    private readonly Dictionary<GameObject, List<string>> reportData = new();
    private bool ignoreSkinnedMeshRenderers = false;


    private void OnEnable()
    {
        componentTextStyle = new(EditorStyles.label)
        {
            wordWrap = true,
            richText = true
        };
    }

    private void OnGUI()
    {
        // Input
        // --------------------------------
        var originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 200f;

        EditorGUI.BeginChangeCheck();
        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);
        ignoreSkinnedMeshRenderers = EditorGUILayout.Toggle("Ignore Skinned Mesh Renderers", ignoreSkinnedMeshRenderers);
        if (EditorGUI.EndChangeCheck())
        {
            reportData.Clear();

            if (targetObject != null)
            {
                AnalyseExistingComponents(targetObject);
            }
        }

        EditorGUIUtility.labelWidth = originalLabelWidth;

        Services.Separator();


        // Report
        // --------------------------------
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Object", GUILayout.Width(nameColumnWidth));
        // Draw resize handle area
        Rect resizeRect = GUILayoutUtility.GetLastRect();
        resizeRect.x += resizeRect.width;
        resizeRect.width = 5f;

        GUILayout.Label("Components");
        EditorGUILayout.EndHorizontal();

        // handle column resizing
        HandleResize(resizeRect);

        // Report Content
        // --------------------------------
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (reportData != null && reportData.Count > 0)
        {
            foreach (var kvp in reportData)
            {
                EditorGUILayout.BeginHorizontal("box"); // use box style for better separation

                // GUILayout.Width(nameColumnWidth) enables width control
                EditorGUILayout.ObjectField(kvp.Key, typeof(GameObject), true, GUILayout.Width(nameColumnWidth));

                GUILayout.Space(10);

                string componentsString = string.Join(", ", kvp.Value);

                // replace 'ModularAvatar' with 'MA_' for brevity
                componentsString = componentsString.Replace("ModularAvatar", "MA_");

                EditorGUILayout.LabelField(componentsString, componentTextStyle);

                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("Please select a Target Object to analyze.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("No non-transform components found.");
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
            nameColumnWidth = Mathf.Clamp(nameColumnWidth, 100f, position.width - 100f); // set min and max widths
            Repaint(); // refresh the window to show changes
        }

        if (Event.current.type == EventType.MouseUp)
        {
            isResizing = false;
        }
    }


    private void AnalyseExistingComponents(GameObject rootObject)
    {
        foreach (var t in rootObject.GetComponentsInChildren<Transform>(true))
        {
            var components = t.GetComponents<Component>();
            var nonTransformComponents = components
                .Where(c => c != null && c.GetType() != typeof(Transform))
                .Where(c => !(ignoreSkinnedMeshRenderers && c is SkinnedMeshRenderer))
                .ToList();

            if (nonTransformComponents.Count > 0)
            {
                reportData[t.gameObject] = nonTransformComponents.Select(c => c.GetType().Name).ToList();
            }
        }
    }

}
