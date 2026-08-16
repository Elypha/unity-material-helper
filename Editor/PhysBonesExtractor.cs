using System.Collections.Generic;
using System.Linq;
using Elypha.Common;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Elypha.UnityMaterialHelper
{
    public class PhysBonesExtractor : EditorWindow
    {
        [MenuItem("Elypha/Phys Bones Extractor", false, 207)]
        public static void ShowWindow()
        {
            var window = GetWindow<PhysBonesExtractor>("Phys Bones Extractor");
            window.minSize = new Vector2(400, 400);
        }

        private GameObject _referenceOutfit;
        private GameObject _referenceArmature;
        private GameObject _targetArmature;
        private GameObject _physBoneParentObject;
        private GameObject _physBoneCollidersParentObject;
        private VRCPhysBone[] _physBones;
        private VRCPhysBoneCollider[] _physBoneColliders;
        private readonly Dictionary<VRCPhysBoneCollider, VRCPhysBoneCollider> _pbcMap = new();

        private readonly List<string> _armatureReferenceOnlyReport = new();
        private readonly List<string> _armatureTargetOnlyReport = new();
        private readonly List<string> _nullColliderReport = new();
        private readonly List<string> _extraComponentReport = new();

        private readonly GuiMessage _guiMessage = new();

        private Vector2 _scrollPosition;

        private void OnGUI()
        {
            _referenceOutfit = (GameObject)EditorGUILayout.ObjectField("Reference Outfit", _referenceOutfit, typeof(GameObject), true);
            _referenceArmature = (GameObject)EditorGUILayout.ObjectField("Reference Armature", _referenceArmature, typeof(GameObject), true);
            _targetArmature = (GameObject)EditorGUILayout.ObjectField("Target Armature", _targetArmature, typeof(GameObject), true);
            _physBoneParentObject = (GameObject)EditorGUILayout.ObjectField("Create PBs under", _physBoneParentObject, typeof(GameObject), true);
            _physBoneCollidersParentObject = (GameObject)EditorGUILayout.ObjectField("Create PB Colliders under", _physBoneCollidersParentObject, typeof(GameObject), true);

            GUI.enabled = _referenceArmature && _targetArmature;
            if (GUILayout.Button("Analyse Armature"))
            {
                _nullColliderReport.Clear();
                _extraComponentReport.Clear();
                _armatureReferenceOnlyReport.Clear();
                _armatureTargetOnlyReport.Clear();

                FetchArmatureData();
            }
            GUI.enabled = true;

            GUI.enabled = _referenceArmature && _targetArmature && _physBoneParentObject && _physBoneCollidersParentObject;
            if (GUILayout.Button("Extract Phys Bones"))
            {
                ExtractPhysBones();
            }
            GUI.enabled = true;

            Services.Separator();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            Services.DrawTitle1("Status Messages", 8);


            EditorGUILayout.LabelField($"Found {_physBones?.Length} PhysBones and {_physBoneColliders?.Length} PhysBoneColliders.");

            EditorGUILayout.LabelField("Null collider references:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (_nullColliderReport.Any())
            {
                foreach (var line in _nullColliderReport)
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
            if (_extraComponentReport.Any())
            {
                foreach (var line in _extraComponentReport)
                {
                    EditorGUILayout.LabelField(line);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Ok");
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Armature Reference Only:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (_armatureReferenceOnlyReport.Any())
            {
                foreach (var text in _armatureReferenceOnlyReport)
                {
                    EditorGUILayout.LabelField(text);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Ok");
            }
            EditorGUI.indentLevel--;


            EditorGUILayout.LabelField("Armature Target Only:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (_armatureTargetOnlyReport.Any())
            {
                foreach (var text in _armatureTargetOnlyReport)
                {
                    EditorGUILayout.LabelField(text);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Ok");
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndScrollView();

            _guiMessage.Draw(10, Repaint);

        }

        private void FetchArmatureData()
        {
            _physBones = _referenceOutfit.GetComponentsInChildren<VRCPhysBone>(true);
            _physBoneColliders = _referenceOutfit.GetComponentsInChildren<VRCPhysBoneCollider>(true);

            foreach (var pb in _physBones)
            {
                if (pb.colliders.Any(x => x == null))
                {
                    var relative_path = pb.transform.GetRelativePath(_referenceOutfit.transform);
                    _nullColliderReport.Add($"PhysBone '{relative_path}' has null colliders.");
                }
            }

            var referenceArmatureTransforms = _referenceArmature.GetComponentsInChildren<Transform>(true);
            var referenceArmatureRelPaths = referenceArmatureTransforms
                .Select(t => t.GetRelativePath(_referenceArmature.transform))
                .ToHashSet();

            var targetArmatureTransforms = _targetArmature.GetComponentsInChildren<Transform>(true);
            var targetArmatureRelPaths = targetArmatureTransforms
                .Select(t => t.GetRelativePath(_targetArmature.transform))
                .ToHashSet();

            foreach (var transform in referenceArmatureTransforms)
            {
                var path = transform.GetRelativePath(_referenceArmature.transform);
                if (!targetArmatureRelPaths.Contains(path))
                {
                    var numPBs = transform.GetComponents<VRCPhysBone>().Length;
                    var numPBCs = transform.GetComponents<VRCPhysBoneCollider>().Length;
                    _armatureReferenceOnlyReport.Add($"{path}" + (numPBs > 0 ? $",PB={numPBs}" : "") + (numPBCs > 0 ? $",PBC={numPBCs}" : ""));
                }

                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component is Transform || component is VRCPhysBone || component is VRCPhysBoneCollider)
                        continue;

                    var name = component.GetType().Name;
                    // skip modular avatar on referenceArmature
                    if (path == "" && name.StartsWith("ModularAvatar"))
                        continue;

                    _extraComponentReport.Add($"{path}: <{name}>.");
                }
            }

            foreach (var transform in targetArmatureTransforms)
            {
                var path = transform.GetRelativePath(_targetArmature.transform);
                if (!referenceArmatureRelPaths.Contains(path))
                {
                    var numPBs = transform.GetComponents<VRCPhysBone>().Length;
                    var numPBCs = transform.GetComponents<VRCPhysBoneCollider>().Length;
                    _armatureTargetOnlyReport.Add($"{path}" + (numPBs > 0 ? $",PB={numPBs}" : "") + (numPBCs > 0 ? $",PBC={numPBCs}" : ""));
                }
            }
        }

        private void ExtractPhysBones()
        {
            var errors = new List<string>();
            var results = new List<string>();

            _pbcMap.Clear();
            Undo.SetCurrentGroupName("Extract PhysBones");
            int group = Undo.GetCurrentGroup();

            foreach (var pbc in _physBoneColliders)
            {
                var go = new GameObject(pbc.name);
                Undo.RegisterCreatedObjectUndo(go, $"Create PhysBoneCollider {pbc.name}");
                go.transform.SetParent(_physBoneCollidersParentObject.transform, false);
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
                    _colliderRoot = Services.GetCorrespondingTransformByRelativePath(pbcRoot, _referenceArmature.transform, _targetArmature.transform);

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
                    _colliderRoot = Services.GetCorrespondingTransformByRelativePath(pbcRoot, _referenceArmature.transform, _targetArmature.transform);

                    // Technically, we can just copy the values but since I haven't seen one, please also double-check the results.
                    errors.Add($"PhysBoneCollider '{pbc.name}' has a rootTransform set to '{pbcRoot.name}' -> '{(_colliderRoot == null ? "null" : _colliderRoot.name)}', you need to double-check the results.");
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

                results.Add($"PhysBoneCollider: '{pbc.transform.GetRelativePath(_referenceArmature.transform)}' => '{_pbc.name}'");

                _pbcMap[pbc] = _pbc;
            }

            foreach (var pb in _physBones)
            {
                var go = new GameObject(pb.name);
                Undo.RegisterCreatedObjectUndo(go, $"Create PhysBone {pb.name}");
                go.transform.SetParent(_physBoneParentObject.transform, false);
                var _pb = (VRCPhysBone)Undo.AddComponent(go, pb.GetType());
                EditorUtility.CopySerialized(pb, _pb);

                var pb_root = Services.GetPhysBoneRoot(pb);
                var relative_path = pb_root.GetRelativePath(_referenceArmature.transform);
                var _pb_root = _targetArmature.transform.Find(relative_path);

                Undo.RecordObject(_pb, "Set PhysBone Root");
                _pb.rootTransform = _pb_root;

                results.Add($"PhysBone: '{pb.transform.GetRelativePath(_referenceArmature.transform)}' => '{_pb.name}'");

                if (_pb.colliders.Count > 0)
                {
                    var newColliderList = new List<VRCPhysBoneColliderBase>();

                    foreach (var oldCollider in pb.colliders)
                    {
                        if (oldCollider is VRCPhysBoneCollider typedOldCollider && _pbcMap.TryGetValue(typedOldCollider, out var newCollider))
                        {
                            results.Add($"- with collider: '{oldCollider.transform.GetRelativePath(_referenceArmature.transform)}' mapped to '{newCollider.name}'");
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

            _guiMessage.Show($"Extracted {_physBones.Length} PhysBones and {_physBoneColliders.Length} PhysBoneColliders. {errors.Count} errors.", 3.0);
        }
    }
}
