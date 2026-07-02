using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Elypha.Common;
using Elypha.I18N;

namespace Elypha.UnityMaterialHelper
{
    public class SoftDisableEditor : EditorWindow
    {
        private Vector2 _scrollPosition;

        private AnimationClip _targetClip;
        private GameObject _animatorRootObject;
        private readonly List<GameObject> _objectsToDisable = new() { null };

        private bool _showAdvancedSettings = false;
        private static PluginLanguage _language = PluginLanguage.English;
        private readonly TemplateI18N _i18n = new(_language);
        private readonly GuiMessage _guiMessage = new();


        [MenuItem("Elypha/Soft Disable")]
        public static void ShowWindow()
        {
            var window = GetWindow<SoftDisableEditor>("Soft Disable");
            window.minSize = new Vector2(300, 400);
        }

        private void OnGUI()
        {

            Services.DrawAdvancedSettings(ref _showAdvancedSettings, ref _language, _i18n);

            Services.DrawTitle1(_i18n.Localise("Settings"));

            EditorGUILayout.LabelField("Target Animation Clip", EditorStyles.boldLabel);
            _targetClip = (AnimationClip)EditorGUILayout.ObjectField("Write curves to", _targetClip, typeof(AnimationClip), false);
            _animatorRootObject = (GameObject)EditorGUILayout.ObjectField("Path relative to", _animatorRootObject, typeof(GameObject), true);


            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Skinned Mesh Renderers to disable", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawGameObjectList();

            EditorGUILayout.EndScrollView();

            Services.Separator(Color.grey, 1, 0, 4);

            _guiMessage.Draw(10, Repaint);


            GUI.enabled = IsAllInputsValid();
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            if (GUILayout.Button("Generate Disable Animation", GUILayout.Height(40)))
            {
                ProcessAnimation();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        private void DrawGameObjectList()
        {
            for (int i = 0; i < _objectsToDisable.Count; i++)
            {
                EditorGUI.BeginChangeCheck();
                GameObject newObj = (GameObject)EditorGUILayout.ObjectField(_objectsToDisable[i], typeof(GameObject), true);

                if (EditorGUI.EndChangeCheck())
                {
                    if (newObj != null && newObj.GetComponent<SkinnedMeshRenderer>() == null)
                    {
                        Debug.LogError($"错误: '{newObj.name}' 不包含 SkinnedMeshRenderer 组件，无法添加。");
                    }
                    else
                    {
                        _objectsToDisable[i] = newObj;
                    }
                }
            }

            if (_objectsToDisable.Count == 0 || _objectsToDisable[_objectsToDisable.Count - 1] != null)
            {
                _objectsToDisable.Add(null);
                Repaint();
            }
        }

        private void ProcessAnimation()
        {
            List<GameObject> validObjects = _objectsToDisable.Where(obj => obj != null).ToList();

            // Register the clip for an undo operation. This single call covers all subsequent modifications.
            Undo.RecordObject(_targetClip, "Generate Soft Disable Animation");

            _targetClip.ClearCurves();

            Transform rootTransform = _animatorRootObject != null ? _animatorRootObject.transform : null;

            foreach (GameObject go in validObjects)
            {
                string path = AnimationUtility.CalculateTransformPath(go.transform, rootTransform);

                EditorCurveBinding binding = new()
                {
                    path = path,
                    type = typeof(SkinnedMeshRenderer),
                    propertyName = "m_Enabled" // The internal property name for enabling/disabling a component.
                };

                // We set tangents to create a "stepped" curve.
                Keyframe key = new(time: 0f, value: 0f, inTangent: float.PositiveInfinity, outTangent: float.PositiveInfinity);
                AnimationCurve curve = new(key);

                // Note: For performance, it's better to build a list and call SetEditorCurves once,
                // but for this tool, applying one by one is fine and conceptually simpler.
                AnimationUtility.SetEditorCurve(_targetClip, binding, curve);
            }

            EditorUtility.SetDirty(_targetClip);

            // string successMessage = $"成功为 {} 个对象在动画剪辑 '{}' 中生成了禁用关键帧。";
            string successMessage = $"Done! {validObjects.Count} disabled in '{_targetClip.name}'.";
            _guiMessage.Show(successMessage, 3);
            Debug.Log(successMessage);
        }

        private bool IsAllInputsValid()
        {
            return _targetClip != null && _objectsToDisable.Any(obj => obj != null);
        }

    }
}
