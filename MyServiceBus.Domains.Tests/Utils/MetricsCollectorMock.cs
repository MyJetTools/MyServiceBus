namespace MyServiceBus.Domains.Tests.Utils
{
    public class MetricsCollectorMock : IMetricCollector
    {
        public void TopicQueueSize(string topicId, long queueSize)
        {
            
        }

        public void ToPersistSize(string topicId, long queueSize)
        {
            
        }
    }
}