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

        public HostItem(string input)
        {
            if (TryParseHost(input, out var _, out var host, out var port))
            {
                Host = host;
                Port = port;;
            }
        }

        [JsonIgnore]
        public ServerConnectionOptions ConnectionOptions
        {
            get {
                if (TryParseHost(Host, out var _, out var hostname, out var port))
                    return new ServerConnectionOptions(hostname, port);
                else
                    return default;
            }
        }

        public static bool TryParseHost(string input, out string formatted, out string hostname, out ushort port)
        {
            formatted = "";
            hostname = "";
            port = 0;

            var hostnamePort = input.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (hostnamePort.Length == 0)
                return false;

            hostname = hostnamePort[0];
            if (hostnamePort.Length < 2 || !ushort.TryParse(hostnamePort[1], out port))
                port = Options.DefaultOptionValues.Port;

            formatted = $"{hostname}:{port}";
            return true;
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
