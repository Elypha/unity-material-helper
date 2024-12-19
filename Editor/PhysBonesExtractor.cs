using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System;
using Elypha.Helper;
using Elypha.I18N;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Elypha
{
    public class PhysBonesExtractor : EditorWindow
    {
        [MenuItem("Tools/Elypha Toolkit/Phys Bones Extractor")]
        public static void ShowWindow()
        {
            var window = GetWindow<PhysBonesExtractor>("Phys Bones Extractor");
            window.minSize = new Vector2(400, 400);

        }

        private GameObject sourceArmature;
        private GameObject targetArmature;
        private GameObject physBoneParent;
        private GameObject PhysBoneCollidersParent;
        private VRCPhysBone[] physBones;
        private VRCPhysBoneCollider[] physBoneColliders;
        private Dictionary<VRCPhysBoneCollider, VRCPhysBoneCollider> pbcMap = new();
        private List<string> leftOnly;
        private List<string> rightOnly;
        private List<string> armatureStatus;

        private void OnGUI()
        {
            sourceArmature = (GameObject)EditorGUILayout.ObjectField("Source Armature", sourceArmature, typeof(GameObject), true);
            targetArmature = (GameObject)EditorGUILayout.ObjectField("Target Armature", targetArmature, typeof(GameObject), true);
            physBoneParent = (GameObject)EditorGUILayout.ObjectField("Phys Bone Parent", physBoneParent, typeof(GameObject), true);
            PhysBoneCollidersParent = (GameObject)EditorGUILayout.ObjectField("Phys Bone Colliders Parent", PhysBoneCollidersParent, typeof(GameObject), true);

            if (GUILayout.Button("Get Armature Data"))
            {
                armatureStatus.Clear();
                UpdatePhysBones();
                UpdateArmature();
            }

            if (GUILayout.Button("Extract Phys Bones"))
            {
                ExtractPhysBones();
            }

            UnityHelper.LabelBoldColored("# Status", UnityHelper.ColourTitle);

            if (armatureStatus != null && armatureStatus.Count > 0)
            {
                foreach (var status in armatureStatus)
                {
                    EditorGUILayout.LabelField(status);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No status");
            }

            UnityHelper.LabelBoldColored("# Diff", UnityHelper.ColourTitle);
            // left only
            if (leftOnly != null && leftOnly.Count > 0)
            {
                UnityHelper.LabelBoldColored("Left Only", UnityHelper.ColourBold);
                foreach (var lo in leftOnly)
                {
                    EditorGUILayout.LabelField(lo);
                }
            }
            // right only
            if (rightOnly != null && rightOnly.Count > 0)
            {
                UnityHelper.LabelBoldColored("Right Only", UnityHelper.ColourBold);
                foreach (var ro in rightOnly)
                {
                    EditorGUILayout.LabelField(ro);
                }
            }
            // none
            if ((leftOnly == null || leftOnly.Count == 0) && (rightOnly == null || rightOnly.Count == 0))
            {
                EditorGUILayout.LabelField("No difference");
            }
        }



        private void UpdatePhysBones()
        {
            physBones = sourceArmature.GetComponentsInChildren<VRCPhysBone>(true);
            physBoneColliders = sourceArmature.GetComponentsInChildren<VRCPhysBoneCollider>(true);

            armatureStatus.Add($"Found {physBones.Length} PhysBones and {physBoneColliders.Length} PhysBoneColliders");

            foreach (var pb in physBones)
            {
                // if has colliders and if any is null
                if (pb.colliders.Count > 0 && pb.colliders.Any(x => x == null))
                {
                    var relative_path = UnityHelper.GetRelativePathInHierarchy(sourceArmature.transform, pb.transform);
                    armatureStatus.Add($"PhysBone {relative_path} has null colliders");
                }
            }
        }

        private void UpdateArmature()
        {
            var sourceBones = sourceArmature.GetComponentsInChildren<Transform>(true);
            var targetBones = targetArmature.GetComponentsInChildren<Transform>(true);

            leftOnly.Clear();
            rightOnly.Clear();

            foreach (var sb in sourceBones)
            {
                var relative_path = UnityHelper.GetRelativePathInHierarchy(sourceArmature.transform, sb);
                var tb = targetBones.FirstOrDefault(x => x.name == sb.name);
                if (tb == null)
                {
                    leftOnly.Add($".{relative_path}");
                }
            }

            foreach (var tb in targetBones)
            {
                var relative_path = UnityHelper.GetRelativePathInHierarchy(targetArmature.transform, tb);
                var sb = sourceBones.FirstOrDefault(x => x.name == tb.name);
                if (sb == null)
                {
                    rightOnly.Add($".{relative_path}");
                }
            }

            // find all gameobjects with components that are not physbones or physbonecolliders
            foreach (var go in sourceArmature.GetComponentsInChildren<Transform>(true))
            {
                var components = go.GetComponents<Component>();
                foreach (var c in components)
                {
                    if (c is Transform || c is VRCPhysBone || c is VRCPhysBoneCollider) continue;

                    var relative_path = UnityHelper.GetRelativePathInHierarchy(sourceArmature.transform, go);
                    armatureStatus.Add($"{relative_path} <{c.GetType().Name}>");
                }
            }


        }

        private void ExtractPhysBones()
        {
            foreach (var pbc in physBoneColliders)
            {
                var _pbc = Instantiate(pbc, PhysBoneCollidersParent.transform);

                var pbc_root = UnityHelper.GetPhysBoneColliderRoot(pbc);
                var relative_path = UnityHelper.GetRelativePathInHierarchy(sourceArmature.transform, pbc_root);
                var _pbc_root = targetArmature.transform.Find(relative_path);
                if (_pbc_root == null)
                {
                    Debug.LogError($"PhysBoneCollider: No matching bone for {relative_path}");
                    continue;
                }
                _pbc.rootTransform = _pbc_root;

                _pbc.name = pbc.name;
                _pbc.transform.localPosition = pbc.transform.localPosition;
                _pbc.transform.localRotation = pbc.transform.localRotation;
                _pbc.transform.localScale = pbc.transform.localScale;

                pbcMap[pbc] = _pbc;
            }

            foreach (var pb in physBones)
            {
                var _pb = Instantiate(pb, physBoneParent.transform);

                // remove all children
                while (_pb.transform.childCount > 0)
                {
                    DestroyImmediate(_pb.transform.GetChild(0).gameObject);
                }

                var pb_root = UnityHelper.GetPhysBoneRoot(pb);
                var relative_path = UnityHelper.GetRelativePathInHierarchy(sourceArmature.transform, pb_root);
                var _pb_root = targetArmature.transform.Find(relative_path);
                if (_pb_root == null)
                {
                    Debug.LogError($"PhysBone: No matching bone for {relative_path}");
                    continue;
                }
                _pb.GetComponent<VRCPhysBone>().rootTransform = _pb_root;

                _pb.name = pb.name;
                _pb.transform.localPosition = Vector3.zero;
                _pb.transform.localRotation = Quaternion.identity;
                _pb.transform.localScale = Vector3.one;


                if (_pb.colliders.Count > 0)
                {
                    // update colliders
                    var colliders = _pb.colliders;
                    for (int i = 0; i < colliders.Count; i++)
                    {
                        var pbc = colliders[i];
                        if (pbcMap.ContainsKey((VRCPhysBoneCollider)pbc))
                        {
                            colliders[i] = pbcMap[(VRCPhysBoneCollider)pbc];
                        }
                    }
                    _pb.colliders = colliders;
                }
            }
        }

    }

}