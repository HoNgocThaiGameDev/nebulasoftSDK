using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class GroupAttribute : Attribute
    {
        public string ID { get; private set; }
        public string Label { get; private set; }
        public int Order { get; private set; }

        public GroupAttribute(string id, string label = "", int order = 0)
        {
            ID = id;
            Label = label;
            Order = order;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }
    }
}