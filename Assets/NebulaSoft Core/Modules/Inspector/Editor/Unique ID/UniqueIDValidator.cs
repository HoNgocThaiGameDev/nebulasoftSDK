using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    public static class UniqueIDValidator
    {
        private const string VALIDATOR_TITLE = "ID Validator";

        [MenuItem("Tools/Editor/Validate IDs")]
        public static void ValidateIDs()
        {
            IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(x => !x.IsGenericType && x.IsSubclassOf(typeof(MonoBehaviour)) && x.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(v => v.GetCustomAttribute<UniqueIDAttribute>() != null));

            EditorCoroutines.Execute(ValidateEnumerator(types));
        }

        private static IEnumerator ValidateEnumerator(IEnumerable<Type> types)
        {
            EditorUtility.DisplayProgressBar(VALIDATOR_TITLE, "Validating objects...", 0);

            foreach (var type in types)
            {
                UnityEngine.Object[] objects = GameObject.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);

                foreach(var obj in objects)
                {
                    Selection.activeObject = obj;

                    yield return null;
                }
            }

            EditorUtility.ClearProgressBar();

            yield return null;

            Debug.Log("IDs are validated!");
        }
    }
}
