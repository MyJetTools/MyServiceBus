using System;
using MyServiceBus.Domains.QueueSubscribers;

namespace MyServiceBus.Domains.Sessions
{
    public class SubscriberInfo
    {
        public MetricPerSecond DeliveryPayloadsPerSecond { get; } = new MetricPerSecond();
        public MetricPerSecond DeliveryMessagesPerSecond { get; } = new MetricPerSecond();
        
        public DateTime LastDeliveryPacketSendDateTime { get; set; }

        public SubscriberInfo(QueueSubscriber subscriber)
        {
            Subscriber = subscriber;
        }
        public QueueSubscriber Subscriber { get;}


        public void OnSecondTimer()
        {
            DeliveryPayloadsPerSecond.OneSecondTimer();
            DeliveryMessagesPerSecond.OneSecondTimer();
        }
    }
}