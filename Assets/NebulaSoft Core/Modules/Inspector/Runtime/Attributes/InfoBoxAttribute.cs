using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class InfoBoxAttribute : Attribute
    {
        public InfoBoxAttribute(string text, InfoBoxType type = InfoBoxType.Normal)
        {
            Text = text;
            Type = type;
        }

        public string Text { get; private set; }
        public InfoBoxType Type { get; private set; }
    }

    public enum InfoBoxType
    {
        Normal,
        Warning,
        Error
    }
}
