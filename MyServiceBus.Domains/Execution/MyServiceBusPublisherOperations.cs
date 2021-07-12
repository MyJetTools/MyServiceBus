using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Execution
{

    public class PublishMessage
    {
        public MessageContentMetaDataItem[] MetaData { get; set; }
        
        public byte[] Data { get; set; }
    }
    
    public class MyServiceBusPublisherOperations
    {
        private readonly TopicsList _topicsList;
        private readonly IMessagesToPersistQueue _messagesToPersistQueue;
        private readonly MyServiceBusDeliveryHandler _myServiceBusDeliveryHandler;
        private readonly MessageContentPersistentProcessor _messageContentPersistentProcessor;
        private readonly TopicsAndQueuesPersistenceProcessor _topicsAndQueuesPersistenceProcessor;

        public MyServiceBusPublisherOperations(TopicsList topicsList, 
            IMessagesToPersistQueue messagesToPersistQueue,
            MyServiceBusDeliveryHandler myServiceBusDeliveryHandler, 
            MessageContentPersistentProcessor messageContentPersistentProcessor,
            TopicsAndQueuesPersistenceProcessor topicsAndQueuesPersistenceProcessor
            )
        {
            _topicsList = topicsList;
            _messagesToPersistQueue = messagesToPersistQueue;
            _myServiceBusDeliveryHandler = myServiceBusDeliveryHandler;
            _messageContentPersistentProcessor = messageContentPersistentProcessor;
            _topicsAndQueuesPersistenceProcessor = topicsAndQueuesPersistenceProcessor;
        }


        private void PersistMessagesContent(MyTopic topic)
        {
            Task.Run(()=>_messageContentPersistentProcessor.PersistMessageContentAsync(topic));
        }

        public async ValueTask<MyTopic> CreateTopicIfNotExists(MyServiceBusSession session, string topicId)
        {
            var topic = _topicsList.AddIfNotExists(topicId);
            await _topicsAndQueuesPersistenceProcessor.PersistTopicsAndQueuesInBackgroundAsync(_topicsList.Get());
            session.PublisherInfo.AddIfNotExists(topicId);
            return topic;
        }
        

        public async ValueTask<ExecutionResult> PublishAsync(MyServiceBusSession session, 
            string topicId, IEnumerable<PublishMessage> messages, DateTime now, 
            bool persistImmediately)
        {
            
            var topic = _topicsList.TryGet(topicId);

            if (topic == null)
                return ExecutionResult.TopicNotFound;
            
            session.PublisherInfo.AddIfNotExists(topicId);
            
            var addedMessages = topic.Publish(messages, now);

            _messagesToPersistQueue.EnqueueToPersist(topicId, addedMessages);
            
            if (addedMessages.Count == 0)
                return ExecutionResult.Ok;
            
            if (persistImmediately)
                PersistMessagesContent(topic);

            foreach (var topicQueue in topic.GetQueues())
                topicQueue.EnqueueMessages(addedMessages.Select(itm => itm.MessageId));

            await _myServiceBusDeliveryHandler.SendMessagesAsync(topic);

            return ExecutionResult.Ok;
        }
        
    }
}