using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains.Execution
{
    
    public class MyServiceBusPublisher
    {
        private readonly TopicsList _topicsList;
        private readonly IMessagesToPersistQueue _messagesToPersistQueue;
        private readonly MyServiceBusDeliveryHandler _myServiceBusDeliveryHandler;
        private readonly TopicsAndQueuesPersistenceProcessor _topicsAndQueuesPersistenceProcessor;
        private readonly MessageContentCacheByTopic _messageContentCacheByTopic;
        private readonly MessageContentPersistentProcessor _messageContentPersistentProcessor;

        public MyServiceBusPublisher(TopicsList topicsList, 
            IMessagesToPersistQueue messagesToPersistQueue,
            MyServiceBusDeliveryHandler myServiceBusDeliveryHandler, 
            TopicsAndQueuesPersistenceProcessor topicsAndQueuesPersistenceProcessor,
            MessageContentCacheByTopic messageContentCacheByTopic,
            MessageContentPersistentProcessor messageContentPersistentProcessor
            )
        {
            _topicsList = topicsList;
            _messagesToPersistQueue = messagesToPersistQueue;
            _myServiceBusDeliveryHandler = myServiceBusDeliveryHandler;
            _topicsAndQueuesPersistenceProcessor = topicsAndQueuesPersistenceProcessor;
            _messageContentCacheByTopic = messageContentCacheByTopic;
            _messageContentPersistentProcessor = messageContentPersistentProcessor;
        }
        

        public async ValueTask<ExecutionResult> PublishAsync(MySession session, string topicId, IEnumerable<byte[]> messages, DateTime now, bool persistImmediately
            )
        {
            
            var topic = _topicsList.TryFindTopic(topicId);

            if (topic == null)
                return ExecutionResult.TopicNotFound;
            
            session.PublishToTopic(topicId);
            
            var addedMessages = topic.Publish(messages, now, msgs =>
            {
                var cache = _messageContentCacheByTopic.GetTopic(topicId);
                cache.AddMessages(msgs); 
                _messagesToPersistQueue.EnqueueToPersist(topicId, msgs);
            });

            
            if (addedMessages.Count == 0)
                return ExecutionResult.Ok;
            
            Task persistentQueueTask = null;
            Task persistMessagesTask = null;
            if (persistImmediately)
            {
                persistentQueueTask =  _topicsAndQueuesPersistenceProcessor.PersistTopicsAndQueuesInBackgroundAsync(_topicsList.Get());
                persistMessagesTask = _messageContentPersistentProcessor.PersistMessageContentInBackgroundAsync(topic);
            }

            await _myServiceBusDeliveryHandler.SendMessagesAsync(topic);

            if (persistentQueueTask != null)
                await persistentQueueTask;

            if (persistMessagesTask != null)
                await persistMessagesTask;

            return ExecutionResult.Ok;
        }
        
    }
}