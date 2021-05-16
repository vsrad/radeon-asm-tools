using System.Collections.Generic;
using System.IO;

namespace VSRAD.DebugServer.SharedUtils
{
    public static class TextDebuggerOutputParser
    {
        public static List<uint> ReadTextOutput(string filePath, int lineOffset, int lineCount = 0)
        {
            sbyte[] hexDigitLookup = new sbyte[256];
            for (int i = 0; i < 256; ++i)
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

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, FileOptions.SequentialScan))
            {
                if (lineCount == 0)
                    lineCount = int.MaxValue; // Read until EOF

                var values = new List<uint>();

                byte[] buffer = new byte[16384];
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
