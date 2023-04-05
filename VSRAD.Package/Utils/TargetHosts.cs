using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VSRAD.Package.Utils
{
    public sealed class HostItem : DefaultNotifyPropertyChanged
    {
        private string _host = "";
        public string Host { get => _host; set => SetField(ref _host, value); }

        private ushort _port = 0;
        public ushort Port { get => _port; set => SetField(ref _port, value); }

        private string _alias = "";
        public string Alias { get => _alias; set => SetField(ref _alias, value); }

        [JsonIgnore]
        private static readonly Regex _aliasHostPortRegex = new Regex(@"(?<alias>\w+) \((?<hostAndPort>.+)\)",
                                                                    RegexOptions.Compiled | RegexOptions.Singleline);

        [JsonIgnore]
        public string Formatted => $"{Host}:{Port}";

        [JsonIgnore]
        public string Name => string.IsNullOrWhiteSpace(Alias)
                                ? Formatted
                                : $"{Alias} ({Formatted})";

        [JsonConstructor]
        public HostItem(string host, ushort port, string alias = "")
        {
            Host = host;
            Port = port;
            Alias = alias;
        }

        [JsonIgnore]
        public ServerConnectionOptions ConnectionOptions => new ServerConnectionOptions(Host, Port);

        public static HostItem TryParseHost(string input)
        {
            var alias = "";
            var match = _aliasHostPortRegex.Match(input);
            if (match.Success)
            {
                alias = match.Groups["alias"].ToString();
                input = match.Groups["hostAndPort"].ToString();
            }
            var hostnamePort = input.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (hostnamePort.Length < 2)
                return default(HostItem);

            var hostname = hostnamePort[0];
            if (!ushort.TryParse(hostnamePort[1], out var port))
                port = Options.DefaultOptionValues.Port;
            return new HostItem(hostname, port, alias);
        }

        // we provide custom converter here instead of one defined in MruCollection.cs
        // because we want seamless migration from old config files, where hosts
        // are serialiazed as strings ("127.0.0.1:9339")
        public sealed class HostItemMruCollectionConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(MruCollection<HostItem>);

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                try // to parse new format
                {
                    var items = serializer.Deserialize<List<HostItem>>(reader);
                    ((MruCollection<HostItem>)existingValue).AddRange(items);
                    return existingValue;
                }
                catch (Exception e) when (e.InnerException.Message == /* old format in config file */
                                "Could not cast or convert from System.String to VSRAD.Package.Utils.HostItem.")
                {
                    while (reader.TokenType == JsonToken.String)
                    {
                        var item = serializer.Deserialize<string>(reader);
                        ((MruCollection<HostItem>)existingValue).Add(TryParseHost(item));
                        reader.Read();
                    }
                    return existingValue;
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
                serializer.Serialize(writer, (MruCollection<HostItem>) value);
        }
    }

    public readonly struct ServerConnectionOptions : IEquatable<ServerConnectionOptions>
    {
        public string RemoteMachine { get; }
        public int Port { get; }

        public ServerConnectionOptions(string remoteMachine = Options.DefaultOptionValues.RemoteMachineAdress,
                                                int port = Options.DefaultOptionValues.Port)
        {
            RemoteMachine = remoteMachine;
            Port = port;
        }

        public override string ToString() => $"{RemoteMachine}:{Port}";

        public bool Equals(ServerConnectionOptions s) => RemoteMachine == s.RemoteMachine && Port == s.Port;
        public override bool Equals(object o) => o is ServerConnectionOptions s && Equals(s);
        public override int GetHashCode() => (RemoteMachine, Port).GetHashCode();
        public static bool operator ==(ServerConnectionOptions left, ServerConnectionOptions right) => left.Equals(right);
        public static bool operator !=(ServerConnectionOptions left, ServerConnectionOptions right) => !(left == right);
    }
}
