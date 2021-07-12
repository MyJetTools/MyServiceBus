using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Domains.QueueSubscribers;

namespace MyServiceBus.Domains.Sessions
{
    public class SubscribersInfo
    {

        public MetricPerSecond DeliveryPayloadsPerSecond { get; } = new MetricPerSecond();
        public MetricPerSecond DeliveryMessagesPerSecond { get; } = new MetricPerSecond();

        
        private readonly Dictionary<string, Dictionary<string, SubscriberInfo>> _subscribers = new Dictionary<string, Dictionary<string, SubscriberInfo>>();

        private IReadOnlyList<SubscriberInfo> _subscribersAsList = Array.Empty<SubscriberInfo>();

        private readonly object _lockObject = new object();


        private readonly DateTime _defaultDateTime = DateTime.UtcNow;


        public IReadOnlyList<SubscriberInfo> GetAll()
        {
            return _subscribersAsList;
        }

        public DateTime GetSubscriberLastPacketDateTime(string topicName, string queueId)
        {
            lock (_lockObject)
            {
                if (_subscribers.TryGetValue(topicName, out var subscriberInfo))
                {
                    if (subscriberInfo.TryGetValue(queueId, out var result))
                        return result.LastDeliveryPacketSendDateTime;
                } 
            }
            return _defaultDateTime;
        }

        private Dictionary<string, SubscriberInfo> GetSubscribersByTopic(string topicId)
        {

            if (_subscribers.TryGetValue(topicId, out var result))
                return result;

            result = new Dictionary<string, SubscriberInfo>();
            _subscribers.Add(topicId, result);
            return result;
        }

        public void Subscribe(QueueSubscriber queueSubscriber)
        {
            lock (_lockObject)
            {
                var byTopic = GetSubscribersByTopic(queueSubscriber.TopicQueue.Topic.TopicId);
                byTopic.Add(queueSubscriber.TopicQueue.QueueId, new SubscriberInfo(queueSubscriber));
                _subscribersAsList = _subscribers.SelectMany(itm1 => itm1.Value.Select(itm => itm.Value)).ToList();
            }
            
        }

        public void OneSecondTimer()
        {
            DeliveryPayloadsPerSecond.OneSecondTimer();
            DeliveryMessagesPerSecond.OneSecondTimer();

            lock (_lockObject)
            {
                foreach (var subscriberInfo in _subscribers.Values.SelectMany(subscribers => subscribers.Values))
                {
                    subscriberInfo.OnSecondTimer();
                }
            }
        }
    }
}