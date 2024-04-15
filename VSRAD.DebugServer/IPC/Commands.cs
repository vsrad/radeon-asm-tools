using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.IPC.Commands
{
#pragma warning disable CA1028 // Using byte for enum storage because it's used for binary serialization
    public enum CommandType : byte
    {
        Execute = 0,
        FetchMetadata = 1,
        FetchResultRange = 2,
        ListEnvironmentVariables = 3,
        ListFiles = 4,
        GetFiles = 5,
        PutFiles = 6,
        ExchangeVersions = 7,

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
                case CommandType.ListEnvironmentVariables: return ListEnvironmentVariables.Deserialize(reader);
                case CommandType.ListFiles: return ListFilesCommand.Deserialize(reader);
                case CommandType.GetFiles: return GetFilesCommand.Deserialize(reader);
                case CommandType.PutFiles: return PutFilesCommand.Deserialize(reader);
                case CommandType.ExchangeVersions: return ExchangeVersionsCommand.Deserialize(reader);

                case CommandType.CompressedCommand: return CompressedCommand.Deserialize(reader);
                default: throw new InvalidDataException($"Unexpected command type byte: {type}");
            }
        }

        public static void WriteCommand(this IPCWriter writer, ICommand command)
        {
            CommandType type;
            switch (command)
            {
                case Execute _: type = CommandType.Execute; break;
                case FetchMetadata _: type = CommandType.FetchMetadata; break;
                case FetchResultRange _: type = CommandType.FetchResultRange; break;
                case ListEnvironmentVariables _: type = CommandType.ListEnvironmentVariables; break;
                case ListFilesCommand _: type = CommandType.ListFiles; break;
                case GetFilesCommand _: type = CommandType.GetFiles; break;
                case PutFilesCommand _: type = CommandType.PutFiles; break;
                case ExchangeVersionsCommand _: type = CommandType.ExchangeVersions; break;

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

        public IReadOnlyDictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public bool RunAsAdministrator { get; set; } = false;

        public bool WaitForCompletion { get; set; } = true;

        public int ExecutionTimeoutSecs { get; set; } = 0;

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "Execute",
            $"WorkingDirectory = {WorkingDirectory}",
            $"Executable = {Executable}",
            $"Arguments = {Arguments}",
            $"EnvironmentVariables = {{ {string.Join(", ", EnvironmentVariables.Select(kv => kv.Key + " = " + kv.Value))} }}",
            $"RunAsAdministrator = {RunAsAdministrator}",
            $"WaitForCompletion = {WaitForCompletion}",
            $"ExecutionTimeoutSecs = {ExecutionTimeoutSecs}"
        });

        public static Execute Deserialize(IPCReader reader) => new Execute
        {
            WorkingDirectory = reader.ReadString(),
            Executable = reader.ReadString(),
            Arguments = reader.ReadString(),
            EnvironmentVariables = reader.ReadLengthPrefixedStringDict(),
            RunAsAdministrator = reader.ReadBoolean(),
            WaitForCompletion = reader.ReadBoolean(),
            ExecutionTimeoutSecs = reader.ReadInt32()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(WorkingDirectory);
            writer.Write(Executable);
            writer.Write(Arguments);
            writer.WriteLengthPrefixedDict(EnvironmentVariables);
            writer.Write(RunAsAdministrator);
            writer.Write(WaitForCompletion);
            writer.Write(ExecutionTimeoutSecs);
        }
    }

    public sealed class FetchMetadata : ICommand
    {
        public string FilePath { get; set; } = "";

        public bool BinaryOutput { get; set; } = true;

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "FetchMetadata",
            $"FilePath = {FilePath}",
            $"BinaryOutput = {BinaryOutput}"
        });

        public static FetchMetadata Deserialize(IPCReader reader) => new FetchMetadata
        {
            FilePath = reader.ReadString(),
            BinaryOutput = reader.ReadBoolean()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(FilePath);
            writer.Write(BinaryOutput);
        }
    }

    public sealed class FetchResultRange : ICommand
    {
        public string FilePath { get; set; } = "";

        public bool BinaryOutput { get; set; } = true;

        public int ByteOffset { get; set; } = 0;

        public int ByteCount { get; set; } = 0;

        public int OutputOffset { get; set; } = 0;

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "FetchResultRange",
            $"FilePath = {FilePath}",
            $"BinaryOutput = {BinaryOutput}",
            $"ByteOffset = {ByteOffset}",
            $"ByteCount = {ByteCount}",
            $"OutputOffset = {OutputOffset}"
        });

        public static FetchResultRange Deserialize(IPCReader reader) => new FetchResultRange
        {
            FilePath = reader.ReadString(),
            BinaryOutput = reader.ReadBoolean(),
            ByteOffset = reader.ReadInt32(),
            ByteCount = reader.ReadInt32(),
            OutputOffset = reader.ReadInt32()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(FilePath);
            writer.Write(BinaryOutput);
            writer.Write(ByteOffset);
            writer.Write(ByteCount);
            writer.Write(OutputOffset);
        }
    }

    public sealed class ListFilesCommand : ICommand
    {
        public string RootPath { get; set; } = "";

        public string[] Globs { get; set; } = Array.Empty<string>();

        public override string ToString() => string.Join(Environment.NewLine, new[] {
            "ListFilesCommand",
            $"RootPath = {RootPath}",
            $"Globs = {string.Join(";", Globs)}"
        });

        public static ListFilesCommand Deserialize(IPCReader reader) => new ListFilesCommand
        {
            RootPath = reader.ReadString(),
            Globs = reader.ReadLengthPrefixedStringArray()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(RootPath);
            writer.WriteLengthPrefixedArray(Globs);
        }
    }

    public sealed class GetFilesCommand : ICommand
    {
        public string RootPath { get; set; }

        public string[] Paths { get; set; }

        public bool UseCompression { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[] {
            "GetFilesCommand",
            $"RootPath = {RootPath}",
            $"Paths = {string.Join(";", Paths)}",
            $"UseCompression = {UseCompression}"
        });

        public static GetFilesCommand Deserialize(IPCReader reader) => new GetFilesCommand
        {
            RootPath = reader.ReadString(),
            Paths = reader.ReadLengthPrefixedStringArray(),
            UseCompression = reader.ReadBoolean()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(RootPath);
            writer.WriteLengthPrefixedArray(Paths);
            writer.Write(UseCompression);
        }
    }

    public sealed class PutFilesCommand : ICommand
    {
        public string RootPath { get; set; } = "";

        public PackedFile[] Files { get; set; } = Array.Empty<PackedFile>();

        public bool PreserveTimestamps { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "PutFilesCommand",
            $"RootPath = {RootPath}",
            $"Files = <{Files.Length} files>",
            $"PreserveTimestamps = {PreserveTimestamps}"
        });

        public static PutFilesCommand Deserialize(IPCReader reader) => new PutFilesCommand
        {
            RootPath = reader.ReadString(),
            Files = reader.ReadLengthPrefixedFileArray(),
            PreserveTimestamps = reader.ReadBoolean()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(RootPath);
            writer.WriteLengthPrefixedFileArray(Files);
            writer.Write(PreserveTimestamps);
        }
    }


    public sealed class ExchangeVersionsCommand : ICommand
    {
        public Version ClientVersion { get; set; }
        public OSPlatform ClientPlatform { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[] {
            "ExchangeVersionsCommand",
            $"ClientVersion = {ClientVersion}",
            $"ClientPlatform = {ClientPlatform}"
        });

        public static ExchangeVersionsCommand Deserialize(IPCReader reader) => new ExchangeVersionsCommand
        {
            ClientVersion = Version.Parse(reader.ReadString()),
            ClientPlatform = OSPlatform.Create(reader.ReadString())
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(ClientVersion.ToString());
            writer.Write(ClientPlatform.ToString());
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

    public sealed class ListEnvironmentVariables : ICommand
    {
        public override string ToString() => "ListEnvironmentVariables";

        public static ListEnvironmentVariables Deserialize(IPCReader _) => new ListEnvironmentVariables();

        public void Serialize(IPCWriter _) { }
    }
}
