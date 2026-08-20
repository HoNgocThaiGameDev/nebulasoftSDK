using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NebulaSoft
{
    [InitializeOnLoad]
    public static class RewardsMapGenerator
    {
        static RewardsMapGenerator()
        {
            Generate();
        }

        [MenuItem("Tools/Rewards/Regenerate Map")]
        public static void Generate()
        {
            Dictionary<Type, Type> currentViewMap = RewardsMap.ViewMap;
            
            TypeCache.TypeCollection rewardTypes = TypeCache.GetTypesDerivedFrom<Reward>();

            List<(Type reward, Type view)> entries = rewardTypes.Where(t => !t.IsAbstract).Select(t =>
            {
                RegisterRewardAttribute attr = t.GetCustomAttribute<RegisterRewardAttribute>();
                
                if (attr == null || attr.ViewType == null) return (null, null);

                return (reward: t, view: attr.ViewType);
            }).Where(x => x.reward != null && x.view != null).ToList();

            if(AreMapsEqual(entries, currentViewMap))
                return;

            string nl = Environment.NewLine;
            string mapLines = string.Join(nl, entries.Select(e => $"            map[typeof({e.reward.FullName})] = typeof({e.view.FullName});"));
            string code = $@"// Auto-generated file. Do not edit.
using System;
using System.Collections.Generic;

namespace NebulaSoft
{{
    public static class RewardsMap
    {{
        public static Dictionary<Type, Type> ViewMap {{ get; }} = GetMap();

        public static Dictionary<Type, Type> GetMap()
        {{
            Dictionary<Type, Type> map = new Dictionary<Type, Type>();
{mapLines}

            return map;
        }}
    }}
}}";
            code = NormalizeEol(code, nl);

            string path = GetTargetPath();

            File.WriteAllText(path, code);

            AssetDatabase.Refresh();

            Debug.Log($"[RewardsMapGenerator] Generated {entries.Count} reward mappings.");
        }

        private static string NormalizeEol(string s, string newline)
        {
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");

            return s.Replace("\n", newline);
        }

        private static bool AreMapsEqual(List<(Type reward, Type view)> a, Dictionary<Type, Type> b)
        {
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;

            foreach ((Type reward, Type view) v in a)
            {
                if (!b.TryGetValue(v.reward, out var val)) return false;
                if (val != v.view) return false;
            }

            return true;
        }

        private static string GetTargetPath()
        {
            MonoScript script = EditorUtils.GetAsset<MonoScript>("MD_GeneratedRewardsMap");
            if (script != null)
                return AssetDatabase.GetAssetPath(script);

            return Path.Combine(CoreEditor.FOLDER_CORE, "Modules", "Reward", "Scripts", "MD_GeneratedRewardsMap.cs");
        }
    }
}
