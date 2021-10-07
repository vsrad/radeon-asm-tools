using System;
using System.Collections.Generic;

namespace VSRAD.DebugServer.IPC
{
#pragma warning disable CA1028 // Using byte for enum storage because it's used for binary serialization
    public enum ServerCapability : byte
    {
        Base = 0
    }

    public enum ServerPlatform : byte
    {
        Windows = 0,
        Linux = 1
    }
#pragma warning restore CA1028

    public sealed class CapabilityInfo
    {
        public string Version { get; }
        public ServerPlatform Platform { get; }
        public HashSet<ServerCapability> Capabilities { get; }

        public static readonly HashSet<ServerCapability> LatestServerCapabilities = new HashSet<ServerCapability>(new[]
        {
            ServerCapability.Base
        });

        public CapabilityInfo(string version, ServerPlatform platform, HashSet<ServerCapability> capabilities)
        {
            Version = version;
            Platform = platform;
            Capabilities = capabilities;
        }

        public bool IsUpToDate()
        {
            return Capabilities.SetEquals(LatestServerCapabilities);
        }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            $"Version = {Version}",
            $"Platform = {Platform}",
            $"Capabilities = {string.Join(", ", Capabilities)}"
        });

        public static CapabilityInfo Deserialize(IPCReader reader)
        {
            var version = reader.ReadString();
            var platform = (ServerPlatform)reader.ReadByte();
            var capabilityCount = reader.Read7BitEncodedInt();
            var capabilities = new HashSet<ServerCapability>();
            for (int i = 0; i < capabilityCount; ++i)
                capabilities.Add((ServerCapability)reader.ReadByte());
            return new CapabilityInfo(version, platform, capabilities);
        }

        public void Serialize(IPCWriter writer)
        {
            writer.Write(Version);
            writer.Write((byte)Platform);
            writer.Write7BitEncodedInt(Capabilities.Count);
            foreach (var cap in Capabilities)
                writer.Write((byte)cap);
        }
    }
}
