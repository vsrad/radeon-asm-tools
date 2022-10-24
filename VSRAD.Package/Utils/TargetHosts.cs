using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public bool UsedInActiveProfile { get; }

        public string Formatted => $"{Host}:{Port}";

        public HostItem(string host, bool usedInActiveProfile, string alias = "")
        {
            Alias = alias;
            Host = host;
            UsedInActiveProfile = usedInActiveProfile;
        }

        public HostItem(string input)
        {
            if (TryParseHost(input, out var _, out var host, out var port))
            {
                Host = host;
                Port = port;
                UsedInActiveProfile = false;
            }
        }

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
                port = 9339;

            formatted = $"{hostname}:{port}";
            return true;
        }
    }

    public readonly struct ServerConnectionOptions : IEquatable<ServerConnectionOptions>
    {
        public string RemoteMachine { get; }
        public int Port { get; }

        public ServerConnectionOptions(string remoteMachine = "127.0.0.1", int port = 9339)
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
