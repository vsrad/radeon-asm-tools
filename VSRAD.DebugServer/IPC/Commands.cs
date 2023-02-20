using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;
using VSRAD.DebugServer.SharedUtils;
using System.Text;

namespace VSRAD.DebugServer.IPC.Commands
{
#pragma warning disable CA1028 // Using byte for enum storage because it's used for binary serialization
    public enum CommandType : byte
    {
        Execute = 0,
        FetchMetadata = 1,
        FetchResultRange = 2,
        Deploy = 3,
        ListEnvironmentVariables = 4,
        PutFile = 5,
        CheckOutdatedFiles = 6,
        ListFiles = 7,
        SendFile = 8,
        GetFile = 9,
        PutDirectory = 10
    }
#pragma warning restore CA1028
    public enum HandShakeStatus
    {
        ClientAccepted,
        ClientNotAccepted,
        ServerAccepted,
        ServerNotAccepted
    }

    public interface ICommand
    {
        void Serialize(IPCWriter writer);
    }

    public static class BinaryCommandExtensions
    {
        // TODO: Rename commands and responses to follow a predictable pattern of XCommand -> XResponse
        // (e.g. ExecuteCommand and ExecuteResponse instead of Execute and ExecutionFinished)

        public static ICommand ReadCommand(this IPCReader reader)
        {
            var type = reader.ReadByte();
            switch ((CommandType)type)
            {
                case CommandType.Execute: return Execute.Deserialize(reader);
                case CommandType.FetchMetadata: return FetchMetadata.Deserialize(reader);
                case CommandType.FetchResultRange: return FetchResultRange.Deserialize(reader);
                case CommandType.Deploy: return Deploy.Deserialize(reader);
                case CommandType.ListEnvironmentVariables: return ListEnvironmentVariables.Deserialize(reader);
                case CommandType.PutFile: return PutFileCommand.Deserialize(reader);
                case CommandType.CheckOutdatedFiles: return CheckOutdatedFiles.Deserialize(reader);
                case CommandType.ListFiles: return ListFilesCommand.Deserialize(reader);
                case CommandType.SendFile: return SendFileCommand.Deserialize(reader);
                case CommandType.GetFile: return GetFileCommand.Deserialize(reader);
                case CommandType.PutDirectory: return PutDirectoryCommand.Deserialize(reader);
            }
            throw new InvalidDataException($"Unexpected command type byte: {type}");
        }

        public static void WriteCommand(this IPCWriter writer, ICommand command)
        {
            CommandType type;
            switch (command)
            {
                case Execute _: type = CommandType.Execute; break;
                case FetchMetadata _: type = CommandType.FetchMetadata; break;
                case FetchResultRange _: type = CommandType.FetchResultRange; break;
                case Deploy _: type = CommandType.Deploy; break;
                case ListEnvironmentVariables _: type = CommandType.ListEnvironmentVariables; break;
                case PutFileCommand _: type = CommandType.PutFile; break;
                case CheckOutdatedFiles _: type = CommandType.CheckOutdatedFiles; break;
                case ListFilesCommand _: type = CommandType.ListFiles; break;
                case SendFileCommand _: type = CommandType.SendFile; break;
                case GetFileCommand _: type = CommandType.GetFile; break;
                case PutDirectoryCommand _: type = CommandType.PutDirectory; break;

                default: throw new ArgumentException($"Unable to serialize {command.GetType()}");
            }
            writer.Write((byte)type);
            command.Serialize(writer);
        }
    }

    public sealed class Execute : ICommand
    {
        public string WorkingDirectory { get; set; } = "";

        public string Executable { get; set; } = "";

        public string Arguments { get; set; } = "";

        public bool RunAsAdministrator { get; set; }

        // Note that WaitForCompletion cannot be set to false for remote execution --
        // it is simply not sent so we don't have to change the serialization format,
        // which will break backward compatibility
        public bool WaitForCompletion { get; set; } = true;

        public int ExecutionTimeoutSecs { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "Execute",
            $"WorkingDirectory = {WorkingDirectory}",
            $"Executable = {Executable}",
            $"Arguments = {Arguments}",
            $"RunAsAdministrator = {RunAsAdministrator}",
            $"WaitForCompletion = {WaitForCompletion}",
            $"ExecutionTimeoutSecs = {ExecutionTimeoutSecs}"
        });

        public static Execute Deserialize(IPCReader reader) => new Execute
        {
            WorkingDirectory = reader.ReadString(),
            Executable = reader.ReadString(),
            Arguments = reader.ReadString(),
            RunAsAdministrator = reader.ReadBoolean(),
            ExecutionTimeoutSecs = reader.ReadInt32()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(WorkingDirectory);
            writer.Write(Executable);
            writer.Write(Arguments);
            writer.Write(RunAsAdministrator);
            writer.Write(ExecutionTimeoutSecs);
        }
    }

    public sealed class FetchMetadata : ICommand
    {
        public string[] FilePath { get; set; }

        public bool BinaryOutput { get; set; } = true;

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "FetchMetadata",
            $"FilePath = {string.Join(", ", FilePath)}",
            $"BinaryOutput = {BinaryOutput}"
        });

        public static FetchMetadata Deserialize(IPCReader reader) => new FetchMetadata
        {
            FilePath = reader.ReadLengthPrefixedStringArray(),
            BinaryOutput = reader.ReadBoolean()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.WriteLengthPrefixedArray(FilePath);
            writer.Write(BinaryOutput);
        }
    }

    public sealed class FetchResultRange : ICommand
    {
        public string[] FilePath { get; set; }

        public bool BinaryOutput { get; set; } = true;

        public int ByteOffset { get; set; }

        public int ByteCount { get; set; }
        public int OutputOffset { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "FetchResultRange",
            $"FilePath = {string.Join(", ", FilePath)}",
            $"BinaryOutput = {BinaryOutput}",
            $"ByteOffset = {ByteOffset}",
            $"ByteCount = {ByteCount}",
            $"OutputOffset = {OutputOffset}"
        });

        public static FetchResultRange Deserialize(IPCReader reader) => new FetchResultRange
        {
            FilePath = reader.ReadLengthPrefixedStringArray(),
            BinaryOutput = reader.ReadBoolean(),
            ByteOffset = reader.ReadInt32(),
            ByteCount = reader.ReadInt32(),
            OutputOffset = reader.ReadInt32()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.WriteLengthPrefixedArray(FilePath);
            writer.Write(BinaryOutput);
            writer.Write(ByteOffset);
            writer.Write(ByteCount);
            writer.Write(OutputOffset);
        }
    }

    public sealed class PutFileCommand : ICommand
    {
        public byte[] Data { get; set; }

        public string Path { get; set; }

        public string WorkDir { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "PutFileCommand",
            $"Data = <{Data.Length} bytes>",
            $"Path = {Path}",
            $"WorkDir = {WorkDir}"
        });

        public static PutFileCommand Deserialize(IPCReader reader) => new PutFileCommand
        {
            Data = reader.ReadLengthPrefixedBlob(),
            Path = reader.ReadString(),
            WorkDir = reader.ReadString()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.WriteLengthPrefixedBlob(Data);
            writer.Write(Path);
            writer.Write(WorkDir);
        }
    }

    public sealed class Deploy : ICommand
    {
        public byte[] Data { get; set; }

        public string Destination { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[] {
            "Deploy",
            $"Data = <{Data.Length} bytes>",
            $"Destination Folder = {Destination}"
        });

        public static Deploy Deserialize(IPCReader reader) => new Deploy
        {
            Data = reader.ReadLengthPrefixedBlob(),
            Destination = reader.ReadString()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.WriteLengthPrefixedBlob(Data);
            writer.Write(Destination);
        }
    }

    public sealed class ListEnvironmentVariables : ICommand
    {
        public override string ToString() => "ListEnvironmentVariables";

        public static ListEnvironmentVariables Deserialize(IPCReader _) => new ListEnvironmentVariables();

        public void Serialize(IPCWriter _) { }
    }

    public sealed class ListFilesCommand : ICommand
    {
        public string RemoteWorkDir { get; set; }
        public string ListPath { get; set; }
        public override string ToString() => string.Join(Environment.NewLine, new[] {
            "ListFilesCommand",
            $"RemoteWorkDir = <{RemoteWorkDir}>",
            $"ListPath = <{ListPath}>"           
        });
        public static ListFilesCommand Deserialize(IPCReader reader) => new ListFilesCommand
        {
            RemoteWorkDir = reader.ReadString(),
            ListPath = reader.ReadString(),
        };
        public void Serialize(IPCWriter writer)
        {
            writer.Write(RemoteWorkDir);
            writer.Write(ListPath);
        }
    }

    public sealed class SendFileCommand : ICommand
    {
        public string LocalWorkDir { get; set; }
        public string RemoteWorkDir { get; set; }
        public string DstPath { get; set; }

        public string SrcPath { get; set; }

        public bool UseCompression { get; set; }

        public FileMetadata Metadata { get; set; }

        private static XmlSerializer getFormatter()
        {
            return new XmlSerializer(typeof(FileMetadata));
        }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "SendFileCommand",
            $"RemoteWorkDir = {RemoteWorkDir}",
            $"DstPath = {DstPath}",
            $"SrcPath = {SrcPath}",
            $"UseCompression = {UseCompression}",
            $"Metadata = {Metadata.ToString()}"
        });

        public static SendFileCommand Deserialize(IPCReader reader) => new SendFileCommand
        {
            LocalWorkDir = reader.ReadString(),
            RemoteWorkDir = reader.ReadString(),
            DstPath = reader.ReadString(),
            SrcPath = reader.ReadString(),
            UseCompression = reader.ReadBoolean(),
            Metadata = getFormatter().Deserialize(reader.BaseStream) as FileMetadata
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(LocalWorkDir);
            writer.Write(RemoteWorkDir);
            writer.Write(DstPath);
            writer.Write(SrcPath);
            writer.Write(UseCompression);
            try
            {
                getFormatter().Serialize(writer.BaseStream, Metadata);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }

    public sealed class GetFileCommand : ICommand
    {
        public string LocalWorkDir { get; set; }
        public string RemoteWorkDir { get; set; }
        public string SrcPath { get; set; }

        public string DstPath { get; set; }

        public bool UseCompression { get; set; }

        public FileMetadata Metadata { get; set; }

        private static XmlSerializer getFormatter()
        {
            return new XmlSerializer(typeof(FileMetadata));
        }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "GetFileCommand",
            $"RemoteWorkDir = {RemoteWorkDir}",
            $"SrcPath = {SrcPath}",
            $"ListDir = {DstPath}",
            $"UseCompression = {UseCompression}",
            $"Metadata = {Metadata.ToString()}"
        });

        public static GetFileCommand Deserialize(IPCReader reader) => new GetFileCommand
        {
            LocalWorkDir = reader.ReadString(),
            RemoteWorkDir = reader.ReadString(),
            SrcPath = reader.ReadString(),
            DstPath = reader.ReadString(),
            UseCompression = reader.ReadBoolean(),
            Metadata = getFormatter().Deserialize(reader.BaseStream) as FileMetadata
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(LocalWorkDir);
            writer.Write(RemoteWorkDir);
            writer.Write(SrcPath);
            writer.Write(DstPath);
            writer.Write(UseCompression);
            try
            {
                getFormatter().Serialize(writer.BaseStream, Metadata);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }

    public sealed class PutDirectoryCommand : ICommand
    {
        public string RemoteWorkDir { get; set; }
        public string TargetPath { get; set; }

        public FileMetadata Metadata { get; set; }

        private static XmlSerializer getFormatter()
        {
            return new XmlSerializer(typeof(FileMetadata));
        }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "PutDirectoryCommand",
            $"RemoteWorkDir = {RemoteWorkDir}",
            $"ListDir = {TargetPath}",
            $"Metadata = {Metadata.ToString()}"
        });

        public static PutDirectoryCommand Deserialize(IPCReader reader) => new PutDirectoryCommand
        {
            RemoteWorkDir = reader.ReadString(),
            TargetPath = reader.ReadString(),
            Metadata = getFormatter().Deserialize(reader.BaseStream) as FileMetadata
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(RemoteWorkDir);
            writer.Write(TargetPath);
            try
            {
                getFormatter().Serialize(writer.BaseStream, Metadata);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }

    public sealed class CheckOutdatedFiles : ICommand
    {
        public string RemoteWorkDir { get; set; }

        public string TargetPath { get; set; }

        public List<FileMetadata> Files { get; set; }
        private static XmlSerializer getFormatter()
        {
            return new XmlSerializer(typeof(List<FileMetadata>));
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var file in Files)
            {
                sb.AppendLine(file.ToString());
            }

            return string.Join(Environment.NewLine, new[] {
                "CheckOutdatedFiles",
                $"RemoteWorkDir = {RemoteWorkDir}",
                $"TargetPath = {TargetPath}",
                $"Files = <{sb.ToString()} >"
            });
        }
        
        public static CheckOutdatedFiles Deserialize(IPCReader reader) => new CheckOutdatedFiles
        {
            RemoteWorkDir = reader.ReadString(),
            TargetPath = reader.ReadString(),
            Files = getFormatter().Deserialize(reader.BaseStream) as List<FileMetadata>
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(RemoteWorkDir);
            writer.Write(TargetPath);
            try
            {
                getFormatter().Serialize(writer.BaseStream, Files);
            } catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
