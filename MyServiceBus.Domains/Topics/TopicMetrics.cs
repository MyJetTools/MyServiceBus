using System.Collections.Generic;
using MyServiceBus.Domains.Metrics;

namespace MyServiceBus.Domains.Topics
{
    public class TopicMetrics
    {
        public MetricPerSecond PublishPayloadsPerSecond { get; } = new MetricPerSecond();
        public MetricPerSecond PublishedMessagesPerSecond { get; } = new MetricPerSecond();


        private readonly MetricHistory<int> _publishPerSecond = new MetricHistory<int>();

        private readonly object _lock = new object();
        public void OneSecondTimer()
        {
            lock (_lock)
            {
                _publishPerSecond.PutData(PublishedMessagesPerSecond.Value);
                PublishedMessagesPerSecond.OneSecondTimer();
                PublishPayloadsPerSecond.OneSecondTimer();
            }
        }

        public IReadOnlyList<int> GetPublishPerSecondHistory()
        {
            lock (_lock)
            {
                return _publishPerSecond.GetItems();
            }
        }
    }
}