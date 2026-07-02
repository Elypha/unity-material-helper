using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using UnityEditor;
using UnityEngine;
using Elypha.Common;

namespace Elypha.UnityMaterialHelper
{
    public class RemapBones : EditorWindow
    {
        [MenuItem("Elypha/Remap Bones")]
        public static void ShowWindow()
        {
            var window = GetWindow<RemapBones>("Remap Bones");
            window.minSize = new Vector2(500, 500);
        }

        private Vector2 _scrollPosition;

        private SkinnedMeshRenderer _renderer;
        private Transform _newRootBone;
        private SkinnedMeshRenderer _referenceRenderer;
        private readonly Dictionary<string, Transform> _boneDictionary = new();
        private string _result;
        private GUIStyle _monoStyle;

        private void OnEnable()
        {
            _monoStyle = new GUIStyle()
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
            _renderer =
                EditorGUILayout.ObjectField("Renderer", _renderer, typeof(SkinnedMeshRenderer), true) as
                    SkinnedMeshRenderer;
            _newRootBone =
                EditorGUILayout.ObjectField("New RootBone", _newRootBone, typeof(Transform), true) as Transform;
            _referenceRenderer =
                EditorGUILayout.ObjectField("Reference Renderer", _referenceRenderer, typeof(SkinnedMeshRenderer), true)
                    as SkinnedMeshRenderer;

            if (GUILayout.Button("Analyse Renderer"))
            {
                _result = AnalyseSkinnedMeshRenderer();
            }

            if (GUILayout.Button("Update Bones from Reference"))
            {
                _result = UpdateSkinnedMeshRenderer();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            try
            {
                GUILayout.Label(_result, _monoStyle);
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
            var largerLength = Math.Max(_renderer.bones.Length, _referenceRenderer.bones.Length);
            for (int i = 0; i < largerLength; i++)
            {
                sb.Append($"{i,4}  ");
                TryGetElement(_renderer.bones, i, out var bone);
                var boneName = bone ? bone.name : "null";

                sb.Append($"{boneName,24}  ");
                TryGetElement(_referenceRenderer.bones, i, out var refBone);
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

            if (_renderer.bones.Length != _referenceRenderer.bones.Length)
            {
                sb.AppendLine(
                    $"ERROR: The number of bones in the renderer ({_renderer.bones.Length}) does not match the reference renderer ({_referenceRenderer.bones.Length}).");
                return sb.ToString();
            }

            sb.AppendLine($"\nSummary:");
            sb.AppendLine($"Total    : {_renderer.bones.Length}");
            sb.AppendLine($"OK       : {countOk}");
            sb.AppendLine($"Mismatch : {countMismatch}");
            sb.AppendLine($"Null     : {countMissing}\n");


            // check new root bone
            // --------------------------------
            var bonePathMap = _referenceRenderer.bones
                .Where(b => b != null)
                .ToDictionary(b => b.name,
                    b => b.gameObject.transform.GetRelativePath(_referenceRenderer.rootBone.parent));
            var newRootBoneMissingCount = 0;
            foreach (var bone in bonePathMap)
            {
                var targetInNewRootBone = _newRootBone.parent.Find(bone.Value);
                if (targetInNewRootBone == null)
                {
                    sb.AppendLine($"{bone.Key} at '{bone.Value}' not found in the new Root Bone.");
                    newRootBoneMissingCount++;
                }

                _boneDictionary[bone.Key] = targetInNewRootBone;
            }

            sb.AppendLine($"Total missing bones in new Root Bone: {newRootBoneMissingCount}\n");

            return sb.ToString();
        }

        private string UpdateSkinnedMeshRenderer()
        {
            var sb = new StringBuilder();
            Transform[] newBones = _boneDictionary.Values.ToArray();

            _renderer.bones = newBones;
            _renderer.rootBone = _newRootBone;
            sb.AppendLine($"Updated Bones in {_renderer.name}:");
            sb.AppendLine($"  - rootBone set to: {_renderer.rootBone.transform.GetPath()}");
            sb.AppendLine($"  - bones total: {_renderer.bones.Length}");
            sb.AppendLine($"  - bones missing: {_renderer.bones.Count(b => b == null)}");

            return sb.ToString();
        }

        public static bool TryGetElement<T>(T[] array, int index, out T result)
            where T : class
        {
            if (index >= 0 && index < array.Length && !EqualityComparer<T>.Default.Equals(array[index], null))
            {
                result = array[index];
                return true;
            }

            result = null;
            return false;
        }
    }
}
