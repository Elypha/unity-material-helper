using System;
using System.Collections.Generic;
using System.Linq;
using Elypha.Common;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Elypha.UnityMaterialHelper
{
    public class MaterialSwapper : EditorWindow
    {
        // functions
        private Vector2 _scrollPosition;
        private readonly GuiMessage _guiMessage = new();

        // user settings
        private GameObject _outfitObject;
        private bool _isOverrideOutfitObjectName = false;
        private string _overrideOutfitObjectName = "";
        private GameObject _assumedRootObject;
        private readonly List<Material> _targetMaterials = new();
        private ReorderableList _targetMaterialsList;
        private AnimationClip _animationClip;
        private int _targetFrameNumber;
        private int _clipNextFrame;

        // data
        private int _lastOutfitObject = -1;
        private int _lastAnimationClip = -1;
        private readonly Dictionary<GameObject, RendererConfig> _rendererConfigs = new();
        private readonly Dictionary<Material, MaterialConfig> _materialConfigs = new();


        [MenuItem("Elypha/Material Swapper")]
        public static void ShowWindow()
        {
            var window = GetWindow<MaterialSwapper>("Material Swapper");
            window.minSize = new Vector2(400, 400);
        }

        private void OnEnable()
        {
            _targetMaterialsList = new ReorderableList(_targetMaterials, typeof(Material), true, true, true, true)
            {
                drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Target Materials"); },
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    _targetMaterials[index] = (Material)EditorGUI.ObjectField(rect, _targetMaterials[index], typeof(Material), false);
                }
            };
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, false, false, GUILayout.ExpandWidth(true));

            Services.DrawTitle1("Settings");
            DrawSettings();

            Services.DrawTitle1("Edit");
            DrawEdit();

            Services.DrawTitle1("Process");
            DrawProcess();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettings()
        {
            var labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            var labelWidth = 150;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Outfit Object", labelStyle, GUILayout.Width(labelWidth));
            _outfitObject = (GameObject)EditorGUILayout.ObjectField(_outfitObject, typeof(GameObject), true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("┗ Override Object name", labelStyle, GUILayout.Width(labelWidth));
            _isOverrideOutfitObjectName = EditorGUILayout.Toggle(_isOverrideOutfitObjectName, GUILayout.Width(16));
            if (_isOverrideOutfitObjectName)
            {
                _overrideOutfitObjectName = EditorGUILayout.TextField(_overrideOutfitObjectName);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Animation path relative to", labelStyle, GUILayout.Width(labelWidth));
            _assumedRootObject = (GameObject)EditorGUILayout.ObjectField(_assumedRootObject, typeof(GameObject), true);
            EditorGUILayout.EndHorizontal();

            // validation
            var _currentOutfitObject = _outfitObject == null ? -1 : _outfitObject.GetInstanceID();
            if (_lastOutfitObject != _currentOutfitObject)
            {
                _lastOutfitObject = _outfitObject.GetInstanceID();
                LoadRenderers();
                LoadMaterials();
                // set relative to the parent of the outfit object, if not set
                if (_assumedRootObject == null && _outfitObject != null && _outfitObject.transform.parent != null)
                {
                    _assumedRootObject = _outfitObject.transform.parent.gameObject;
                }
            }

            // Reload
            if (GUILayout.Button("Reload"))
            {
                LoadRenderers();
                LoadMaterials();
            }
        }

        private void DrawEdit()
        {
            var maxWidth = 250;
            GUILayout.BeginHorizontal();

            // Renderer List
            GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
            GUILayout.BeginHorizontal();
            Services.LabelBoldColored("Renderers", Services.ColourBold);
            if (GUILayout.Button("All", GUILayout.Width(50)))
            {
                foreach (var pair in _rendererConfigs)
                {
                    pair.Value.Enabled = true;
                }
                LoadMaterials();
            }
            if (GUILayout.Button("None", GUILayout.Width(50)))
            {
                foreach (var pair in _rendererConfigs)
                {
                    pair.Value.Enabled = false;
                }
                LoadMaterials();
            }
            GUILayout.EndHorizontal();

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
                    Color originalColor = GUI.color;
                    if (_rendererConfigs[go].HasEnabledMaterial)
                    {
                        GUI.color = Color.cyan;
                    }
                    EditorGUILayout.ObjectField(go, typeof(GameObject), false);
                    GUI.color = originalColor;
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
            GUILayout.EndVertical();


            // Material List
            GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
            GUILayout.BeginHorizontal();
            Services.LabelBoldColored("Materials", Services.ColourBold);
            if (GUILayout.Button("All", GUILayout.Width(50)))
            {
                foreach (var pair in _materialConfigs)
                {
                    pair.Value.Enabled = true;
                }
                UpdateRendererColour();
            }
            if (GUILayout.Button("None", GUILayout.Width(50)))
            {
                foreach (var pair in _materialConfigs)
                {
                    pair.Value.Enabled = false;
                }
                UpdateRendererColour();
            }
            GUILayout.EndHorizontal();

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
                        UpdateRendererColour();
                    }
                    EditorGUILayout.ObjectField(mat, typeof(Material), false);
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
                GUILayout.EndHorizontal();
                GUI.enabled = true;
            }
            GUILayout.EndVertical();


            // Target Materials List
            GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
            _targetMaterialsList.DoLayoutList();

            // Drag and drop area; clear button
            GUILayout.BeginHorizontal();
            var dropRect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop Materials Here", new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter });
            if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
            {
                if (dropRect.Contains(Event.current.mousePosition) && DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences.All(o => o is Material))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (Event.current.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (!_targetMaterials.Contains(obj as Material))
                            {
                                _targetMaterials.Add(obj as Material);
                            }
                        }
                    }
                    Event.current.Use();
                }
            }
            if (GUILayout.Button("↺", new GUILayoutOption[] { GUILayout.Width(28), GUILayout.Height(28) }))
            {
                _targetMaterials.Clear();
            }
            GUILayout.EndHorizontal();


            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawProcess()
        {
            Services.LabelBoldColored("Select Animation Clip for swap", Services.ColourBold);

            // Animation Clip
            GUILayout.BeginHorizontal();
            _animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _animationClip, typeof(AnimationClip), false, Services.LayoutExpanded);
            var _currentAnimationClip = _animationClip == null ? -1 : _animationClip.GetInstanceID();
            if (_lastAnimationClip != _currentAnimationClip)
            {
                _lastAnimationClip = _currentAnimationClip;
                LoadAnimationClip();
            }
            if (GUILayout.Button("Reload"))
            {
                LoadAnimationClip();
            }
            GUILayout.EndHorizontal();

            if (_animationClip == null) GUI.enabled = false;
            EditorGUILayout.IntField("┗ Next frame on this clip", _clipNextFrame);
            if (_animationClip == null) GUI.enabled = true;


            // Next Frame
            Services.LabelBoldColored("Add keyframe", Services.ColourBold);

            if (_animationClip == null) GUI.enabled = false;
            _targetFrameNumber = EditorGUILayout.IntField("Target Frame", _targetFrameNumber);
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
                if (GUILayout.Button($"Add {_targetMaterials.Count} materials to Frame #{_targetFrameNumber} [{seconds}:{frames:00}]"))
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
        }

        private void UpdateRendererColour()
        {
            Services.Unfocus();
            if (_outfitObject == null) return;

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

                pair.Value.HasEnabledMaterial = hasEnabledMaterial;
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
                            Enabled = false,
                            TargetMaterial = mat,
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
                            Enabled = false,
                            TargetMaterial = null,
                            Excluded = true
                        };
                    }
                }
            }
            UpdateRendererColour();
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

            foreach (SkinnedMeshRenderer renderer in GetEnabledRenderers())
            {
                // path
                var path = renderer.transform.GetRelativePath(_assumedRootObject.transform);
                if (_isOverrideOutfitObjectName && !string.IsNullOrEmpty(_overrideOutfitObjectName))
                {
                    path = Utils.ReplaceLastOccurrence(path, _outfitObject.name, _overrideOutfitObjectName);
                }

                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    var mat = renderer.sharedMaterials[i];
                    if (!_materialConfigs.ContainsKey(mat) || !_materialConfigs[mat].Enabled) continue;

                    var binding = EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), $"m_Materials.Array.data[{i}]");
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(_animationClip, binding) ?? new ObjectReferenceKeyframe[0];

                    // Add a series of keyframes, one per target material at consecutive frames
                    for (int targetIndex = 0; targetIndex < _targetMaterials.Count; targetIndex++)
                    {
                        var frameTime = (_targetFrameNumber + targetIndex) / _animationClip.frameRate;
                        var targetMat = _targetMaterials[targetIndex];

                        var newKeyframe = new ObjectReferenceKeyframe
                        {
                            time = frameTime,
                            value = targetMat
                        };

                        // Check if a keyframe already exists at this time; update or append
                        bool isUpdated = false;
                        for (int j = 0; j < keyframes.Length; j++)
                        {
                            if (Mathf.Approximately(keyframes[j].time, newKeyframe.time))
                            {
                                keyframes[j].value = targetMat;
                                isUpdated = true;
                                break;
                            }
                        }
                        if (!isUpdated)
                        {
                            keyframes = keyframes.Append(newKeyframe).ToArray();
                        }
                    }

                    AnimationUtility.SetObjectReferenceCurve(_animationClip, binding, keyframes);
                }
            }

            AssetDatabase.SaveAssets();
            LoadAnimationClip();
        }


        // data
        // --------------------------------
        private class RendererConfig
        {
            public bool Enabled = true;
            public Material[] OriginalMaterials;
            public bool HasEnabledMaterial;
        }

        private class MaterialConfig
        {
            public bool Enabled = true;
            public Material TargetMaterial;
            public bool Excluded = false;
        }

        private SkinnedMeshRenderer[] GetEnabledRenderers() => _rendererConfigs.Where(pair => pair.Value.Enabled).Select(pair => pair.Key.GetComponent<SkinnedMeshRenderer>()).ToArray();
        private SkinnedMeshRenderer[] GetRenderers() => _rendererConfigs.Keys.Select(go => go.GetComponent<SkinnedMeshRenderer>()).ToArray();
    }
}
