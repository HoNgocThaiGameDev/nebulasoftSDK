using System;
using UnityEngine;

namespace NebulaSoft
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class CreateScriptableObjectAttribute : PropertyAttribute
    {
        public CreateScriptableObjectAttribute() { }
    }
}
