using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;


namespace Elypha.Helper
{
    public static class UnityHelper
    {
        public static Color ColourTitle = new(230 / 255f, 194 / 255f, 153 / 255f);
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

    }
}
