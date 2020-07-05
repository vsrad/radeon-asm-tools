using Newtonsoft.Json;
using System;

namespace VSRAD.Package.Options
{
    public sealed class MacroItem
    {
        public string Name { get; set; }

        public string Value { get; set; }

        public bool IsPredefined { get; }

        public MacroItem() : this("", "", false) { }

        public MacroItem(string name, string value, bool predefined)
        {
            Name = name;
            Value = value;
            IsPredefined = predefined;
        }
    }

    public sealed class MacroItemConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartArray)
            {
                string name = null, value = null;
                if (reader.Read() && reader.TokenType == JsonToken.String)
                    name = (string)reader.Value;
                if (reader.Read() && reader.TokenType == JsonToken.String)
                    value = (string)reader.Value;
                if (reader.Read() && reader.TokenType == JsonToken.EndArray)
                    return new MacroItem(name, value, predefined: false);
            }
            throw new JsonReaderException($"Encountered unexpected token {reader.TokenType} when reading MacroItem");
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(MacroItem);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var item = (MacroItem)value;
            writer.WriteStartArray();
            writer.WriteValue(item.Name);
            writer.WriteValue(item.Value);
            writer.WriteEndArray();
        }
    }
}
