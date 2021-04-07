using MyServiceBus.Domains;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Topics;
using Prometheus;

namespace MyServiceBus.Server.Services
{
    public class PrometheusMetrics : IMetricCollector
    {
        public readonly Gauge ServiceBusPersistSize = Metrics.CreateGauge("service_bus_persist_queue_size",
            "Queue to persist size", new GaugeConfiguration
            {
                LabelNames = new[] {"topicId"}
            });
        
        public readonly Gauge ServiceBusTopicQSize = Metrics.CreateGauge("service_bus_unhandled_topic_size",
            "Max unhandled queue size of a topic", new GaugeConfiguration
            {
                LabelNames = new[] {"topicId"}
            });
        
        
        public readonly Gauge ServiceBusQSize = Metrics.CreateGauge("service_bus_unhandled_queue_size",
            "Max unhandled queue size of a topic", new GaugeConfiguration
            {
                LabelNames = new[] {"topicId", "queueId"}
            });
        public void UpdateTopicQueueSize(MyTopic topic)
        {
            ServiceBusTopicQSize
                .WithLabels(topic.TopicId)
                .Set(topic.MessagesCount);
        }

        public void UpdateTopicQueueSize(TopicQueue topicQueue)
        {
            ServiceBusQSize
                .WithLabels(topicQueue.Topic.TopicId, topicQueue.QueueId)
                .Set(topicQueue.GetMessagesCount());
        }

        public void UpdateToPersistSize(string topicId, long queueSize)
        {
            ServiceBusPersistSize.WithLabels(topicId).Set(queueSize);
        }
 
   
    }
}