using System;
using System.Linq;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Server.Models;

namespace MyServiceBus.Server.Hubs
{
    public static class HubModelsMappers
    {

        internal static TopicHubModel ToTopicHubModel(this MyTopic topic)
        {
            return TopicHubModel.Create(topic.TopicId,
                topic.MessagesContentCache.GetPages().Select(itm => TopicPageModel.Create(itm.no+":"+itm.size.ByteSizeToString(), itm.percent)));

        }
        
        internal static ConnectionHubModel ToTcpConnectionHubModel(this MyServiceBusSession session)
        {
            return new ()
            {
                Id = session.Id,
                Name = session.Name,
                Ip = session.Ip,
                Connected = (DateTime.UtcNow - session.Created).FormatTimeStamp(),
                Recv = (DateTime.UtcNow - session.Created).FormatTimeStamp(),
                ReadBytes = session.ReadBytes,
                SentBytes = session.SentBytes,
                PublishMessagesPerSecond = session.PublisherInfo.PublishMessagesPerSecond.Value,
                PublishPayloadsPerSecond = session.PublisherInfo.PublishPayloadsPerSecond.Value,
                DeliveryMessagesPerSecond = session.Subscribers.DeliveryMessagesPerSecond.Value,
                DeliveryPayloadsPerSecond = session.Subscribers.DeliveryMessagesPerSecond.Value,
                
                ProtocolVersion = session.ProtocolVersion,
                Topics =  session.PublisherInfo.GetTopicsToPublish().Select(TopicConnectionHubModel.Create),
                Queues = session.Subscribers.GetAll().Select(itm => SessionSubscriberHubModel.Create(itm.Subscriber))
            };
        }


    }
}