using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class LabelAttribute : Attribute
    {
        private string label;
        public string Label => label;

        public LabelAttribute(string label)
        {
            this.label = label;
        }
    }
}
