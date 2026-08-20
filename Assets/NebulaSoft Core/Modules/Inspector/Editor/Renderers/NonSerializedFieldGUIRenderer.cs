using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NebulaSoft
{
    public sealed class NonSerializedFieldGUIRenderer : GUIRenderer
    {
        private FieldInfo fieldInfo;
        private GUIContent labelContent;
        private object target;

        private InfoBoxAttribute infoBoxAttribute;

        public NonSerializedFieldGUIRenderer(FieldInfo fieldInfo, Object target)
        {
            this.fieldInfo = fieldInfo;
            this.target = target;

            TabAttribute = (TabAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(TabAttribute));
            GroupAttribute = (GroupAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(GroupAttribute));

            OrderAttribute orderAttribute = (OrderAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(OrderAttribute));
            if (orderAttribute != null)
            {
                Order = orderAttribute.Order;
            }
            else
            {
                Order = GUIRenderer.ORDER_NON_SERIALIZED;
            }

            string label = fieldInfo.Name;

            LabelAttribute labelAttribute = (LabelAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(LabelAttribute));
            if (labelAttribute != null)
            {
                label = labelAttribute.Label;
            }

            labelContent = new GUIContent(label);

            infoBoxAttribute = PropertyUtility.GetAttribute<InfoBoxAttribute>(fieldInfo);
        }

        public override void OnGUI()
        {
            object value = fieldInfo.GetValue(target);

            if (value == null)
            {
                string warning = "Unsuported field type";

                EditorGUILayout.HelpBox(warning, MessageType.Warning);

                Debug.LogWarning(warning);

                return;
            }

            bool propertyStatus = false;

            LabelWidthAttribute labelWidthAttribute = PropertyUtility.GetAttribute<LabelWidthAttribute>(fieldInfo);
            if (labelWidthAttribute != null)
            {
                using (new LabelWidthScope(labelWidthAttribute.Width))
                {
                    if (infoBoxAttribute != null)
                    {
                        DrawInfoBox(infoBoxAttribute.Text, infoBoxAttribute.Type);
                    }

                    propertyStatus = EditorGUILayoutCustom.DrawLayoutField(value, labelContent);
                }
            }
            else
            {
                if (infoBoxAttribute != null)
                {
                    DrawInfoBox(infoBoxAttribute.Text, infoBoxAttribute.Type);
                }

                propertyStatus = EditorGUILayoutCustom.DrawLayoutField(value, labelContent);
            }

            if(!propertyStatus)
            {
                string warning = "Unsuported field type";

                EditorGUILayout.HelpBox(warning, MessageType.Warning);

                Debug.LogWarning(warning);
            }
        }
    }
}