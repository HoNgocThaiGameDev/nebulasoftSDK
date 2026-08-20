using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ShowNonSerializedAttribute : Attribute { }
}
