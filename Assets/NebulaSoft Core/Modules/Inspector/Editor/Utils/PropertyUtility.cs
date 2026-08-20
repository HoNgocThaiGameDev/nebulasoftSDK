using UnityEditor;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace NebulaSoft
{
    public static class PropertyUtility
    {
        public static readonly Type TYPE_BOOL = typeof(bool);

        public static GUIRenderer[] GroupRenderers(CustomInspector editor, IEnumerable<GUIRenderer> baseRenderers)
        {
            List<GroupGUIRenderer> groupGUIRenderers = new List<GroupGUIRenderer>();

            IGrouping<GroupAttribute, GUIRenderer>[] groupRenderers = baseRenderers.Where(x => x.GroupAttribute != null).GroupBy(x => x.GroupAttribute).ToArray();
            foreach (IGrouping<GroupAttribute, GUIRenderer> group in groupRenderers)
            {
                groupGUIRenderers.Add(new GroupGUIRenderer(editor, group.Key, group.ToList()));
            }

            Dictionary<string, GroupGUIRenderer> groupsDictionary = groupGUIRenderers.ToDictionary(g => g.GroupID, g => g);

            for (int i = 0; i < groupGUIRenderers.Count; i++)
            {
                GroupGUIRenderer group = groupGUIRenderers[i];
                if (!string.IsNullOrEmpty(group.ParentPath))
                {
                    if (groupsDictionary.TryGetValue(group.ParentPath, out var parentGroup))
                    {
                        parentGroup.AddRenderer(group);
                        groupGUIRenderers.Remove(group);

                        i--;
                    }
                }
            }

            GUIRenderer[] groupedRenderers = groupGUIRenderers.Concat(baseRenderers.Where(x => x.GroupAttribute == null)).OrderBy(x => x.Order).ToArray();

            groupGUIRenderers = null; 
            groupsDictionary = null;

            return groupedRenderers;
        }

        public static T GetAttribute<T>(FieldInfo fieldInfo) where T : Attribute
        {
            if (fieldInfo != null)
                return fieldInfo.GetCustomAttribute<T>();

            return null;
        }

        public static T GetAttribute<T>(PropertyInfo propertyInfo) where T : Attribute
        {
            if (propertyInfo != null)
                return propertyInfo.GetCustomAttribute<T>();

            return null;
        }

        public static T GetAttribute<T>(SerializedProperty property) where T : Attribute
        {
            T[] attributes = GetAttributes<T>(property);
            if (attributes != null && attributes.Length > 0)
                return attributes[0];

            return null;
        }

        public static T[] GetAttributes<T>(SerializedProperty property) where T : Attribute
        {
            Type targetType = property.serializedObject.targetObject.GetType();

            foreach (Type type in GetClassNestedTypes(targetType))
            {
                FieldInfo fieldInfo = type.GetField(property.name, ReflectionUtils.FLAGS_INSTANCE);
                if (fieldInfo != null)
                {
                    return (T[])Attribute.GetCustomAttributes(fieldInfo, typeof(T));
                }
            }

            return null;
        }

        public static UnityEngine.Object GetTargetObject(SerializedProperty property)
        {
            return property.serializedObject.targetObject;
        }

        public static string GetSubstringBeforeLastSlash(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            int lastSlashIndex = input.LastIndexOf('/');

            if (lastSlashIndex == -1 || lastSlashIndex == input.Length - 1)
            {
                return string.Empty;
            }

            return input.Substring(0, lastSlashIndex);
        }

        public static List<Type> GetClassNestedTypes(Type type)
        {
            List<Type> typesList = new List<Type> { type };

            Type lastAddedType = type;

            while (lastAddedType.BaseType != null)
            {
                lastAddedType = lastAddedType.BaseType;

                typesList.Add(lastAddedType);
            }

            return typesList;
        }

        public static IEnumerable<SerializedProperty> GetPropertiesByGroup(this SerializedObject serializedObject, string groupName)
        {
            Type targetType = serializedObject.targetObject.GetType();

            IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE).Where(x => x.CompareGroupID(groupName));
            foreach (var field in fieldInfos)
            {
                SerializedProperty serializedProperty = serializedObject.FindProperty(field.Name);
                if (serializedProperty != null)
                    yield return serializedProperty;
            }
        }

        public static IEnumerable<SerializedProperty> GetUngroupProperties(this SerializedObject serializedObject)
        {
            Type targetType = serializedObject.targetObject.GetType();

            IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE).Where(x => x.GetCustomAttribute<GroupAttribute>() == null && x.GetCustomAttribute<HideAttribute>() == null);
            foreach (var field in fieldInfos)
            {
                SerializedProperty serializedProperty = serializedObject.FindProperty(field.Name);
                if (serializedProperty != null)
                    yield return serializedProperty;
            }
        }

        public static IEnumerable<SerializedProperty> GetPropertiesByGroup(this SerializedProperty serializedProperty, string groupName)
        {
            if (serializedProperty.propertyType == SerializedPropertyType.Generic)
            {
                Type targetType = serializedProperty.boxedValue.GetType();
                IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE).Where(x => x.CompareGroupID(groupName));
                foreach (var field in fieldInfos)
                {
                    SerializedProperty subProperty = serializedProperty.FindPropertyRelative(field.Name);
                    if (subProperty != null)
                        yield return subProperty;
                }
            }
        }

        public static IEnumerable<SerializedProperty> GetUngroupProperties(this SerializedProperty serializedProperty)
        {
            if (serializedProperty.propertyType == SerializedPropertyType.Generic)
            {
                Type targetType = serializedProperty.boxedValue.GetType();
                IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE).Where(x => x.GetCustomAttribute<GroupAttribute>() == null);
                foreach (var field in fieldInfos)
                {
                    SerializedProperty subProperty = serializedProperty.FindPropertyRelative(field.Name);
                    if (subProperty != null)
                        yield return subProperty;
                }
            }
        }

        public static bool CompareGroupID(this FieldInfo fieldInfo, string groupID)
        {
            Attribute attribute = fieldInfo.GetCustomAttribute(typeof(GroupAttribute), false);
            if (attribute != null)
            {
                GroupAttribute groupAttribute = (GroupAttribute)attribute;
                if (groupAttribute != null)
                {
                    return groupAttribute.ID == groupID;
                }
            }

            return false;
        }

        public static IEnumerable<SerializedProperty> GetPropertiesByGroup(this CustomInspector editor, string groupName)
        {
            List<Type> nestedTypes = editor.NestedClassTypes;
            foreach (Type type in nestedTypes)
            {
                IEnumerable<FieldInfo> fieldInfos = type.GetFields(ReflectionUtils.FLAGS_INSTANCE).Where(x => x.CompareGroupID(groupName));
                foreach (FieldInfo field in fieldInfos)
                {
                    SerializedProperty serializedProperty = editor.serializedObject.FindProperty(field.Name);
                    if (serializedProperty != null)
                        yield return serializedProperty;
                }
            }
        }

        public static IEnumerable<SerializedProperty> GetUngroupProperties(this CustomInspector editor)
        {
            List<Type> nestedTypes = editor.NestedClassTypes;
            foreach (Type type in nestedTypes)
            {
                IEnumerable<FieldInfo> fieldInfos = type.GetFields(ReflectionUtils.FLAGS_INSTANCE).Where(x => x.GetCustomAttribute<GroupAttribute>() == null);
                foreach (FieldInfo field in fieldInfos)
                {
                    SerializedProperty serializedProperty = editor.serializedObject.FindProperty(field.Name);
                    if (serializedProperty != null)
                        yield return serializedProperty;
                }
            }
        }

        public static IEnumerable<SerializedProperty> GetPropertiesByGroup(this SerializedObject serializedObject, List<Type> nestedTypes, string groupName)
        {
            foreach (Type type in nestedTypes)
            {
                IEnumerable<FieldInfo> fieldInfos = type.GetFields(ReflectionUtils.FLAGS_INSTANCE).Where(x => x.CompareGroupID(groupName));
                foreach (FieldInfo field in fieldInfos)
                {
                    SerializedProperty serializedProperty = serializedObject.FindProperty(field.Name);
                    if (serializedProperty != null)
                        yield return serializedProperty;
                }
            }
        }

        public static IEnumerable<SerializedProperty> GetUngroupProperties(this SerializedObject serializedObject, List<Type> nestedTypes)
        {
            foreach (Type type in nestedTypes)
            {
                IEnumerable<FieldInfo> fieldInfos = type.GetFields(ReflectionUtils.FLAGS_INSTANCE).Where(x => x.GetCustomAttribute<GroupAttribute>() == null);
                foreach (FieldInfo field in fieldInfos)
                {
                    SerializedProperty serializedProperty = serializedObject.FindProperty(field.Name);
                    if (serializedProperty != null)
                        yield return serializedProperty;
                }
            }
        }

        public static bool IsAnyObjectVisible(this IEnumerable<GUIRenderer> renderers)
        {
            foreach (GUIRenderer renderer in renderers)
            {
                if (renderer.IsVisible)
                    return true;
            }

            return false;
        }
    }
}