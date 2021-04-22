using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using VSRAD.DebugServer.SharedUtils;
using static VSRAD.DebugServer.IPC.CapabilityInfo;

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
        PutDirectory = 6,
        ListFiles = 7,
        GetFiles = 8,
        GetServerCapabilities = 9,
        ExecutionTimedOutAction = 10,

        CompressedCommand = 0xFF
    }
#pragma warning restore CA1028

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
                case CommandType.PutDirectory: return PutDirectoryCommand.Deserialize(reader);
                case CommandType.ListFiles: return ListFilesCommand.Deserialize(reader);
                case CommandType.GetFiles: return GetFilesCommand.Deserialize(reader);
                case CommandType.GetServerCapabilities: return GetServerCapabilitiesCommand.Deserialize(reader);
                case CommandType.ExecutionTimedOutAction: return ExecutionTimedOutActionCommand.Deserialize(reader);

                case CommandType.CompressedCommand: return CompressedCommand.Deserialize(reader);
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
                case PutDirectoryCommand _: type = CommandType.PutDirectory; break;
                case ListFilesCommand _: type = CommandType.ListFiles; break;
                case GetFilesCommand _: type = CommandType.GetFiles; break;
                case GetServerCapabilitiesCommand _: type = CommandType.GetServerCapabilities; break;
                case ExecutionTimedOutActionCommand _: type = CommandType.ExecutionTimedOutAction; break;

                case CompressedCommand _: type = CommandType.CompressedCommand; break;
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

        public string WorkDir { get; set; } = "";

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

    public sealed class PutDirectoryCommand : ICommand
    {
        public PackedFile[] Files { get; set; } = Array.Empty<PackedFile>();

        public string Path { get; set; }

        public bool PreserveTimestamps { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "PutDirectoryCommand",
            $"Files = <{Files.Length} files>",
            $"Path = {Path}",
            $"PreserveTimestamps = {PreserveTimestamps}"
        });

        public static PutDirectoryCommand Deserialize(IPCReader reader) => new PutDirectoryCommand
        {
            Files = reader.ReadLengthPrefixedFileArray(),
            Path = reader.ReadString(),
            PreserveTimestamps = reader.ReadBoolean()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.WriteLengthPrefixedArray(Files);
            writer.Write(Path);
            writer.Write(PreserveTimestamps);
        }
    }

    public sealed class ListFilesCommand : ICommand
    {
        public string Path { get; set; }

        public bool IncludeSubdirectories { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[] {
            "ListFilesCommand",
            $"Path = {Path}",
            $"IncludeSubdirectories = {IncludeSubdirectories}"
        });

        public static ListFilesCommand Deserialize(IPCReader reader) => new ListFilesCommand
        {
            Path = reader.ReadString(),
            IncludeSubdirectories = reader.ReadBoolean()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(Path);
            writer.Write(IncludeSubdirectories);
        }
    }

    public sealed class GetFilesCommand : ICommand
    {
        public bool UseCompression { get; set; }

        public string RootPath { get; set; }

        public string[] Paths { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[] {
            "GetFilesCommand",
            $"UseCompression = {UseCompression}",
            $"RootPath = {RootPath}",
            $"Paths = {string.Join(", ", Paths)}"
        });

        public static GetFilesCommand Deserialize(IPCReader reader) => new GetFilesCommand
        {
            UseCompression = reader.ReadBoolean(),
            RootPath = reader.ReadString(),
            Paths = reader.ReadLengthPrefixedStringArray()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(UseCompression);
            writer.Write(RootPath);
            writer.WriteLengthPrefixedArray(Paths);
        }
    }

    public sealed class GetServerCapabilitiesCommand : ICommand
    {
        public HashSet<ExtensionCapability> ExtensionCapabilities { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "GetServerCapabilitiesCommand",
            $"ExtensionCapabilities = {string.Join(", ", ExtensionCapabilities)}"
        });

        public static GetServerCapabilitiesCommand Deserialize(IPCReader reader)
        {
            var capabilityCount = reader.Read7BitEncodedInt();
            var capabilities = new HashSet<ExtensionCapability>();
            for (int i = 0; i < capabilityCount; ++i)
                capabilities.Add((ExtensionCapability)reader.ReadByte());
            return new GetServerCapabilitiesCommand { ExtensionCapabilities = capabilities };
        }

        public void Serialize(IPCWriter writer)
        {
            writer.Write7BitEncodedInt(ExtensionCapabilities.Count);
            foreach (var cap in ExtensionCapabilities)
                writer.Write((byte)cap);
        }
    }

    public sealed class ExecutionTimedOutActionCommand : ICommand
    {
        public bool TerminateProcesses { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "ExecutionTimedOutActionCommand",
            $"TerminateProcesses = {TerminateProcesses}"
        });

        public static ExecutionTimedOutActionCommand Deserialize(IPCReader reader) => new ExecutionTimedOutActionCommand
        {
            TerminateProcesses = reader.ReadBoolean()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(TerminateProcesses);
        }
    }

    public sealed class CompressedCommand : ICommand
    {
        public ICommand InnerCommand { get; }

        public CompressedCommand(ICommand command)
        {
            InnerCommand = command;
        }

        public override string ToString() =>
            "CompressedCommand: " + InnerCommand.ToString();

        public static ICommand Deserialize(IPCReader reader)
        {
            var commandData = reader.ReadLengthPrefixedBlob();
            using (var uncompresedStream = new MemoryStream())
            {
                using (var inputStream = new MemoryStream(commandData))
                using (var dstream = new DeflateStream(inputStream, CompressionMode.Decompress))
                    dstream.CopyTo(uncompresedStream);

                uncompresedStream.Seek(0, SeekOrigin.Begin);
                using (var uncompressedReader = new IPCReader(uncompresedStream))
                    return uncompressedReader.ReadCommand();
            }
        }

        public void Serialize(IPCWriter writer)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var dstream = new DeflateStream(outputStream, CompressionLevel.Optimal))
                using (var compressedWriter = new IPCWriter(dstream))
                    compressedWriter.WriteCommand(InnerCommand);

                writer.WriteLengthPrefixedBlob(outputStream.ToArray());
            }
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
}
