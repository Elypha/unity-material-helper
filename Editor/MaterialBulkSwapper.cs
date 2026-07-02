using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using Elypha.Common;
using Elypha.I18N;

namespace Elypha.UnityMaterialHelper
{
    public class MaterialBulkSwapper : EditorWindow
    {
        private Vector2 _scrollPosition;

        private bool _showAdvancedSettings = false;
        private static PluginLanguage _language = PluginLanguage.English;
        private readonly MaterialBulkSwapperI18N _i18n = new(_language);
        private readonly GuiMessage _guiMessage = new();

        private GameObject _assumedRootObject;
        private GameObject _outfitObject;
        private bool _isOverrideOutfitObjectName = false;
        private string _overrideOutfitObjectName = "";
        private int _lastOutfitObject = -1;
        private AnimationClip _animationClip;
        private int _lastAnimationClip = -1;
        private int _targetFrameNumber;
        private readonly Dictionary<GameObject, RendererConfig> _rendererConfigs = new();
        private readonly Dictionary<Material, MaterialConfig> _materialConfigs = new();
        private int _clipNextFrame;
        private float _targetFrameTime;


        private readonly string[] actionTabs = new string[] { "Replace In-Place", "Replace In Animation" };
        private int actionTabIndex = 0;


        [MenuItem("Elypha/Material Bulk Swapper")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialBulkSwapper>("Material Bulk Swapper");
            window.minSize = new Vector2(400, 400);
        }


        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MinWidth(400));

            // Settings
            // --------------------------------
            Services.DrawAdvancedSettings(ref _showAdvancedSettings, ref _language, _i18n);

            Services.DrawTitle1(_i18n.Localise("Settings"));


            // Outfit Object
            _outfitObject = (GameObject)EditorGUILayout.ObjectField(_i18n.Localise("Outfit Object"), _outfitObject, typeof(GameObject), true, Services.LayoutExpanded);
            _assumedRootObject = (GameObject)EditorGUILayout.ObjectField(_i18n.Localise("Path relative to"), _assumedRootObject, typeof(GameObject), true, Services.LayoutExpanded);
            _isOverrideOutfitObjectName = EditorGUILayout.Toggle(_i18n.Localise("Override Outfit Object Name"), _isOverrideOutfitObjectName);
            if (_isOverrideOutfitObjectName)
            {
                _overrideOutfitObjectName = EditorGUILayout.TextField(_i18n.Localise("Outfit Object Name"), _overrideOutfitObjectName);
            }
            var _currentOutfitObject = _outfitObject == null ? -1 : _outfitObject.GetInstanceID();
            if (_lastOutfitObject != _currentOutfitObject)
            {
                _lastOutfitObject = _outfitObject.GetInstanceID();
                LoadRenderers();
                LoadMaterials();
            }

            // Reload
            if (GUILayout.Button(_i18n.Localise("Reload")))
            {
                LoadRenderers();
                LoadMaterials();
            }


            Services.DrawTitle1(_i18n.Localise("Edit"));

            // Renderers
            // --------------------------------
            GUILayout.BeginHorizontal();
            Services.LabelBoldColored("Skinned Mesh Renderers", Services.ColourBold);
            if (GUILayout.Button(_i18n.Localise("Select All")))
            {
                foreach (var pair in _rendererConfigs)
                {
                    pair.Value.Enabled = true;
                }
                LoadMaterials();
            }
            if (GUILayout.Button(_i18n.Localise("Select None")))
            {
                foreach (var pair in _rendererConfigs)
                {
                    pair.Value.Enabled = false;
                }
                LoadMaterials();
            }
            GUILayout.EndHorizontal();

            // Renderer List
            if (_rendererConfigs.Count > 0)
            {
                foreach (GameObject go in _rendererConfigs.Keys.OrderBy(go => go.name))
                {
                    GUILayout.BeginHorizontal();
                    var currentState = _rendererConfigs[go].Enabled;
                    _rendererConfigs[go].Enabled = EditorGUILayout.Toggle(_rendererConfigs[go].Enabled, GUILayout.Width(16));
                    if (currentState != _rendererConfigs[go].Enabled)
                    {
                        LoadMaterials();
                    }
                    EditorGUILayout.ObjectField(go, typeof(GameObject), false);
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUI.enabled = false;
                GUILayout.BeginHorizontal();
                EditorGUILayout.Toggle(false, GUILayout.Width(16));
                EditorGUILayout.ObjectField(null, typeof(GameObject), false);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }


            // Materials
            // --------------------------------
            GUILayout.BeginHorizontal();
            Services.LabelBoldColored(_i18n.Localise("Unique Materials"), Services.ColourBold);
            if (GUILayout.Button(_i18n.Localise("Select All")))
            {
                foreach (var pair in _materialConfigs)
                {
                    pair.Value.Enabled = true;
                }
            }
            if (GUILayout.Button(_i18n.Localise("Select None")))
            {
                foreach (var pair in _materialConfigs)
                {
                    pair.Value.Enabled = false;
                }
            }
            GUILayout.EndHorizontal();

            // Material List
            if (_materialConfigs.Count > 0)
            {
                foreach (var pair in _materialConfigs.OrderBy(pair => pair.Value.Excluded).ThenBy(pair => pair.Key.name))
                {
                    var mat = pair.Key;
                    if (pair.Value.Excluded) GUI.enabled = false;
                    GUILayout.BeginHorizontal();
                    var currentState = _materialConfigs[mat].Enabled;
                    _materialConfigs[mat].Enabled = EditorGUILayout.Toggle(_materialConfigs[mat].Enabled, GUILayout.Width(16));
                    if (currentState != _materialConfigs[mat].Enabled)
                    {
                        LoadRenderers();
                    }
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);
                    EditorGUILayout.LabelField("->", Services.LabelStyleCentred, GUILayout.Width(16));
                    _materialConfigs[mat].TargetMaterial = (Material)EditorGUILayout.ObjectField(_materialConfigs[mat].TargetMaterial, typeof(Material), false);
                    GUILayout.EndHorizontal();
                    if (pair.Value.Excluded) GUI.enabled = true;
                }
            }
            else
            {
                GUI.enabled = false;
                GUILayout.BeginHorizontal();
                EditorGUILayout.Toggle(false, GUILayout.Width(16));
                EditorGUILayout.ObjectField(null, typeof(Material), false);
                EditorGUILayout.LabelField("->", Services.LabelStyleCentred, GUILayout.Width(25));
                EditorGUILayout.ObjectField(null, typeof(Material), false);
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }


            Services.DrawTitle1(_i18n.Localise("Select Action"));

            // Actions
            // --------------------------------
            GUILayout.BeginVertical();
            actionTabIndex = GUILayout.Toolbar(actionTabIndex, actionTabs.Select(_i18n.Localise).ToArray());
            GUILayout.EndVertical();

            switch (actionTabIndex)
            {
                case 0:
                    DrawReplaceTab();
                    break;
                case 1:
                    DrawCreateAnimationTab();
                    break;
                default:
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawReplaceTab()
        {
            Services.LabelBoldColored(_i18n.Localise("Replace Material"), Services.ColourBold);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_i18n.Localise("Restore")))
            {
                ApplyInPlaceMaterialOriginal();
            }
            if (GUILayout.Button(_i18n.Localise("Replace Material")))
            {
                ApplyInPlaceMaterialSwapped();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCreateAnimationTab()
        {
            Services.LabelBoldColored(_i18n.Localise("Select Animation Clip for swap"), Services.ColourBold);

            // Animation Clip
            GUILayout.BeginHorizontal();
            _animationClip = (AnimationClip)EditorGUILayout.ObjectField(_i18n.Localise("Animation Clip"), _animationClip, typeof(AnimationClip), false, Services.LayoutExpanded);
            var _currentAnimationClip = _animationClip == null ? -1 : _animationClip.GetInstanceID();
            if (_lastAnimationClip != _currentAnimationClip)
            {
                _lastAnimationClip = _currentAnimationClip;
                LoadAnimationClip();
            }
            if (GUILayout.Button(_i18n.Localise("Reload")))
            {
                LoadAnimationClip();
            }
            GUILayout.EndHorizontal();

            if (_animationClip == null) GUI.enabled = false;
            EditorGUILayout.IntField("┗ Next frame on this clip", _clipNextFrame);
            if (_animationClip == null) GUI.enabled = true;


            // Next Frame
            Services.LabelBoldColored(_i18n.Localise("Add keyframe"), Services.ColourBold);

            if (_animationClip == null) GUI.enabled = false;
            _targetFrameNumber = EditorGUILayout.IntField(_i18n.Localise("Target Frame"), _targetFrameNumber);
            if (_animationClip == null) GUI.enabled = true;

            if (_animationClip == null)
            {
                GUI.enabled = false;
                GUILayout.Button("Please select an Animation Clip");
                GUI.enabled = true;
            }
            else
            {
                var seconds = Math.Floor(_targetFrameNumber / _animationClip.frameRate);
                var frames = _targetFrameNumber % _animationClip.frameRate;
                _targetFrameTime = _targetFrameNumber / _animationClip.frameRate;
                if (GUILayout.Button($"Add to Frame #{_targetFrameNumber} [{seconds}:{frames:00}] (at {_targetFrameTime:0.000}s)"))
                {
                    AddMaterialSwapKeyframes();
                }
            }
        }


        private void LoadRenderers()
        {
            Services.Unfocus();
            if (_outfitObject == null) return;

            _rendererConfigs.Clear();

            var renderers = Services.GetSkinnedGameObjects(_outfitObject);
            foreach (GameObject go in renderers)
            {
                _rendererConfigs[go] = new RendererConfig
                {
                    OriginalMaterials = go.GetComponent<SkinnedMeshRenderer>().sharedMaterials,
                };
            }

            // set to off, if all its materials are not enabled
            foreach (var pair in _rendererConfigs)
            {
                bool hasEnabledMaterial = false;
                foreach (var mat in pair.Value.OriginalMaterials)
                {
                    if (_materialConfigs.ContainsKey(mat) && _materialConfigs[mat].Enabled)
                    {
                        hasEnabledMaterial = true;
                        break;
                    }
                }
                if (!hasEnabledMaterial)
                {
                    pair.Value.Enabled = false;
                }
            }
        }

        private void LoadMaterials()
        {
            Services.Unfocus();
            if (_outfitObject == null) return;

            _materialConfigs.Clear();
            var renderers = GetRenderers();
            var enabledRenderers = GetEnabledRenderers();
            foreach (SkinnedMeshRenderer renderer in enabledRenderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && !_materialConfigs.ContainsKey(mat))
                    {
                        _materialConfigs[mat] = new MaterialConfig
                        {
                            TargetMaterial = mat
                        };
                    }
                }
            }
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null && !_materialConfigs.ContainsKey(mat))
                    {
                        _materialConfigs[mat] = new MaterialConfig
                        {
                            TargetMaterial = null,
                            Enabled = false,
                            Excluded = true
                        };
                    }
                }
            }
        }

        private void LoadAnimationClip()
        {
            Services.Unfocus();
            if (_animationClip == null) return;

            _clipNextFrame = _animationClip.empty ? 0 : (int)(_animationClip.length * _animationClip.frameRate);
            _targetFrameNumber = _clipNextFrame;
        }



        private void AddMaterialSwapKeyframes()
        {
            Services.Unfocus();

            var renderers = GetEnabledRenderers();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                var path = renderer.transform.GetRelativePath(_assumedRootObject.transform);

                if (_isOverrideOutfitObjectName && !string.IsNullOrEmpty(_overrideOutfitObjectName))
                {
                    path = Utils.ReplaceLastOccurrence(path, _outfitObject.name, _overrideOutfitObjectName);
                }

                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    var mat = renderer.sharedMaterials[i];

                    if (!_materialConfigs.ContainsKey(mat) || !_materialConfigs[mat].Enabled)
                    {
                        continue;
                    }

                    var binding = EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), $"m_Materials.Array.data[{i}]");
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(_animationClip, binding);

                    var newKeyframe = new ObjectReferenceKeyframe
                    {
                        time = _targetFrameTime,
                        value = _materialConfigs[mat].TargetMaterial
                    };

                    if (keyframes != null && keyframes.Length > 0)
                    {
                        bool isUpdated = false;
                        for (int j = 0; j < keyframes.Length; j++)
                        {
                            if (Mathf.Approximately(keyframes[j].time, newKeyframe.time))
                            {
                                keyframes[j].value = _materialConfigs[mat].TargetMaterial;
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

                    AnimationUtility.SetObjectReferenceCurve(_animationClip, binding, keyframes);
                }
            }

            AssetDatabase.SaveAssets();
            LoadAnimationClip();
        }

        private void ApplyInPlaceMaterialOriginal()
        {
            Services.Unfocus();

            var renderers = GetEnabledRenderers();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var originalMat = _rendererConfigs[renderer.gameObject].OriginalMaterials[i];
                    if (materials[i] != originalMat)
                    {
                        materials[i] = originalMat;
                    }
                }
                renderer.sharedMaterials = materials;
                Undo.RecordObject(renderer, "Change Material");
                EditorUtility.SetDirty(renderer);
            }
        }

        private void ApplyInPlaceMaterialSwapped()
        {
            Services.Unfocus();

            var renderers = GetEnabledRenderers();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var originalMat = _rendererConfigs[renderer.gameObject].OriginalMaterials[i];
                    if (_materialConfigs.ContainsKey(originalMat) && _materialConfigs[originalMat].Enabled)
                    {
                        materials[i] = _materialConfigs[originalMat].TargetMaterial;
                    }
                }
                renderer.sharedMaterials = materials;
                Undo.RecordObject(renderer, "Change Material");
                EditorUtility.SetDirty(renderer);
            }
        }



        // data
        // --------------------------------
        private class RendererConfig
        {
            public bool Enabled = true;
            public Material[] OriginalMaterials;
        }

        private class MaterialConfig
        {
            public Material TargetMaterial;
            public bool Enabled = true;
            public bool Excluded = false;
        }

        private SkinnedMeshRenderer[] GetEnabledRenderers() => _rendererConfigs.Where(pair => pair.Value.Enabled).Select(pair => pair.Key.GetComponent<SkinnedMeshRenderer>()).ToArray();
        private SkinnedMeshRenderer[] GetRenderers() => _rendererConfigs.Keys.Select(go => go.GetComponent<SkinnedMeshRenderer>()).ToArray();
    }
}
