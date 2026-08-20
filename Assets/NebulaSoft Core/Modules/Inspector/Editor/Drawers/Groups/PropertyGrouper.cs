using System;

namespace NebulaSoft
{
    public abstract class PropertyGrouper
    {
        public abstract void BeginGroup(CustomInspector editor, string groupID, string label);
        public abstract void EndGroup();

        public virtual bool DrawRenderers(CustomInspector editor, string groupID) { return true; }
    }
}
