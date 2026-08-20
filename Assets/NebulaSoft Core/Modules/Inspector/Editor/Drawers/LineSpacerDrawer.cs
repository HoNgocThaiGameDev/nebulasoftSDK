using UnityEngine;
using UnityEditor;

namespace NebulaSoft
{
    [CustomPropertyDrawer(typeof(LineSpacerAttribute))]
    public class LineSpacerDrawer : DecoratorDrawer
    {
        public override void OnGUI(Rect position)
        {
            LineSpacerAttribute lineSpacer = (LineSpacerAttribute)attribute;

            if(!string.IsNullOrEmpty(lineSpacer.title))
            {
                EditorGUI.LabelField(new Rect(position.x, position.y + lineSpacer.height - 12, position.width, 18), lineSpacer.title, EditorStyles.boldLabel);
                EditorGUI.LabelField(new Rect(position.x, position.y + lineSpacer.height, position.width, lineSpacer.height), "", GUI.skin.horizontalSlider);
            }
            else
            {
                EditorGUI.LabelField(new Rect(position.x, position.y, position.width, lineSpacer.height), "", GUI.skin.horizontalSlider);
            }
        }

        public override float GetHeight()
        {
            LineSpacerAttribute lineSpacer = (LineSpacerAttribute)attribute;

            float height = base.GetHeight();
            if (!string.IsNullOrEmpty(lineSpacer.title))
            {
                height += lineSpacer.height;
            }

            return height;
        }
    }
}