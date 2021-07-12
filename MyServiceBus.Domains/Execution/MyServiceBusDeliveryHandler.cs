using System;
using System.Linq;
using System.Threading.Tasks;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains.Execution
{
    
    public class MyServiceBusDeliveryHandler
    {
        private readonly MessagesPageLoader _messagesPageLoader;
        private readonly IMyServiceBusSettings _myServiceBusSettings;
        private readonly Log _log;
        
        public MyServiceBusDeliveryHandler(MessagesPageLoader messagesPageLoader,
            IMyServiceBusSettings myServiceBusSettings, Log log)
        {
            _messagesPageLoader = messagesPageLoader;
            _myServiceBusSettings = myServiceBusSettings;
            _log = log;
        }

        private async ValueTask<MessagesPage> GetMessagesPageAsync(TopicQueue topicQueue, MessagesPageId pageId)
        {

            var result = topicQueue.Topic.MessagesContentCache.TryGetPage(pageId);

            if (result != null)
                return result;

            using (await topicQueue.Topic.MessagesPersistenceLock.LockAsync())
            {
                result = topicQueue.Topic.MessagesContentCache.TryGetPage(pageId);

                if (result != null)
                    return result;


                return await _messagesPageLoader.LoadPageAsync(topicQueue.Topic, pageId);
            }

        }

        private async ValueTask FillMessagesAsync(TopicQueue topicQueue, QueueSubscriber subscriber)
        {

            MessagesPage messagesPage = null;
            
            foreach (var (messageId, attemptNo) in topicQueue.DequeNextMessage())
            {

                if (messageId < 0)
                    return;

                var pageId = messageId.GetMessageContentPageId();


                if (messagesPage == null)
                {
                    messagesPage = await GetMessagesPageAsync(topicQueue, pageId);
                }
                else
                {
                    if (!messagesPage.PageId.EqualsTo(pageId))
                        messagesPage = await GetMessagesPageAsync(topicQueue, pageId);
                }


                var messageContent = messagesPage.TryGet(messageId);

                if (messageContent == null)
                    continue;

                subscriber.EnqueueMessage(messageContent, attemptNo);

                if (subscriber.Session.Disconnected)
                {
                    _log.AddLog(LogLevel.Warning, topicQueue,
                        $"Disconnected while we were Filling package with Messages for the Session: {subscriber.Session.Id}");
                    return;
                }

                if (subscriber.MessagesSize >= _myServiceBusSettings.MaxDeliveryPackageSize)
                    return;
            }

        }

        public async ValueTask SendMessagesAsync(TopicQueue topicQueue)
        {
            var leasedSubscriber = topicQueue.SubscribersList.LeaseSubscriber();
            
            if (leasedSubscriber == null)
                return;

            try
            {
                await FillMessagesAsync(topicQueue, leasedSubscriber);
            }
            catch (Exception ex)
            {
                if (leasedSubscriber.MessagesSize > 0)
                {
                    leasedSubscriber.TopicQueue.EnqueueMessages(leasedSubscriber.MessagesOnDelivery.Select(itm => itm.message.MessageId));
                    leasedSubscriber.Reset();
                }
    
                
                _log.AddLog(LogLevel.Error, topicQueue, ex.Message);
                Console.WriteLine(ex);
            }
            finally
            {
                topicQueue.SubscribersList.UnLease(leasedSubscriber);
            }
        }

        public async ValueTask SendMessagesAsync(MyTopic topic)
        {
            foreach (var topicQueue in topic.GetQueues())
                await SendMessagesAsync(topicQueue);
        }

    }
}