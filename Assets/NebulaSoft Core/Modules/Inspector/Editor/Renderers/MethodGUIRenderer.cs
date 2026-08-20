using System;
using System.Reflection;
using UnityEngine;

namespace NebulaSoft
{
    public sealed class MethodGUIRenderer : GUIRenderer
    {
        private MethodInfo methodInfo;
        private object target;
        private ButtonAttribute buttonAttribute;
        private CustomInspector editor;

        private InfoBoxAttribute infoBoxAttribute;

        public MethodGUIRenderer(CustomInspector editor, MethodInfo methodInfo, object target, ButtonAttribute buttonAttribute)
        {
            this.editor = editor;
            this.methodInfo = methodInfo;
            this.target = target;
            this.buttonAttribute = buttonAttribute;

            TabAttribute = (TabAttribute)Attribute.GetCustomAttribute(methodInfo, typeof(TabAttribute));
            GroupAttribute = (GroupAttribute)Attribute.GetCustomAttribute(methodInfo, typeof(GroupAttribute));

            OrderAttribute orderAttribute = (OrderAttribute)Attribute.GetCustomAttribute(methodInfo, typeof(OrderAttribute));
            if (orderAttribute != null)
            {
                Order = orderAttribute.Order;
            }
            else
            {
                Order = 10000;
            }

            UpdateVisabilityState();

            infoBoxAttribute = (InfoBoxAttribute)Attribute.GetCustomAttribute(methodInfo, typeof(InfoBoxAttribute));
        }

        private void UpdateVisabilityState()
        {
            if (!string.IsNullOrEmpty(buttonAttribute.VisabilityConditionName))
            {
                MethodInfo conditionMethod = editor.GetMethod(buttonAttribute.VisabilityConditionName);
                if (conditionMethod != null && conditionMethod.ReturnType == typeof(bool) && conditionMethod.GetParameters().Length == 0)
                {
                    bool conditionValue = (bool)conditionMethod.Invoke(target, null);
                    if (buttonAttribute.VisibilityOption == ButtonVisibility.ShowIf)
                    {
                        if (!conditionValue)
                        {
                            IsVisible = false;

                            return;
                        }
                    }
                    else if (buttonAttribute.VisibilityOption == ButtonVisibility.HideIf)
                    {
                        if (conditionValue)
                        {
                            IsVisible = false;

                            return;
                        }
                    }
                }

                FieldInfo conditionField = editor.GetField(buttonAttribute.VisabilityConditionName);
                if (conditionField != null && conditionField.FieldType == typeof(bool))
                {
                    bool conditionValue = (bool)conditionField.GetValue(target);
                    if (buttonAttribute.VisibilityOption == ButtonVisibility.ShowIf)
                    {
                        if (!conditionValue)
                        {
                            IsVisible = false;

                            return;
                        }
                    }
                    else if (buttonAttribute.VisibilityOption == ButtonVisibility.HideIf)
                    {
                        if (conditionValue)
                        {
                            IsVisible = false;

                            return;
                        }
                    }
                }

                PropertyInfo conditionProperty = editor.GetProperty(buttonAttribute.VisabilityConditionName);
                if (conditionProperty != null && conditionProperty.PropertyType == typeof(bool))
                {
                    bool conditionValue = (bool)conditionProperty.GetValue(target);
                    if (buttonAttribute.VisibilityOption == ButtonVisibility.ShowIf)
                    {
                        if (!conditionValue)
                        {
                            IsVisible = false;

                            return;
                        }
                    }
                    else if (buttonAttribute.VisibilityOption == ButtonVisibility.HideIf)
                    {
                        if (conditionValue)
                        {
                            IsVisible = false;

                            return;
                        }
                    }
                }
            }

            IsVisible = true;
        }

        public override void OnGUI()
        {
            if (!IsVisible) return;

            if (infoBoxAttribute != null)
            {
                DrawInfoBox(infoBoxAttribute.Text, infoBoxAttribute.Type);
            }

            string buttonText = string.IsNullOrEmpty(buttonAttribute.Text) ? methodInfo.Name : buttonAttribute.Text;
            if (GUILayout.Button(buttonText, GUILayout.Height(22)))
            {
                object[] attributeParams = buttonAttribute.Params;
                if (attributeParams != null && attributeParams.Length > 0)
                {
                    ParameterInfo[] methodParams = methodInfo.GetParameters();
                    if (attributeParams.Length == methodParams.Length)
                    {
                        bool allowInvoke = true;
                        for (int p = 0; p < attributeParams.Length; p++)
                        {
                            if (attributeParams[p].GetType() != methodParams[p].ParameterType)
                            {
                                allowInvoke = false;

                                Debug.LogWarning(string.Format("Invalid parameters are specified ({0})", buttonText));

                                break;
                            }
                        }

                        if (allowInvoke)
                        {
                            if (editor.targets.Length > 1)
                            {
                                if (target.GetType() == editor.target.GetType())
                                {
                                    foreach (UnityEngine.Object target in editor.targets)
                                    {
                                        object returnObject = methodInfo.Invoke(target, buttonAttribute.Params);

                                        if (returnObject != null)
                                            Debug.Log(returnObject);
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning("Button callback doesn't work with multiple nested scripts!");
                                }
                            }
                            else
                            {
                                object returnObject = methodInfo.Invoke(target, buttonAttribute.Params);

                                if (returnObject != null)
                                    Debug.Log(returnObject);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning(string.Format("Invalid parameters are specified ({0})", buttonText));
                    }
                }
                else
                {
                    if (editor.targets.Length > 1)
                    {
                        if (target.GetType() == editor.target.GetType())
                        {
                            foreach (UnityEngine.Object target in editor.targets)
                            {
                                object returnObject = methodInfo.Invoke(target, null);

                                if (returnObject != null)
                                    Debug.Log(returnObject);
                            }
                        }
                        else
                        {
                            Debug.LogWarning("Button callback doesn't work with multiple nested scripts!");
                        }
                    }
                    else
                    {
                        object returnObject = methodInfo.Invoke(target, null);

                        if (returnObject != null)
                            Debug.Log(returnObject);
                    }
                }
            }
        }

        public override void OnGUIChanged()
        {
            UpdateVisabilityState();
        }
    }
}