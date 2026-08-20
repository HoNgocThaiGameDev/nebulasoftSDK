using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    [CustomEditor(typeof(RemoteConfigHandler), true)]
    public class RemoteConfigHandlerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (GUILayout.Button("Copy Config"))
            {
                string configText = GetAllConfigsAsJson();

                EditorGUIUtility.systemCopyBuffer = configText;

                Debug.Log("Remote config copied to clipboard");
            }
        }

        public static string GetAllConfigsAsJson()
        {
            Dictionary<string, string> configObjects = new Dictionary<string, string>();

            Type interfaceType = typeof(RemoteConfigData);
            Assembly[] types = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in types)
            {
                Type[] exportedTypes;

                try
                {
                    exportedTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    exportedTypes = ex.Types!;
                }

                foreach (var type in exportedTypes)
                {
                    if (type == null || !interfaceType.IsAssignableFrom(type) || type.IsAbstract || !type.IsClass)
                        continue;

                    RemoteConfigData instance = Activator.CreateInstance(type) as RemoteConfigData;

                    if (instance == null)
                        continue;

                    string key = instance.Key;

                    JsonSerializerSettings settings = new JsonSerializerSettings
                    {
                        Formatting = instance.PrettyPrint ? Formatting.Indented : Formatting.None,
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    string json = JsonConvert.SerializeObject(instance, settings);
                    configObjects[key] = json;
                }
            }

            return ToPseudoJsonBody(configObjects);
        }

        public static string ToPseudoJsonBody(Dictionary<string, string> dict)
        {
            List<string> entries = new List<string>();

            foreach (var kvp in dict)
            {
                string keyEscaped = EscapeString(kvp.Key);
                string entry = $"\"{keyEscaped}\": {kvp.Value}";
                entries.Add(entry);
            }

            return string.Join(",\n", entries);
        }

        private static string EscapeString(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}