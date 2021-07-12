using System;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;


namespace MyServiceBus.Domains.Execution
{


    public enum ReplayMessageResult
    {
        Ok, MessageNotFound
    }

    public class MyServiceBusSubscriberOperations
    {
        private readonly MyServiceBusDeliveryHandler _myServiceBusDeliveryHandler;
        private readonly TopicsList _topicsList;
        private readonly MessagesPageLoader _messagesPageLoader;

        public MyServiceBusSubscriberOperations(MyServiceBusDeliveryHandler myServiceBusDeliveryHandler, 
            TopicsList topicsList, MessagesPageLoader messagesPageLoader)
        {
            _myServiceBusDeliveryHandler = myServiceBusDeliveryHandler;
            _topicsList = topicsList;
            _messagesPageLoader = messagesPageLoader;
        }

        public async ValueTask<QueueSubscriber> SubscribeToQueueAsync(TopicQueue topicQueue, MyServiceBusSession session)
        {
            var queueSubscriber = topicQueue.SubscribersList.Subscribe(session);
            await _myServiceBusDeliveryHandler.SendMessagesAsync(topicQueue);
            
            
            if (topicQueue.TopicQueueType == TopicQueueType.PermanentWithSingleConnection)
            {
                var subscriberToUnsubscribe  = topicQueue.SubscribersList.RemoveAllExceptThisOne(queueSubscriber);

                foreach (var subscriber in subscriberToUnsubscribe)
                    HandleAfterUnsubscribe(subscriber);
                    
            }
            return queueSubscriber;
        }

        private void HandleAfterUnsubscribe(QueueSubscriber subscriber)
        {
            if (subscriber.Status == SubscriberStatus.OnDelivery)
            {
                subscriber.TopicQueue.EnqueueMessages(subscriber.MessagesOnDelivery.Select(itm => itm.message.MessageId));
            }
        }

        public ValueTask ConfirmDeliveryAsync(TopicQueue topicQueue, long confirmationId)
        {
            var subscriber = topicQueue.SubscribersList.TryGetSubscriber(confirmationId);

            if (subscriber == null)
                throw new Exception($"Can not find subscriber by confirmationId {confirmationId}");

            subscriber.TopicQueue.ConfirmDelivered(subscriber.MessagesOnDelivery.Select(itm => itm.message.MessageId));
            subscriber.Reset();
            
            return _myServiceBusDeliveryHandler.SendMessagesAsync(topicQueue);
        }
        
        public ValueTask ConfirmNotDeliveryAsync(TopicQueue topicQueue, long confirmationId)
        {
            var subscriber = topicQueue.SubscribersList.TryGetSubscriber(confirmationId);

            if (subscriber == null)
                throw new Exception($"Can not find subscriber by confirmationId {confirmationId}");
            
            subscriber.TopicQueue.EnqueueMessagesBack(subscriber.MessagesOnDelivery.Select(itm => itm.message.MessageId));
            subscriber.Reset();
            
            return _myServiceBusDeliveryHandler.SendMessagesAsync(topicQueue);
        }
        
        public ValueTask ConfirmMessagesByConfirmedListAsync(TopicQueue topicQueue, long confirmationId, QueueWithIntervals confirmedMessages)
        {
            var subscriber = topicQueue.SubscribersList.TryGetSubscriber(confirmationId);

            if (subscriber == null)
                throw new Exception($"Can not find subscriber by confirmationId {confirmationId}");

            var messagesToPutBack =
                subscriber.MessagesOnDelivery.Where(itm => !confirmedMessages.HasMessageId(itm.message.MessageId))
                    .Select(itm => itm.message.MessageId).ToList();

            if (messagesToPutBack.Count > 0)
                subscriber.TopicQueue.EnqueueMessagesBack(messagesToPutBack);
   
            subscriber.Reset();
            return _myServiceBusDeliveryHandler.SendMessagesAsync(topicQueue);
        }
        
        public ValueTask ConfirmMessagesByNotConfirmedListAsync(TopicQueue topicQueue, long confirmationId, QueueWithIntervals notConfirmedMessages)
        {
            var subscriber = topicQueue.SubscribersList.TryGetSubscriber(confirmationId);

            if (subscriber == null)
                throw new Exception($"Can not find subscriber by confirmationId {confirmationId}");

            var messagesToPutBack =
                subscriber.MessagesOnDelivery.Where(itm => notConfirmedMessages.HasMessageId(itm.message.MessageId))
                    .Select(itm => itm.message.MessageId).ToList();

            if (messagesToPutBack.Count > 0)
                subscriber.TopicQueue.EnqueueMessagesBack(messagesToPutBack);
   
            subscriber.Reset();
            return _myServiceBusDeliveryHandler.SendMessagesAsync(topicQueue);
        }

        public async ValueTask DisconnectSubscriberAsync(MyServiceBusSession session)
        {
            var topics = _topicsList.Get();

            foreach (var topic in topics)
            {
                foreach (var queue in topic.GetQueues())
                {

                    var subscriber = queue.SubscribersList.Unsubscribe(session);
                    
                    if (subscriber == null)
                        continue;

                    subscriber.Session.Disconnected = true;

                    while (subscriber.Status == SubscriberStatus.Leased)
                        await Task.Delay(100);

                    if (subscriber.Status == SubscriberStatus.OnDelivery)
                    {
                        subscriber.TopicQueue.EnqueueMessagesBack(subscriber.MessagesOnDelivery.Select(itm => itm.message.MessageId));
                        subscriber.Reset();
                    }
                    
                    await _myServiceBusDeliveryHandler.SendMessagesAsync(queue);
                    
                    //ToDo - сделать таймер на удаление очередей

                }
            }
        }


        public async ValueTask<ReplayMessageResult> ReplayMessageAsync(TopicQueue topicQueue, long messageId)
        {

            var pageId = MessagesPageId.CreateFromMessageId(messageId);
            var result = topicQueue.Topic.MessagesContentCache.TryGetMessage(pageId, messageId);
            
            if (!result.pageIsLoaded)
            {
                await _messagesPageLoader.LoadPageAsync(topicQueue.Topic, pageId);
                result = topicQueue.Topic.MessagesContentCache.TryGetMessage(pageId, messageId);
                if (!result.pageIsLoaded)
                    return ReplayMessageResult.MessageNotFound;
            }
            
            if (result.message == null)
                return ReplayMessageResult.MessageNotFound;
            
            topicQueue.Topic.SetQueueMessageId(messageId, topicQueue);

            return ReplayMessageResult.Ok;
        }
        
    }

    
}