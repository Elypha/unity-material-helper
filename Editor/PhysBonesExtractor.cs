using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Elypha.Common;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.Dynamics;

public class PhysBonesExtractor : EditorWindow
{
    [MenuItem("Elypha/Phys Bones Extractor")]
    public static void ShowWindow()
    {
        var window = GetWindow<PhysBonesExtractor>("Phys Bones Extractor");
        window.minSize = new Vector2(400, 400);
    }

    private GameObject referenceArmature;
    private GameObject targetArmature;
    private GameObject physBoneParentObject;
    private GameObject PhysBoneCollidersParentObject;
    private VRCPhysBone[] physBones;
    private VRCPhysBoneCollider[] physBoneColliders;
    private readonly Dictionary<VRCPhysBoneCollider, VRCPhysBoneCollider> pbcMap = new();

    private readonly List<string> referenceOnly = new();
    private readonly List<string> targetOnly = new();

    private readonly List<string> nullColliderStatus = new();
    private readonly List<string> extraComponentStatus = new();

    private readonly GuiMessage guiMessage = new();

    private Vector2 scrollPosition;

    private void OnGUI()
    {
        referenceArmature = (GameObject)EditorGUILayout.ObjectField("Reference Armature", referenceArmature, typeof(GameObject), true);
        targetArmature = (GameObject)EditorGUILayout.ObjectField("Target Armature", targetArmature, typeof(GameObject), true);
        physBoneParentObject = (GameObject)EditorGUILayout.ObjectField("Create PBs under", physBoneParentObject, typeof(GameObject), true);
        PhysBoneCollidersParentObject = (GameObject)EditorGUILayout.ObjectField("Create PB Colliders under", PhysBoneCollidersParentObject, typeof(GameObject), true);

        GUI.enabled = referenceArmature && targetArmature;
        if (GUILayout.Button("Analyse Armature"))
        {
            nullColliderStatus.Clear();
            extraComponentStatus.Clear();
            referenceOnly.Clear();
            targetOnly.Clear();

            UpdatePhysBonesFromReference();
            UpdateArmature();
        }
        GUI.enabled = true;

        GUI.enabled = referenceArmature && targetArmature && physBoneParentObject && PhysBoneCollidersParentObject;
        if (GUILayout.Button("Extract Phys Bones"))
        {
            ExtractPhysBones();
        }
        GUI.enabled = true;

        Services.Separator();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        Services.DrawTitle1("Status Messages", 8);


        EditorGUILayout.LabelField($"Found {physBones?.Length} PhysBones and {physBoneColliders?.Length} PhysBoneColliders.");

        EditorGUILayout.LabelField("Null collider references:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (nullColliderStatus.Any())
        {
            foreach (var line in nullColliderStatus)
            {
                EditorGUILayout.LabelField(line);
            }
        }
        else
        {
            EditorGUILayout.LabelField("Ok");
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Extra components on reference armature:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (extraComponentStatus.Any())
        {
            foreach (var line in extraComponentStatus)
            {
                EditorGUILayout.LabelField(line);
            }
        }
        else
        {
            EditorGUILayout.LabelField("Ok");
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("Armature Difference:", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (!referenceOnly.Any() && !targetOnly.Any())
        {
            EditorGUILayout.LabelField("Ok");
        }
        else
        {
            if (referenceOnly.Any())
            {
                Services.LabelBoldColored("Reference Only", Services.ColourTitle2);
                foreach (var ro in referenceOnly)
                {
                    EditorGUILayout.LabelField(ro);
                }
            }
            if (targetOnly.Any())
            {
                Services.LabelBoldColored("Target Only", Services.ColourTitle2);
                foreach (var to in targetOnly)
                {
                    EditorGUILayout.LabelField(to);
                }
            }
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.EndScrollView();

        guiMessage.Draw(10, Repaint);

    }

    private void UpdatePhysBonesFromReference()
    {
        physBones = referenceArmature.GetComponentsInChildren<VRCPhysBone>(true);
        physBoneColliders = referenceArmature.GetComponentsInChildren<VRCPhysBoneCollider>(true);

        foreach (var pb in physBones)
        {
            if (pb.colliders.Any(x => x == null))
            {
                var relative_path = pb.transform.GetRelativePath(referenceArmature.transform);
                nullColliderStatus.Add($"PhysBone '{relative_path}' has null colliders.");
            }
        }
    }

    private void UpdateArmature()
    {
        var referencePaths = referenceArmature.GetComponentsInChildren<Transform>(true)
            .Select(t => t.GetRelativePath(referenceArmature.transform))
            .ToHashSet();
        var targetPaths = targetArmature.GetComponentsInChildren<Transform>(true)
            .Select(t => t.GetRelativePath(targetArmature.transform))
            .ToHashSet();

        foreach (var referenceTransform in referenceArmature.GetComponentsInChildren<Transform>(true))
        {
            var relativePath = referenceTransform.GetRelativePath(referenceArmature.transform);
            if (!targetPaths.Contains(relativePath))
            {
                if (referenceTransform.GetComponent<VRCPhysBone>() != null || referenceTransform.GetComponent<VRCPhysBoneCollider>() != null)
                    continue;

                referenceOnly.Add(relativePath);
            }

            foreach (var component in referenceTransform.GetComponents<Component>())
            {
                if (component is Transform || component is VRCPhysBone || component is VRCPhysBoneCollider)
                    continue;

                var name = component.GetType().Name;
                // skip modular avatar
                if (relativePath == "" && name.StartsWith("ModularAvatar"))
                    continue;

                extraComponentStatus.Add($"Object '{relativePath}' has extra component: <{component.GetType().Name}>.");
            }
        }

        foreach (var targetTransform in targetArmature.GetComponentsInChildren<Transform>(true))
        {
            var relativePath = targetTransform.GetRelativePath(targetArmature.transform);
            if (!referencePaths.Contains(relativePath))
            {
                targetOnly.Add(relativePath);
            }
        }
    }

    private void ExtractPhysBones()
    {
        var errors = new List<string>();
        var results = new List<string>();

        pbcMap.Clear();
        Undo.SetCurrentGroupName("Extract PhysBones");
        int group = Undo.GetCurrentGroup();

        foreach (var pbc in physBoneColliders)
        {
            var go = new GameObject(pbc.name);
            Undo.RegisterCreatedObjectUndo(go, $"Create PhysBoneCollider {pbc.name}");
            go.transform.SetParent(PhysBoneCollidersParentObject.transform, false);
            var _pbc = (VRCPhysBoneCollider)Undo.AddComponent(go, pbc.GetType());
            EditorUtility.CopySerialized(pbc, _pbc);

            Vector3 _position;
            Quaternion _rotation;
            Vector3 _scale;
            Transform _colliderRoot;
            Vector3 _colliderPosition;
            Quaternion _colliderRotation;
            // If rootTransform is not set || set to itself, use the parent transform
            if (pbc.rootTransform == null || pbc.rootTransform == pbc.transform)
            {
                var pbcRoot = pbc.transform.parent.transform;
                _colliderRoot = Services.GetCorrespondingTransformByRelativePath(pbcRoot, referenceArmature.transform, targetArmature.transform);

                if (!Services.IsPhysBoneColliderTransformValueDefault(pbc))
                {
                    errors.Add($"PhysBoneCollider '{pbc.name}' has already set position and rotation offsets. You need to manually setup.");
                    continue;
                }

                if (Services.IsTransformValueDefault(pbc.transform))
                {
                    _position = Vector3.zero;
                    _rotation = Quaternion.identity;
                    _scale = Vector3.one;
                    _colliderPosition = Vector3.zero;
                    _colliderRotation = Quaternion.identity;
                }
                // If pbc does not have a transform set to default values, we need to warn the user
                else
                {
                    errors.Add($"PhysBoneCollider '{pbc.name}' has a transform not set to default values. It is created, but you need to double-check the results.");
                    // Technically, we can pick up the values and set it to the bone collider component's position and rotation.
                    _position = pbc.transform.localPosition;  // although this should not work since we set rootTransform
                    _rotation = pbc.transform.localRotation;  // although this should not work since we set rootTransform
                    _scale = pbc.transform.localScale;
                    // Try setting the same offset but to the collider
                    _colliderPosition = pbc.transform.localPosition;
                    _colliderRotation = pbc.transform.localRotation;
                }

            }
            // If rootTransform is set to any other, we just use it
            else
            {
                var pbcRoot = pbc.rootTransform;
                _colliderRoot = Services.GetCorrespondingTransformByRelativePath(pbcRoot, referenceArmature.transform, targetArmature.transform);

                // Technically, we can just copy the values but since I haven't seen one, please also double-check the results.
                errors.Add($"PhysBoneCollider '{pbc.name}' has a rootTransform set to '{pbcRoot.name}'. It is created, but you need to double-check the results.");
                _position = pbc.transform.localPosition;
                _rotation = pbc.transform.localRotation;
                _scale = pbc.transform.localScale;
                _colliderPosition = pbc.position; // Use the collider's position
                _colliderRotation = pbc.rotation; // Use the collider's rotation
            }

            Undo.RecordObject(_pbc, "Set Collider Root");
            _pbc.name = pbc.name;
            // set transform values
            _pbc.transform.localPosition = _position;
            _pbc.transform.localRotation = _rotation;
            _pbc.transform.localScale = _scale;
            // set collider values
            _pbc.rootTransform = _colliderRoot;
            _pbc.position = _colliderPosition;
            _pbc.rotation = _colliderRotation;

            results.Add($"PhysBoneCollider: '{pbc.transform.GetRelativePath(referenceArmature.transform)}' => '{_pbc.name}'");

            pbcMap[pbc] = _pbc;
        }

        foreach (var pb in physBones)
        {
            var go = new GameObject(pb.name);
            Undo.RegisterCreatedObjectUndo(go, $"Create PhysBone {pb.name}");
            go.transform.SetParent(physBoneParentObject.transform, false);
            var _pb = (VRCPhysBone)Undo.AddComponent(go, pb.GetType());
            EditorUtility.CopySerialized(pb, _pb);

            var pb_root = Services.GetPhysBoneRoot(pb);
            var relative_path = pb_root.GetRelativePath(referenceArmature.transform);
            var _pb_root = targetArmature.transform.Find(relative_path);

            Undo.RecordObject(_pb, "Set PhysBone Root");
            _pb.rootTransform = _pb_root;

            results.Add($"PhysBone: '{pb.transform.GetRelativePath(referenceArmature.transform)}' => '{_pb.name}'");

            if (_pb.colliders.Count > 0)
            {
                var newColliderList = new List<VRCPhysBoneColliderBase>();

                foreach (var oldCollider in pb.colliders)
                {
                    if (oldCollider is VRCPhysBoneCollider typedOldCollider && pbcMap.TryGetValue(typedOldCollider, out var newCollider))
                    {
                        results.Add($"- with collider: '{oldCollider.transform.GetRelativePath(referenceArmature.transform)}' mapped to '{newCollider.name}'");
                        newColliderList.Add(newCollider);
                    }
                    else if (oldCollider != null)
                    {
                        errors.Add($"PhysBone '{pb.name}' references a collider '{oldCollider.name}' but is not found under the reference armature. It is not included and you need to manually set it up.");
                    }
                }
                _pb.colliders = newColliderList;
            }
        }
        Undo.CollapseUndoOperations(group);


        if (results.Any())
        {
            Debug.Log("PhysBones extraction completed!\n" + string.Join("\n", results));
        }
        if (errors.Any())
        {
            Debug.LogError("However, during extraction there are errors:\n" + string.Join("\n", errors));
        }

        guiMessage.Show($"Extracted {physBones.Length} PhysBones and {physBoneColliders.Length} PhysBoneColliders. {errors.Count} errors.", 3.0);
    }
}
