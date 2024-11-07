using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;

public class MaterialBulkSwapper : EditorWindow
{
    private GameObject avatarObject;
    private GameObject outfitObject;
    private AnimationClip animationClip;
    private int clipNextFrame;
    private int targetFrameNumber;
    private float targetFrameTime;
    private readonly List<Material> uniqueMaterials = new();
    private readonly Dictionary<Material, Material> materialSwapMap = new();
    private GUIStyle LabelStyleCentered => new(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };

    [MenuItem("Tools/Elypha/Material Bulk Swapper")]
    public static void ShowWindow()
    {
        GetWindow<MaterialBulkSwapper>("Material Bulk Swapper");
    }

    private void OnGUI()
    {
        avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar Object", avatarObject, typeof(GameObject), true);
        outfitObject = (GameObject)EditorGUILayout.ObjectField("Outfit Object", outfitObject, typeof(GameObject), true);
        animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false);

        if (GUILayout.Button("Load Data") && outfitObject != null && animationClip != null)
        {
            LoadData();
        }

        if (uniqueMaterials.Count > 0)
        {
            EditorGUILayout.LabelField($"Next frame on this clip: {clipNextFrame}");

            targetFrameNumber = EditorGUILayout.IntField("Target Frame", targetFrameNumber);
            targetFrameTime = targetFrameNumber / animationClip.frameRate;


            EditorGUILayout.LabelField("Unique Materials:");
            foreach (Material mat in uniqueMaterials)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(mat, typeof(Material), false);
                EditorGUILayout.LabelField("->", LabelStyleCentered, GUILayout.Width(25));
                materialSwapMap[mat] = (Material)EditorGUILayout.ObjectField(materialSwapMap.ContainsKey(mat) ? materialSwapMap[mat] : null, typeof(Material), false);
                EditorGUILayout.EndHorizontal();
            }
        }

        var seconds = Math.Floor(targetFrameNumber / animationClip.frameRate);
        var frames = targetFrameNumber % animationClip.frameRate;
        if (GUILayout.Button($"Add to Frame #{targetFrameNumber} [{seconds}:{frames:00}] (at {targetFrameTime:0.000}s)") && animationClip != null)
        {
            AddMaterialSwapKeyframes();
        }
    }

    private void LoadData()
    {
        uniqueMaterials.Clear();
        materialSwapMap.Clear();
        var renderers = outfitObject.GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && !uniqueMaterials.Contains(mat))
                {
                    uniqueMaterials.Add(mat);
                }
            }
        }

        foreach (Material mat in uniqueMaterials)
        {
            materialSwapMap[mat] = mat;
        }
        uniqueMaterials.Sort((a, b) => a.name.CompareTo(b.name));

        clipNextFrame = animationClip.empty ? 0 : (int)(animationClip.length * animationClip.frameRate);
    }

    private void AddMaterialSwapKeyframes()
    {
        var renderers = outfitObject.GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            var path = GetRelativePathInHierarchy(avatarObject.transform, renderer.transform);

            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                var binding = EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), $"m_Materials.Array.data[{i}]");
                var keyframes = AnimationUtility.GetObjectReferenceCurve(animationClip, binding);

                var originalMaterial = renderer.sharedMaterials[i];
                var targetMaterial = materialSwapMap[originalMaterial];
                var newKeyframe = new ObjectReferenceKeyframe
                {
                    time = targetFrameTime,
                    value = targetMaterial
                };

                if (keyframes != null && keyframes.Length > 0)
                {
                    bool isUpdated = false;
                    for (int j = 0; j < keyframes.Length; j++)
                    {
                        if (Mathf.Approximately(keyframes[j].time, newKeyframe.time))
                        {
                            keyframes[j].value = targetMaterial;
                            isUpdated = true;
                            break;
                        }
                    }
                    if (!isUpdated)
                    {
                        keyframes = keyframes.Append(newKeyframe).ToArray();
                    }
                }
                else
                {
                    keyframes = new ObjectReferenceKeyframe[] { newKeyframe };
                }

                AnimationUtility.SetObjectReferenceCurve(animationClip, binding, keyframes);
            }
        }

        AssetDatabase.SaveAssets();
        LoadData();
    }

    private string GetRelativePathInHierarchy(Transform root, Transform target)
    {
        if (root == target)
        {
            return "";
        }

        var pathStack = new Stack<string>();
        var current = target;

        while (current != null && current != root)
        {
            pathStack.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", pathStack);
    }
}
