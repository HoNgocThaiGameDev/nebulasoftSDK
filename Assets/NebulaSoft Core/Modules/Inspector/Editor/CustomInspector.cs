using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Editor = UnityEditor.Editor;

namespace NebulaSoft
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    public sealed class MonoBehaviorInspector : CustomInspector { }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableObject), true)]
    public sealed class ScriptableObjectInspector : CustomInspector { }

    public class CustomInspector : Editor
    {
        private List<Type> nestedClassTypes;
        public List<Type> NestedClassTypes => nestedClassTypes;

        private GUIRenderer[] renderers;

        private bool isTabsActive;
        private EditorTabs editorTabs;

        protected bool showScriptField = true;

        private bool elementsExist;
        public bool ElementsExist => elementsExist;

        private SerializedProperty scriptProperty;
        public SerializedProperty ScriptProperty => scriptProperty;

        private Dictionary<string, EditorFoldoutBool> foldouts;

        protected virtual void OnEnable()
        {
            EditorCustomStyles.CheckStyles();

            if (target == null) return;

            Type targetType = target.GetType();

            foldouts = new Dictionary<string, EditorFoldoutBool>();

            showScriptField = Attribute.GetCustomAttribute(targetType, typeof(HideScriptFieldAttribute)) == null;

            // Cache scripts property
            scriptProperty = serializedObject.FindProperty("m_Script");

            // Cache nested classes
            nestedClassTypes = PropertyUtility.GetClassNestedTypes(targetType);

            SerializedProperty propertiesIterator = serializedObject.GetIterator();

            List<GUIRenderer> enumerableRenderers = CacheGUIRenderers(propertiesIterator);

            elementsExist = enumerableRenderers.Any();
            isTabsActive = enumerableRenderers.Any(x => x.TabAttribute != null);

            if(isTabsActive)
            {
                editorTabs = new EditorTabs(this, enumerableRenderers);
            }
            else
            {
                renderers = PropertyUtility.GroupRenderers(this, enumerableRenderers);
            }

            propertiesIterator.Dispose();
        }

        private void OnDisable()
        {
            nestedClassTypes = null;
            renderers = null;

            foldouts = null;

            if(editorTabs != null)
            {
                editorTabs.Unload();
                editorTabs = null;
            }
        }

        public override void OnInspectorGUI()
        {
            if(!CoreEditor.UseCustomInspector)
            {
                DrawDefaultInspector();

                return;
            }

            if (showScriptField)
            {
                if (scriptProperty != null)
                {
                    using (new EditorGUI.DisabledScope(disabled: true))
                    {
                        EditorGUILayout.PropertyField(scriptProperty);
                    }
                }
            }

            serializedObject.UpdateIfRequiredOrScript();

            if(!isTabsActive)
            {
                if (renderers != null && renderers.Length > 0)
                {
                    foreach (GUIRenderer renderer in renderers)
                    {
                        renderer.OnGUI();
                    }
                }
            }
            else
            {
                editorTabs.OnInpectorGUI();
            }

            serializedObject.ApplyModifiedProperties();

            if(GUI.changed)
            {
                if(isTabsActive)
                {
                    editorTabs.OnGUIChanged();
                }
                else
                {
                    if (renderers != null && renderers.Length > 0)
                    {
                        foreach (GUIRenderer renderer in renderers)
                        {
                            renderer.OnGUIChanged();
                        }
                    }
                }
            }
        }

        private void CacheNested(SerializedProperty serializedProperty, FieldInfo baseFieldInfo, object targetObject, ref List<GUIRenderer> renderers)
        {
            List<GUIRenderer> nestedRenderers = new List<GUIRenderer>();

            object nestedObject = baseFieldInfo.GetValue(targetObject);

            List<Type> nestedTypes = PropertyUtility.GetClassNestedTypes(nestedObject.GetType());

            SerializedProperty nestedProperty = serializedProperty.Copy();

            SerializedProperty end = nestedProperty.GetEndProperty();

            nestedProperty.NextVisible(enterChildren: true);

            int propertyDepth = nestedProperty.depth;

            do
            {
                if (SerializedProperty.EqualContents(nestedProperty, end))
                {
                    break;
                }

                if (propertyDepth != nestedProperty.depth)
                    continue;

                FieldInfo propertyField = nestedObject.GetType().GetField(nestedProperty.name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (nestedProperty.propertyType == SerializedPropertyType.Generic)
                {
                    if (propertyField.GetCustomAttribute<UnpackNestedAttribute>() != null)
                    {
                        CacheNested(nestedProperty, propertyField, nestedObject, ref nestedRenderers);

                        continue;
                    }
                }

                SerializedProperty targetProperty = serializedObject.FindProperty(nestedProperty.propertyPath);
                if(targetProperty.isArray)
                {
                    nestedRenderers.Add(new SerializedArrayGUIRenderer(this, serializedObject.FindProperty(nestedProperty.propertyPath), propertyField, nestedObject, nestedTypes));
                }
                else
                {
                    nestedRenderers.Add(new SerializedPropertyGUIRenderer(this, serializedObject.FindProperty(nestedProperty.propertyPath), propertyField, nestedObject, nestedTypes));
                }
            }
            while (nestedProperty.NextVisible(enterChildren: true));

            IEnumerable<MethodInfo> methodInfos = nestedObject.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).Where(m => m.GetCustomAttribute(typeof(ButtonAttribute), true) != null);
            foreach (MethodInfo method in methodInfos)
            {
                IEnumerable<ButtonAttribute> buttonAttributes = method.GetCustomAttributes<ButtonAttribute>(false);
                foreach (ButtonAttribute buttonAttribute in buttonAttributes)
                {
                    nestedRenderers.Add(new MethodGUIRenderer(this, method, nestedObject, buttonAttribute));
                }
            }

            renderers.Add(new UnwrapNestedGUIRenderer(this, baseFieldInfo, PropertyUtility.GroupRenderers(this, nestedRenderers), targetObject, PropertyUtility.GetClassNestedTypes(targetObject.GetType())));
        }

        private List<GUIRenderer> CacheGUIRenderers(SerializedProperty serializedProperty, SerializedProperty endProperty = null)
        {
            List<GUIRenderer> renderers = new List<GUIRenderer>();

            if (serializedProperty.NextVisible(true))
            {
                do
                {
                    if (!serializedProperty.name.Equals("m_Script", StringComparison.OrdinalIgnoreCase))
                    {
                        FieldInfo propertyField = GetField(serializedProperty.name);
                        if (serializedProperty.propertyType == SerializedPropertyType.Generic)
                        {
                            if (Attribute.GetCustomAttribute(propertyField, typeof(UnpackNestedAttribute)) != null)
                            {
                                CacheNested(serializedProperty, propertyField, target, ref renderers);

                                continue;
                            }
                        }

                        SerializedProperty targetProperty = serializedObject.FindProperty(serializedProperty.propertyPath);
                        if (targetProperty.isArray)
                        {
                            renderers.Add(new SerializedArrayGUIRenderer(this, serializedObject.FindProperty(serializedProperty.propertyPath), propertyField, target, nestedClassTypes));
                        }
                        else
                        {
                            renderers.Add(new SerializedPropertyGUIRenderer(this, serializedObject.FindProperty(serializedProperty.propertyPath), propertyField, target, nestedClassTypes));
                        }

                    }
                }
                while (serializedProperty.NextVisible(false));
            }

            // Draw non-serialized fields
            List<FieldInfo> fields = GetFields(f => Attribute.GetCustomAttribute(f, typeof(ShowNonSerializedAttribute)) != null);
            foreach (FieldInfo field in fields)
            {
                renderers.Add(new NonSerializedFieldGUIRenderer(field, target));
            }

            List<PropertyInfo> properties = GetProperties(f => Attribute.GetCustomAttribute(f, typeof(ShowNonSerializedAttribute)) != null);
            foreach (PropertyInfo property in properties)
            {
                renderers.Add(new PropertyGUIRenderer(property, target));
            }

            // Cache methods with DrawerAttribute
            List<MethodInfo> methods = GetMethods(m => m.GetCustomAttributes(typeof(ButtonAttribute), true) != null);
            foreach (MethodInfo method in methods)
            {
                IEnumerable<ButtonAttribute> buttonAttributes = method.GetCustomAttributes<ButtonAttribute>(false);
                foreach(ButtonAttribute buttonAttribute in buttonAttributes)
                {
                    renderers.Add(new MethodGUIRenderer(this, method, target, buttonAttribute));
                }
            }

            // Cache help buttons
            List<HelpButtonAttribute> helpButtons = GetHelpButtons();
            foreach (HelpButtonAttribute helpButton in helpButtons)
            {
                renderers.Add(new HelpButtonGUIRenderer(helpButton));
            }

            return renderers;
        }

        public FieldInfo GetField(string name)
        {
            foreach (var type in nestedClassTypes)
            {
                FieldInfo fieldInfo = type.GetField(name, ReflectionUtils.FLAGS_STATIC);
                if (fieldInfo != null)
                    return fieldInfo;
            }

            return null;
        }

        public MethodInfo GetMethod(string name)
        {
            foreach (var type in nestedClassTypes)
            {
                MethodInfo methodInfo = type.GetMethod(name, ReflectionUtils.FLAGS_INSTANCE);
                if (methodInfo != null)
                    return methodInfo;
            }

            return null;
        }

        public PropertyInfo GetProperty(string name)
        {
            foreach (var type in nestedClassTypes)
            {
                PropertyInfo propertyInfo = type.GetProperty(name, ReflectionUtils.FLAGS_INSTANCE);
                if (propertyInfo != null)
                    return propertyInfo;
            }

            return null;
        }

        private List<FieldInfo> GetFields(Func<FieldInfo, bool> predicate)
        {
            List<FieldInfo> fields = new List<FieldInfo>();
            foreach (var type in nestedClassTypes)
            {
                IEnumerable<FieldInfo> fieldInfos = type.GetFields(ReflectionUtils.FLAGS_STATIC).Where(predicate);

                fields.InsertRange(0, fieldInfos);
            }

            return fields;
        }

        private List<PropertyInfo> GetProperties(Func<PropertyInfo, bool> predicate)
        {
            List<PropertyInfo> properties = new List<PropertyInfo>();
            foreach (var type in nestedClassTypes)
            {
                IEnumerable<PropertyInfo> propertyInfos = type.GetProperties(ReflectionUtils.FLAGS_STATIC).Where(predicate);

                properties.AddRange(propertyInfos);
            }

            return properties;
        }

        private List<MethodInfo> GetMethods(Func<MethodInfo, bool> predicate)
        {
            List<MethodInfo> methods = new List<MethodInfo>();
            foreach (var type in nestedClassTypes)
            {
                IEnumerable<MethodInfo> methodInfos = type.GetMethods(ReflectionUtils.FLAGS_STATIC).Where(predicate);

                foreach (MethodInfo method in methodInfos)
                {
                    if (!methods.Any(x => x.MetadataToken == method.MetadataToken))
                    {
                        methods.Add(method);
                    }
                }
            }

            return methods;
        }

        private List<HelpButtonAttribute> GetHelpButtons()
        {
            List<HelpButtonAttribute> buttonAttributes = new List<HelpButtonAttribute>();
            foreach (Type type in nestedClassTypes)
            {
                Attribute[] attributes = Attribute.GetCustomAttributes(type, typeof(HelpButtonAttribute));
                foreach (Attribute attribute in attributes)
                {
                    HelpButtonAttribute helpButtonAttribute = (HelpButtonAttribute)attribute;
                    if(helpButtonAttribute != null)
                    {
                        buttonAttributes.Add(helpButtonAttribute);
                    }
                }
            }

            return buttonAttributes;
        }

        public EditorFoldoutBool GetFoldout(string key, bool defaultValue = true)
        {
            if (foldouts.ContainsKey(key))
                return foldouts[key];

            EditorFoldoutBool foldoutBool = new EditorFoldoutBool(string.Format("{0}_{1}", key, target.GetType()), defaultValue);

            foldouts.Add(key, foldoutBool);

            return foldoutBool;
        }

        public void SetScriptFieldState(bool state)
        {
            showScriptField = state;
        }

        public class EditorTabs
        {
            private CustomInspector editor;

            private Tab[] tabs;
            private Tab activeTab;

            private GUIContent[] filteredTabs;

            private GUIRenderer[] renderersWithoutTab;

            private int filteredTabIndex;

            public EditorTabs(CustomInspector editor, IEnumerable<GUIRenderer> renderers)
            {
                this.editor = editor;

                IGrouping<TabAttribute, GUIRenderer>[] tabRenderers = renderers.Where(p => p.TabAttribute != null).GroupBy(p => p.TabAttribute).ToArray();
                if (tabRenderers.Length > 0)
                {
                    IEnumerable<GUIRenderer>[] tabArrayRenderers = new IEnumerable<GUIRenderer>[tabRenderers.Length];

                    tabs = new Tab[tabRenderers.Length];

                    for (int i = 0; i < tabRenderers.Length; i++)
                    {
                        tabArrayRenderers[i] = PropertyUtility.GroupRenderers(editor, tabRenderers[i]);
                        tabs[i] = new Tab(editor, tabRenderers[i].Key, tabArrayRenderers[i]);
                    }

                    filteredTabs = GetActiveTabs().ToArray();

                    filteredTabIndex = 0;

                    activeTab = tabs[0];

                    renderersWithoutTab = PropertyUtility.GroupRenderers(editor, renderers.Where(p => p.TabAttribute == null));
                }
            }

            private int GetFilteredTabIndex(Tab tab)
            {
                for (int i = 0; i < filteredTabs.Length; i++) 
                {
                    if (filteredTabs[i].text == tab.Content.text)
                    {
                        return i;
                    }
                }

                return 0;
            }

            private Tab GetTab(GUIContent tabContent)
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    if (tabs[i].Content.text == tabContent.text)
                    {
                        return tabs[i];
                    }
                }

                return tabs[0];
            }

            private IEnumerable<GUIContent> GetActiveTabs()
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    if(tabs[i].IsActive)
                    {
                        yield return tabs[i].Content;
                    }
                }
            }

            public void OnInpectorGUI()
            {
                Rect panelRect = EditorGUILayout.BeginVertical();

                if (filteredTabs.Length > 0)
                {
                    GUILayout.Space(35);

                    int tempTab = GUI.Toolbar(new Rect(0, panelRect.y, Screen.width, 30), filteredTabIndex, filteredTabs, EditorCustomStyles.tab);
                    if (tempTab != filteredTabIndex)
                    {
                        if (tempTab < 0 || tempTab >= filteredTabs.Length)
                        {
                            tempTab = 0;
                        }

                        filteredTabIndex = tempTab;

                        activeTab = GetTab(filteredTabs[tempTab]);
                    }
                }

                activeTab.DrawRenderers();

                if (renderersWithoutTab.Any())
                {
                    GUILayout.Space(12);

                    EditorGUILayoutCustom.LineSpacer();

                    foreach (GUIRenderer renderer in renderersWithoutTab)
                    {
                        renderer.OnGUI();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            public void OnGUIChanged()
            {
                UpdateActiveTabs();

                foreach(var tab in tabs)
                {
                    tab.OnGUIChanged();
                }

                foreach (GUIRenderer renderer in renderersWithoutTab)
                {
                    renderer.OnGUIChanged();
                }
            }

            public void UpdateActiveTabs()
            {
                bool updateRequired = false;
                for(int i = 0; i < tabs.Length; i++)
                {
                    if (tabs[i].UpdateState())
                    {
                        updateRequired = true;
                    }
                }
                
                if(updateRequired)
                {
                    filteredTabs = GetActiveTabs().ToArray();

                    if (!activeTab.IsActive)
                    {
                        for(int i = 0; i < tabs.Length; i++)
                        {
                            if (tabs[i].IsActive)
                            {
                                activeTab = tabs[i];

                                break;
                            }
                        }
                    }

                    filteredTabIndex = GetFilteredTabIndex(activeTab);
                }
            }

            public void Unload()
            {
                renderersWithoutTab = null;
                tabs = null;
                filteredTabs = null;
            }

            private class Tab
            {
                public GUIContent Content { get; private set; }

                public bool IsActive { get; private set; }

                private IEnumerable<GUIRenderer> renderers;
                private TabAttribute tabAttribute;

                private CustomInspector editor;

                public Tab(CustomInspector editor, TabAttribute tabAttribute, IEnumerable<GUIRenderer> renderers)
                {
                    this.editor = editor;
                    this.renderers = renderers;
                    this.tabAttribute = tabAttribute;

                    Content = new GUIContent(tabAttribute.Title, !string.IsNullOrEmpty(tabAttribute.IconName) ? EditorCustomStyles.GetIcon(tabAttribute.IconName) : null);

                    UpdateState();
                }

                public void DrawRenderers()
                {
                    foreach (GUIRenderer renderer in renderers)
                    {
                        renderer.OnGUI();
                    }
                }

                public bool UpdateState()
                {
                    bool tabState = true;

                    if (!string.IsNullOrEmpty(tabAttribute.ShowIf))
                    {
                        List<Type> nestedTypes = editor.NestedClassTypes;
                        foreach (Type type in nestedTypes)
                        {
                            FieldInfo conditionField = type.GetField(tabAttribute.ShowIf, ReflectionUtils.FLAGS_INSTANCE);
                            if (conditionField != null && conditionField.FieldType == PropertyUtility.TYPE_BOOL && !(bool)conditionField.GetValue(editor.target))
                            {
                                tabState = false;

                                break;
                            }

                            PropertyInfo propertyInfo = type.GetProperty(tabAttribute.ShowIf, ReflectionUtils.FLAGS_INSTANCE);
                            if (propertyInfo != null && propertyInfo.PropertyType == PropertyUtility.TYPE_BOOL && !(bool)propertyInfo.GetValue(editor.target))
                            {
                                tabState = false;

                                break;
                            }

                            MethodInfo conditionMethod = type.GetMethod(tabAttribute.ShowIf, ReflectionUtils.FLAGS_INSTANCE);
                            if (conditionMethod != null && conditionMethod.ReturnType == PropertyUtility.TYPE_BOOL && conditionMethod.GetParameters().Length == 0 && !(bool)conditionMethod.Invoke(editor.target, null))
                            {
                                tabState = false;

                                break;
                            }
                        }
                    }

                    bool stateChanged = IsActive != tabState;

                    IsActive = tabState;

                    return stateChanged;
                }

                public void OnGUIChanged()
                {
                    foreach (GUIRenderer renderer in renderers)
                    {
                        renderer.OnGUIChanged();
                    }
                }
            }
        }
    }
}