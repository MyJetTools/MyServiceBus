using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Domains.MessagesContent;

namespace MyServiceBus.Domains.Persistence
{

    public interface IMessagesToPersistQueue
    {
        void EnqueueToPersist(string topicId, IEnumerable<MessageContent> messages);

        IReadOnlyList<MessageContent> GetMessagesToPersist(string topicId);

        IReadOnlyList<(string topic, int count)> GetMessagesToPersistCount();

    } 
    
    public class MessagesToPersistQueue : IMessagesToPersistQueue
    {
        private readonly IMetricCollector _metricCollector;

        private readonly Dictionary<string, List<MessageContent>> _messagesToPersist 
            = new Dictionary<string, List<MessageContent>>();


        public MessagesToPersistQueue(IMetricCollector metricCollector)
        {
            _metricCollector = metricCollector;
        }

        public IReadOnlyList<(string topic, int count)> GetMessagesToPersistCount()
        {
            lock (_messagesToPersist)
                return _messagesToPersist.Select(itm => (itm.Key, itm.Value.Count)).ToList();
        }

        public void EnqueueToPersist(string topicId, IEnumerable<MessageContent> messages)
        {
            lock (_messagesToPersist)
            {
                
                if (!_messagesToPersist.ContainsKey(topicId))
                    _messagesToPersist.Add(topicId, new List<MessageContent>());

                _messagesToPersist[topicId].AddRange(messages);
                
                PutMessagesToPersistMetric(topicId);
            }
        }

        private void PutMessagesToPersistMetric(string topicId)
        {
            _metricCollector.ToPersistSize(topicId,_messagesToPersist[topicId].Count);
        }

        public IReadOnlyList<MessageContent> GetMessagesToPersist(string topicId)
        {

            lock (_messagesToPersist)
            {
                if (!_messagesToPersist.ContainsKey(topicId))
                    return Array.Empty<MessageContent>();

                var queue = _messagesToPersist[topicId];
                
                if (queue.Count == 0)
                    return Array.Empty<MessageContent>();
                
                _messagesToPersist[topicId] = new List<MessageContent>();
                PutMessagesToPersistMetric(topicId);
                
                return queue;
            }

        }

    }


    public class MessagesToPersistInProxyMode : IMessagesToPersistQueue
    {
        public void EnqueueToPersist(string topicId, IEnumerable<MessageContent> messages)
        {
            
        }

        public IReadOnlyList<MessageContent> GetMessagesToPersist(string topicId)
        {
            return Array.Empty<MessageContent>();
        }

        public IReadOnlyList<(string topic, int count)> GetMessagesToPersistCount()
        {
            return Array.Empty<(string topic, int count)>();
        }
    }
    
}