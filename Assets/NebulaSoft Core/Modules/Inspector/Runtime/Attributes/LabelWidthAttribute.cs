using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class LabelWidthAttribute : Attribute
    {
        private float width;
        public float Width => width;

        public LabelWidthAttribute(float width)
        {
            this.width = width;
        }
    }
}
