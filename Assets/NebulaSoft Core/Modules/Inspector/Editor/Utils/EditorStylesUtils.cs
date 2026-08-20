using UnityEngine;

namespace NebulaSoft
{
    public static class EditorStylesUtils
    {
        public static GUIStyle ConvertButtonToMini(this GUIStyle parentStyle)
        {
            GUIStyle miniButton = new GUIStyle(parentStyle);
            miniButton.padding = new RectOffset(2, 2, 2, 2);
            miniButton.margin = new RectOffset(1, 1, 1, 1);
            miniButton.fontStyle = FontStyle.Bold;
            miniButton.fontSize = 12;

            return miniButton;
        }

        public static GUIStyle GetAligmentStyle(this GUIStyle style, TextAnchor textAnchor)
        {
            GUIStyle tempStyle = new GUIStyle(style);
            tempStyle.alignment = textAnchor;

            return tempStyle;
        }

        public static GUIStyle GetTextColorStyle(this GUIStyle style, Color color)
        {
            GUIStyle tempStyle = new GUIStyle(style);
            tempStyle.normal.textColor = color;

            return tempStyle;
        }

        public static GUIStyle GetTextFontSizeStyle(this GUIStyle style, int fontSize)
        {
            GUIStyle tempStyle = new GUIStyle(style);
            tempStyle.fontSize = fontSize;

            return tempStyle;
        }

        public static GUIStyle GetPaddingStyle(this GUIStyle style, RectOffset padding)
        {
            GUIStyle tempStyle = new GUIStyle(style);
            tempStyle.padding = padding;

            return tempStyle;
        }

        public static Texture2D ChangeColor(this Texture2D texture, Color color)
        {
            if (texture != null)
            {
                Texture2D tempTexture = new Texture2D(texture.width, texture.height);
                tempTexture.SetPixels(texture.GetPixels());

                for (int x = 0; x < tempTexture.width; x++)
                {
                    for (int y = 0; y < tempTexture.height; y++)
                    {
                        Color tempColor = tempTexture.GetPixel(x, y);
                        if (tempColor.a > 0)
                        {
                            tempTexture.SetPixel(x, y, new Color(color.r, color.g, color.b, tempColor.a));
                        }
                    }
                }

                tempTexture.Apply();

                return tempTexture;
            }

            return null;
        }
    }
}
