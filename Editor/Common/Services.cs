using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using VRC.SDK3.Dynamics.PhysBone.Components;


namespace Elypha.Common
{
    public static class Services
    {
        public static Color ColourTitle1 = new(230 / 255f, 194 / 255f, 153 / 255f);
        public static Color ColourTitle2 = new(130 / 255f, 187 / 255f, 255 / 255f);
        public static Color ColourBold = new(210 / 255f, 210 / 255f, 210 / 255f);


        public static readonly GUILayoutOption[] LayoutExpanded = new GUILayoutOption[] {
            GUILayout.ExpandWidth(true),
            GUILayout.MinWidth(300),
        };

        public static readonly GUIStyle LabelStyleCentred = new(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };


        public static void Separator() => Separator(Color.grey, 1, 0, 4);
        public static void Separator(Color colour, int thickness = 2, int paddingTop = 4, int paddingBottom = 4)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(paddingTop + paddingBottom + thickness));
            r.height = thickness;
            r.y += paddingTop;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, colour);
        }

        public static void LabelBoldColored(string label, Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color } };
            EditorGUILayout.LabelField(label, style);
        }

        public static void Unfocus() => GUI.FocusControl(null);

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

        public static bool IsTransformValueDefault(Transform transform)
        {
            return transform.localPosition == Vector3.zero
                && transform.localRotation == Quaternion.identity
                && transform.localScale == Vector3.one;
        }

        public static bool IsPhysBoneColliderTransformValueDefault(VRCPhysBoneCollider pbc)
        {
            return pbc.position == Vector3.zero
                && pbc.rotation == Quaternion.identity;
        }

        public static Transform GetCorrespondingTransformByRelativePath(Transform source, Transform sourceRoot, Transform targetRoot)
        {
            if (source == null || sourceRoot == null || targetRoot == null) return null;

            string relativePath = source.GetRelativePath(sourceRoot);
            Transform targetTransform = targetRoot.Find(relativePath);

            if (targetTransform == null)
                Debug.LogWarning($"Target transform not found for: {relativePath}");

            return targetTransform;
        }

        public static Transform GetPhysBoneRoot(VRCPhysBone physBone)
        {
            if (physBone.rootTransform != null) return physBone.rootTransform;
            return physBone.transform;
        }

        public static void DrawTitle1(string title, float spacePixels = 8)
        {
            GUILayout.Space(spacePixels);
            LabelBoldColored($"# {title}", ColourTitle1);
            Separator();
        }

        public static void DrawAdvancedSettings(ref bool showAdvancedSettings, ref I18N.PluginLanguage language, I18N.TemplateI18N i18n)
        {
            showAdvancedSettings = EditorGUILayout.Foldout(
                showAdvancedSettings,
                "Advanced Settings",
                true,
                new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold
                }
            );

            if (showAdvancedSettings)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Language", GUILayout.Width(100));
                language = (I18N.PluginLanguage)EditorGUILayout.EnumPopup(language, GUILayout.Width(200));
                if (language != i18n.language)
                {
                    i18n.SetLanguage(language);
                }
                GUILayout.EndHorizontal();
            }
        }

    }



}
