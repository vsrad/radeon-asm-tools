using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public PinnableMruCollection<string> LastAppArgs { get; } = new PinnableMruCollection<string>();

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
        [DefaultValue(512)]
        public uint GroupSize { get => _groupSize; set => SetField(ref _groupSize, Math.Max(value, 1)); }

        private uint _waveSize = 64;
        [DefaultValue(64)]
        public uint WaveSize { get => _waveSize; set => SetField(ref _waveSize, Math.Max(value, 1)); }

        private BreakMode _breakMode;
        public BreakMode BreakMode { get => _breakMode; set => SetField(ref _breakMode, value); }

        private bool _forceOppositeTab = true;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool ForceOppositeTab { get => _forceOppositeTab; set => SetField(ref _forceOppositeTab, value); }
        
        private bool _preserveActiveDoc = true;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool PreserveActiveDoc { get => _preserveActiveDoc; set => SetField(ref _preserveActiveDoc, value); }

        public DebuggerOptions() { }
        public DebuggerOptions(List<Watch> watches) => Watches = watches;

        public void UpdateLastAppArgs()
        {
            if (string.IsNullOrWhiteSpace(AppArgs)) return;
            LastAppArgs.AddElement(AppArgs);
        }
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
                    watches.Add(new Watch((string)reader.Value, new VariableInfo { Type = VariableType.Hex, Size = 32 }, isAVGPR: false));
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
