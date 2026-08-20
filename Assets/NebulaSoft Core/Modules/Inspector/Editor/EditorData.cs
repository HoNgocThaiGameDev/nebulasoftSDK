using UnityEngine;
using UnityEditor;

namespace NebulaSoft
{
    [HideScriptField]
    [CreateAssetMenu(fileName = "Editor Data", menuName = "Data/Core/Editor/Editor Data")]
    public class EditorData : ScriptableObject
    {
        [BoxGroup("Styles", "Styles")]
        [SerializeField] GUISkin defaultGUISkin;
        [BoxGroup("Styles")]
        [SerializeField] GUISkin proGUISkin;

        [Space]
        [BoxGroup("Styles")]
        [SerializeField] Texture2D[] icons;

        [BoxGroup("Styles")]
        [SerializeField] Texture2D missingIcon;
        public Texture2D MissingIcon => missingIcon;

        [BoxGroup("Hierarchy", "Hierarchy")]
        [SerializeField] HierarchyItem[] hierarchyIcons;
        public HierarchyItem[] HierarchyIcons => hierarchyIcons;

        private Color defaultIconColor = Color.black;
        private Color darkIconColor = Color.white;

        public Color IconColor
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                    return darkIconColor;

                return defaultIconColor;
            }
        }

        public GUISkin Skin
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                    return proGUISkin;

                return defaultGUISkin;
            }
        }

        public Texture2D GetIcon(string name)
        {
            for(int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null && icons[i].name == name)
                    return icons[i];
            }

            return missingIcon;
        }

        [System.Serializable]
        public class HierarchyItem
        {
            [SerializeField] string name;
            public string Name => name;

            [SerializeField] Texture texture;
            public Texture Texture => texture;
        }
    }
}
