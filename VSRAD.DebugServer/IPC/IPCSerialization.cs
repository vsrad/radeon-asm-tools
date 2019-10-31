using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

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

        public void WriteLengthPrefixedDict(IReadOnlyDictionary<string, string> dictionary)
        {
            Write7BitEncodedInt(dictionary.Count);
            foreach (var (key, value) in dictionary)
            {
                Write(key);
                Write(value);
            }
        }

        public void Write(DateTime timestamp) =>
            Write(timestamp.ToBinary());
    }

    public sealed class IPCReader : BinaryReader
    {
        public IPCReader(Stream stream) : base(stream) { }
        private static readonly Regex envMacroRegex = new Regex(@"\$ENVR\(([^)]+)\)", RegexOptions.Compiled);

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

        public Dictionary<string, string> ReadLengthPrefixedStringDict()
        {
            var count = Read7BitEncodedInt();
            var dict = new Dictionary<string, string>(count);
            for (int i = 0; i < count; ++i)
                dict[ReadString()] = ReadString();
            return dict;
        }

        public DateTime ReadDateTime() =>
            DateTime.FromBinary(ReadInt64());

        public override string ReadString()
        {
            var rawString = base.ReadString();
            foreach(Match m in envMacroRegex.Matches(rawString))
            {
                var envName = m.Groups[1].Value;
                var envValue = Environment.GetEnvironmentVariable(envName);
                rawString = rawString.Replace(m.Value, envValue);
            }
            return rawString;
        }
    }
}
