using UnityEngine;
using System.Collections.Generic;

public static class TransformExtensions
{
    public static string GetPath(this Transform current)
    {
        if (current == null) return "";

        var pathStack = new Stack<string>();
        var pointer = current;
        while (pointer != null)
        {
            pathStack.Push(pointer.name);
            pointer = pointer.parent;
        }
        return string.Join("/", pathStack);
    }

    public static string GetRelativePath(this Transform current, Transform relativeTo)
    {
        if (current == relativeTo) return "";

        var pathStack = new Stack<string>();
        var pointer = current;

        while (pointer != null && pointer != relativeTo)
        {
            pathStack.Push(pointer.name);
            pointer = pointer.parent;
        }

        return string.Join("/", pathStack);
    }
}
