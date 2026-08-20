using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    [CustomPropertyDrawer(typeof(ToggleAttribute))]
    public class TogglePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if(property.propertyType == SerializedPropertyType.Boolean)
            {
                EditorGUI.PrefixLabel(position, label);

                property.boolValue = GUI.Toggle(new Rect(position.x + EditorGUIUtility.labelWidth, position.y, 35, position.height), property.boolValue, label, EditorCustomStyles.Skin.toggle);
            }
            else
            {
                string warning = "[ToggleAttribute] can be used only on bool fields";

                EditorGUI.HelpBox(position, warning, MessageType.Warning);

                Debug.LogWarning(warning);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
