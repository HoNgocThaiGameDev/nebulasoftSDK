using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    [PropertyCondition(typeof(ShowIfAttribute))]
    public class ShowIfPropertyDrawCondition : PropertyCondition
    {
        public override bool CanBeDrawn(CustomInspector editor, FieldInfo fieldInfo, object targetObject, List<Type> nestedTypes)
        {
            ShowIfAttribute showIfAttribute = PropertyUtility.GetAttribute<ShowIfAttribute>(fieldInfo);

            foreach (Type type in nestedTypes)
            {
                FieldInfo conditionField = type.GetField(showIfAttribute.ConditionName, ReflectionUtils.FLAGS_INSTANCE);
                if (conditionField != null && conditionField.FieldType == PropertyUtility.TYPE_BOOL)
                {
                    return (bool)conditionField.GetValue(targetObject);
                }

                PropertyInfo propertyInfo = type.GetProperty(showIfAttribute.ConditionName, ReflectionUtils.FLAGS_INSTANCE);
                if (propertyInfo != null && propertyInfo.PropertyType == PropertyUtility.TYPE_BOOL)
                {
                    return (bool)propertyInfo.GetValue(targetObject);
                }

                MethodInfo conditionMethod = type.GetMethod(showIfAttribute.ConditionName, ReflectionUtils.FLAGS_INSTANCE);
                if (conditionMethod != null && conditionMethod.ReturnType == PropertyUtility.TYPE_BOOL && conditionMethod.GetParameters().Length == 0)
                {
                    return (bool)conditionMethod.Invoke(targetObject, null);
                }
            }

            return true;
        }
    }
}