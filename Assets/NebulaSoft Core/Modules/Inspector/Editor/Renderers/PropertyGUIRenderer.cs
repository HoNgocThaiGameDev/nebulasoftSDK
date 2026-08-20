using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NebulaSoft
{
    public sealed class PropertyGUIRenderer : GUIRenderer
    {
        private PropertyInfo propertyInfo;
        private GUIContent labelContent;
        private object target;

        public PropertyGUIRenderer(PropertyInfo propertyInfo, Object target)
        {
            this.propertyInfo = propertyInfo;
            this.target = target;

            TabAttribute = (TabAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(TabAttribute));
            GroupAttribute = (GroupAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(GroupAttribute));

            OrderAttribute orderAttribute = (OrderAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(OrderAttribute));
            if (orderAttribute != null)
            {
                Order = orderAttribute.Order;
            }
            else
            {
                Order = GUIRenderer.ORDER_NON_SERIALIZED;
            }

            string label = propertyInfo.Name;

            LabelAttribute labelAttribute = (LabelAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(LabelAttribute));
            if (labelAttribute != null)
            {
                label = labelAttribute.Label;
            }

            labelContent = new GUIContent(label);
        }

        public override void OnGUI()
        {
            object value = propertyInfo.GetValue(target);

            if (value == null)
            {
                string warning = "Unsuported property type";

                EditorGUILayout.HelpBox(warning, MessageType.Warning);

                Debug.LogWarning(warning);
            }

            bool propertyStatus = false;

            LabelWidthAttribute labelWidthAttribute = PropertyUtility.GetAttribute<LabelWidthAttribute>(propertyInfo);
            if (labelWidthAttribute != null)
            {
                using (new LabelWidthScope(labelWidthAttribute.Width))
                {
                    propertyStatus = EditorGUILayoutCustom.DrawLayoutField(value, labelContent);
                }
            }
            else
            {
                propertyStatus = EditorGUILayoutCustom.DrawLayoutField(value, labelContent);
            }

            if (!propertyStatus)
            {
                string warning = "Unsuported property type";

                EditorGUILayout.HelpBox(warning, MessageType.Warning);

                Debug.LogWarning(warning);
            }
        }
    }
}