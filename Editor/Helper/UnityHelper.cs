using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using VRC.SDK3.Dynamics.PhysBone.Components;


namespace Elypha.Helper
{
    public static class UnityHelper
    {
        public static Color ColourTitle1 = new(230 / 255f, 194 / 255f, 153 / 255f);
        public static Color ColourTitle2 = new(130 / 255f, 187 / 255f, 255 / 255f);
        public static Color ColourBold = new(210 / 255f, 210 / 255f, 210 / 255f);

        public static void Separator(Color color, int thickness = 2, int paddingTop = 4, int paddingBottom = 4)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(paddingTop + paddingBottom + thickness));
            r.height = thickness;
            r.y += paddingTop;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, color);
        }

        public static void LabelBoldColored(string label, Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color } };
            EditorGUILayout.LabelField(label, style);
        }

        public static void Unfocus() => GUI.FocusControl(null);

        public static string GetRelativePathInHierarchy(Transform root, Transform target)
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

        public static List<GameObject> GetSkinnedGameObjects(GameObject parent)
        {
            List<GameObject> results = new();
            ScanSkinnedMeshRenderers(parent.transform, results);
            return results;
        }

        private static void ScanSkinnedMeshRenderers(Transform parent, List<GameObject> results)
        {
            foreach (Transform child in parent)
            {
                if (child.TryGetComponent(out SkinnedMeshRenderer smr))
                {
                    results.Add(child.gameObject);
                }
                // Recursively check children
                ScanSkinnedMeshRenderers(child, results);
            }
        }

        public static bool IsDefaultTransform(Transform transform)
        {
            return transform.localPosition == Vector3.zero
                && transform.localRotation == Quaternion.identity
                && transform.localScale == Vector3.one;
        }

        public static Transform GetPhysBoneRoot(VRCPhysBone physBone)
        {
            if (physBone.rootTransform != null) return physBone.rootTransform;
            return physBone.transform;
        }

        public static Transform GetPhysBoneColliderRoot(VRCPhysBoneCollider physBoneCollider)
        {
            // If rootTransform is not set || set to itself, use the parent transform
            if (physBoneCollider.rootTransform == null || physBoneCollider.rootTransform == physBoneCollider.transform)
            {
                if (IsDefaultTransform(physBoneCollider.transform))
                {
                    return physBoneCollider.transform.parent.transform;
                }

                Debug.LogError($"PhysBoneCollider: {physBoneCollider.name} already has transform set");
                return physBoneCollider.transform.parent.transform;
            }

            // If rootTransform is set to any other, use the rootTransform
            return physBoneCollider.rootTransform;
        }

        public static void DrawTitle1(string title, float spacePixels = 8)
        {
            GUILayout.Space(spacePixels);
            LabelBoldColored($"# {title}", ColourTitle1);
            Separator(Color.grey, 1, 0, 4);
        }

    }
}
