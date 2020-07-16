using Newtonsoft.Json;
using System;
using System.ComponentModel;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public sealed class MacroItem : DefaultNotifyPropertyChanged, IDataErrorInfo
    {
        private string _name;
        public string Name { get => _name; set => SetField(ref _name, value); }

        private string _value;
        public string Value { get => _value; set => SetField(ref _value, value); }

        public bool IsUserDefined { get; }

        public MacroItem() : this("", "", true) { }

        public MacroItem(string name, string value, bool userDefined)
        {
            Name = name;
            Value = value;
            IsUserDefined = userDefined;
        }

        string IDataErrorInfo.Error => ((IDataErrorInfo)this)[nameof(Name)];

        string IDataErrorInfo.this[string columnName]
        {
            get
            {
                if (columnName == nameof(Name) && string.IsNullOrEmpty(Name))
                    return "Macro name cannot be empty";
                return "";
            }
        }

        public override bool Equals(object obj) =>
            obj is MacroItem item && item.Name == Name && item.Value == Value && item.IsUserDefined == IsUserDefined;

        // Note: This implementation makes it a bad idea to use MacroItem as a key in a hash-based collection,
        // but it prevents DataGrid (MacroListEditor) from misidentifying MacroItems when the name (and a name-based hashcode) changes
        public override int GetHashCode() => IsUserDefined ? 1 : 0;
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
                    return new MacroItem(name, value, userDefined: true);
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
