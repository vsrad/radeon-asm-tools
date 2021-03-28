using System;
using System.Collections.Generic;
using System.IO;
using VSRAD.DebugServer.SharedUtils;

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
            foreach (var entry in dictionary)
            {
                Write(entry.Key);
                Write(entry.Value);
            }
        }

        public void Write(DateTime timestamp)
        {
            if (timestamp != default && timestamp.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Attempting to serialize a non-UTC DateTime", nameof(timestamp));

            Write(timestamp.ToBinary());
        }

        public new void Write7BitEncodedInt(int value) =>
            base.Write7BitEncodedInt(value);

        public void WriteLengthPrefixedFileArray(PackedFile[] files)
        {
            Write7BitEncodedInt(files.Length);
            foreach (var file in files)
            {
                WriteLengthPrefixedBlob(file.Data);
                Write(file.RelativePath);
                Write(file.LastWriteTimeUtc);
            }
        }
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

        public new int Read7BitEncodedInt() =>
            base.Read7BitEncodedInt();

        public PackedFile[] ReadLengthPrefixedFileArray()
        {
            var fileCount = Read7BitEncodedInt();
            var files = new PackedFile[fileCount];
            for (int i = 0; i < fileCount; ++i)
                files[i] = new PackedFile(ReadLengthPrefixedBlob(), ReadString(), ReadDateTime());
            return files;
        }
    }
}
