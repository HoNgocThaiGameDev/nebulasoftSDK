using UnityEditor;

namespace NebulaSoft
{
    [PropertyGrouper(typeof(FoldoutAttribute))]
    public class FoldoutPropertyGrouper : PropertyGrouper
    {
        public override void BeginGroup(CustomInspector editor, string groupID, string label)
        {
            EditorFoldoutBool foldoutBool = editor.GetFoldout(groupID);

            foldoutBool.Value = EditorGUILayout.Foldout(foldoutBool.Value, !string.IsNullOrEmpty(label) ? label : groupID, true);

            EditorGUI.indentLevel++;

        }

        public override void EndGroup()
        {
            EditorGUI.indentLevel--;
        }

        public override bool DrawRenderers(CustomInspector editor, string groupID) => editor.GetFoldout(groupID).Value;
    }
}
