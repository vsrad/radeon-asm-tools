using System.IO;
using System;

namespace VSRAD.DebugServer.IPC
{
    public sealed class IPCWriter : BinaryWriter
    {
        public IPCWriter(Stream stream) : base(stream) { }

        public void WriteLengthPrefixedArray(string[] strings)
        {
            Write7BitEncodedInt(strings.Length);
            foreach (string str in strings) Write(str);
        }

        public void WriteLengthPrefixedBlob(byte[] data)
        {
            Write7BitEncodedInt(data.Length);
            Write(data);
        }

        public void Write(DateTime timestamp) =>
            Write(timestamp.ToBinary());
    }

    public sealed class IPCReader : BinaryReader
    {
        public IPCReader(Stream stream) : base(stream) { }

        public string[] ReadLengthPrefixedStringArray()
        {
            var length = Read7BitEncodedInt();
            var strings = new string[length];
            for (int i = 0; i < length; ++i)
                strings[i] = ReadString();
            return strings;
        }

        public byte[] ReadLengthPrefixedBlob()
        {
            var length = Read7BitEncodedInt();
            return ReadBytes(length);
        }

        public DateTime ReadDateTime() =>
            DateTime.FromBinary(ReadInt64());
    }
}
