using System;
using System.Reflection;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NebulaSoft
{
    public sealed class SerializedArrayGUIRenderer : SerializedPropertyGUIRenderer
    {
        private bool containsUniqueID;
        private FieldInfo[] uniqueIDFields;

        public SerializedArrayGUIRenderer(CustomInspector editor, SerializedProperty serializedProperty, FieldInfo fieldInfo, object targetObject, List<Type> nestedTypes) : base(editor, serializedProperty, fieldInfo, targetObject, nestedTypes)
        {
            Type elementType = fieldInfo.FieldType.GetElementType();

            if (elementType != null)
            {
                if(elementType.IsSubclassOf(typeof(ScriptableObject)) || elementType.IsSubclassOf(typeof(MonoBehaviour)))
                {
                    containsUniqueID = false;
                }
                else
                {
                    uniqueIDFields = ReflectionUtils.GetParentTypes(elementType).SelectMany(x => x.GetFields(ReflectionUtils.FLAGS_INSTANCE)).Where(f => Attribute.GetCustomAttribute(f, typeof(UniqueIDAttribute)) != null).ToArray();
                    if (!uniqueIDFields.IsNullOrEmpty())
                    {
                        containsUniqueID = true;
                    }
                }
            }
        }

        public override void OnGUI()
        {
            if(containsUniqueID)
            {
                int tempArraySize = serializedProperty.arraySize;

                base.OnGUI();

                if (serializedProperty.arraySize > tempArraySize)
                {
                    SerializedProperty addedElement = serializedProperty.GetArrayElementAtIndex(serializedProperty.arraySize - 1);

                    foreach (FieldInfo field in uniqueIDFields)
                    {
                        addedElement.FindPropertyRelative(field.Name).stringValue = "";
                    }
                }

                return;
            }

            base.OnGUI();
        }
    }
}