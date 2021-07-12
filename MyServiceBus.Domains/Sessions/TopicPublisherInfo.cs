using System;
using System.Collections.Generic;

namespace MyServiceBus.Domains.Sessions
{
    public class TopicPublisherInfo
    {

        private Dictionary<string, DateTime> _topicsPublishers = new Dictionary<string, DateTime>();


        public readonly MetricPerSecond PublishPayloadsPerSecond = new MetricPerSecond();
        public readonly MetricPerSecond PublishMessagesPerSecond = new MetricPerSecond();


        public void AddIfNotExists(string topic)
        {

            if (_topicsPublishers.ContainsKey(topic))
            {
                _topicsPublishers[topic] = DateTime.UtcNow;
                return;
            }

            lock (_topicsPublishers)
            {
                var result = _topicsPublishers.AddIfNotExistsByCreatingNewDictionary(topic, () => DateTime.UtcNow);

                if (result.added)
                    _topicsPublishers = result.newDictionary;
            }

        }

        public bool IsTopicPublisher(string topicName)
        {
            return _topicsPublishers.ContainsKey(topicName);
        }
        
                
        public Dictionary<string, DateTime> GetTopicsToPublish()
        {
            return _topicsPublishers;
        }


        public void OneSecondTimer()
        {
            PublishMessagesPerSecond.OneSecondTimer();
            PublishPayloadsPerSecond.OneSecondTimer();
        }
    }
}