using System.IO;

namespace VSRAD.BuildTools
{
    public sealed class IPCBuildResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; }
        public string Stderr { get; set; }

        public static IPCBuildResult Read(Stream stream)
        {
            using (var reader = new BinaryReader(stream))
                return new IPCBuildResult
                {
                    ExitCode = reader.ReadInt32(),
                    Stdout = reader.ReadString(),
                    Stderr = reader.ReadString()
                };
        }

        public byte[] ToArray()
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(ExitCode);
                writer.Write(Stdout);
                writer.Write(Stderr);

                return memStream.ToArray();
            }
        }
    }
}
