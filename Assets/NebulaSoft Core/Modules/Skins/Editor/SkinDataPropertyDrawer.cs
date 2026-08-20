using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    [CustomPropertyDrawer(typeof(AbstractSkinData), true)]
    public class SkinDataPropertyDrawer : PropertyDrawer, System.IDisposable
    {
        private Dictionary<int, PropertyData> propertiesData = new Dictionary<int, PropertyData>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PropertyData data = GetPropertyData(property);

            EditorGUI.BeginProperty(position, label, property);

            if (data.PreviewProperty != null)
            {
                position.width -= 60;
            }

            Rect propertyPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            foreach(var subProperty in data.Properties)
            {
                EditorGUI.PropertyField(propertyPosition, subProperty, true);

                propertyPosition.y += EditorGUI.GetPropertyHeight(subProperty, GUIContent.none, true) + 2;
            }

            if(data.PreviewProperty != null)
            {
                Rect boxRect = new Rect(position.x + propertyPosition.width + 2, position.y, 58, 58);
                GUI.Box(boxRect, GUIContent.none);

                Object prefabObject = data.PreviewProperty.objectReferenceValue;
                if (prefabObject != null)
                {
                    Texture2D previewTexture = AssetPreview.GetAssetPreview(prefabObject);
                    if (previewTexture != null)
                    {
                        GUI.DrawTexture(new Rect(boxRect.x + 2, boxRect.y + 2, 55, 55), previewTexture);
                    }
                }
                else
                {
                    GUI.DrawTexture(new Rect(boxRect.x + 2, boxRect.y + 2, 55, 55), EditorCustomStyles.GetMissingIcon());
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            PropertyData data = GetPropertyData(property);

            float height = 0;

            int propertiesCount = data.Properties.Count;
            if(propertiesCount > 0)
            {
                foreach(SerializedProperty subProperty in data.Properties)
                {
                    height += EditorGUI.GetPropertyHeight(subProperty, GUIContent.none, true);
                }

                height += (propertiesCount - 1) * 2;
            }

            return Mathf.Clamp(height, 58, float.MaxValue);
        }

        private PropertyData GetPropertyData(SerializedProperty property)
        {
            PropertyData propertyData;

            int hash = property.propertyPath.GetHashCode();
            if (!propertiesData.TryGetValue(hash, out propertyData))
            {
                propertyData = new PropertyData(property);

                propertiesData.Add(hash, propertyData);
            }

            return propertyData;
        }

        public void Dispose()
        {
            propertiesData.Clear();
        }

        private class PropertyData
        {
            public List<SerializedProperty> Properties { get; private set; }
            public SerializedProperty PreviewProperty { get; private set; }

            public PropertyData(SerializedProperty property)
            {
                PreviewProperty = null;
                Properties = new List<SerializedProperty>
                {
                    property.FindPropertyRelative("id")
                };

                System.Type targetType = property.boxedValue.GetType();
                IEnumerable<FieldInfo> fieldInfos = targetType.GetFields(ReflectionUtils.FLAGS_INSTANCE);

                foreach (var field in fieldInfos)
                {
                    SerializedProperty subProperty = property.FindPropertyRelative(field.Name);
                    if (subProperty != null)
                    {
                        Properties.Add(subProperty);

                        if (field.GetCustomAttribute<SkinPreviewAttribute>() != null)
                        {
                            PreviewProperty = subProperty;
                        }
                    }
                }
            }
        }
    }
}
