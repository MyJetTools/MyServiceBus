using System.Collections.Generic;
using MyServiceBus.Domains.Metrics;

namespace MyServiceBus.Domains.Topics
{
    public class TopicMetrics
    {
        public MetricPerSecond PublishPayloadsPerSecond { get; } = new ();
        public MetricPerSecond PublishedMessagesPerSecond { get; } = new();


        private readonly MetricHistory<int> _publishPerSecond = new ();

        private readonly object _lock = new();
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