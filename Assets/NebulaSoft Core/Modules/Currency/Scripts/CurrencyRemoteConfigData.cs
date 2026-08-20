using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NebulaSoft
{
    [System.Serializable]
    [JsonConverter(typeof(CurrencyContainerConverter))]
    public class CurrencyRemoteConfigData : RemoteConfigData
    {
        public override string Key => "currencies";
        public override bool PrettyPrint => true;

        [JsonProperty("defaultCount")]
        public List<Dictionary<string, int>> SerializedList => GenerateSerializedList();

        [JsonIgnore]
        public List<Currency> array = new()
        {
            new Currency { name = "Coins", defaultCount = 0 },
        };

        private List<Dictionary<string, int>> GenerateSerializedList()
        {
            List<Dictionary<string, int>> list = new List<Dictionary<string, int>>();
            foreach (Currency c in array)
            {
                list.Add(new Dictionary<string, int> { [c.name] = c.defaultCount });
            }

            return list;
        }

        public Currency GetCurrencyOverride(CurrencyType currencyType)
        {
            if (array != null)
            {
                string currencyName = currencyType.ToString();
                foreach (Currency currency in array)
                {
                    if (currency.name == currencyName)
                    {
                        return currency;
                    }
                }
            }

            return null;
        }

        public class CurrencyContainerConverter : JsonConverter<CurrencyRemoteConfigData>
        {
            public override void WriteJson(JsonWriter writer, CurrencyRemoteConfigData value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("defaultCount");
                writer.WriteStartArray();

                foreach (var currency in value.array)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName(currency.name);
                    writer.WriteValue(currency.defaultCount);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            public override CurrencyRemoteConfigData ReadJson(JsonReader reader, Type objectType, CurrencyRemoteConfigData existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                CurrencyRemoteConfigData container = new CurrencyRemoteConfigData();
                container.array.Clear();

                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "defaultCount")
                    {
                        reader.Read(); // Move to StartArray

                        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                        {
                            if (reader.TokenType == JsonToken.StartObject)
                            {
                                reader.Read(); // Move to PropertyName
                                string name = reader.Value.ToString();
                                reader.Read(); // Move to Value
                                int count = Convert.ToInt32(reader.Value);
                                reader.Read(); // Move to EndObject

                                container.array.Add(new Currency { name = name, defaultCount = count });
                            }
                        }
                    }

                    if (reader.TokenType == JsonToken.EndObject)
                        break;
                }

                return container;
            }
        }

        [System.Serializable]
        public class Currency
        {
            public string name;
            public int defaultCount;
        }
    }
}
