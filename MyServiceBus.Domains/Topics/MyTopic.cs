#nullable enable
using System;
using System.Collections.Generic;
using MyServiceBus.Abstractions;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Topics
{

    public class MyTopic
    {

        private readonly TopicQueueList _topicQueueList; 
        public MessageIdGenerator MessageIdGenerator { get; }
        public MessagesContentCache MessagesContentCache { get; }
        public AsyncLock MessagesPersistenceLock { get; }
        public TopicMetrics Metrics { get; } = new ();
        
        public override string ToString()
        {
            return "Topic: " + TopicId;
        }

        public MyTopic(string id, long startMessageId)
        {
            TopicId = id;
            MessageIdGenerator = new MessageIdGenerator(startMessageId);
            _topicQueueList = new TopicQueueList();
            MessagesContentCache = new MessagesContentCache(id);
            MessagesPersistenceLock = new AsyncLock(new object());
        }
        
        public string TopicId { get; }
        

        internal void OenSecondTimer()
        {
            Metrics.OneSecondTimer();
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
            return _topicQueueList.CreateQueueIfNotExists(this, queueName, topicQueueType, MessageIdGenerator.Value, overrideTopicQueueType);
        }

        public IReadOnlyList<MessageContentGrpcModel> Publish(IEnumerable<PublishMessage> messages, DateTime now)
        {
            
            Metrics.PublishPayloadsPerSecond.EventHappened();
            var newMessages = new List<MessageContentGrpcModel>();

            MessageIdGenerator.Lock(generator =>
            {
                foreach (var message in messages)
                {

                    var newMessage = new MessageContentGrpcModel
                    {
                        MessageId = generator.GetNextMessageId(),
                        Created = now,
                        Data = message.Data,
                        MetaData = message.MetaData
                    };

                    newMessages.Add(newMessage);

                }
            });

            MessagesContentCache.AddMessages(newMessages);
            Metrics.PublishedMessagesPerSecond.EventsHappened(newMessages.Count);
            return newMessages;
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
                MessageId = MessageIdGenerator.Value,
                QueueSnapshots = _topicQueueList.GetQueuesSnapshot()
            };
        }


        public TopicQueue GetQueue(string queueId)
        {
            return _topicQueueList.GetQueue(queueId);
        }
        
        public TopicQueue? TryGetQueue(string queueId)
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

        
        public void SetQueueMessageId(long minMessageId, TopicQueue topicQueue)
        {
            MessageIdGenerator.Lock(_ =>
            {
                topicQueue.SetMinimalMessageId(minMessageId, MessageIdGenerator.Value);
            });
        }
        
    }


}