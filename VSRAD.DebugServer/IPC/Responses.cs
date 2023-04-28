using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using VSRAD.DebugServer.SharedUtils;
using System.Text;

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
        CheckOutdatedFiles = 5,
        ListFiles = 6,
        SendFile = 7,
        GetFile = 8,
        PutDirectory
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
                case ResponseType.CheckOutdatedFiles: return CheckOutdatedFilesResponse.Deserialize(reader);
                case ResponseType.ListFiles: return ListFilesResponse.Deserialize(reader);
                case ResponseType.SendFile: return SendFileResponse.Deserialize(reader);
                case ResponseType.GetFile: return GetFileResponse.Deserialize(reader);
                case ResponseType.PutDirectory: return PutDirectoryResponse.Deserialize(reader);
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
                case CheckOutdatedFilesResponse _: type = ResponseType.CheckOutdatedFiles; break;
                case ListFilesResponse _: type = ResponseType.ListFiles; break;
                case SendFileResponse _: type = ResponseType.SendFile; break;
                case GetFileResponse _: type = ResponseType.GetFile; break;
                case PutDirectoryResponse _: type = ResponseType.PutDirectory; break;
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

    public sealed class SendFileResponse : IResponse
    {
        public SendFileStatus Status { get; set; }

        public string Message { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "SendFileResponse",
            $"Status = {Status}",
        });

        public static SendFileResponse Deserialize(IPCReader reader) => new SendFileResponse
        {
            Status = (SendFileStatus)reader.ReadByte(),
            Message = reader.ReadString()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write((byte)Status);
            writer.Write(Message);
        }
    }

    public sealed class PutDirectoryResponse : IResponse
    {
        public PutDirectoryStatus Status { get; set; }

        public string Message { get; set; } 

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "PutDirectoryResponse",
            $"Status = {Status}",
        });

        public static PutDirectoryResponse Deserialize(IPCReader reader) => new PutDirectoryResponse
        {
            Status = (PutDirectoryStatus)reader.ReadByte(),
            Message = reader.ReadString()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write((byte)Status);
            writer.Write(Message);
        }
    }
    public sealed class GetFileResponse : IResponse
    {
        public GetFileStatus Status { get; set; }

        public string Message { get; set; }

        public override string ToString() => string.Join(Environment.NewLine, new[]
        {
            "SendFileResponse",
            $"Status = {Status}",
        });

        public static GetFileResponse Deserialize(IPCReader reader) => new GetFileResponse
        {
            Status = (GetFileStatus)reader.ReadByte(),
            Message = reader.ReadString()
        };

        public void Serialize(IPCWriter writer)
        {
            writer.Write((byte)Status);
            writer.Write(Message);
        }
    }

    public sealed class ListFilesResponse : IResponse
    {
        public List<FileMetadata> Files { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var file in Files)
            {
                sb.AppendLine(file.RelativePath);
            }

            return string.Join(Environment.NewLine, new[] {
                "ListFileResponse",
                $"Files = <{sb.ToString()} >"
            });
        }
        private static XmlSerializer GetFormatter()
        {
            return new XmlSerializer(typeof(List<FileMetadata>));
        }

        public static ListFilesResponse Deserialize(IPCReader reader)
        {
            List<FileMetadata> files = GetFormatter().Deserialize(reader.BaseStream) as List<FileMetadata>;
            return new ListFilesResponse { Files = files };
        }
        public void Serialize(IPCWriter writer)
        {
            GetFormatter().Serialize(writer.BaseStream, Files);
        }
    }
    public sealed class CheckOutdatedFilesResponse : IResponse
    {
        public List<FileMetadata> Files { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var file in Files)
            {
                sb.AppendLine(file.RelativePath);
            }

            return string.Join(Environment.NewLine, new[] {
                "CheckOutdatedFilesResponse",
                $"Files = <{sb.ToString()} >"
            });
        }

        private static XmlSerializer GetFormatter()
        {
            return new XmlSerializer(typeof(List<FileMetadata>));
        }
        public static CheckOutdatedFilesResponse Deserialize(IPCReader reader)
        {
            List<FileMetadata> files = GetFormatter().Deserialize(reader.BaseStream) as List<FileMetadata>;
            return new CheckOutdatedFilesResponse { Files = files };
        }

        public void Serialize(IPCWriter writer)
        {
            GetFormatter().Serialize(writer.BaseStream, Files);
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

    public enum SendFileStatus : byte
    {
        Successful = 0,
        PermissionDenied = 1,
        OtherIOError = 2
    }

    public enum PutDirectoryStatus : byte
    {
        Successful = 0,
        PermissionDenied = 1,
        OtherIOError = 2
    }

    public enum GetFileStatus : byte
    {
        Successful = 0,
        PermissionDenied = 1,
        OtherIOError = 2
    }
#pragma warning restore CA1028
}
