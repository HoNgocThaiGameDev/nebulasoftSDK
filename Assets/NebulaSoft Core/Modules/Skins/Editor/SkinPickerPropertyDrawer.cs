using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NebulaSoft
{
    [CustomPropertyDrawer(typeof(SkinPickerAttribute))]
    public class SkinPickerPropertyDrawer : PropertyDrawer, System.IDisposable
    {
        private Dictionary<int, PropertyData> propertiesData = new Dictionary<int, PropertyData>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, "Incorect property type!", MessageType.Error);

                EditorGUI.EndProperty();

                return;
            }

            PropertyData data = GetPropertyData(property);

            position.width -= 60;

            EditorGUI.LabelField(position, label);

            position.x += EditorGUIUtility.labelWidth;
            position.width -= EditorGUIUtility.labelWidth;

            Rect propertyPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            if (string.IsNullOrEmpty(property.stringValue))
            {
                DrawBlock(propertyPosition, "Skin isn't selected!", null, property);

                EditorGUI.EndProperty();

                return;
            }

            if (data.SelectedSkinData == null)
            {
                DrawBlock(propertyPosition, "Skin isn't selected!", null, property);

                EditorGUI.EndProperty();

                return;
            }

            EditorGUI.BeginChangeCheck();

            DrawBlock(propertyPosition, property.stringValue, data.PreviewObject, property);

            if(EditorGUI.EndChangeCheck())
            {
                data.UpdateValues();
            }

            EditorGUI.EndProperty();
        }

        private void DrawBlock(Rect propertyPosition, string idText, Object prefabObject, SerializedProperty property)
        {
            float defaultYPosition = propertyPosition.y;

            EditorGUI.LabelField(propertyPosition, "Selected skin:");

            using (new EditorGUI.DisabledScope(disabled: true))
            {
                propertyPosition.y += EditorGUIUtility.singleLineHeight + 2;

                EditorGUI.LabelField(propertyPosition, idText);

                propertyPosition.y += EditorGUIUtility.singleLineHeight + 2;
            }

            Rect boxRect = new Rect(propertyPosition.x + propertyPosition.width + 2, defaultYPosition, 58, 58);

            GUI.Box(boxRect, GUIContent.none);

            Texture2D previewTexture = AssetPreview.GetAssetPreview(prefabObject);
            if (previewTexture != null)
            {
                GUI.DrawTexture(new Rect(boxRect.x + 2, boxRect.y + 2, 55, 55), previewTexture);
            }
            else
            {
                GUI.DrawTexture(new Rect(boxRect.x + 2, boxRect.y + 2, 55, 55), EditorCustomStyles.GetMissingIcon());
            }

            if (property != null)
            {
                if (GUI.Button(new Rect(propertyPosition.x + propertyPosition.width + 5, defaultYPosition + 40, 53, 16), new GUIContent("Select")))
                {
                    SkinPickerAttribute skinPickerAttribute = (SkinPickerAttribute)attribute;
                    if(skinPickerAttribute != null)
                    {
                        SkinPickerWindow.PickSkin(property, skinPickerAttribute.DatabaseType);
                    }
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Mathf.Clamp(EditorGUIUtility.singleLineHeight * 3 + 2, 58, float.MaxValue);
        }

        private PropertyData GetPropertyData(SerializedProperty property)
        {
            PropertyData propertyData;

            int hash = property.stringValue.GetHashCode();
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
            public string ID { get; private set; }
            public ISkinData SelectedSkinData { get; private set; }
            public Object PreviewObject { get; private set; }

            public PropertyData(SerializedProperty property)
            {
                ID = property.stringValue;

                UpdateValues();
            }

            public void UpdateValues()
            {
                SelectedSkinData = null;
                foreach (AbstractSkinDatabase provider in EditorSkinsProvider.SkinsDatabases)
                {
                    SelectedSkinData = provider.GetSkinData(ID);
                    if (SelectedSkinData != null)
                    {
                        break;
                    }
                }

                if (SelectedSkinData != null)
                {
                    FieldInfo preview = SelectedSkinData.GetType().GetFields(ReflectionUtils.FLAGS_INSTANCE).First(x => x.GetCustomAttribute<SkinPreviewAttribute>() != null);
                    object previewValue = preview.GetValue(SelectedSkinData);
                    if (previewValue != null)
                    {
                        PreviewObject = previewValue as Object;
                    }
                }
            }
        }
    }
}
