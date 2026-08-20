using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace NebulaSoft
{
    public class SerializedPropertyGUIRenderer : GUIRenderer
    {
        private FieldInfo fieldInfo;
        private object targetObject;

        protected SerializedProperty serializedProperty;
        private GUIContent labelContent;

        private CustomInspector editor;

        private PropertyCondition propertyCondition;

        private List<Type> nestedTypes;

        private InfoBoxAttribute infoBoxAttribute;

        public SerializedPropertyGUIRenderer(CustomInspector editor, SerializedProperty serializedProperty, FieldInfo fieldInfo, object targetObject, List<Type> nestedTypes)
        {
            this.editor = editor;
            this.serializedProperty = serializedProperty;
            this.targetObject = targetObject;
            this.fieldInfo = fieldInfo;
            this.nestedTypes = nestedTypes;

            TabAttribute = PropertyUtility.GetAttribute<TabAttribute>(fieldInfo);
            GroupAttribute = PropertyUtility.GetAttribute<GroupAttribute>(fieldInfo);

            OrderAttribute orderAttribute = PropertyUtility.GetAttribute<OrderAttribute>(fieldInfo);
            if (orderAttribute != null)
            {
                Order = orderAttribute.Order;
            }

            string label = serializedProperty.displayName;

            LabelAttribute labelAttribute = PropertyUtility.GetAttribute<LabelAttribute>(fieldInfo);
            if (labelAttribute != null)
            {
                label = labelAttribute.Label;
            }

            labelContent = new GUIContent(label);

            // Check if visible
            ConditionAttribute showIfAttribute = PropertyUtility.GetAttribute<ConditionAttribute>(fieldInfo);

            if (showIfAttribute != null)
            {
                propertyCondition = CustomAttributesDatabase.GetConditionAttribute(showIfAttribute.GetType());

                UpdateVisabilityState();
            }

            infoBoxAttribute = PropertyUtility.GetAttribute<InfoBoxAttribute>(fieldInfo);
        }

        public bool IsEnabled()
        {
            ReadOnlyAttribute readOnlyAttribute = PropertyUtility.GetAttribute<ReadOnlyAttribute>(fieldInfo);
            if (readOnlyAttribute != null)
            {
                return false;
            }

            DisableIfAttribute disableIfAttribute = PropertyUtility.GetAttribute<DisableIfAttribute>(fieldInfo);
            if(disableIfAttribute != null)
            {
                string methodName = disableIfAttribute.ConditionName;
                if(!string.IsNullOrEmpty(methodName))
                {
                    foreach(Type type in nestedTypes)
                    {
                        FieldInfo conditionField = type.GetField(methodName, ReflectionUtils.FLAGS_INSTANCE);
                        if (conditionField != null && conditionField.FieldType == PropertyUtility.TYPE_BOOL)
                        {
                            return !(bool)conditionField.GetValue(targetObject);
                        }

                        PropertyInfo propertyInfo = type.GetProperty(methodName, ReflectionUtils.FLAGS_INSTANCE);
                        if (propertyInfo != null && propertyInfo.PropertyType == PropertyUtility.TYPE_BOOL)
                        {
                            return !(bool)propertyInfo.GetValue(targetObject);
                        }

                        MethodInfo conditionMethod = type.GetMethod(methodName, ReflectionUtils.FLAGS_INSTANCE);
                        if (conditionMethod != null && conditionMethod.ReturnType == PropertyUtility.TYPE_BOOL && conditionMethod.GetParameters().Length == 0)
                        {
                            return !(bool)conditionMethod.Invoke(targetObject, null);
                        }
                    }
                }
            }

            EnableIfAttribute enableIfAttribute = PropertyUtility.GetAttribute<EnableIfAttribute>(fieldInfo);
            if (enableIfAttribute != null)
            {
                string methodName = enableIfAttribute.ConditionName;
                if (!string.IsNullOrEmpty(methodName))
                {
                    foreach (Type type in nestedTypes)
                    {
                        FieldInfo conditionField = type.GetField(methodName, ReflectionUtils.FLAGS_INSTANCE);
                        if (conditionField != null && conditionField.FieldType == PropertyUtility.TYPE_BOOL)
                        {
                            return (bool)conditionField.GetValue(targetObject);
                        }

                        PropertyInfo propertyInfo = type.GetProperty(methodName, ReflectionUtils.FLAGS_INSTANCE);
                        if (propertyInfo != null && propertyInfo.PropertyType == PropertyUtility.TYPE_BOOL)
                        {
                            return (bool)propertyInfo.GetValue(targetObject);
                        }

                        MethodInfo conditionMethod = type.GetMethod(methodName, ReflectionUtils.FLAGS_INSTANCE);
                        if (conditionMethod != null && conditionMethod.ReturnType == PropertyUtility.TYPE_BOOL && conditionMethod.GetParameters().Length == 0)
                        {
                            return (bool)conditionMethod.Invoke(targetObject, null);
                        }
                    }
                }
            }

            return true;
        }

        public override void OnGUIChanged()
        {
            UpdateVisabilityState();
        }

        private void UpdateVisabilityState()
        {
            if(propertyCondition != null)
            {
                // Check if visible
                IsVisible = propertyCondition.CanBeDrawn(editor, fieldInfo, targetObject, nestedTypes);
            }
        }

        public override void OnGUI()
        {
            if (!IsVisible) return;

            IndentAttribute indentAttribute = PropertyUtility.GetAttribute<IndentAttribute>(fieldInfo);

            EditorGUILayout.BeginHorizontal();

            if (indentAttribute != null)
                GUILayout.Space(indentAttribute.Space);

            EditorGUILayout.BeginVertical();

            // Check if enabled and draw
            EditorGUI.BeginChangeCheck();
            bool enabled = IsEnabled();

            using (new EditorGUI.DisabledScope(disabled: !enabled))
            {
                LabelWidthAttribute labelWidthAttribute = PropertyUtility.GetAttribute<LabelWidthAttribute>(fieldInfo);
                if (labelWidthAttribute != null)
                {
                    using (new LabelWidthScope(labelWidthAttribute.Width))
                    {
                        if(infoBoxAttribute != null)
                        {
                            DrawInfoBox(infoBoxAttribute.Text, infoBoxAttribute.Type);
                        }

                        EditorGUILayout.PropertyField(serializedProperty, labelContent, true);
                    }
                }
                else
                {
                    if (infoBoxAttribute != null)
                    {
                        DrawInfoBox(infoBoxAttribute.Text, infoBoxAttribute.Type);
                    }

                    EditorGUILayout.PropertyField(serializedProperty, labelContent, true);
                }
            }

            // Call OnValueChanged callbacks
            if (EditorGUI.EndChangeCheck())
            {
                OnValueChangedAttribute onValueChangedAttribute = PropertyUtility.GetAttribute<OnValueChangedAttribute>(fieldInfo);
                if (onValueChangedAttribute != null)
                {
                    MethodInfo callbackMethod = targetObject.GetType().GetMethod(onValueChangedAttribute.CallbackName, ReflectionUtils.FLAGS_INSTANCE);
                    if (editor.targets.Length > 1)
                    {
                        if (callbackMethod != null && callbackMethod.ReturnType == typeof(void) && callbackMethod.GetParameters().Length == 0)
                        {
                            serializedProperty.serializedObject.ApplyModifiedProperties();

                            if(targetObject.GetType() == editor.target.GetType())
                            {
                                foreach (UnityEngine.Object target in editor.targets)
                                {
                                    callbackMethod.Invoke(target, null);
                                }
                            }
                            else
                            {
                                Debug.LogWarning("OnValueChange callback doesn't work with multiple nested scripts!");
                            }
                        }
                        else
                        {
                            Debug.LogWarning(string.Format("OnValueChange callback can't be performed. Method {0} is missing or has a different structure (void return type and no parameters)", onValueChangedAttribute.CallbackName));
                        }
                    }
                    else
                    {
                        if (callbackMethod != null && callbackMethod.ReturnType == typeof(void) && callbackMethod.GetParameters().Length == 0)
                        {
                            serializedProperty.serializedObject.ApplyModifiedProperties();

                            callbackMethod.Invoke(targetObject, null);
                        }
                        else
                        {
                            Debug.LogWarning(string.Format("OnValueChange callback can't be performed. Method {0} is missing or has a different structure (void return type and no parameters)", onValueChangedAttribute.CallbackName));
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();

            InlineButtonAttribute inlineButtonAttribute = PropertyUtility.GetAttribute<InlineButtonAttribute>(fieldInfo);
            if (inlineButtonAttribute != null)
            {
                GUIStyle buttonStyle = EditorCustomStyles.buttonMini;
                GUIContent buttonContent = new GUIContent(inlineButtonAttribute.Title);
                float buttonWidth = buttonStyle.CalcSize(buttonContent).x;

                if (GUILayout.Button(buttonContent, buttonStyle, GUILayout.Width(buttonWidth)))
                {
                    MethodInfo conditionMethod = targetObject.GetType().GetMethod(inlineButtonAttribute.MethodName, ReflectionUtils.FLAGS_INSTANCE);
                    if (editor.targets.Length > 1)
                    {
                        if (conditionMethod != null && conditionMethod.GetParameters().Length == 0)
                        {
                            serializedProperty.serializedObject.ApplyModifiedProperties();

                            if (targetObject.GetType() == editor.target.GetType())
                            {
                                foreach (UnityEngine.Object target in editor.targets)
                                {
                                    conditionMethod.Invoke(target, null);
                                }
                            }
                            else
                            {
                                Debug.LogWarning("InlineButton callback doesn't work with multiple nested scripts!");
                            }
                        }
                        else
                        {
                            Debug.LogWarning(string.Format("InlineButton callback can't be performed. Method {0} is missing or has a different structure (void return type and no parameters)", inlineButtonAttribute.MethodName));
                        }
                    }
                    else
                    {
                        if (conditionMethod != null && conditionMethod.GetParameters().Length == 0)
                        {
                            serializedProperty.serializedObject.ApplyModifiedProperties();

                            conditionMethod.Invoke(targetObject, null);
                        }
                        else
                        {
                            Debug.LogWarning(string.Format("InlineButton callback can't be performed. Method {0} is missing or has a different structure (void return type and no parameters)", inlineButtonAttribute.MethodName));
                        }
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}