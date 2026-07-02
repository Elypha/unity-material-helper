using System.Collections.Generic;
using System.Linq;
using Elypha.Common;
using UnityEditor;
using UnityEngine;

namespace Elypha.UnityMaterialHelper
{
    public class MaterialMatcher : EditorWindow
    {
        [MenuItem("Elypha/Material Matcher")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialMatcher>("Material Matcher");
            window.minSize = new Vector2(400, 400);
        }

        private GameObject _referenceObject;
        private GameObject _targetObject;

        private readonly List<string> _referenceUnusedReport = new();
        private readonly List<string> _targetUnsetReport = new();
        private int _matchCount = 0;

        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            _referenceObject = (GameObject)EditorGUILayout.ObjectField("Reference Object", _referenceObject, typeof(GameObject), true);
            _targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", _targetObject, typeof(GameObject), true);

            EditorGUILayout.Space();
            GUI.enabled = _referenceObject && _targetObject;
            if (GUILayout.Button("Apply Materials"))
            {
                ApplyMaterials();
            }
            GUI.enabled = true;

            Services.Separator();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_matchCount > 0 || _referenceUnusedReport.Any() || _targetUnsetReport.Any())
            {
                EditorGUILayout.HelpBox($"Matched: {_matchCount}, Reference Unused: {_referenceUnusedReport.Count}, Target Unset: {_targetUnsetReport.Count}", MessageType.Info);
            }

            EditorGUILayout.LabelField("Reference Unused:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (_referenceUnusedReport.Any())
            {
                foreach (var line in _referenceUnusedReport)
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
            if (_targetUnsetReport.Any())
            {
                foreach (var line in _targetUnsetReport)
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
            _referenceUnusedReport.Clear();
            _targetUnsetReport.Clear();
            _matchCount = 0;

            Undo.SetCurrentGroupName("Match Materials");
            int group = Undo.GetCurrentGroup();

            var refRenderers = _referenceObject.GetComponentsInChildren<Renderer>(true);
            var refDataMap = new Dictionary<string, Material[]>();

            foreach (var r in refRenderers)
            {
                if (IsValidRenderer(r))
                {
                    string path = r.transform.GetRelativePath(_referenceObject.transform);
                    refDataMap[path] = r.sharedMaterials;
                }
            }

            var targetRenderers = _targetObject.GetComponentsInChildren<Renderer>(true);
            var targetPathsProcessed = new HashSet<string>();

            foreach (var tRenderer in targetRenderers)
            {
                if (!IsValidRenderer(tRenderer)) continue;

                string path = tRenderer.transform.GetRelativePath(_targetObject.transform);
                targetPathsProcessed.Add(path);

                if (refDataMap.TryGetValue(path, out Material[] mats))
                {
                    Undo.RecordObject(tRenderer, "Apply Material Match");
                    tRenderer.sharedMaterials = mats;
                    _matchCount++;
                }
                else
                {
                    _targetUnsetReport.Add(path);
                }
            }

            foreach (var kvp in refDataMap)
            {
                if (!targetPathsProcessed.Contains(kvp.Key))
                {
                    _referenceUnusedReport.Add(kvp.Key);
                }
            }

            Undo.CollapseUndoOperations(group);

            string logMsg = $"<b>[Material Matcher]</b> Completed.\nMatched: {_matchCount}\nRef Unused: {_referenceUnusedReport.Count}\nTarget Unset: {_targetUnsetReport.Count}";
            Debug.Log(logMsg);

            if (_referenceUnusedReport.Count > 0)
            {
                Debug.LogWarning("[Ref Unused Paths]:\n" + string.Join("\n", _referenceUnusedReport));
            }
            if (_targetUnsetReport.Count > 0)
            {
                Debug.LogWarning("[Target Unset Paths]:\n" + string.Join("\n", _targetUnsetReport));
            }
        }

        private bool IsValidRenderer(Renderer r)
        {
            return r is MeshRenderer || r is SkinnedMeshRenderer;
        }
    }
}
