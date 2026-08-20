using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NebulaSoft
{
    [StaticUnload]
    public static class RemoteConfigController
    {
        private static Dictionary<string, string> configData;

        public static void Init(string config)
        {
            if (!string.IsNullOrEmpty(config))
            {
                configData = ParseJsonLikeString(config);

                Debug.Log("[Remote Config] Remote config initialized successfully.");
            }
            else
            {
                Debug.LogWarning("[Remote Config] Remote config is empty or null.");
            }
        }

        public static T TryGetConfig<T>(string key) where T : RemoteConfigData
        {
            if (configData == null)
                return null;

            if (!configData.TryGetValue(key, out string rawJson))
                return null;

            try
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                };

                return JsonConvert.DeserializeObject<T>(rawJson, settings);
            }
            catch
            {
                Debug.LogError($"[Remote Config] Failed to parse config for key '{key}'. Please check the format of the remote config data.");

                return null;
            }
        }

        private static Dictionary<string, string> ParseJsonLikeString(string input)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            string pattern = @"""(?<key>[^""]+)""\s*:\s*(\{(?<value>(?>[^{}]+|\{(?<open>)|\}(?<-open>))*(?(open)(?!)))\})";
            MatchCollection matches = Regex.Matches(input, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string key = match.Groups["key"].Value;
                string value = match.Groups["value"].Value;

                result[key] = "{" + value + "}";
            }

            return result;
        }

        private static void UnloadStatic()
        {
            configData = null;
        }
    }
}
