using System;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class HelpButtonAttribute : Attribute
    {
        public string Name { get; private set; }
        public string URL { get; private set; }

        public HelpButtonAttribute(string name, string url)
        {
            Name = name;
            URL = url;
        }
    }
}
