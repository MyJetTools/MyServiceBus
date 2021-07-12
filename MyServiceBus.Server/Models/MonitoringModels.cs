using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Server.Hubs;
using MyServiceBus.Server.Services.Sessions;
using MyServiceBus.Server.Tcp;

namespace MyServiceBus.Server.Models
{

    public class QueueSlice
    {
        public long From { get; set; }
        public long To { get; set; }

        public static QueueSlice Create(IQueueIndexRange src)
        {
            return new ()
            {
                From = src.FromId,
                To = src.ToId
            };
        }
    }
    
    public class ConsumerModel
    {
        public string QueueId { get; set; }
        public int QueueType { get; set; }
        public int Connections { get; set; }
        public long QueueSize { get; set; }
        public IEnumerable<QueueSlice> ReadySlices { get; set; }
        public long LeasedAmount { get; set; }
        public IEnumerable<int> ExecutionDuration { get; set; }
    }


    public class UnknownConnectionModel
    {
        public string Id { get; set; }
        public string Ip { get; set; }
        public string ConnectedTimeStamp { get; set; }
        public long SentBytes { get; set; }
        public string LastSendDuration { get; set; }
        public long ReceivedBytes { get; set; }
        public string SentTimeStamp { get; set; }
        public string ReceiveTimeStamp { get; set; }

    }


    public class ConnectionQueueInfoModel
    {
        public string Id { get; set; }
        public IEnumerable<QueueSlice> Leased { get; set; }
    }



    public class QueueToPersist
    {
        public string TopicId { get; set; }
        public int Count { get; set; }
    }
    

    public class SnapshotsContract
    {
        public int TopicSnapshotId { get; set; }
        public int TcpConnections { get; set; }
    }
}