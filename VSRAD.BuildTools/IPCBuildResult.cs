using System.IO;
using System.Linq;
using System.Text;

namespace VSRAD.BuildTools
{
    public sealed class IPCBuildResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; }
        public string Stderr { get; set; }
        public string ServerError { get; set; }

        public static IPCBuildResult Read(Stream stream)
        {
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadBoolean()) // success
                    return new IPCBuildResult
                    {
                        ExitCode = reader.ReadInt32(),
                        Stdout = reader.ReadString(),
                        Stderr = reader.ReadString()
                    };
                else
                    return new IPCBuildResult
                    {
                        ServerError = reader.ReadString()
                    };
            }
        }

        public byte[] ToArray()
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                if (ServerError == null)
                {
                    writer.Write(true); // success
                    writer.Write(ExitCode);
                    writer.Write(Stdout);
                    writer.Write(Stderr);
                }
                else
                {
                    writer.Write(false); // success
                    writer.Write(ServerError);
                }

                return memStream.ToArray();
            }
        }

        public static string GetIPCPipeName(string project)
        {
            using (var sha512 = new System.Security.Cryptography.SHA512Managed())
            {
                var hash = sha512.ComputeHash(Encoding.UTF8.GetBytes(project));
                return "vsrad-" + string.Join("", hash.Select((b) => b.ToString("x2")));
            }
        }
    }
}
