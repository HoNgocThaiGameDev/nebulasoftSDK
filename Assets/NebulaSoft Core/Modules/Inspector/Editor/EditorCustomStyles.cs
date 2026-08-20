using System;
using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    [InitializeOnLoad]
    public static class EditorCustomStyles
    {
        public static readonly Color HIERARCHY_COLOR;
        public const string ICON_SPACE = "  ";

        private static EditorData editorData;

        private static GUISkin guiSkin;
        public static GUISkin Skin => guiSkin;

        public static GUIStyle tab;

        public static GUIStyle box;
        public static GUIStyle boxHeader;
        public static GUIStyle boxBottomPanel;

        public static GUIStyle boxGroupBackground;
        public static GUIStyle boxContent;

        public static GUIStyle label;
        public static GUIStyle labelBold;
        public static GUIStyle labelCentered;

        public static GUIStyle labelSmall;
        public static GUIStyle labelSmallBold;

        public static GUIStyle labelMedium;
        public static GUIStyle labelMediumBold;

        public static GUIStyle labelLarge;
        public static GUIStyle labelLargeBold;

        public static GUIStyle button;
        public static GUIStyle buttonMini;
        public static GUIStyle buttonHover;

        public static GUIStyle buttonBlue;
        public static GUIStyle buttonGreen;
        public static GUIStyle buttonRed;

        public static GUIStyle padding00;
        public static GUIStyle padding05;
        public static GUIStyle padding10;

        public static GUIStyle boxCompiling;

        public static GUIStyle windowSpacedContent;

        public static GUIContent foldoutArrowDown;
        public static GUIContent foldoutArrowUp;
        public static GUIContent foldoutArrowRight;

        public static GUIContent menuContent;

        private static int stylesLoadAttempt = 0;

        static EditorCustomStyles()
        {
            HIERARCHY_COLOR = EditorGUIUtility.isProSkin ? new Color(0.76f, 0.76f, 0.76f) : new Color(0.42f, 0.42f, 0.42f);

            editorData = EditorUtils.GetAsset<EditorData>();

            if(editorData == null)
            {
                guiSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Game);

                // Initialize dummy styles to prevent warning messages
                tab = guiSkin.button;
                box = guiSkin.box;
                boxHeader = guiSkin.box;
                boxBottomPanel = guiSkin.box;
                boxGroupBackground = guiSkin.box;
                boxContent = guiSkin.box;
                boxCompiling = guiSkin.box;
                label = guiSkin.label;
                labelBold = guiSkin.label;
                labelCentered = guiSkin.label;
                labelSmall = guiSkin.label;
                labelSmallBold = guiSkin.label;
                labelMedium = guiSkin.label;
                labelMediumBold = guiSkin.label;
                labelLarge = guiSkin.label;
                labelLargeBold = guiSkin.label;
                button = guiSkin.button;
                buttonMini = guiSkin.button;
                buttonHover = guiSkin.button;
                buttonBlue = guiSkin.button;
                buttonGreen = guiSkin.button;
                buttonRed = guiSkin.button;
                padding00 = new GUIStyle();
                padding05 = new GUIStyle();
                padding10 = new GUIStyle();
                windowSpacedContent = new GUIStyle();

                foldoutArrowDown = new GUIContent("↓");
                foldoutArrowUp = new GUIContent("↑");
                foldoutArrowRight = new GUIContent("→");

                menuContent = new GUIContent("", EditorGUIUtility.IconContent("_Menu@2x").image, "Menu");

                stylesLoadAttempt = 0;

                LoadStylesData();

                return;
            }

            guiSkin = editorData.Skin;

            InitStyles();
        }

        public static void CheckStyles()
        {
            if(editorData == null)
            {
                LoadStylesData();
            }
        }

        private static void LoadStylesData()
        {
            if (editorData != null)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += LoadStylesData;

                return;
            }

            editorData = EditorUtils.GetAsset<EditorData>();

            if (editorData != null)
            {
                guiSkin = editorData.Skin;

                InitStyles();
            }
            else
            {
                stylesLoadAttempt++;

                if(stylesLoadAttempt <= 10)
                {
                    EditorApplication.delayCall += LoadStylesData;
                }
                else
                {
                    Debug.LogError("EditorData asset is required for the proper work of the project. Reimport the CORE package to resolve this issue.");
                }
            }
        }

        public static void InitStyles()
        {
            if (guiSkin == null) return;

            tab = guiSkin.GetStyle("tab");

            box = guiSkin.box;
            boxHeader = guiSkin.GetStyle("boxHeader");
            boxBottomPanel = guiSkin.GetStyle("boxBottomPanel");

            boxGroupBackground = new GUIStyle(box);
            boxGroupBackground.padding = new RectOffset(4, 4, 0, 0);
            boxGroupBackground.margin = new RectOffset(0, 0, 4, 4);

            boxContent = new GUIStyle();
            boxContent.padding = new RectOffset(2, 2, 4, 6);

            label = guiSkin.label;
            labelBold = new GUIStyle(label);
            labelBold.fontStyle = FontStyle.Bold;

            labelCentered = new GUIStyle(label);
            labelCentered.alignment = TextAnchor.MiddleCenter;

            PrepareLabelStyle(ref labelSmall, ref labelSmallBold, 9);
            PrepareLabelStyle(ref labelMedium, ref labelMediumBold, 14);
            PrepareLabelStyle(ref labelLarge, ref labelLargeBold, 16);

            button = guiSkin.button;
            buttonHover = guiSkin.GetStyle("buttonHover");

            buttonMini = new GUIStyle(button);
            buttonMini.padding = new RectOffset(3, 3, 3, 3);
            buttonMini.margin = new RectOffset(1, 1, 2, 1);
            buttonMini.fontSize = 10;
            buttonMini.alignment = TextAnchor.MiddleCenter;

            buttonBlue = guiSkin.GetStyle("buttonBlue");
            buttonGreen = guiSkin.GetStyle("buttonGreen");
            buttonRed = guiSkin.GetStyle("buttonRed");

            padding00 = new GUIStyle().GetPaddingStyle(new RectOffset(0, 0, 0, 0));
            padding05 = new GUIStyle().GetPaddingStyle(new RectOffset(0, 0, 5, 5));
            padding10 = new GUIStyle().GetPaddingStyle(new RectOffset(0, 0, 10, 10));

            boxCompiling = guiSkin.GetStyle("boxCompiling");

            windowSpacedContent = new GUIStyle();
            windowSpacedContent.padding = new RectOffset(15, 4, 5, 5);

            foldoutArrowDown = new GUIContent(GetIcon("foldout_arrow_down"));
            foldoutArrowUp = new GUIContent(GetIcon("foldout_arrow_up"));
            foldoutArrowRight = new GUIContent(GetIcon("foldout_arrow_right"));

            menuContent = new GUIContent("", EditorGUIUtility.IconContent("_Menu@2x").image, "Menu");
        }

        public static Texture2D GetMissingIcon()
        {
            if (editorData == null) return null;

            return editorData.MissingIcon;
        }

        public static Texture2D GetIcon(string name)
        {
            if (editorData == null) return null;

            return editorData.GetIcon(name);
        }

        public static Texture2D GetIcon(string name, Color color)
        {
            if (editorData == null) return null;

            return editorData.GetIcon(name).ChangeColor(color);
        }

        private static void PrepareLabelStyle(ref GUIStyle labelStyle, ref GUIStyle boltLabelStyle, int fontSize)
        {
            labelStyle = new GUIStyle(label);
            labelStyle.fontSize = fontSize;

            boltLabelStyle = new GUIStyle(labelStyle);
            boltLabelStyle.fontStyle = FontStyle.Bold;
        }
    }
}
