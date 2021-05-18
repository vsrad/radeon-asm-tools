using System.Collections.Generic;
using System.IO;

namespace VSRAD.DebugServer.SharedUtils
{
    public static class TextDebuggerOutputParser
    {
        // Amount of bytes to buffer while reading the file
        // (In microbenchmarks for ReadTextOutput, smaller buffer sizes lead to a small slowdown, while larger buffer sizes don't seem to measurably improve performance)
        private const int _readBufferSize = 16384;

        private const int _charsInByte = 256;

        public static List<uint> ReadTextOutput(string filePath, int lineOffset, int lineCount = 0)
        {
            sbyte[] hexDigitLookup = new sbyte[_charsInByte];
            for (int i = 0; i < _charsInByte; ++i)
            {
                if (i >= '0' && i <= '9')
                    hexDigitLookup[i] = (sbyte)(i - '0');
                else if (i >= 'A' && i <= 'F')
                    hexDigitLookup[i] = (sbyte)(i - 'A' + 10);
                else if (i >= 'a' && i <= 'f')
                    hexDigitLookup[i] = (sbyte)(i - 'a' + 10);
                else
                    hexDigitLookup[i] = -1;
            }

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _readBufferSize, FileOptions.SequentialScan))
            {
                if (lineCount == 0)
                    lineCount = int.MaxValue; // Read until EOF

                var values = new List<uint>();

                byte[] buffer = new byte[_readBufferSize];
                int bytesRead;

                bool scannedCr = false, scanningValue = false;
                uint value = 0;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < bytesRead; ++i)
                    {
                        if (scannedCr && buffer[i] == '\n') // Consume crlf (\r\n)
                        {
                            scannedCr = false;
                            continue;
                        }

                        scannedCr = buffer[i] == '\r';
                        bool scannedNewline = scannedCr || buffer[i] == '\n';

                        if (lineOffset > 0)
                        {
                            if (scannedNewline)
                                lineOffset--;
                            continue;
                        }

                        if (scannedNewline)
                        {
                            if (scanningValue)
                            {
                                values.Add(value);
                                value = 0;
                                scanningValue = false;

                                if (values.Count >= lineCount)
                                    return values;
                            }
                            continue;
                        }

                        sbyte digit = hexDigitLookup[buffer[i]];
                        if (digit >= 0)
                        {
                            scanningValue = true;
                            value = value * 16 + (uint)digit;
                        }
                    }
                }

                if (scanningValue)
                    values.Add(value);

                return values;
            }
        }
    }
}
