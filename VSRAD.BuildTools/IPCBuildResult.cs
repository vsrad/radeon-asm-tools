using System;
using System.IO;
using System.Linq;
using System.Text;

namespace VSRAD.BuildTools
{
    public sealed class IPCBuildResult
    {
        public enum MessageKind { Error = 0, Warning = 1, Note = 2 }

        public sealed class Message
        {
            public MessageKind Kind { get; set; }
            public string Text { get; set; }
            public string SourceFile { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
        }

        public bool Skipped { get; set; }
        public bool Successful { get; set; }
        public string ServerError { get; set; } = "";
        public int ExitCode { get; set; }
        public Message[] ErrorMessages { get; set; } = Array.Empty<Message>();

        public byte[] ToArray()
        {
            using (var memStream = new MemoryStream())
            using (var writer = new BinaryWriter(memStream))
            {
                writer.Write(Skipped);
                writer.Write(Successful);
                writer.Write(ServerError);
                writer.Write(ExitCode);
                writer.Write(ErrorMessages.Length);
                foreach (var message in ErrorMessages)
                {
                    writer.Write((int)message.Kind);
                    writer.Write(message.Text);
                    writer.Write(message.SourceFile);
                    writer.Write(message.Line);
                    writer.Write(message.Column);
                }
                return memStream.ToArray();
            }
        }

        public static IPCBuildResult Read(Stream stream)
        {
            using (var reader = new BinaryReader(stream))
            {
                var buildResult = new IPCBuildResult
                {
                    Skipped = reader.ReadBoolean(),
                    Successful = reader.ReadBoolean(),
                    ServerError = reader.ReadString(),
                    ExitCode = reader.ReadInt32()
                };
                var messages = new Message[reader.ReadInt32()];
                for (int m = 0; m < messages.Length; ++m)
                    messages[m] = new Message
                    {
                        Kind = (MessageKind)reader.ReadInt32(),
                        Text = reader.ReadString(),
                        SourceFile = reader.ReadString(),
                        Line = reader.ReadInt32(),
                        Column = reader.ReadInt32()
                    };
                buildResult.ErrorMessages = messages;
                return buildResult;
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
