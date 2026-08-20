using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class InlineButtonAttribute : Attribute
    {
        public string Title { get; private set; }
        public string MethodName { get; private set; }

        public InlineButtonAttribute(string title, string methodName)
        {
            Title = title;
            MethodName = methodName;
        }
    }
}
