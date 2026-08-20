using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    public sealed class HelpButtonGUIRenderer : GUIRenderer
    {
        private HelpButtonAttribute helpButtonAttribute;

        private GUIContent buttonContent;

        public HelpButtonGUIRenderer(HelpButtonAttribute helpButtonAttribute)
        {
            this.helpButtonAttribute = helpButtonAttribute;

            Order = GUIRenderer.ORDER_HELP_BUTTON;

            buttonContent = new GUIContent(helpButtonAttribute.Name, EditorCustomStyles.GetIcon("icon_link"), helpButtonAttribute.URL);
        }

        public override void OnGUI()
        {
            if (GUILayout.Button(buttonContent, EditorCustomStyles.button, GUILayout.Height(22)))
            {
                Application.OpenURL(helpButtonAttribute.URL);
            }
        }
    }
}