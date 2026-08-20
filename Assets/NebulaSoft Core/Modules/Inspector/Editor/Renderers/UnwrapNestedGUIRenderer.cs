using System;
using System.Collections.Generic;
using System.Reflection;

namespace NebulaSoft
{
    public sealed class UnwrapNestedGUIRenderer : GUIRenderer
    {
        private GUIRenderer[] renderers;

        private FieldInfo fieldInfo;
        private object targetObject;
        private CustomInspector editor;

        private PropertyCondition propertyCondition;

        private List<Type> nestedTypes;

        public UnwrapNestedGUIRenderer(CustomInspector editor, FieldInfo fieldInfo, GUIRenderer[] renderers, object targetObject, List<Type> nestedTypes)
        {
            this.editor = editor;
            this.targetObject = targetObject;
            this.fieldInfo = fieldInfo;
            this.nestedTypes = nestedTypes;
            this.renderers = renderers;

            TabAttribute = (TabAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(TabAttribute));
            GroupAttribute = (GroupAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(GroupAttribute));

            // Check if visible
            ConditionAttribute showIfAttribute = PropertyUtility.GetAttribute<ConditionAttribute>(fieldInfo);

            if (showIfAttribute != null)
            {
                propertyCondition = CustomAttributesDatabase.GetConditionAttribute(showIfAttribute.GetType());

                UpdateVisabilityState();
            }
            else
            {
                IsVisible = renderers.IsAnyObjectVisible();
            }
        }

        public override void OnGUI()
        {
            if (!IsVisible) return;

            foreach (GUIRenderer renderer in renderers)
            {
                renderer.OnGUI();
            }
        }

        public override void OnGUIChanged()
        {
            foreach (GUIRenderer renderer in renderers)
            {
                renderer.OnGUIChanged();
            }

            UpdateVisabilityState();
        }

        private void UpdateVisabilityState()
        {
            if (propertyCondition != null)
            {
                // Check if visible
                if (propertyCondition.CanBeDrawn(editor, fieldInfo, targetObject, nestedTypes))
                {
                    IsVisible = renderers.IsAnyObjectVisible();
                }
                else
                {
                    IsVisible = false;
                }
            }
            else
            {
                IsVisible = renderers.IsAnyObjectVisible();
            }
        }
    }
}