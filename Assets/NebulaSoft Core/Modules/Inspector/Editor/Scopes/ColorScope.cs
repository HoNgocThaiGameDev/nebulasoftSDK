using UnityEngine;

namespace NebulaSoft
{
    public class ColorScope : GUI.Scope
    {
        private readonly Color defaultColor;

        public ColorScope(Color newColor)
        {
            defaultColor = GUI.color;

            GUI.color = newColor;
        }

        protected override void CloseScope()
        {
            GUI.color = defaultColor;
        }
    }
}