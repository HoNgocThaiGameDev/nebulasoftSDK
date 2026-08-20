namespace NebulaSoft
{
    [PropertyGrouper(typeof(BoxFoldoutAttribute))]
    public class BoxFoldoutPropertyGrouper : PropertyGrouper
    {
        public override void BeginGroup(CustomInspector editor, string groupID, string label)
        {
            EditorFoldoutBool foldoutBool = editor.GetFoldout(groupID);

            foldoutBool.Value = EditorGUILayoutCustom.BeginExpandBoxGroup(!string.IsNullOrEmpty(label) ? label : groupID, foldoutBool.Value);
        }

        public override void EndGroup()
        {
            EditorGUILayoutCustom.EndBoxGroup();
        }

        public override bool DrawRenderers(CustomInspector editor, string groupID) => editor.GetFoldout(groupID).Value;
    }
}
