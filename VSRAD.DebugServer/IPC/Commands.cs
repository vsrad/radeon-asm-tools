using System;
using System.IO;

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
        PutFile = 5
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
}
