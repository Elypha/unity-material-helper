using UnityEngine;
using UnityEditor;
using System;


namespace Elypha.Common
{
    public class GuiMessage
    {
        private string Message = "";
        private double MessageExpireTime = 0.0;
        private static readonly Color MessageColor = new (0.65f, 0.95f, 0.88f);

        public bool IsActive => !string.IsNullOrEmpty(Message) && EditorApplication.timeSinceStartup < MessageExpireTime;

        public void Show(string message, double duration = 3.0)
        {
            Message = message;
            MessageExpireTime = EditorApplication.timeSinceStartup + duration;
        }

        public void Clear()
        {
            Message = "";
            MessageExpireTime = 0.0;
        }

        public void Draw(float spacePixels = 10, Action callback = null)
        {
            if (IsActive)
            {
                GUILayout.Space(spacePixels);
                GUILayout.Label(Message, new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = MessageColor }
                });
                callback?.Invoke();
            }
        }

    }
}
