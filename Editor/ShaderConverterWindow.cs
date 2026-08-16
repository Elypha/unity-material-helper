using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Elypha.Common;

namespace Elypha.UnityMaterialHelper
{
    public class ShaderConverterWindow : EditorWindow
    {
        public enum TargetShaderType
        {
            Silent_Filamented,
        }

        private struct MaterialResult
        {
            public Material MaterialObject;
            public int UsageCount;
            public string CurrentShaderName;
        }

        [MenuItem("Elypha/Shader Converter", false, 209)]
        public static void ShowWindow()
        {
            var window = GetWindow<ShaderConverterWindow>("Shader Converter");
            window.minSize = new Vector2(500, 400);
        }

        private Vector2 _scrollPosition;
        private bool _isResizing = false;

        private float _materialColumnWidth = 250f;
        private const float CountColumnWidth = 60f;

        private GameObject _targetObject;
        private TargetShaderType _targetType = TargetShaderType.Silent_Filamented;

        private List<MaterialResult> _reportData = new List<MaterialResult>();

        private void OnGUI()
        {
            // Input Section
            // --------------------------------
            var originalLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 120f;

            EditorGUI.BeginChangeCheck();

            _targetObject = (GameObject)EditorGUILayout.ObjectField("Target GameObject", _targetObject, typeof(GameObject), true);
            _targetType = (TargetShaderType)EditorGUILayout.EnumPopup("Target Shader", _targetType);

            if (EditorGUI.EndChangeCheck())
            {
                AnalyzeMaterials();
            }

            EditorGUIUtility.labelWidth = originalLabelWidth;

            // Buttons Section
            // --------------------------------
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // 1. Refresh Button
            if (GUILayout.Button("Refresh", GUILayout.Height(24)))
            {
                AnalyzeMaterials();
            }

            EditorGUI.BeginDisabledGroup(_reportData == null || _reportData.Count == 0);

            // 2. Replace Button
            if (GUILayout.Button("Replace All", GUILayout.Height(24)))
            {
                ReplaceShaders();
            }

            // 3. VRCLV Button
            if (GUILayout.Button("Enable VRCLV", GUILayout.Height(24)))
            {
                EnableVRCLV();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            Services.Separator();

            // Report Header
            // --------------------------------
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 1. Material Asset Header (Resizable)
            GUILayout.Label("Material Asset", GUILayout.Width(_materialColumnWidth));

            // Resize Handle logic
            Rect resizeRect = GUILayoutUtility.GetLastRect();
            resizeRect.x += resizeRect.width;
            resizeRect.width = 5f;

            // 2. Usage Count Header
            GUILayout.Label("Count", GUILayout.Width(CountColumnWidth));

            // 3. Current Shader Header
            GUILayout.Label("Current Shader");

            EditorGUILayout.EndHorizontal();

            HandleResize(resizeRect);

            // Report Content
            // --------------------------------
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_reportData != null && _reportData.Count > 0)
            {
                foreach (var item in _reportData)
                {
                    EditorGUILayout.BeginHorizontal("box");

                    // Col 1: Clickable Object
                    // -----------------------------
                    EditorGUILayout.ObjectField(item.MaterialObject, typeof(Material), false, GUILayout.Width(_materialColumnWidth));

                    // Col 2: Usage Count
                    // -----------------------------
                    GUILayout.Label(item.UsageCount.ToString(), GUILayout.Width(CountColumnWidth));

                    // Col 3: Shader Name
                    // -----------------------------
                    // If Standard, show yellow warning; if target shader (targetType), show green; else default
                    GUIStyle labelStyle = EditorStyles.label;
                    if (item.CurrentShaderName == "Standard")
                        GUI.color = Color.yellow;
                    else if (item.CurrentShaderName == GetShaderName(_targetType))
                        GUI.color = Color.green;

                    GUILayout.Label(item.CurrentShaderName, labelStyle);
                    GUI.color = Color.white; // Reset color

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                if (_targetObject == null)
                {
                    EditorGUILayout.LabelField("Please drag a GameObject into the field above.");
                }
                else
                {
                    EditorGUILayout.LabelField("No materials found in the hierarchy.");
                }
            }

            EditorGUILayout.EndScrollView();
        }


        // Logic Functions
        // --------------------------------
        private void AnalyzeMaterials()
        {
            _reportData.Clear();
            if (_targetObject == null) return;

            Renderer[] renderers = _targetObject.GetComponentsInChildren<Renderer>(true);
            Dictionary<Material, int> usageCountMap = new Dictionary<Material, int>();

            int total = renderers.Length;
            for (int i = 0; i < total; i++)
            {
                if (i % 50 == 0) EditorUtility.DisplayProgressBar("Scanning Materials", "Analyzing Renderers...", (float)i / total);

                Renderer r = renderers[i];
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;

                    if (usageCountMap.ContainsKey(mat))
                    {
                        usageCountMap[mat]++;
                    }
                    else
                    {
                        usageCountMap[mat] = 1;
                    }
                }
            }
            EditorUtility.ClearProgressBar();

            // Convert Dictionary to List structure
            foreach (var kvp in usageCountMap)
            {
                _reportData.Add(new MaterialResult
                {
                    MaterialObject = kvp.Key,
                    UsageCount = kvp.Value,
                    CurrentShaderName = kvp.Key.shader ? kvp.Key.shader.name : "Missing Shader"
                });
            }

            // Sort by: Is Standard Shader (desc), Material Name (asc)
            _reportData = _reportData.OrderByDescending(x => x.CurrentShaderName == "Standard").ThenBy(x => x.MaterialObject.name).ToList();
        }

        private void ReplaceShaders()
        {
            if (_targetObject == null || _reportData.Count == 0) return;

            string shaderName = GetShaderName(_targetType);
            Shader newShader = Shader.Find(shaderName);

            if (newShader == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find shader: {shaderName}", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroupIndex = Undo.GetCurrentGroup();
            int changedCount = 0;

            foreach (var item in _reportData)
            {
                Material mat = item.MaterialObject;
                // Replace only if the shader name is different (to avoid redundant operations)
                if (mat != null && mat.shader.name != shaderName && mat.shader.name == "Standard")
                {
                    Undo.RecordObject(mat, "Replace Shader");
                    mat.shader = newShader;
                    changedCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroupIndex);
            Debug.Log($"Replaced shader on {changedCount} materials.");

            AnalyzeMaterials();
        }

        private void EnableVRCLV()
        {
            if (_targetObject == null || _reportData.Count == 0) return;

            string targetShader = GetShaderName(_targetType);

            Undo.IncrementCurrentGroup();
            int undoGroupIndex = Undo.GetCurrentGroup();
            int changedCount = 0;

            foreach (var item in _reportData)
            {
                Material mat = item.MaterialObject;
                // Ensure only materials with the target shader are modified
                if (mat != null && mat.shader.name == targetShader)
                {
                    if (mat.HasProperty("_VRCLV"))
                    {
                        // Check current value, if already 1, do not record Undo to avoid pollution
                        if (mat.GetFloat("_VRCLV") != 1.0f)
                        {
                            Undo.RecordObject(mat, "Enable VRCLV");
                            mat.SetFloat("_VRCLV", 1.0f);
                            changedCount++;
                        }
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroupIndex);
            Debug.Log($"Enabled VRCLV on {changedCount} materials.");
        }

        private string GetShaderName(TargetShaderType type)
        {
            switch (type)
            {
                case TargetShaderType.Silent_Filamented:
                    return "Silent/Filamented";
                default:
                    return "Standard";
            }
        }

        private void HandleResize(Rect resizeRect)
        {
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
            {
                _isResizing = true;
            }

            if (_isResizing)
            {
                _materialColumnWidth = Event.current.mousePosition.x;
                _materialColumnWidth = Mathf.Clamp(_materialColumnWidth, 100f, position.width - 150f);
                Repaint();
            }

            if (Event.current.type == EventType.MouseUp)
            {
                _isResizing = false;
            }
        }
    }
}
