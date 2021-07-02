using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Server.Models;
using MyServiceBus.Server.Tcp;

namespace MyServiceBus.Server.Hubs
{
    public class InitHubResponseModel
    {
        public string Version { get; set; }
    }



    public class TopicPageModel
    {
        public string Label { get; set; }
        public int Percent { get; set; }

        public static TopicPageModel Create(string label, int percent)
        {
            return new()
            {
                Label = label,
                Percent = percent
            };
        }
    }

    
    public class TopicHubModel
    {
        public string Id { get; set; }
        
        public IEnumerable<TopicPageModel> Pages { get; set; }

        public static TopicHubModel Create(string id, IEnumerable<TopicPageModel> pages)
        {
            return new TopicHubModel
            {
                Id = id,
                Pages = pages
            };
        }

    }
    
    public class TopicConnectionHubModel
    {
        public string Id { get; set; }
        public int Light { get; set; }
        internal static readonly TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);
        public static TopicConnectionHubModel Create(KeyValuePair<string, DateTime> src)
        {
            return new ()
            {
                Id = src.Key,
                Light = DateTime.UtcNow - src.Value < TwoSeconds ? 1 : 0
            };
        }
    }


    public class TopicQueueHubModel
    {
        public string Id { get; set; }
        public int QueueType { get; set; }
        public long Size { get; set; }
        public int Connections { get; set; }
        public int Leased { get; set; }
        public IEnumerable<QueueSliceHubModel> Ready { get; set; }

        public static TopicQueueHubModel Create(TopicQueue topicQueue)
        {
            try
            {
                var connections = topicQueue.SubscribersList.GetCount();
                var queueType = (int)topicQueue.TopicQueueType;
                var messagesCount = topicQueue.GetMessagesCount();
                var ready = topicQueue.GetReadyQueueSnapshot().Select(QueueSliceHubModel.Create);
                var leased = topicQueue.GetLeasedMessagesCount();
                return new ()
                {
                    Id = topicQueue.QueueId,
                    Connections = connections,
                    QueueType = queueType,
                    Size = messagesCount,
                    Ready = ready,
                    Leased = leased
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }
    }
    
    public class QueueSliceHubModel
    {
        public long From { get; set; }
        public long To { get; set; }

        public static QueueSliceHubModel Create(IQueueIndexRange src)
        {
            return new ()
            {
                From = src.FromId,
                To = src.ToId
            };
        }
    }

    public class TcpConnectionSubscribeHubModel
    {
        public string TopicId { get; set; }
        public string QueueId { get; set; }
        public int Light { get; set; }
        public IEnumerable<QueueSliceHubModel> Leased { get; set; }

        public static TcpConnectionSubscribeHubModel Create(TopicQueue topicQueue, MyServiceBusTcpContext tcpContext)
        {
            return new ()
            {
                TopicId = topicQueue.Topic.TopicId,
                QueueId = topicQueue.QueueId,
                Leased = topicQueue.GetLeasedQueueSnapshot(tcpContext).Select(QueueSliceHubModel.Create),
                Light = DateTime.UtcNow - tcpContext.SessionContext.SubscriberInfo.GetSubscriberLastPacketDateTime(topicQueue.Topic.TopicId, topicQueue.QueueId) < TopicConnectionHubModel.TwoSeconds ? 1 : 0
            };
        }
    }

    public class TcpConnectionHubModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Ip { get; set; }
        public string Connected { get; set; }
        public string Recv { get; set; }
        public long ReadBytes { get; set; }
        public long SentBytes { get; set; }
        
        public int DeliveryEventsPerSecond { get; set; }
        public int ProtocolVersion { get; set; }
        
        public IEnumerable<TopicConnectionHubModel> Topics { get; set; }
        
        public IEnumerable<TcpConnectionSubscribeHubModel> Queues { get; set; }
    }
    

    public class TopicMetricsHubModel
    {
        public string Id { get; set; }
        public int MsgPerSec { get; set; }
        public int ReqPerSec { get; set; }
        public IEnumerable<TopicPageModel> Pages { get; set; }
        
        public IEnumerable<TopicQueueHubModel> Queues { get; set; }

        public static TopicMetricsHubModel Create(MyTopic topic)
        {

            return new()
            {
                Id = topic.TopicId,
                Pages = topic.MessagesContentCache.GetPages().Select(itm =>
                    TopicPageModel.Create(itm.no + ":" + itm.size.ByteSizeToString(), itm.percent)),
                MsgPerSec = topic.MessagesPerSecond,
                ReqPerSec = topic.RequestsPerSecond,
                Queues = topic.GetQueues().Select(TopicQueueHubModel.Create).Where(itm => itm != null)
            };

        }
    
    }

    public class PersistentQueueHubModel
    {
        public string Id { get; set; }
        public int Size { get; set; }
    }

}