using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class FoldoutAttribute : BaseFoldoutGroupAttribute
    {
        public FoldoutAttribute(string id, string label = "", int order = 0, bool defaultState = true) : base(id, label, order, defaultState)
        {

        }
    }
}
