using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains
{
    public interface IMetricCollector
    {
        void UpdateTopicQueueSize(MyTopic topic);
        void UpdateTopicQueueSize(TopicQueue topicQueue);
        void UpdateToPersistSize(string topicId, long queueSize);
    }
}