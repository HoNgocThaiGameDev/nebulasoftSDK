using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaSoft
{
    public static class CustomAttributesDatabase
    {
        private static Dictionary<Type, PropertyGrouper> groupersAttributes;
        private static Dictionary<Type, PropertyCondition> conditionAttributes;

        static CustomAttributesDatabase()
        {
            CacheDrawer(out groupersAttributes, typeof(PropertyGrouperAttribute));
            CacheDrawer(out conditionAttributes, typeof(PropertyConditionAttribute));
        }

        public static PropertyGrouper GetGroupAttribute(Type attributeType)
        {
            if (groupersAttributes.TryGetValue(attributeType, out PropertyGrouper grouper)) { return grouper; }

            return null;
        }

        public static PropertyCondition GetConditionAttribute(Type attributeType)
        {
            if (conditionAttributes.TryGetValue(attributeType, out PropertyCondition condition)) { return condition; }

            return null;
        }

        private static void CacheDrawer<T>(out Dictionary<Type, T> dictionary, Type attributeType)
        {
            dictionary = new Dictionary<Type, T>();

            Type drawerType = typeof(T);

            IEnumerable<Type> types = drawerType.Assembly.GetTypes().Where(x => !x.IsAbstract && x.IsSubclassOf(drawerType));
            foreach (Type t in types)
            {
                BaseAttribute attribute = (BaseAttribute)Attribute.GetCustomAttribute(t, attributeType);
                if (attribute != null)
                {
                    dictionary.Add(attribute.TargetAttributeType, (T)Activator.CreateInstance(t));
                }
            }
        }
    }
}

