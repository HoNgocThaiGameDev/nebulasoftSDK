using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace NebulaSoft
{
    [PropertyCondition(typeof(HideIfAttribute))]
    public class HideIfPropertyDrawCondition : PropertyCondition
    {
        public override bool CanBeDrawn(CustomInspector editor, FieldInfo fieldInfo, object targetObject, List<Type> nestedTypes)
        {
            HideIfAttribute hideIfAttribute = PropertyUtility.GetAttribute<HideIfAttribute>(fieldInfo);

            foreach (Type type in nestedTypes)
            {
                FieldInfo conditionField = type.GetField(hideIfAttribute.ConditionName, ReflectionUtils.FLAGS_INSTANCE);
                if (conditionField != null && conditionField.FieldType == PropertyUtility.TYPE_BOOL)
                {
                    return !(bool)conditionField.GetValue(targetObject);
                }

                PropertyInfo propertyInfo = type.GetProperty(hideIfAttribute.ConditionName, ReflectionUtils.FLAGS_INSTANCE);
                if (propertyInfo != null && propertyInfo.PropertyType == PropertyUtility.TYPE_BOOL)
                {
                    return !(bool)propertyInfo.GetValue(targetObject);
                }

                MethodInfo conditionMethod = type.GetMethod(hideIfAttribute.ConditionName, ReflectionUtils.FLAGS_INSTANCE);
                if (conditionMethod != null && conditionMethod.ReturnType == PropertyUtility.TYPE_BOOL && conditionMethod.GetParameters().Length == 0)
                {
                    return !(bool)conditionMethod.Invoke(targetObject, null);
                }
            }

            return true;
        }
    }
}