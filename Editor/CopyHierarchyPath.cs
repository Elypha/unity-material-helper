using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CopyHierarchyPath {
    private const string MenuPath = "GameObject/Copy Hierarchy Path";

    [MenuItem(MenuPath, false, 0)]
    private static void Copy(MenuCommand command) {
        var gameObject = command.context as GameObject ?? Selection.activeGameObject;
        if (gameObject == null || !gameObject.scene.IsValid()) {
            return;
        }

        var names = new List<string>();
        for (var current = gameObject.transform; current != null; current = current.parent) {
            names.Add(current.name);
        }
        names.Reverse();

        var hierarchyPath = string.Join("/", names);
        EditorGUIUtility.systemCopyBuffer = $"{gameObject.scene.name}.unity {hierarchyPath}";
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateCopy() {
        var gameObject = Selection.activeGameObject;
        return gameObject != null && gameObject.scene.IsValid();
    }
}
