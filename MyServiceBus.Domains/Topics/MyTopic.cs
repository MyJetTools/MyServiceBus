using System;
using System.Collections.Generic;
using MyServiceBus.Abstractions;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Topics
{

    public class MyTopic
    {
        private readonly IMetricCollector _metricCollector;

        private readonly TopicQueueList _topicQueueList; 
        public MessageIdGenerator MessageId { get; }
        public MessagesContentCache MessagesContentCache { get; }

        public readonly AsyncLock MessagesPersistenceLock;
        
        public override string ToString()
        {
            return "Topic: " + TopicId;
        }

        public MyTopic(string id, long startMessageId, IMetricCollector metricCollector)
        {
            _metricCollector = metricCollector;
            TopicId = id;
            MessageId = new MessageIdGenerator(startMessageId);
            _topicQueueList = new TopicQueueList();
            MessagesContentCache = new MessagesContentCache(id);
            MessagesPersistenceLock = new AsyncLock(new object());
        }
        
        public string TopicId { get; }

        private int _messagePerSecond;
        public int MessagesPerSecond { get; private set; }
        
        private int _requestsPerSecond;
        public int RequestsPerSecond { get; private set; }

        internal void KickMetricsTimer()
        {
            MessagesPerSecond = _messagePerSecond;
            _messagePerSecond = 0;

            RequestsPerSecond = _requestsPerSecond;
            _requestsPerSecond = 0;

            _topicQueueList.OneSecondTimer();
        }

        public IReadOnlyList<TopicQueue> GetQueues()
        {
            return _topicQueueList.GetQueues();
        }
        
        public (IReadOnlyList<TopicQueue> queues, int snapshotId) GetQueuesWithSnapshotId()
        {
            return _topicQueueList.GetQueuesWithSnapshotId();
        }

        public long MessagesCount => _topicQueueList.GetMessagesCount();

        public void DeleteQueue(string queueName)
        {
            _topicQueueList.DeleteQueue(queueName);
        }

        public TopicQueue CreateQueueIfNotExists(string queueName, TopicQueueType topicQueueType, bool overrideTopicQueueType)
        {
            return _topicQueueList.CreateQueueIfNotExists(this, queueName, topicQueueType, MessageId.Value, overrideTopicQueueType);
        }

        public IReadOnlyList<MessageContentGrpcModel> Publish(IEnumerable<byte[]> messages, DateTime now)
        {
            _requestsPerSecond++;

            var newMessages = new List<MessageContentGrpcModel>();

            MessageId.Lock(generator =>
            {
                foreach (var message in messages)
                {

                    var newMessage = new MessageContentGrpcModel
                    {
                        MessageId = generator.GetNextMessageId(),
                        Created = now,
                        Data = message
                    };

                    newMessages.Add(newMessage);

                    _messagePerSecond++;
                }
            });

            MessagesContentCache.AddMessages(newMessages);
            return newMessages;
        }

        public TopicQueue ConfirmDelivery(string queueName, long confirmationId, bool ok, QueueWithIntervals okMessages)
        {
            var queue = _topicQueueList.GetQueue(queueName);

            var subscriber = queue.SubscribersList.TryGetSubscriber(confirmationId);

            if (subscriber == null)
                return queue;

            var duration = DateTime.UtcNow - subscriber.OnDeliveryStart;

            duration = subscriber.MessagesOnDelivery.Count == 0
                ? default
                : duration;
            
            if (okMessages != null)
                queue.ConfirmSomeDelivered(subscriber, duration, okMessages);
            if (ok)
                queue.ConfirmDelivery(subscriber, duration);
            else
                queue.ConfirmNotDelivery(subscriber, duration);  
 
            _topicQueueList.CalcMinMessageId();

            return queue;
        }

        public TopicQueue ConfirmMessagesButNotDelivery(string queueName, long confirmationId, QueueWithIntervals queueWithIntervals)
        {
            var queue = _topicQueueList.GetQueue(queueName);

            var subscriber = queue.SubscribersList.TryGetSubscriber(confirmationId);

            if (subscriber == null)
                return queue;
            
            var duration = DateTime.UtcNow - subscriber.OnDeliveryStart;

            duration = subscriber.MessagesOnDelivery.Count == 0
                ? default
                : duration;
            
            queue.ConfirmMessagesByNotDelivery(subscriber, duration, queueWithIntervals);
            
            _topicQueueList.CalcMinMessageId();

            return queue;
        }
        

        public long GetQueueMessagesCount(string queueName)
        {
            return _topicQueueList.GetQueueMessagesCount(queueName);
        }

        public ITopicPersistence GetQueuesSnapshot()
        {
            return new TopicPersistence
            {
                TopicId = TopicId,
                MessageId = MessageId.Value,
                QueueSnapshots = _topicQueueList.GetQueuesSnapshot()
            };
        }

        public long GetMinMessageId()
        {
            return _topicQueueList.MinMessageId;
        }

        public TopicQueue GetQueue(string queueId)
        {
            return _topicQueueList.GetQueue(queueId);
        }
        
        public TopicQueue TryGetQueue(string queueId)
        {
            return _topicQueueList.TryGetQueue(queueId);
        }

        public void Init(IReadOnlyList<IQueueSnapshot> queueSnapshots)
        {
            foreach (var queueSnapshot in queueSnapshots)
            {
                Console.WriteLine($"Restoring Queue: {TopicId}.{queueSnapshot.QueueId} with Ranges:");
                foreach (var indexRange in queueSnapshot.Ranges)
                {
                    Console.WriteLine(indexRange.FromId + "-" + indexRange.ToId);
                }

                _topicQueueList.Init(this, queueSnapshot);
            }
        }

        public void SetQueueMessageId(string queueId, long messageId)
        {

            var queue = _topicQueueList.GetQueue(queueId);

            if (queue == null)
                throw new Exception($"Queue {queueId} is not found");

            if (messageId < 0)
                throw new Exception($"MessageId must be above 0");

            if (messageId > MessageId.Value)
                throw new Exception($"MessageId can not be greater than the Topic messageId which is {MessageId} now");

            queue.SetInterval(messageId, MessageId.Value);
        }
        
    }


}