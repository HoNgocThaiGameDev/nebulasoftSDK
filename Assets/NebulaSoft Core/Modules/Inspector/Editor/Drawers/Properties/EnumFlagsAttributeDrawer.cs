using UnityEngine;
using UnityEditor;

namespace NebulaSoft
{
    [CustomPropertyDrawer(typeof(EnumFlagsAttribute))]
    public class EnumFlagsAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();

            int flagsValue = EditorGUI.MaskField(position, property.displayName, property.intValue, property.enumNames);

            if (EditorGUI.EndChangeCheck())
                property.intValue = flagsValue;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return 18;
        }
    }
}