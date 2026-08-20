using UnityEngine;
using UnityEditor;

namespace NebulaSoft
{
    [CustomPropertyDrawer(typeof(SliderAttribute))]
    public class SliderPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SliderAttribute sliderAttribute = (SliderAttribute)attribute;

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                EditorGUI.IntSlider(position, property, (int)sliderAttribute.MinValue, (int)sliderAttribute.MaxValue, label);
            }
            else if (property.propertyType == SerializedPropertyType.Float)
            {
                EditorGUI.Slider(position, property, sliderAttribute.MinValue, sliderAttribute.MaxValue, label);
            }
            else
            {
                string warning = "[SliderAttribute] can be used only on int or float fields";

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