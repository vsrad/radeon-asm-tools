using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace VSRAD.DebugServer.SharedUtils
{
    public static class TextDebuggerOutputParser
    {
        public static async Task<byte[]> ReadTextOutputAsync(Stream stream, int offsetBeforeData, int dataOffset = 0, int byteCount = 0)
        {
            if (byteCount == 0)
                byteCount = int.MaxValue; // We'll break out of the read loop upon encountering EOF

            using (var reader = new StreamReader(stream))
            {
                for (int i = 0; i < offsetBeforeData; i++)
                    await reader.ReadLineAsync();

                var values = new List<uint>();
                var offset = (dataOffset % 4 == 0)
                    ? dataOffset
                    : dataOffset - (4 - dataOffset % 4);
                var read = 0;
                for (; read < offset + byteCount; read += 4)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        break;
                    if (read < offset)
                        continue;
                    if (uint.TryParse(line.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                        values.Add(hex);
                }
                byte[] data = new byte[values.Count * 4];
                Buffer.BlockCopy(values.ToArray(), 0, data, 0, data.Length);
                return data;
            }
        }
    }
}
