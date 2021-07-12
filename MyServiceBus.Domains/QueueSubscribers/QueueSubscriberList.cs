
using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Sessions;

namespace MyServiceBus.Domains.QueueSubscribers
{


    public class QueueSubscribersList
    {
        private readonly TopicQueue _topicQueue;
        
        private readonly DictionaryWithList<string, QueueSubscriber> _subscribers 
            = new DictionaryWithList<string, QueueSubscriber>();
        
        private readonly Dictionary<long, QueueSubscriber> _subscribersByDeliveryId 
            = new Dictionary<long, QueueSubscriber>();


        public QueueSubscribersList(TopicQueue topicQueue)
        {
            _topicQueue = topicQueue;
        }

        public QueueSubscriber Subscribe(MyServiceBusSession session)
        {
   
                if (_subscribers.ContainsKey(session.Id))
                    throw new Exception($"Subscriber to topic: {_topicQueue.Topic}  and queue: {_topicQueue.QueueId} is already exists");

                var theSubscriber = new QueueSubscriber(session, _topicQueue);
                
                _subscribers.Add(session.Id, theSubscriber);
                _subscribersByDeliveryId.Add(theSubscriber.ConfirmationId, theSubscriber);

                return theSubscriber;
        }


        private QueueSubscriber Unsubscribe(string sessionId)
        {
            var removedItem = _subscribers.TryRemoveOrDefault(sessionId);
            
            if (removedItem == null) 
                return null;
            
            _subscribersByDeliveryId.Remove(removedItem.ConfirmationId);
            return removedItem;

        }

        public QueueSubscriber Unsubscribe(MyServiceBusSession session)
        {

            return Unsubscribe(session.Id);

        }

        public QueueSubscriber LeaseSubscriber()
        {

                var readyToBeLeased
                    = _subscribers
                        .GetAllValues()
                        .FirstOrDefault(itm => itm.Status == SubscriberStatus.Ready);

                if (readyToBeLeased == null)
                    return null;
                
                readyToBeLeased.SetToLeased();

                return readyToBeLeased;
        }

        public void UnLease(QueueSubscriber subscriber)
        {
                if (subscriber.MessagesSize > 0)
                    subscriber.SetOnDeliveryAndSendMessages();
                else
                    subscriber.Reset();
        }

        public QueueSubscriber TryGetSubscriber(MyServiceBusSession session)
        {
            return _subscribers.TryGetValue(session.Id); 
        }

        public QueueSubscriber TryGetSubscriber(long confirmationId)
        {
                return _subscribersByDeliveryId.TryGetValue(confirmationId, out var subscriber) 
                    ? subscriber 
                    : null;
        }
        
        
        public int GetCount()
        {
                return _subscribers.Count;
        }


        public void OneSecondTimer()
        {
            foreach (var subscriber in _subscribers.GetAllValues())
                subscriber.OneSecondTimer();
        }


        public IReadOnlyList<QueueSubscriber> GetSubscribers()
        {
            return _subscribers.GetAllValues();
        }

        public IReadOnlyList<QueueSubscriber> RemoveAllExceptThisOne(QueueSubscriber subscriber)
        {

            var result = _subscribers.GetAllValues().Where(itm => itm.Session.Id != subscriber.Session.Id)
                .ToList();

            foreach (var queueSubscriber in result)
                Unsubscribe(queueSubscriber.Session.Id);

            return result;

        }



    }

}