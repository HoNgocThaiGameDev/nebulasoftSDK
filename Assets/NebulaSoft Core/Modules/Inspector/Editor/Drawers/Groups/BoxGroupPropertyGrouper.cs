using UnityEngine;

namespace NebulaSoft
{
    [PropertyGrouper(typeof(BoxGroupAttribute))]
    public class BoxGroupPropertyGrouper : PropertyGrouper
    {
        public override void BeginGroup(CustomInspector editor, string groupID, string label)
        {
            EditorGUILayoutCustom.BeginBoxGroup(label);
        }

        public override void EndGroup()
        {
            EditorGUILayoutCustom.EndBoxGroup();
        }
    }
}
