using System;
using System.Collections.Generic;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class TabAttribute : Attribute
    {
        public string Title { get; private set; }
        public string IconName { get; private set; }

        public string ShowIf { get; private set; }

        public TabAttribute(string title, string iconName = "", string showIf = "")
        {
            Title = title;
            IconName = iconName;
            ShowIf = showIf;
        }

        public override int GetHashCode()
        {
            return Title.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return obj.GetHashCode() == GetHashCode();
        }
    }
}
