using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    [CustomPropertyDrawer(typeof(CreateScriptableObjectAttribute))]
    public class CreateScriptableObjectDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Always wrap the original rect + label once.
            EditorGUI.BeginProperty(position, label, property);

            // Early validation (but still finish with EndProperty).
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.HelpBox(position, "[CreateScriptableObject] works only with ObjectReference fields.", MessageType.Warning);

                EditorGUI.EndProperty();

                return;
            }

            const float pad = 2f;
            const float btnW = 22f;

            bool isEmpty = property.objectReferenceValue == null;

            // Split rect into field rect (+ optional button rect)
            Rect fieldRect = position;
            Rect buttonRect = Rect.zero;

            if (isEmpty)
            {
                fieldRect.width -= (btnW + pad);
                buttonRect = new Rect(fieldRect.xMax + pad, position.y, btnW, EditorGUIUtility.singleLineHeight);
            }

            EditorGUI.ObjectField(fieldRect, property, label);

            // Draw the [+] only when empty.
            if (isEmpty)
            {
                if (GUI.Button(buttonRect, "+", EditorStyles.miniButton))
                {
                    // Determine the expected ScriptableObject type from the field (supports T, T[], List<T>)
                    Type soType = GetExpectedObjectType();
                    if (soType == null || !typeof(ScriptableObject).IsAssignableFrom(soType) || soType.IsAbstract)
                    {
                        Debug.LogWarning($"[CreateScriptableObject] Cannot create instance for type '{soType?.Name ?? "null"}'. Ensure the field is a concrete ScriptableObject type.");
                    }
                    else
                    {
                        SerializedProperty tempProperty = property;
                        SerializedObject serializedObject = tempProperty.serializedObject;

                        ScriptableObject asset = CreateAssetWithDialog(soType, $"New {soType.Name}");
                        if (asset != null)
                        {
                            tempProperty.objectReferenceValue = asset;
                            serializedObject.ApplyModifiedProperties();
                                                        
                            EditorGUIUtility.PingObject(asset);
                        }
                    }

                    GUIUtility.ExitGUI();
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        // Returns the expected object type for this field (supports T, T[], List<T>)
        private Type GetExpectedObjectType()
        {
            if (fieldInfo == null)
                return typeof(UnityEngine.Object);

            Type t = fieldInfo.FieldType;

            if (t.IsArray) // T[]
                t = t.GetElementType();
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) // List<T>
                t = t.GetGenericArguments()[0];

            return t ?? typeof(UnityEngine.Object);
        }

        // Creates a ScriptableObject asset via a Save File dialog.
        // Put this inside your drawer (or any editor class).
        private static ScriptableObject CreateAssetWithDialog(Type soType, string defaultFileName = null)
        {
            // Guard: type must be a concrete ScriptableObject
            if (soType == null || !typeof(ScriptableObject).IsAssignableFrom(soType) || soType.IsAbstract)
            {
                Debug.LogWarning("[CreateSO] Provided type must be a non-abstract ScriptableObject.");
                return null;
            }

            // Per-type EditorPrefs key
            string key = "NebulaSoft.LastScriptablePath." + soType.FullName;

            // Load last folder; seed from selection; fallback to "Assets"
            string startFolder = EditorPrefs.GetString(key, "");
            startFolder = string.IsNullOrWhiteSpace(startFolder) ? "" : startFolder.Replace('\\', '/');

            if (string.IsNullOrEmpty(startFolder) || !AssetDatabase.IsValidFolder(startFolder))
            {
                string sel = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(sel))
                {
                    if (AssetDatabase.IsValidFolder(sel)) startFolder = sel;
                    else
                    {
                        string dir = System.IO.Path.GetDirectoryName(sel)?.Replace('\\', '/');
                        if (!string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir)) startFolder = dir;
                    }
                }
                if (string.IsNullOrEmpty(startFolder) || !AssetDatabase.IsValidFolder(startFolder))
                    startFolder = "Assets";
            }

            // Sanitize default file name
            string fileName = string.IsNullOrEmpty(defaultFileName) ? $"New {soType.Name}" : defaultFileName;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c.ToString(), "");

            // Ask user where to save
            string targetPath = EditorUtility.SaveFilePanelInProject(
                "Create ScriptableObject",
                fileName,
                "asset",
                "Choose where to save the asset",
                startFolder
            );

            if (string.IsNullOrEmpty(targetPath))
                return null;

            targetPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);

            // Create asset
            var asset = ScriptableObject.CreateInstance(soType);
            AssetDatabase.CreateAsset(asset, targetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Remember chosen folder (per-type)
            string chosenFolder = System.IO.Path.GetDirectoryName(targetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(chosenFolder) && AssetDatabase.IsValidFolder(chosenFolder))
                EditorPrefs.SetString(key, chosenFolder);

            return asset as ScriptableObject;
        }
    }
}