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

        public TopicQueueList Queues { get; }
        public MessageIdGenerator MessageIdGenerator { get; }
        public MessagesContentCache MessagesContentCache { get; }
        public AsyncLock MessagesPersistenceLock { get; }
        public TopicMetrics Metrics { get; } = new TopicMetrics();
        
        public override string ToString()
        {
            return "Topic: " + TopicId;
        }

        public MyTopic(string id, long startMessageId)
        {
            TopicId = id;
            MessageIdGenerator = new MessageIdGenerator(startMessageId);
            Queues = new TopicQueueList();
            MessagesContentCache = new MessagesContentCache(id);
            MessagesPersistenceLock = new AsyncLock(new object());
        }
        
        public string TopicId { get; }
        

        internal void OenSecondTimer()
        {
            Metrics.OneSecondTimer();
            Queues.OneSecondTimer();
        }

        public TopicQueue CreateQueueIfNotExists(string queueName, TopicQueueType topicQueueType, bool overrideTopicQueueType)
        {
            return Queues.CreateQueueIfNotExists(this, queueName, topicQueueType, MessageIdGenerator.Value, overrideTopicQueueType);
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


        public ITopicPersistence GetQueuesSnapshot()
        {
            return new TopicPersistence
            {
                TopicId = TopicId,
                MessageId = MessageIdGenerator.Value,
                QueueSnapshots = Queues.GetQueuesSnapshot()
            };
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

                Queues.Init(this, queueSnapshot);
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