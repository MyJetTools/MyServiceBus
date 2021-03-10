using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.Abstractions.QueueIndex;
using MyTcpSockets.Extensions;

namespace MyServiceBus.TcpContracts
{

    public class UnsupportedPacketVersionException : Exception
    {

    }
    
    public interface IServiceBusTcpContract
    {
        void Serialize(Stream stream, int protocolVersion, int packetVersion);
        ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct);
    }

    public class PingContract : IServiceBusTcpContract
    {
        
        public static readonly PingContract Instance = new PingContract();
            
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
        }


        public ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
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

        public ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
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

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            Name = await dataReader.ReadPascalStringAsync(ct);
            ProtocolVersion = await dataReader.ReadIntAsync(ct);
        }
    }
    
    public class PublishContract : IServiceBusTcpContract
    {
        public string TopicId { get; private set; }
        public long RequestId { get; private set; }
        public byte ImmediatePersist { get; private set; }
        public IReadOnlyList<(byte[] payload, IReadOnlyList<KeyValuePair<string, string>> metaData)> Data { get; private set; }
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {

            if (packetVersion == 0)
            {
                stream.WritePascalString(TopicId);
                stream.WriteLong(RequestId, protocolVersion);
                var data = Data.Select(itm => itm.payload).ToList();
                stream.WriteListOfByteArray(data);
                stream.WriteByte(ImmediatePersist);
            }
            else
            {
                stream.WritePascalString(TopicId);
                stream.WriteLong(RequestId, protocolVersion);
                stream.SerializeList(Data, item =>
                {
                    
                    stream.WriteByteArray(item.payload);
                    stream.SerializeListWithCounterAsByte(item.metaData, metaData =>
                    {
                        stream.WritePascalString(metaData.Key);
                        stream.WritePascalString(metaData.Value);
                    });
                });
                stream.WriteByte(ImmediatePersist);
            }
            
        }
        
        public static readonly IReadOnlyList<KeyValuePair<string, string>> EmptyMetaData = Array.Empty<KeyValuePair<string, string>>();

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            if (packetVersion == 0)
            {
                TopicId = await dataReader.ReadPascalStringAsync(ct);
                RequestId = await dataReader.ReadLongAsync(protocolVersion, ct);
                var payLoadAsByteArray = await dataReader.ReadListOfByteArrayAsync(ct);
                Data = payLoadAsByteArray.Select(itm => (itm, EmptyMetaData)).ToList();
                ImmediatePersist = await dataReader.ReadAndCommitByteAsync(ct);  
            }
            else
            if (packetVersion == 1)
            {
                TopicId = await dataReader.ReadPascalStringAsync(ct);
                RequestId = await dataReader.ReadLongAsync(protocolVersion, ct);
                
                Data = await dataReader.DeserializeList(async () =>
                {
                    var bytes = await dataReader.ReadByteArrayAsync(ct);

                    var metaData = await dataReader.DeserializeListWithCounterAsByte(async () =>
                    {
                        var key = await dataReader.ReadPascalStringAsync(ct);
                        var value = await dataReader.ReadPascalStringAsync(ct);
                        return new KeyValuePair<string, string>(key, value);
                    }, ct);

                    return (bytes, metaData);

                }, ct);
                ImmediatePersist = await dataReader.ReadAndCommitByteAsync(ct);  
            }
            else
            {
                throw new UnsupportedPacketVersionException();
            }

        }

        public static PublishContract Create(string topicId, long requestId, bool immediatePersist, 
            IReadOnlyList<(byte[] data, IReadOnlyList<KeyValuePair<string, string>> metaData)> data)
        {
            return new PublishContract
            {
                TopicId = topicId,
                RequestId = requestId,
                ImmediatePersist = immediatePersist ? (byte) 1 : (byte) 0,
                Data = data
            };
        }
    }    
    
    
    public class PublishResponseContract : IServiceBusTcpContract
    {
        public long RequestId { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WriteLong(RequestId, protocolVersion);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {

            RequestId = await dataReader.ReadLongAsync(protocolVersion, ct);
        }
    }
    
    public class SubscribeContract : IServiceBusTcpContract
    {
        public string TopicId { get; set; }
        public string QueueId { get; set; }
        public TopicQueueType QueueType { get; set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteByte((byte)QueueType);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            QueueType = (TopicQueueType)await dataReader.ReadAndCommitByteAsync(ct);
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

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
        }
    }



    
    public class NewMessagesContract : IServiceBusTcpContract
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

            public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
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
        public IReadOnlyList<NewMessageData> Data { get; set; }
        
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId, protocolVersion);
            stream.WriteArrayOfItems(Data, protocolVersion, packetVersion);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            ConfirmationId = await dataReader.ReadLongAsync(protocolVersion, ct);
            Data = await dataReader.ReadArrayOfItemsAsync<NewMessageData>(protocolVersion, packetVersion, ct);
        }
    }


    
    public class NewMessageConfirmationContract : IServiceBusTcpContract
    {
        public string TopicId { get;  set; }
        public string QueueId { get;  set; }
        public long ConfirmationId { get;  set; }
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId, protocolVersion);
        }
        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            ConfirmationId = await dataReader.ReadLongAsync(protocolVersion, ct);
        }
    }
    
    public class QueueIndexRangeTcpContract : IQueueIndexRange, IServiceBusTcpContract{
        public long FromId { get; private set; }
        public long ToId { get; private set; }

        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WriteLong(FromId);
            stream.WriteLong(ToId);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            FromId = await dataReader.ReadLongAsync(ct);
            ToId = await dataReader.ReadLongAsync(ct);
        }

        public static QueueIndexRangeTcpContract Create(IQueueIndexRange queueIndexRange)
        {
            return new QueueIndexRangeTcpContract
            {
                FromId = queueIndexRange.ToId,
                ToId = queueIndexRange.ToId
            };
        }
    }

    public class ConfirmSomeMessagesOkSomeFail : IServiceBusTcpContract
    {
        public byte PacketVersion { get; private set; }
    
        public string TopicId { get; set; }
        public string QueueId { get; set; }
        public long ConfirmationId { get; set; }
        public IReadOnlyList<IQueueIndexRange> OkMessages { get; set; }
        
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WriteByte(PacketVersion);
            
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId);
            stream.SerializeMessagesIntervals(OkMessages, protocolVersion, packetVersion);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            PacketVersion = await dataReader.ReadAndCommitByteAsync(ct);

            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            ConfirmationId = await dataReader.ReadLongAsync(ct);
            OkMessages = await dataReader.DeserializeMessagesIntervals(ct, protocolVersion, packetVersion);
        }
    }


    public class CreateTopicIfNotExistsContract : IServiceBusTcpContract
    {
        public string TopicId { get;  set; }
        public long MaxMessagesInCache { get; set; }
        
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WriteLong(MaxMessagesInCache, protocolVersion);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
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

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            FromId = await dataReader.ReadLongAsync(protocolVersion, ct);
            ToId = await dataReader.ReadLongAsync(protocolVersion, ct);
        }
    }
    
    public class MessagesConfirmationContract : IServiceBusTcpContract
    {
        public string TopicId { get;  set; }
        public string QueueId { get;  set; }
        
        public IReadOnlyList<MessagesInterval> Ok { get; set; }
        public IReadOnlyList<MessagesInterval> NotOk { get; set; }
        
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteArrayOfItems(Ok, protocolVersion, packetVersion);
            stream.WriteArrayOfItems(NotOk, protocolVersion, packetVersion);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);

            Ok = await dataReader.ReadArrayOfItemsAsync<MessagesInterval>(protocolVersion,packetVersion, ct);
            NotOk = await dataReader.ReadArrayOfItemsAsync<MessagesInterval>(protocolVersion,packetVersion, ct);
        }
    }

    
    public class MessagesConfirmationAsFailContract : IServiceBusTcpContract
    {
        public string TopicId { get;  set; }
        public string QueueId { get;  set; }
        public long ConfirmationId { get;  set; }
        
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId, protocolVersion);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
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
           stream.WriteByteFromStack((byte) _versions.Count);

           foreach (var version in _versions)
           {
               stream.WriteByteFromStack(version.Key);
               stream.WriteInt(version.Value);
               
           }
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            var count = await dataReader.ReadAndCommitByteAsync(ct);

            for (byte i = 0; i < count; i++)
            {
                var key = await dataReader.ReadAndCommitByteAsync(ct);
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

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
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

    public class ConfirmMessagesByNotDeliveryContract : IServiceBusTcpContract
    {
        public byte PacketVersion { get; private set; }

        
        public string TopicId { get; private set; }
        public string QueueId { get; private set; }
        public long ConfirmationId { get; private set; }
        
        public IReadOnlyList<IQueueIndexRange> ConfirmedMessages { get; private set; }
        
        public void Serialize(Stream stream, int protocolVersion, int packetVersion)
        {
            stream.WriteByte(PacketVersion);
            stream.WritePascalString(TopicId);
            stream.WritePascalString(QueueId);
            stream.WriteLong(ConfirmationId);
            stream.SerializeMessagesIntervals(ConfirmedMessages, packetVersion, packetVersion);
        }

        public async ValueTask DeserializeAsync(ITcpDataReader dataReader, int protocolVersion, int packetVersion, CancellationToken ct)
        {
            PacketVersion = await dataReader.ReadAndCommitByteAsync(ct);
            TopicId = await dataReader.ReadPascalStringAsync(ct);
            QueueId = await dataReader.ReadPascalStringAsync(ct);
            ConfirmationId = await dataReader.ReadLongAsync(ct);
            ConfirmedMessages = await dataReader.DeserializeMessagesIntervals(ct, protocolVersion, packetVersion);
        }

        public static ConfirmMessagesByNotDeliveryContract Create(string topicId, string queueId, 
            long confirmationId, IReadOnlyList<IQueueIndexRange> confirmedMessages)
        {
            return new ConfirmMessagesByNotDeliveryContract
            {
                TopicId = topicId,
                QueueId = queueId,
                ConfirmationId = confirmationId,
                ConfirmedMessages = confirmedMessages
            };
        }
        
    }
    
}