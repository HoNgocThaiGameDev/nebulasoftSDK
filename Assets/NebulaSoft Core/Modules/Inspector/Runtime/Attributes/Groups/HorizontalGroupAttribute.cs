using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class HorizontalGroupAttribute : GroupAttribute
    {
        public HorizontalGroupAttribute(string id, int order = 0) : base(id, "", order)
        {
        }
    }
}
