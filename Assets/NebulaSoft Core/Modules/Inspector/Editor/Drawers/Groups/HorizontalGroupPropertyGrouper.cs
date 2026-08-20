using UnityEditor;

namespace NebulaSoft
{
    [PropertyGrouper(typeof(HorizontalGroupAttribute))]
    public class HorizontalGroupPropertyGrouper : PropertyGrouper
    {
        public override void BeginGroup(CustomInspector editor, string groupID, string label)
        {
            EditorGUILayout.BeginHorizontal();
        }

        public override void EndGroup()
        {
            EditorGUILayout.EndHorizontal();
        }
    }
}
