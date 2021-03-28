using System;
using System.Collections.Generic;
using System.IO;
using VSRAD.DebugServer.SharedUtils;

namespace VSRAD.DebugServer.IPC.Responses
{
#pragma warning disable CA1028 // Using byte for enum storage because it's used for binary serialization
    public enum ResponseType : byte
    {
        ExecutionCompleted = 0,
        MetadataFetched = 1,
        ResultRangeFetched = 2,
        EnvironmentVariablesListed = 3,
        PutFile = 4,
        PutDirectory = 5,
        ListFiles = 6,
        GetFiles = 7
    }
#pragma warning restore CA1028

    public interface IResponse
    {
        void Serialize(IPCWriter writer);
    }

    public static class BinaryResponseExtensions
    {
        public static IResponse ReadResponse(this IPCReader reader)
        {
            var type = reader.ReadByte();
            switch ((ResponseType)type)
            {
                case ResponseType.ExecutionCompleted: return ExecutionCompleted.Deserialize(reader);
                case ResponseType.MetadataFetched: return MetadataFetched.Deserialize(reader);
                case ResponseType.ResultRangeFetched: return ResultRangeFetched.Deserialize(reader);
                case ResponseType.EnvironmentVariablesListed: return EnvironmentVariablesListed.Deserialize(reader);
                case ResponseType.PutFile: return PutFileResponse.Deserialize(reader);
                case ResponseType.PutDirectory: return PutDirectoryResponse.Deserialize(reader);
                case ResponseType.ListFiles: return ListFilesResponse.Deserialize(reader);
                case ResponseType.GetFiles: return GetFilesResponse.Deserialize(reader);
            }
            throw new InvalidDataException($"Unexpected response type byte: {type}");
        }

        public static void WriteResponse(this IPCWriter writer, IResponse response)
        {
            ResponseType type;
            switch (response)
            {
                case ExecutionCompleted _: type = ResponseType.ExecutionCompleted; break;
                case MetadataFetched _: type = ResponseType.MetadataFetched; break;
                case ResultRangeFetched _: type = ResponseType.ResultRangeFetched; break;
                case EnvironmentVariablesListed _: type = ResponseType.EnvironmentVariablesListed; break;
                case PutFileResponse _: type = ResponseType.PutFile; break;
                case PutDirectoryResponse _: type = ResponseType.PutDirectory; break;
                case ListFilesResponse _: type = ResponseType.ListFiles; break;
                case GetFilesResponse _: type = ResponseType.GetFiles; break;
                default: throw new ArgumentException($"Unable to serialize {response.GetType()}");
            }
            writer.Write((byte)type);
            response.Serialize(writer);
        }
    }

    public sealed class ExecutionCompleted : IResponse
    {
        public ExecutionStatus Status { get; set; }

        public int ExitCode { get; set; } = -1;

        public string Stdout { get; set; } = "";

        public string Stderr { get; set; } = "";

        public long ExecutionTime { get; set; } = -1;

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "ExecutionCompleted",
            $"Status = {Status}",
            $"ExitCode = {ExitCode}",
            $"Stdout = <{Stdout.Length} chars>",
            $"Stderr = <{Stderr.Length} chars>",
            $"ExecutionTime = {ExecutionTime}",
        });

        public static ExecutionCompleted Deserialize(IPCReader reader) => new ExecutionCompleted
        {
            Status = (ExecutionStatus)reader.ReadByte(),
            ExitCode = reader.ReadInt32(),
            Stdout = reader.ReadString(),
            Stderr = reader.ReadString(),
            ExecutionTime = reader.ReadInt64(),
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write((byte)Status);
            writer.Write(ExitCode);
            writer.Write(Stdout);
            writer.Write(Stderr);
            writer.Write(ExecutionTime);
        }
    }

    public sealed class MetadataFetched : IResponse
    {
        public int ByteCount { get; set; }

        public DateTime Timestamp { get; set; }

        public FetchStatus Status { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "MetadataFetched",
            $"ByteCount = {ByteCount}",
            $"Timestamp = {Timestamp}",
            $"Status = {Status}",
        });

        public static MetadataFetched Deserialize(IPCReader reader) => new MetadataFetched
        {
            ByteCount = reader.ReadInt32(),
            Timestamp = reader.ReadDateTime(),
            Status = (FetchStatus)reader.ReadByte()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write(ByteCount);
            writer.Write(Timestamp);
            writer.Write((byte)Status);
        }
    }

    public sealed class ResultRangeFetched : IResponse
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();

        public DateTime Timestamp { get; set; }

        public FetchStatus Status { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "ResultRangeFetched",
            $"Data = <{Data.Length} bytes>",
            $"Timestamp = {Timestamp}",
            $"Status = {Status}",
        });

        public static ResultRangeFetched Deserialize(IPCReader reader) => new ResultRangeFetched
        {
            Data = reader.ReadLengthPrefixedBlob(),
            Timestamp = reader.ReadDateTime(),
            Status = (FetchStatus)reader.ReadByte()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.WriteLengthPrefixedBlob(Data);
            writer.Write(Timestamp);
            writer.Write((byte)Status);
        }
    }

    public sealed class PutFileResponse : IResponse
    {
        public PutFileStatus Status { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "PutFileResponse",
            $"Status = {Status}",
        });

        public static PutFileResponse Deserialize(IPCReader reader) => new PutFileResponse
        {
            Status = (PutFileStatus)reader.ReadByte()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write((byte)Status);
        }
    }

    public sealed class PutDirectoryResponse : IResponse
    {
        public PutDirectoryStatus Status { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "PutDirectoryResponse",
            $"Status = {Status}",
        });

        public static PutDirectoryResponse Deserialize(IPCReader reader) => new PutDirectoryResponse
        {
            Status = (PutDirectoryStatus)reader.ReadByte()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write((byte)Status);
        }
    }

    public sealed class ListFilesResponse : IResponse
    {
        public FileMetadata[] Files { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "ListFilesResponse",
            $"Files = <{Files.Length} items>",
        });

        public static ListFilesResponse Deserialize(IPCReader reader)
        {
            var length = reader.Read7BitEncodedInt();
            var files = new FileMetadata[length];
            for (int i = 0; i < length; ++i)
                files[i] = new FileMetadata(reader.ReadString(), reader.ReadInt64(), reader.ReadDateTime());
            return new ListFilesResponse { Files = files };
        }

        public void Serialize(IPCWriter writer)
        {
            writer.Write7BitEncodedInt(Files.Length);
            foreach (var file in Files)
            {
                writer.Write(file.RelativePath);
                writer.Write(file.Size);
                writer.Write(file.LastWriteTimeUtc);
            }
        }
    }

    public sealed class GetFilesResponse : IResponse
    {
        public GetFilesStatus Status { get; set; }
        public PackedFile[] Files { get; set; } = Array.Empty<PackedFile>();

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "GetFilesResponse",
            $"Status = {Status}",
            $"Files = <{Files.Length} files>",
        });

        public static GetFilesResponse Deserialize(IPCReader reader) => new GetFilesResponse
        {
            Status = (GetFilesStatus)reader.ReadByte(),
            Files = reader.ReadLengthPrefixedFileArray()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write((byte)Status);
            writer.WriteLengthPrefixedFileArray(Files);
        }
    }

    public sealed class EnvironmentVariablesListed : IResponse
    {
        public IReadOnlyDictionary<string, string> Variables { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "EnvironmentVariablesListed",
            $"Variables = <{Variables.Count} items>",
        });

        public static EnvironmentVariablesListed Deserialize(IPCReader reader) => new EnvironmentVariablesListed
        {
            Variables = reader.ReadLengthPrefixedStringDict()
        };

        public void Serialize(IPCWriter writer) =>
            writer.WriteLengthPrefixedDict(Variables);
    }

#pragma warning disable CA1028 // Using byte for enum storage because it is transferred over the wire
    public enum ExecutionStatus : byte
    {
        Completed = 0,
        TimedOut = 1,
        CouldNotLaunch = 2
    }

    public enum FetchStatus : byte
    {
        Successful = 0,
        FileNotFound = 1
    }

    public enum PutFileStatus : byte
    {
        Successful = 0,
        PermissionDenied = 1,
        OtherIOError = 2
    }

    public enum PutDirectoryStatus : byte
    {
        Successful = 0,
        PermissionDenied = 1,
        TargetPathIsFile = 2,
        OtherIOError = 3
    }

    public enum GetFilesStatus : byte
    {
        Successful = 0,
        FileNotFound = 1,
        PermissionDenied = 2,
        OtherIOError = 3
    }
#pragma warning restore CA1028
}
