using Newtonsoft.Json;
using System;

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
        public string Formatted => $"{Host}:{Port}";

        [JsonIgnore]
        public string Name => string.IsNullOrWhiteSpace(Alias)
                                ? Formatted
                                : Alias;

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
            var hostnamePort = input.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (hostnamePort.Length < 2)
                return default(HostItem);

            var hostname = hostnamePort[0];
            if (!ushort.TryParse(hostnamePort[1], out var port))
                port = Options.DefaultOptionValues.Port;
            return new HostItem(hostname, port);
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
