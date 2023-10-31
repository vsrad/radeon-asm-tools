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
    public sealed class DebuggerOptions : DefaultNotifyPropertyChanged
    {
        [JsonConverter(typeof(BackwardsCompatibilityWatchConverter))]
        public List<Watch> Watches { get; } = new List<Watch>();
        public PinnableMruCollection<string> LastAppArgs { get; } = new PinnableMruCollection<string>();

        public ReadOnlyCollection<string> GetWatchSnapshot() =>
            new ReadOnlyCollection<string>(Watches.Select(w => w.Name).Where(Watch.IsWatchNameValid).Distinct().ToList());

        private uint _nGroups;
        public uint NGroups { get => _nGroups; set => SetField(ref _nGroups, (uint)0); } // always 0 for now as it should be refactored (see ce37993)

        private uint _counter;
        public uint Counter { get => _counter; set => SetField(ref _counter, Math.Max(value, 1)); }

        private string _appArgs = "";
        public string AppArgs { get => _appArgs; set => SetField(ref _appArgs, value); }

        private string _breakArgs = "";
        public string BreakArgs { get => _breakArgs; set => SetField(ref _breakArgs, value); }

        private bool _autosave = true;
        public bool Autosave { get => _autosave; set => SetField(ref _autosave, value); }

        private uint _groupSize = 512;
        [DefaultValue(512)]
        public uint GroupSize { get => _groupSize; set => SetField(ref _groupSize, Math.Max(value, 1)); }

        private uint _waveSize = 64;
        [DefaultValue(64)]
        public uint WaveSize { get => _waveSize; set => SetField(ref _waveSize, value == 32 ? 32u : 64u); }

        private bool _stopOnHit;
        [DefaultValue(false)]
        public bool StopOnHit { get => _stopOnHit; set => SetField(ref _stopOnHit, value); }

        private bool _enableMultipleBreakpoints;
        [DefaultValue(false)]
        public bool EnableMultipleBreakpoints { get => _enableMultipleBreakpoints; set => SetField(ref _enableMultipleBreakpoints, value); }

        private bool _forceOppositeTab = true;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool ForceOppositeTab { get => _forceOppositeTab; set => SetField(ref _forceOppositeTab, value); }

#if true
        [JsonIgnore]
        public bool PreserveActiveDoc => true;
#else
        private bool _preserveActiveDoc = true;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [DefaultValue(true)]
        public bool PreserveActiveDoc { get => _preserveActiveDoc; set => SetField(ref _preserveActiveDoc, value); }
#endif

        public DebuggerOptions() { }

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

            JArray jsonWatchArray = JArray.Load(reader);
            foreach (var jsonWatch in jsonWatchArray)
            {
                VariableType? variableType = null;
                if (jsonWatch["Info"] is JObject infoJson)
                    variableType = infoJson.ToObject<VariableType>();
                else if (jsonWatch["Type"]?.Value<string>() is string type)
                    variableType = new VariableType(category: (VariableCategory)Enum.Parse(typeof(VariableCategory), type), size: 32);

                if ((string)jsonWatch["Name"] is string watchName && variableType is VariableType t)
                    watches.Add(new Watch(watchName, t));
            }

            return watches;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(Watch);

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotImplementedException();
    }
}
