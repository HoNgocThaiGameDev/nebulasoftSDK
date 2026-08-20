using UnityEditor;

namespace NebulaSoft
{
    public abstract class GUIRenderer
    {
        public const int ORDER_NON_SERIALIZED = -1;
        public const int ORDER_METHOD = 10000;
        public const int ORDER_HELP_BUTTON = 11000;

        public int Order { get; protected set; }
        public bool IsVisible { get; protected set; } = true;

        public TabAttribute TabAttribute { get; protected set; }
        public GroupAttribute GroupAttribute { get; protected set; }

        public abstract void OnGUI();

        public virtual void OnGUIChanged() { }

        protected void DrawInfoBox(string infoText, InfoBoxType infoBoxType)
        {
            switch (infoBoxType)
            {
                case InfoBoxType.Normal:
                    EditorGUILayout.HelpBox(infoText, MessageType.Info);
                    break;

                case InfoBoxType.Warning:
                    EditorGUILayout.HelpBox(infoText, MessageType.Warning);
                    break;

                case InfoBoxType.Error:
                    EditorGUILayout.HelpBox(infoText, MessageType.Error);
                    break;
            }
        }
    }
}