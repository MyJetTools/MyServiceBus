using System.Collections.Generic;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Server.Hubs
{
    
    
    public class MonitoringConnectionTopicContext
    {
        public MyTopic Topic { get; private set; }

        private int _synchronizedQueuesSnapshotId;
        

        public IReadOnlyList<TopicQueue> GetQueuesIfNotSynchronized()
        {
            var (queues, snapshotId) = Topic.GetQueuesWithSnapshotId();
            lock (this)
            {

                if (snapshotId == _synchronizedQueuesSnapshotId)
                    return null;

                _synchronizedQueuesSnapshotId = snapshotId;
                
                return queues;
            }
        }
        
        public static MonitoringConnectionTopicContext Create(MyTopic topic)
        {
            return new ()
            {
                Topic = topic
            };
        }
        
    }
}