using System;
using System.Collections.Generic;
using System.Linq;
using DotNetCoreDecorators;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains.Queues
{
    public class TopicQueueList
    {
        private readonly object _lockObject;
        private Dictionary<string, TopicQueue> _topicQueues = new ();
        private IReadOnlyList<TopicQueue> _queuesAsReadOnlyList = Array.Empty<TopicQueue>();

        public TopicQueueList(object lockObject)
        {
            _lockObject = lockObject;
        }
                
        public void Init(MyTopic topic, string queueName, bool deleteOnDisconnect,  IEnumerable<IQueueIndexRange> ranges,
            object lockObject)
        {

            lock (_lockObject)
            {
                var queue = new TopicQueue(topic, queueName, deleteOnDisconnect, ranges, lockObject);
                _topicQueues.Add(queueName, queue);
                _queuesAsReadOnlyList = _topicQueues.Values.AsReadOnlyList();

                CalcMinMessageId();
            }
   
        }
        
        public TopicQueue CreateQueueIfNotExists(MyTopic topic, string queueName, bool deleteOnDisconnect, long messageId,
            object lockObject)
        {

            lock (_lockObject)
            {

                var (added, newDictionary, value) = _topicQueues.AddIfNotExistsByCreatingNewDictionary(queueName,
                    () => new TopicQueue(topic, queueName, deleteOnDisconnect, messageId, lockObject));

                if (!added)
                    return value;

                _topicQueues = newDictionary;
                _queuesAsReadOnlyList = _topicQueues.Values.AsReadOnlyList(); 

                return value;
            }
 
        }

        public void DeleteQueue(string queueName)
        {
            lock (_lockObject)
            {
                if (!_topicQueues.ContainsKey(queueName))
                    return;

                var newDictionary = _topicQueues.RemoveIfExistsByCreatingNewDictionary(queueName,
                    (k1, k2)=> k1 == k2);

                if (!newDictionary.removed) 
                    return;
                
                _topicQueues = newDictionary.result;
                _queuesAsReadOnlyList = _topicQueues.Values.AsReadOnlyList(); 

            }
        }


        public IReadOnlyList<TopicQueue> GetQueues()
        {
            return _queuesAsReadOnlyList;
        }

        public long MinMessageId { get; private set; }


        public long GetMessagesCount()
        {
            return _queuesAsReadOnlyList.Count == 0 
                ? 0 
                : _queuesAsReadOnlyList.Max(itm => itm.GetMessagesCount());
        }

        public void CalcMinMessageId()
        {
            if (_topicQueues.Count == 0)
            {
                MinMessageId = 0;
                return;
            }

            MinMessageId = _topicQueues.Values.Min(itm => itm.GetMinId());
        }
        
        public TopicQueue GetQueue(string queueId)
        {
            if (_topicQueues.TryGetValue(queueId, out var result))
                return result;

            throw new Exception($"Queue with id {queueId} is not found");
        }


        





        public long GetQueueMessagesCount(string queueName)
        {

            if (!_topicQueues.ContainsKey(queueName))
                throw new Exception($"Queue [{queueName}] is not found");

            var topicQueue = _topicQueues[queueName];

            return topicQueue.GetMessagesCount();
        }


        public IReadOnlyList<IQueueSnapshot> GetQueuesSnapshot()
        {
            List<IQueueSnapshot> result = null;

            foreach (var topicQueue in _topicQueues.Values.Where(itm => !itm.DeleteOnDisconnect))
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
}