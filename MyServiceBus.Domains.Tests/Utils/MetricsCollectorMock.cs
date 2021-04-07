using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains.Tests.Utils
{
    public class MetricsCollectorMock : IMetricCollector
    {
        public void UpdateTopicQueueSize(MyTopic topic)
        {
            
        }

        public void UpdateTopicQueueSize(TopicQueue topicQueue)
        {

        }

        public void UpdateToPersistSize(string topicId, long queueSize)
        {
            
        }

    }
}