using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Persistence
{

    public interface IMessagesToPersistQueue
    {
        void EnqueueToPersist(string topicId, IEnumerable<MessageContentGrpcModel> messages);

        IReadOnlyList<MessageContentGrpcModel> GetMessagesToPersist(string topicId);

        IReadOnlyList<(string topic, int count)> GetMessagesToPersistCount();
        
        int Count { get; }

    } 
    
    public class MessagesToPersistQueue : IMessagesToPersistQueue
    {
        private readonly IMetricCollector _metricCollector;

        private readonly Dictionary<string, List<MessageContentGrpcModel>> _messagesToPersist 
            = new ();

        public MessagesToPersistQueue(IMetricCollector metricCollector)
        {
            _metricCollector = metricCollector;
        }

        public IReadOnlyList<(string topic, int count)> GetMessagesToPersistCount()
        {
            lock (_messagesToPersist)
                return _messagesToPersist.Select(itm => (itm.Key, itm.Value.Count)).ToList();
        }

        public int Count
        {
            get
            {
                lock (_messagesToPersist)
                {
                    return _messagesToPersist.Values.Sum(itm => itm.Count);
                }
            }
        }

        public void EnqueueToPersist(string topicId, IEnumerable<MessageContentGrpcModel> messages)
        {
            lock (_messagesToPersist)
            {
                
                if (!_messagesToPersist.ContainsKey(topicId))
                    _messagesToPersist.Add(topicId, new List<MessageContentGrpcModel>());

                _messagesToPersist[topicId].AddRange(messages);
                
                PutMessagesToPersistMetric(topicId);
            }
        }

        private void PutMessagesToPersistMetric(string topicId)
        {
            _metricCollector.UpdateToPersistSize(topicId,_messagesToPersist[topicId].Count);
        }

        public IReadOnlyList<MessageContentGrpcModel> GetMessagesToPersist(string topicId)
        {

            lock (_messagesToPersist)
            {
                if (!_messagesToPersist.ContainsKey(topicId))
                    return Array.Empty<MessageContentGrpcModel>();

                var queue = _messagesToPersist[topicId];
                
                if (queue.Count == 0)
                    return Array.Empty<MessageContentGrpcModel>();
                
                _messagesToPersist[topicId] = new List<MessageContentGrpcModel>();
                PutMessagesToPersistMetric(topicId);
                
                return queue;
            }

        }

    }
}