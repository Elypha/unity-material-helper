using System;
using System.Collections.Generic;
using System.Linq;
using Elypha.Common;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;


public class MaterialSwapper : EditorWindow
{
    // functions
    private Vector2 scrollPosition;
    private readonly GuiMessage guiMessage = new();

    // user settings
    private GameObject outfitObject;
    private bool isOverrideOutfitObjectName = false;
    private string overrideOutfitObjectName = "";
    private GameObject assumedRootObject;
    private readonly List<Material> targetMaterials = new();
    private ReorderableList targetMaterialsList;
    private AnimationClip animationClip;
    private int targetFrameNumber;
    private int clipNextFrame;

    // data
    private int _lastOutfitObject = -1;
    private int _lastAnimationClip = -1;
    private readonly Dictionary<GameObject, RendererConfig> rendererConfigs = new();
    private readonly Dictionary<Material, MaterialConfig> materialConfigs = new();


    [MenuItem("Elypha/Material Swapper")]
    public static void ShowWindow()
    {
        var window = GetWindow<MaterialSwapper>("Material Swapper");
        window.minSize = new Vector2(400, 400);
    }

    private void OnEnable()
    {
        targetMaterialsList = new ReorderableList(targetMaterials, typeof(Material), true, true, true, true)
        {
            drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Target Materials"); },
            drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                targetMaterials[index] = (Material)EditorGUI.ObjectField(rect, targetMaterials[index], typeof(Material), false);
            }
        };
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false, GUILayout.ExpandWidth(true));

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
        outfitObject = (GameObject)EditorGUILayout.ObjectField(outfitObject, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("┗ Override Object name", labelStyle, GUILayout.Width(labelWidth));
        isOverrideOutfitObjectName = EditorGUILayout.Toggle(isOverrideOutfitObjectName, GUILayout.Width(16));
        if (isOverrideOutfitObjectName)
        {
            overrideOutfitObjectName = EditorGUILayout.TextField(overrideOutfitObjectName);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Animation path relative to", labelStyle, GUILayout.Width(labelWidth));
        assumedRootObject = (GameObject)EditorGUILayout.ObjectField(assumedRootObject, typeof(GameObject), true);
        EditorGUILayout.EndHorizontal();

        // validation
        var _currentOutfitObject = outfitObject == null ? -1 : outfitObject.GetInstanceID();
        if (_lastOutfitObject != _currentOutfitObject)
        {
            _lastOutfitObject = outfitObject.GetInstanceID();
            LoadRenderers();
            LoadMaterials();
            // set relative to the parent of the outfit object, if not set
            if (assumedRootObject == null && outfitObject != null && outfitObject.transform.parent != null)
            {
                assumedRootObject = outfitObject.transform.parent.gameObject;
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
            foreach (var pair in rendererConfigs)
            {
                pair.Value.Enabled = true;
            }
            LoadMaterials();
        }
        if (GUILayout.Button("None", GUILayout.Width(50)))
        {
            foreach (var pair in rendererConfigs)
            {
                pair.Value.Enabled = false;
            }
            LoadMaterials();
        }
        GUILayout.EndHorizontal();

        if (rendererConfigs.Count > 0)
        {
            foreach (GameObject go in rendererConfigs.Keys.OrderBy(go => go.name))
            {
                GUILayout.BeginHorizontal();
                var currentState = rendererConfigs[go].Enabled;
                rendererConfigs[go].Enabled = EditorGUILayout.Toggle(rendererConfigs[go].Enabled, GUILayout.Width(16));
                if (currentState != rendererConfigs[go].Enabled)
                {
                    LoadMaterials();
                }
                Color originalColor = GUI.color;
                if (rendererConfigs[go].HasEnabledMaterial)
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
            foreach (var pair in materialConfigs)
            {
                pair.Value.Enabled = true;
            }
            UpdateRendererColour();
        }
        if (GUILayout.Button("None", GUILayout.Width(50)))
        {
            foreach (var pair in materialConfigs)
            {
                pair.Value.Enabled = false;
            }
            UpdateRendererColour();
        }
        GUILayout.EndHorizontal();

        if (materialConfigs.Count > 0)
        {
            foreach (var pair in materialConfigs.OrderBy(pair => pair.Value.Excluded).ThenBy(pair => pair.Key.name))
            {
                var mat = pair.Key;
                if (pair.Value.Excluded) GUI.enabled = false;
                GUILayout.BeginHorizontal();
                var currentState = materialConfigs[mat].Enabled;
                materialConfigs[mat].Enabled = EditorGUILayout.Toggle(materialConfigs[mat].Enabled, GUILayout.Width(16));
                if (currentState != materialConfigs[mat].Enabled)
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
        targetMaterialsList.DoLayoutList();

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
                        if (!targetMaterials.Contains(obj as Material))
                        {
                            targetMaterials.Add(obj as Material);
                        }
                    }
                }
                Event.current.Use();
            }
        }
        if (GUILayout.Button("↺", new GUILayoutOption[] { GUILayout.Width(28), GUILayout.Height(28) }))
        {
            targetMaterials.Clear();
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
        animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false, Services.LayoutExpanded);
        var _currentAnimationClip = animationClip == null ? -1 : animationClip.GetInstanceID();
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

        if (animationClip == null) GUI.enabled = false;
        EditorGUILayout.IntField("┗ Next frame on this clip", clipNextFrame);
        if (animationClip == null) GUI.enabled = true;


        // Next Frame
        Services.LabelBoldColored("Add keyframe", Services.ColourBold);

        if (animationClip == null) GUI.enabled = false;
        targetFrameNumber = EditorGUILayout.IntField("Target Frame", targetFrameNumber);
        if (animationClip == null) GUI.enabled = true;

        if (animationClip == null)
        {
            GUI.enabled = false;
            GUILayout.Button("Please select an Animation Clip");
            GUI.enabled = true;
        }
        else
        {
            var seconds = Math.Floor(targetFrameNumber / animationClip.frameRate);
            var frames = targetFrameNumber % animationClip.frameRate;
            if (GUILayout.Button($"Add {targetMaterials.Count} materials to Frame #{targetFrameNumber} [{seconds}:{frames:00}]"))
            {
                AddMaterialSwapKeyframes();
            }
        }
    }


    private void LoadRenderers()
    {
        Services.Unfocus();
        if (outfitObject == null) return;

        rendererConfigs.Clear();

        var renderers = Services.GetSkinnedGameObjects(outfitObject);
        foreach (GameObject go in renderers)
        {
            rendererConfigs[go] = new RendererConfig
            {
                OriginalMaterials = go.GetComponent<SkinnedMeshRenderer>().sharedMaterials,
            };
        }
    }

    private void UpdateRendererColour()
    {
        Services.Unfocus();
        if (outfitObject == null) return;

        // set to off, if all its materials are not enabled
        foreach (var pair in rendererConfigs)
        {
            bool hasEnabledMaterial = false;
            foreach (var mat in pair.Value.OriginalMaterials)
            {
                if (materialConfigs.ContainsKey(mat) && materialConfigs[mat].Enabled)
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
        if (outfitObject == null) return;

        materialConfigs.Clear();
        var renderers = GetRenderers();
        var enabledRenderers = GetEnabledRenderers();
        foreach (SkinnedMeshRenderer renderer in enabledRenderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && !materialConfigs.ContainsKey(mat))
                {
                    materialConfigs[mat] = new MaterialConfig
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
                if (mat != null && !materialConfigs.ContainsKey(mat))
                {
                    materialConfigs[mat] = new MaterialConfig
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
        if (animationClip == null) return;

        clipNextFrame = animationClip.empty ? 0 : (int)(animationClip.length * animationClip.frameRate);
        targetFrameNumber = clipNextFrame;
    }



    private void AddMaterialSwapKeyframes()
    {
        Services.Unfocus();

        foreach (SkinnedMeshRenderer renderer in GetEnabledRenderers())
        {
            // path
            var path = renderer.transform.GetRelativePath(assumedRootObject.transform);
            if (isOverrideOutfitObjectName && !string.IsNullOrEmpty(overrideOutfitObjectName))
            {
                path = Utils.ReplaceLastOccurrence(path, outfitObject.name, overrideOutfitObjectName);
            }

            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                var mat = renderer.sharedMaterials[i];
                if (!materialConfigs.ContainsKey(mat) || !materialConfigs[mat].Enabled) continue;

                var binding = EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), $"m_Materials.Array.data[{i}]");
                var keyframes = AnimationUtility.GetObjectReferenceCurve(animationClip, binding) ?? new ObjectReferenceKeyframe[0];

                // Add a series of keyframes, one per target material at consecutive frames
                for (int targetIndex = 0; targetIndex < targetMaterials.Count; targetIndex++)
                {
                    var frameTime = (targetFrameNumber + targetIndex) / animationClip.frameRate;
                    var targetMat = targetMaterials[targetIndex];

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

                AnimationUtility.SetObjectReferenceCurve(animationClip, binding, keyframes);
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

    private SkinnedMeshRenderer[] GetEnabledRenderers() => rendererConfigs.Where(pair => pair.Value.Enabled).Select(pair => pair.Key.GetComponent<SkinnedMeshRenderer>()).ToArray();
    private SkinnedMeshRenderer[] GetRenderers() => rendererConfigs.Keys.Select(go => go.GetComponent<SkinnedMeshRenderer>()).ToArray();
}
