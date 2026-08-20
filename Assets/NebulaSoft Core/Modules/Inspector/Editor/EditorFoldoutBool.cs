using UnityEditor;

namespace NebulaSoft
{
    public class EditorFoldoutBool
    {
        public string Key { get; private set; }

        private bool value;
        public bool Value
        {
            get => value;
            set
            {
                this.value = value;

                EditorPrefs.SetBool(Key, value);
            }
        }

        public EditorFoldoutBool(string key, bool defaultValue = true)
        {
            Key = key;
            Value = EditorPrefs.GetBool(key, defaultValue);
        }
    }
}