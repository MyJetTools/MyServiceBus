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
        private readonly MessageContentPersistentProcessor _messageContentPersistentProcessor;

        public MyServiceBusPublisher(TopicsList topicsList, 
            IMessagesToPersistQueue messagesToPersistQueue,
            MyServiceBusDeliveryHandler myServiceBusDeliveryHandler, 
            MessageContentPersistentProcessor messageContentPersistentProcessor
            )
        {
            _topicsList = topicsList;
            _messagesToPersistQueue = messagesToPersistQueue;
            _myServiceBusDeliveryHandler = myServiceBusDeliveryHandler;
            _messageContentPersistentProcessor = messageContentPersistentProcessor;
        }


        private void PersistMessagesContent(MyTopic topic)
        {
            Task.Run(()=>_messageContentPersistentProcessor.PersistMessageContentAsync(topic));
        }
        

        public async ValueTask<ExecutionResult> PublishAsync(MyServiceBusSession session, 
            string topicId, IEnumerable<byte[]> messages, DateTime now, 
            bool persistImmediately)
        {
            
            var topic = _topicsList.TryGet(topicId);

            if (topic == null)
                return ExecutionResult.TopicNotFound;
            
            session.PublishToTopic(topicId);
            
            var addedMessages = topic.Publish(messages, now);

            _messagesToPersistQueue.EnqueueToPersist(topicId, addedMessages);
            
            if (addedMessages.Count == 0)
                return ExecutionResult.Ok;
            
            if (persistImmediately)
                PersistMessagesContent(topic);

            foreach (var topicQueue in topic.GetQueues())
                topicQueue.EnqueueMessages(addedMessages);

            await _myServiceBusDeliveryHandler.SendMessagesAsync(topic);

            return ExecutionResult.Ok;
        }
        
    }
}