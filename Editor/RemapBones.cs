using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using UnityEditor;
using UnityEngine;


public class RemapBones : EditorWindow
{
    [MenuItem("Tools/Elypha Toolkit/Remap Bones")]
    public static void ShowWindow()
    {
        var window = GetWindow<RemapBones>("Remap Bones");
        window.minSize = new Vector2(500, 500);
    }

    private Vector2 scrollPosition;

    private SkinnedMeshRenderer renderer;
    private Transform newRootBone;
    private SkinnedMeshRenderer referenceRenderer;
    private readonly Dictionary<string, Transform> boneDictionary = new();
    private string result;
    private GUIStyle monoStyle;

    private void OnEnable()
    {
        monoStyle = new GUIStyle()
        {
            font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font,
            fontSize = 12,
            normal = { textColor = Color.white },
            wordWrap = true,
            padding = new RectOffset(8, 8, 0, 0)
        };
    }


    private void OnGUI()
    {
        renderer = EditorGUILayout.ObjectField("Renderer", renderer, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        newRootBone = EditorGUILayout.ObjectField("New RootBone", newRootBone, typeof(Transform), true) as Transform;
        referenceRenderer = EditorGUILayout.ObjectField("Reference Renderer", referenceRenderer, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;

        if (GUILayout.Button("Analyse Renderer"))
        {
            result = AnalyseSkinnedMeshRenderer();
        }
        if (GUILayout.Button("Update Bones from Reference"))
        {
            result = UpdateSkinnedMeshRenderer();
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        try
        {
            GUILayout.Label(result, monoStyle);
        }
        catch (Exception e)
        {
            GUILayout.Label(e.Message);
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private string AnalyseSkinnedMeshRenderer()
    {
        var sb = new StringBuilder();

        // check bone matches
        // --------------------------------
        var countOk = 0;
        var countMismatch = 0;
        var countMissing = 0;
        var largerLength = Math.Max(renderer.bones.Length, referenceRenderer.bones.Length);
        for (int i = 0; i < largerLength; i++)
        {
            sb.Append($"{i,4}  ");
            var bone = TryGetElement(renderer.bones, i);
            var boneName = bone ? bone.name : "null";
            sb.Append($"{boneName,24}  ");
            var refBone = TryGetElement(referenceRenderer.bones, i);
            var refBoneName = refBone ? refBone.name : "null";
            sb.Append($"{refBoneName,24}  ");

            if (bone && refBone)
            {
                if (bone.name == refBone.name)
                {
                    sb.Append("OK");
                    countOk++;
                }
                else
                {
                    sb.Append("ERROR");
                    countMismatch++;
                }
            }
            else
            {
                sb.Append("-");
                countMissing++;
            }
            sb.Append("\n");
        }

        if (renderer.bones.Length != referenceRenderer.bones.Length)
        {
            sb.AppendLine($"ERROR: The number of bones in the renderer ({renderer.bones.Length}) does not match the reference renderer ({referenceRenderer.bones.Length}).");
            return sb.ToString();
        }

        sb.AppendLine($"\nSummary:");
        sb.AppendLine($"Total    : {renderer.bones.Length}");
        sb.AppendLine($"OK       : {countOk}");
        sb.AppendLine($"Mismatch : {countMismatch}");
        sb.AppendLine($"Null     : {countMissing}\n");


        // check new root bone
        // --------------------------------
        var bonePathMap = referenceRenderer.bones
            .Where(b => b != null)
            .ToDictionary(b => b.name, b => b.gameObject.transform.GetRelativePath(referenceRenderer.rootBone.parent));
        var newRootBoneMissingCount = 0;
        foreach (var bone in bonePathMap)
        {
            var targetInNewRootBone = newRootBone.parent.Find(bone.Value);
            if (targetInNewRootBone == null)
            {
                sb.AppendLine($"{bone.Key} at '{bone.Value}' not found in the new Root Bone.");
                newRootBoneMissingCount++;
            }
            boneDictionary[bone.Key] = targetInNewRootBone;
        }
        sb.AppendLine($"Total missing bones in new Root Bone: {newRootBoneMissingCount}\n");

        return sb.ToString();
    }

    private string UpdateSkinnedMeshRenderer()
    {
        var sb = new StringBuilder();
        Transform[] newBones = boneDictionary.Values.ToArray();

        renderer.bones = newBones;
        renderer.rootBone = newRootBone;
        sb.AppendLine($"Updated Bones in {renderer.name}:");
        sb.AppendLine($"  - rootBone set to: {renderer.rootBone.transform.GetPath()}");
        sb.AppendLine($"  - bones total: {renderer.bones.Length}");
        sb.AppendLine($"  - bones missing: {renderer.bones.Count(b => b == null)}");

        return sb.ToString();
    }

    public static T? TryGetElement<T>(T[] array, int index) where T : class
    {
        if (index >= 0 && index < array.Length)
            return array[index];
        return null;
    }
}
