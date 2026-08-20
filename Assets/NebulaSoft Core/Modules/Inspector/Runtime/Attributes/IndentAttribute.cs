using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class IndentAttribute : Attribute
    {
        public float Space { get; private set; }

        public IndentAttribute(float space = 24) 
        { 
            Space = space;
        }
    }
}
