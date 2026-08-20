using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace NebulaSoft
{
    [InitializeOnLoad]
    public static class EditorSkinsProvider
    {
        private static List<AbstractSkinDatabase> skinsDatabases;
        public static List<AbstractSkinDatabase> SkinsDatabases => skinsDatabases;

        private static IEnumerable<Type> registeredTypes;

        static EditorSkinsProvider()
        {
            skinsDatabases = new List<AbstractSkinDatabase>();

            registeredTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes()).Where(p => !p.IsAbstract && p.IsSubclassOf(typeof(AbstractSkinDatabase)));

            foreach(Type type in registeredTypes)
            {
                Object database = EditorUtils.GetAsset(type);
                if(database != null)
                {
                    skinsDatabases.Add((AbstractSkinDatabase)database);
                }
            }
        }

        public static void AddDatabase(AbstractSkinDatabase database)
        {
            if (HasSkinsProvider(database)) return;

            skinsDatabases.Add(database);
        }

        public static AbstractSkinDatabase GetSkinsProvider(Type providerType)
        {
            if (!skinsDatabases.IsNullOrEmpty())
            {
                foreach (AbstractSkinDatabase database in skinsDatabases)
                {
                    if (database.GetType() == providerType)
                        return database;
                }
            }

            return null;
        }

        public static bool HasSkinsProvider(AbstractSkinDatabase provider)
        {
            if (!skinsDatabases.IsNullOrEmpty())
            {
                foreach (AbstractSkinDatabase database in skinsDatabases)
                {
                    if (database == provider)
                        return true;
                }
            }

            return false;
        }
    }
}
