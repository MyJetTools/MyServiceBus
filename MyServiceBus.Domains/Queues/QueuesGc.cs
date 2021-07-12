using System;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains.Queues
{
    public class QueuesGc
    {
        private readonly TopicsList _topicsList;
        private readonly IMyServiceBusSettings _settings;

        public QueuesGc(TopicsList topicsList, IMyServiceBusSettings settings)
        {
            _topicsList = topicsList;
            _settings = settings;
        }
        
        public void TimerGc(DateTime now)
        {
            foreach (var myTopic in _topicsList.Get())
            {
                foreach (var topicQueue in myTopic.Queues.GetAll())
                {
                    
                    if (topicQueue.SubscribersCount > 0)
                        continue;

                    if (now - topicQueue.Metrics.LastDisconnect > _settings.QueueGcTimeout)
                    {
                        myTopic.Queues.DeleteQueue(topicQueue.QueueId);
                    }
                    
                }
            }
        }
    }
}