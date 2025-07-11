using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;
using Elypha.Helper;
using Elypha.I18N;

public class AnimationClipViewerWindow : EditorWindow
{
    private Vector2 scrollPosition;

    private AnimationClip materialClip;
    private bool showOnlyUniqueMaterials = false;
    private readonly List<AnimatedPath> processedPaths = new();


    [MenuItem("Tools/Elypha Toolkit/Animation Clip Material Viewer")]
    public static void ShowWindow()
    {
        GetWindow<AnimationClipViewerWindow>("Clip Material Viewer");
    }

    private void OnGUI()
    {
        UnityHelper.DrawTitle1("Settings");

        EditorGUILayout.LabelField("Animation Clip", EditorStyles.boldLabel);

        var newClip = (AnimationClip)EditorGUILayout.ObjectField(materialClip, typeof(AnimationClip), false);
        if (newClip != materialClip)
        {
            materialClip = newClip;
            ProcessAnimationClip();
        }
        if (materialClip == null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Assign an Animation Clip to view its material data.", MessageType.Info);
            return;
        }
        if (processedPaths.Count == 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("This Clip does not appear to animate any materials.", MessageType.Info);
            return;
        }
        EditorGUILayout.Space(5);
        EditorGUI.BeginChangeCheck();
        showOnlyUniqueMaterials = GUILayout.Toggle(showOnlyUniqueMaterials, "Show Only Unique Materials");
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }

        UnityHelper.DrawTitle1("Material List");

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawMaterialList();
        EditorGUILayout.EndScrollView();
    }

    private void DrawMaterialList()
    {
        foreach (var animatedPath in processedPaths)
        {
            EditorGUILayout.LabelField(animatedPath.Path, EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            var materialsToShow = showOnlyUniqueMaterials ? animatedPath.UniqueMaterials : animatedPath.AllMaterials;
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
        processedPaths.Clear();

        if (materialClip == null)
        {
            Repaint();
            return;
        }

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(materialClip);

        foreach (var binding in bindings)
        {
            if (binding.propertyName.StartsWith("m_Materials"))
            {
                ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(materialClip, binding);

                var materials = keyframes
                    .Select(k => k.value as Material)
                    .Where(m => m != null)
                    .ToList();

                if (materials.Any())
                {
                    processedPaths.Add(new AnimatedPath
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