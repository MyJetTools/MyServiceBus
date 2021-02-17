using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyTcpSockets.Extensions;

namespace MyServiceBus.TcpContracts
{

    public class UnsupportedPacketVersionException : Exception
    {

    }


    public interface IServiceBusTcpContract
    {
        void Serialize(Stream stream, int protocolVersion, int packetVersion);
        ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct);
    }

    public class PingContract : IServiceBusTcpContract
    {

        public static readonly PingContract Instance = new PingContract();

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
        }

        public ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            return new ValueTask();
        }
    }

    public class PongContract : IServiceBusTcpContract
    {
        public static readonly PongContract Instance = new PongContract();

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {

        }

        public ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            return new ValueTask();
        }
    }


    public class GreetingContract : IServiceBusTcpContract
    {
        public string Name { get; set; }
        public int ProtocolVersion { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(Name);
            stream.WriteInt(ProtocolVersion);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            Name = await dataReader.ReadPascalStringAsync(ct);
            ProtocolVersion = await dataReader.ReadIntAsync(ct);
        }
    }

    public class PublishContract : IServiceBusTcpContract
    {
        public string TopicId { get; set; }
        public long RequestId { get; set; }

        public byte ImmediatePersist { get; set; }

        public IReadOnlyList<byte[]> Data { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WriteLong(RequestId, protocolVersion);
            stream.WriteListOfByteArray(Data);
            stream.WriteByte(ImmediatePersist);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            RequestId = await dataReader.ReadLongAsync(protocolVersion, ct);
            Data = await dataReader.ReadListOfByteArrayAsync(ct);
            ImmediatePersist = await dataReader.ReadByteAsync(ct);
        }
    }


    public class PublishResponseContract : IServiceBusTcpContract
    {
        public long RequestId { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WriteLong(RequestId, protocolVersion);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {

            RequestId = await dataReader.ReadLongAsync(protocolVersion, ct);
        }
    }

    public class SubscribeContract : IServiceBusTcpContract
    {
        public string TopicId { get; set; }
        public string QueueId { get; set; }
        public bool DeleteOnDisconnect { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteByte(DeleteOnDisconnect ? (byte)1 : (byte)0);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            DeleteOnDisconnect = await dataReader.ReadByteAsync(ct) == 1;
        }
    }

    public class SubscribeResponseContract : IServiceBusTcpContract
    {
        public string TopicId { get; set; }
        public string QueueId { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
        }
    }




    public class NewMessageContract : IServiceBusTcpContract
    {
        public class NewMessageData : IServiceBusTcpContract, IMyServiceBusMessage
        {
            public long Id { get; set; }
            public int AttemptNo { get; set; }

            public ReadOnlyMemory<byte> Data { get; set; }


            public void Serialize(Stream stream, int protocolVersion, int packetVersion)
            {
                if (packetVersion == 0)
                {
                    stream.WriteLong(Id, protocolVersion);
                    stream.WriteByteArray(Data.Span);
                }
                else if (packetVersion == 1)
                {
                    stream.WriteLong(Id, protocolVersion);
                    stream.WriteInt(AttemptNo);
                    stream.WriteByteArray(Data.Span);
                }
                else
                {
                    throw new UnsupportedPacketVersionException();
                }
            }

            public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
            {
                if (packetVersion == 0)
                {
                    Id = await dataReader.ReadLongAsync(protocolVersion, ct);
                    Data = await dataReader.ReadByteArrayAsync(ct);
                }
                else
                if (packetVersion == 1)
                {
                    Id = await dataReader.ReadLongAsync(protocolVersion, ct);
                    AttemptNo = await dataReader.ReadIntAsync(ct);
                    Data = await dataReader.ReadByteArrayAsync(ct);
                }
                else
                {
                    throw new UnsupportedPacketVersionException();
                }

            }

        }

        public string TopicId { get; set; }
        public string QueueId { get; set; }

        public long ConfirmationId { get; set; }
        public IEnumerable<NewMessageData> Data { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId, protocolVersion);
            stream.WriteArrayOfItems(Data, protocolVersion, packetVersion);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            ConfirmationId = await dataReader.ReadLongAsync(protocolVersion, ct);
            Data = await dataReader.ReadArrayOfItemsAsync<NewMessageData>(protocolVersion, packetVersion, ct);
        }
    }



    public class NewMessageConfirmationContract : IServiceBusTcpContract
    {
        public string TopicId { get; set; }
        public string QueueId { get; set; }

        public long ConfirmationId { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId, protocolVersion);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            ConfirmationId = await dataReader.ReadLongAsync(protocolVersion, ct);
        }
    }


    public class CreateTopicIfNotExistsContract : IServiceBusTcpContract
    {
        public string TopicId { get; set; }
        public long MaxMessagesInCache { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WriteLong(MaxMessagesInCache, protocolVersion);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            MaxMessagesInCache = await dataReader.ReadLongAsync(protocolVersion, ct);
        }
    }

    public class MessagesInterval : IServiceBusTcpContract
    {
        public long FromId { get; set; }
        public long ToId { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WriteLong(FromId, protocolVersion);
            stream.WriteLong(ToId, protocolVersion);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            FromId = await dataReader.ReadLongAsync(protocolVersion, ct);
            ToId = await dataReader.ReadLongAsync(protocolVersion, ct);
        }
    }

    public class MessagesConfirmationContract : IServiceBusTcpContract
    {
        public string TopicId { get; set; }
        public string QueueId { get; set; }

        public long ConfirmationId { get; set; }


        //     public IReadOnlyList<MessagesInterval> Ok { get; set; }
        public IReadOnlyList<MessagesInterval> NotOk { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId);
            //stream.WriteArrayOfItems(Ok, protocolVersion, packetVersion);
            stream.WriteArrayOfItems(NotOk, protocolVersion, packetVersion);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            ConfirmationId = await dataReader.ReadLongAsync(protocolVersion, ct);
            //     Ok = await dataReader.ReadArrayOfItemsAsync<MessagesInterval>(protocolVersion, packetVersion, ct);
            NotOk = await dataReader.ReadArrayOfItemsAsync<MessagesInterval>(protocolVersion, packetVersion, ct);
        }
    }


    public class MessagesConfirmationAsFailContract : IServiceBusTcpContract
    {
        public string TopicId { get; set; }
        public string QueueId { get; set; }
        public long ConfirmationId { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId, protocolVersion);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            ConfirmationId = await dataReader.ReadLongAsync(protocolVersion, ct);
        }
    }


    public class PacketVersionsContract : IServiceBusTcpContract
    {

        private readonly Dictionary<byte, int> _versions = new Dictionary<byte, int>();

        public void SetPacketVersion(CommandType type, int version)
        {
            _versions.Add((byte)type, version);
        }


        public IEnumerable<KeyValuePair<byte, int>> GetPackets()
        {
            return _versions;
        }
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WriteByteFromStack((byte)_versions.Count);

            foreach (var version in _versions)
            {
                stream.WriteByteFromStack(version.Key);
                stream.WriteInt(version.Value);

            }
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            var count = await dataReader.ReadByteAsync(ct);

            for (byte i = 0; i < count; i++)
            {
                var key = await dataReader.ReadByteAsync(ct);
                var value = await dataReader.ReadIntAsync(ct);
                _versions.Add(key, value);
            }
        }

    }


    public class RejectConnectionContract : IServiceBusTcpContract
    {

        public string Message { get; set; }


        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {

            stream.WritePascalString(Message);
        }

        public async ValueTask DeserializeAsync(TcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            Message = await dataReader.ReadPascalStringAsync(ct);
        }


        public static RejectConnectionContract Create(string message)
        {
            return new RejectConnectionContract
            {
                Message = message
            };
        }
    }

}