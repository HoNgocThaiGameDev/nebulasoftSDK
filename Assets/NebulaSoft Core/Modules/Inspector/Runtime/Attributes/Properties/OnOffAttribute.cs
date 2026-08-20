using System;
using UnityEngine;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class OnOffAttribute : PropertyAttribute
    {
        public bool IsWide { get; private set; }

        public OnOffAttribute(bool isWide = false) 
        { 
            IsWide = isWide;
        }
    }
}
