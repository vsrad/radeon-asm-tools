﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VSRAD.Package.DebugVisualizer;
using VSRAD.Package.Utils;

namespace VSRAD.Package.Options
{
    public enum BreakMode
    {
        SingleRoundRobin, SingleRerun, Multiple
    }

    public sealed class DebuggerOptions : DefaultNotifyPropertyChanged
    {
        [JsonConverter(typeof(BackwardsCompatibilityWatchConverter))]
        public List<Watch> Watches { get; } = new List<Watch>();

        public ReadOnlyCollection<string> GetWatchSnapshot() =>
            new ReadOnlyCollection<string>(Watches.Where(w => !w.IsEmpty).Select(w => w.Name).Distinct().ToList());

        public ReadOnlyCollection<string> GetAWatchSnapshot() =>
            new ReadOnlyCollection<string>(Watches.Where(w => w.IsAVGPR).Select(w => w.Name).Distinct().ToList());

        private uint _nGroups;
        public uint NGroups { get => _nGroups; set => SetField(ref _nGroups, (uint)0); } // always 0 for now as it should be refactored (see ce37993)

        private uint _counter;
        public uint Counter { get => _counter; set => SetField(ref _counter, value); }

        private string _appArgs = "";
        public string AppArgs { get => _appArgs; set => SetField(ref _appArgs, value); }

        private string _breakArgs = "";
        public string BreakArgs { get => _breakArgs; set => SetField(ref _breakArgs, value); }

        private bool _autosave = true;
        public bool Autosave { get => _autosave; set => SetField(ref _autosave, value); }

        private bool _singleActiveBreakpoint = false;
        public bool SingleActiveBreakpoint { get => _singleActiveBreakpoint; set => SetField(ref _singleActiveBreakpoint, value); }

        private uint _groupSize = 512;
        public uint GroupSize { get => _groupSize; set => SetField(ref _groupSize, value == 0 ? 512 : value); }

        private BreakMode _breakMode;
        public BreakMode BreakMode { get => _breakMode; set => SetField(ref _breakMode, value); }

        public DebuggerOptions() { }
        public DebuggerOptions(List<Watch> watches) => Watches = watches;
    }

    public sealed class BackwardsCompatibilityWatchConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var watches = existingValue as List<Watch> ?? new List<Watch>();
            if (reader.TokenType != JsonToken.StartArray) return watches;

            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                if (reader.TokenType == JsonToken.String)
                    watches.Add(new Watch((string)reader.Value, VariableType.Hex, isAVGPR: false));
                else if (reader.TokenType == JsonToken.StartObject)
                    watches.Add(JObject.Load(reader).ToObject<Watch>());
            }

            return watches;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(Watch);

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();
    }
}
