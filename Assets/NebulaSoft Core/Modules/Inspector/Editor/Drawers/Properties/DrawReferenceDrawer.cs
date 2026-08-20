using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace NebulaSoft
{
    [CustomPropertyDrawer(typeof(DrawReferenceAttribute), true)]
    public class DrawReferenceDrawer : UnityEditor.PropertyDrawer
    {
        private readonly float SPACE = EditorGUIUtility.standardVerticalSpacing * 2;

        private Dictionary<SerializedProperty, SerializedObject> linkedSerializedObjects = new Dictionary<SerializedProperty, SerializedObject>();

        private SerializedObject GetSerializedObject(SerializedProperty property)
        {
            if (property.objectReferenceValue == null)
                return null;

            if(linkedSerializedObjects.ContainsKey(property))
            {
                return linkedSerializedObjects[property];
            }

            SerializedObject serializedObject = new SerializedObject(property.objectReferenceValue);

            linkedSerializedObjects.Add(property, serializedObject);

            return serializedObject;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedObject serializedObject = GetSerializedObject(property);

            int indentLevel = EditorGUI.indentLevel + 1;

            GUI.Box(new Rect(0, position.y, Screen.width, position.height), GUIContent.none);

            position.y += EditorGUIUtility.standardVerticalSpacing;
            position.height = 16;

            if (serializedObject != null)
            {
                property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height), property.isExpanded, label, true);
                EditorGUI.PropertyField(new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, position.height), property, GUIContent.none);

                position.y += 20;

                EditorGUI.indentLevel = indentLevel;

                if (property.isExpanded)
                {
                    serializedObject.Update();

                    SerializedProperty prop = serializedObject.GetIterator();
                    prop.NextVisible(true);

                    while (prop.NextVisible(false))
                    {
                        EditorGUI.indentLevel = prop.depth + 1;

                        position.height = EditorGUI.GetPropertyHeight(prop);
                        EditorGUI.PropertyField(position, prop, prop.isExpanded);
                        position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
                    }

                    if (GUI.changed)
                        serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                EditorGUI.PropertyField(position, property);
            }

            EditorGUI.indentLevel = indentLevel - 1;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                return base.GetPropertyHeight(property, label) + SPACE;
            }

            SerializedObject serializedObject = GetSerializedObject(property);

            float height = base.GetPropertyHeight(property, label) + SPACE;
            if (serializedObject != null)
            {
                if (property.isExpanded)
                {
                    var prop = serializedObject.GetIterator();
                    prop.NextVisible(true);

                    while (prop.NextVisible(false))
                    {
                        height += EditorGUI.GetPropertyHeight(prop) + EditorGUIUtility.standardVerticalSpacing;
                    }

                    height += EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height;
        }
    }
}
