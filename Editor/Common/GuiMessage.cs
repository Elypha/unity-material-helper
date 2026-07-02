using UnityEngine;
using UnityEditor;
using System;


namespace Elypha.Common
{
    public class GuiMessage
    {
        private string _message = "";
        private double _messageExpireTime = 0.0;
        private static readonly Color MessageColor = new (0.65f, 0.95f, 0.88f);

        public bool IsActive => !string.IsNullOrEmpty(_message) && EditorApplication.timeSinceStartup < _messageExpireTime;

        public void Show(string message, double duration = 3.0)
        {
            _message = message;
            _messageExpireTime = EditorApplication.timeSinceStartup + duration;
        }

        public void Clear()
        {
            _message = "";
            _messageExpireTime = 0.0;
        }

        public void Draw(float spacePixels = 10, Action callback = null)
        {
            if (IsActive)
            {
                GUILayout.Space(spacePixels);
                GUILayout.Label(_message, new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = MessageColor }
                });
                callback?.Invoke();
            }
        }

    }
}
