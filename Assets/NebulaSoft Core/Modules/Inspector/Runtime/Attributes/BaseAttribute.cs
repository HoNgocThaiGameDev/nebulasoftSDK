using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public abstract class BaseAttribute : Attribute
    {
        public Type TargetAttributeType { get; private set; }

        public BaseAttribute(Type targetAttributeType)
        {
            TargetAttributeType = targetAttributeType;
        }
    }
}
