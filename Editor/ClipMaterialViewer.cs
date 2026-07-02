using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Elypha.Common;

namespace Elypha.UnityMaterialHelper
{
    public class AnimationClipViewerWindow : EditorWindow
    {
        private Vector2 _scrollPosition;

        private AnimationClip _materialClip;
        private bool _showOnlyUniqueMaterials = false;
        private readonly List<AnimatedPath> _processedPaths = new();


        [MenuItem("Elypha/Animation Clip Material Viewer")]
        public static void ShowWindow()
        {
            GetWindow<AnimationClipViewerWindow>("Clip Material Viewer");
        }

        private void OnGUI()
        {
            Services.DrawTitle1("Settings");

            EditorGUILayout.LabelField("Animation Clip", EditorStyles.boldLabel);

            var newClip = (AnimationClip)EditorGUILayout.ObjectField(_materialClip, typeof(AnimationClip), false);
            if (newClip != _materialClip)
            {
                _materialClip = newClip;
                ProcessAnimationClip();
            }
            if (_materialClip == null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("Assign an Animation Clip to view its material data.", MessageType.Info);
                return;
            }
            if (_processedPaths.Count == 0)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("This Clip does not appear to animate any materials.", MessageType.Info);
                return;
            }
            EditorGUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            _showOnlyUniqueMaterials = GUILayout.Toggle(_showOnlyUniqueMaterials, "Show Only Unique Materials");
            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }

            Services.DrawTitle1("Material List");

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawMaterialList();
            EditorGUILayout.EndScrollView();
        }

        private void DrawMaterialList()
        {
            foreach (var animatedPath in _processedPaths)
            {
                EditorGUILayout.LabelField(animatedPath.Path, EditorStyles.boldLabel);

                EditorGUI.indentLevel++;
                var materialsToShow = _showOnlyUniqueMaterials ? animatedPath.UniqueMaterials : animatedPath.AllMaterials;
                foreach (var material in materialsToShow)
                {
                    EditorGUILayout.ObjectField(material, typeof(Material), false);
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(5);
            }
        }

        private void ProcessAnimationClip()
        {
            _processedPaths.Clear();

            if (_materialClip == null)
            {
                Repaint();
                return;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(_materialClip);

            foreach (var binding in bindings)
            {
                if (binding.propertyName.StartsWith("m_Materials"))
                {
                    ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(_materialClip, binding);

                    var materials = keyframes
                        .Select(k => k.value as Material)
                        .Where(m => m != null)
                        .ToList();

                    if (materials.Any())
                    {
                        _processedPaths.Add(new AnimatedPath
                        {
                            Path = binding.path,
                            AllMaterials = materials,
                            UniqueMaterials = materials.Distinct().ToList()
                        });
                    }
                }
            }

            Repaint();
        }

        private class AnimatedPath
        {
            public string Path;
            public List<Material> AllMaterials;
            public List<Material> UniqueMaterials;
        }
    }
}
