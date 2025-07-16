using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Elypha.Common
{
    public static class Utils
    {
        public static readonly GUIStyle MonoStyle = GetMonoStyle();

        public static string ReplaceLastOccurrence(string source, string find, string replace)
        {
            int place = source.LastIndexOf(find);

            if (place == -1) return source;

            string result = source.Remove(place, find.Length).Insert(place, replace);
            return result;
        }

        public static GUIStyle GetMonoStyle()
        {
            var monoStyle = new GUIStyle()
            {
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            var robotoFont = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;
            if (robotoFont != null)
                monoStyle.font = robotoFont;
            else
                Debug.LogWarning("RobotoMono-Regular.ttf not found. Using default font style.");

            return monoStyle;
        }
    }

}