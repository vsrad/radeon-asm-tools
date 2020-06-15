using System;
using System.IO;

namespace VSRAD.DebugServer.IPC.Commands
{
    public static class BinaryCommandExtensions
    {
        public static ICommand ReadCommand(this IPCReader reader)
        {
            var commandType = reader.ReadByte();
            switch (commandType)
            {
                case 0: return Execute.Deserialize(reader);
                case 1: return FetchMetadata.Deserialize(reader);
                case 2: return FetchResultRange.Deserialize(reader);
                case 3: return Deploy.Deserialize(reader);
                case 4: return ListEnvironmentVariables.Deserialize(reader);
            }
            throw new InvalidDataException($"Unexpected command type byte: {commandType}");
        }

        public static void WriteCommand(this IPCWriter writer, ICommand command)
        {
            byte commandType;
            switch (command)
            {
                case Execute _: commandType = 0; break;
                case FetchMetadata _: commandType = 1; break;
                case FetchResultRange _: commandType = 2; break;
                case Deploy _: commandType = 3; break;
                case ListEnvironmentVariables _: commandType = 4; break;
                default: throw new ArgumentException($"Unable to serialize {command.GetType()}");
            }
            writer.Write(commandType);
            command.Serialize(writer);
        }
    }

    public interface ICommand
    {
        void Serialize(IPCWriter writer);
    }

    public sealed class Execute : ICommand
    {
        public string WorkingDirectory { get; set; } = "";

        public string Executable { get; set; } = "";

        public string Arguments { get; set; } = "";

        public bool RunAsAdministrator { get; set; }

        public int ExecutionTimeoutSecs { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "Execute",
            $"WorkingDirectory = {WorkingDirectory}",
            $"Executable = {Executable}",
            $"Arguments = {Arguments}",
            $"RunAsAdministrator = {RunAsAdministrator}",
            $"ExecutionTimeoutSecs = {ExecutionTimeoutSecs}",
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
