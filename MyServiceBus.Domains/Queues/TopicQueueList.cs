using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Abstractions;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains.Queues
{


    
    public class TopicQueueList
    {
        private readonly object _lockObject = new object();
        private readonly DictionaryWithList<string, TopicQueue> _topicQueues = new DictionaryWithList<string, TopicQueue>();
        public int SnapshotId { get; private set; }
                
        public void Init(MyTopic topic, IQueueSnapshot snapshot)
        {
            lock (_lockObject)
            {
                var queue = new TopicQueue(topic, snapshot.QueueId, snapshot.TopicQueueType, snapshot.Ranges);
                _topicQueues.Add(snapshot.QueueId, queue);
                SnapshotId++;
            }
        }
        
        public TopicQueue CreateQueueIfNotExists(MyTopic topic, string queueName, TopicQueueType topicQueueType, long messageId, bool overrideTopicQueueType)
        {
            lock (_lockObject)
            {

                var queue = _topicQueues.TryGetValue(queueName);

                if (queue == null)
                {
                    queue = new TopicQueue(topic, queueName, topicQueueType, messageId);
                    _topicQueues.Add(queueName, queue);
                }
                else
                {
                    if (overrideTopicQueueType)
                        queue.UpdateTopicQueueType(topicQueueType);
                }

                SnapshotId++;

                return queue;
            }
 
        }

        public void DeleteQueue(string queueName)
        {
            lock (_lockObject)
            {
                if (_topicQueues.Remove(queueName))
                {
                    SnapshotId++;
                }
            }
        }
        
        public void DeleteQueueIfItDoesNotHaveSubscribers(string queueName)
        {
            lock (_lockObject)
            {
                var queue = _topicQueues.TryGetValue(queueName);
                if (queue == null)
                    return;

                queue.DisposeIfItDoesNotHaveSubscribers();

                if (queue.Disposed)
                {
                    if (_topicQueues.Remove(queueName))
                    {
                        SnapshotId++;
                    }
                }
            }
        }

        public IReadOnlyList<TopicQueue> GetAll()
        {
            lock (_lockObject)
            {
                return _topicQueues.GetAllValues();    
            }
        }
        
        public (IReadOnlyList<TopicQueue> queues, int snapshotId) GetQueuesWithSnapshotId()
        {
            lock (_lockObject)
            {
                return (_topicQueues.GetAllValues(), SnapshotId);    
            }
        }
 
        public TopicQueue GetQueue(string queueId)
        {
            lock (_topicQueues)
            {
                var result = _topicQueues.TryGetValue(queueId);
                
                if (result == null)
                    throw new Exception($"Queue with id {queueId} is not found");

                return result;
            }
        }
        
        public TopicQueue TryGetQueue(string queueId)
        {
            lock (_topicQueues)
            {
                return _topicQueues.TryGetValue(queueId);    
            }
        }

        public IReadOnlyList<IQueueSnapshot> GetQueuesSnapshot()
        {
            lock (_lockObject)
            {
                List<IQueueSnapshot> result = null;

                foreach (var topicQueue in _topicQueues.GetAllValues().Where(itm => itm.TopicQueueType != TopicQueueType.DeleteOnDisconnect))
                {
                    var snapshot = topicQueue.GetSnapshot();

                    result ??= new List<IQueueSnapshot>();
                    result.Add(snapshot);
                }

                if (result == null)
                    return Array.Empty<IQueueSnapshot>();

                return result; 
            }

        }
        
        public void OneSecondTimer()
        {
            foreach (var topicQueue in GetAll())
                topicQueue.OneSecondTimer();   
        }


        public long GetMessagesCount()
        {
            return GetAll().Sum(itm => itm.GetMessagesCount());
        }
    }
}