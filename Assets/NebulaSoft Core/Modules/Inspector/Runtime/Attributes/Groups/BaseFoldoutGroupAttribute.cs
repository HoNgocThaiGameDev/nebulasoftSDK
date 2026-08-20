using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public abstract class BaseFoldoutGroupAttribute : GroupAttribute
    {
        public bool DefaultState { get; private set; } = true;

        public BaseFoldoutGroupAttribute(string id, string label = "", int order = 0, bool defaultState = true) : base(id, label, order)
        {
            DefaultState = defaultState;
        }
    }
}
