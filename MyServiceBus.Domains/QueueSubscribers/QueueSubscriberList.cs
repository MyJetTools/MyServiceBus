
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
        
        private readonly Dictionary<string, QueueSubscriber> _subscribers 
            = new ();
        
        private readonly Dictionary<long, QueueSubscriber> _subscribersByDeliveryId 
            = new ();

        private IReadOnlyList<QueueSubscriber> _subscribersAsList = Array.Empty<QueueSubscriber>();


        private readonly object _lockObject = new ();

        public QueueSubscribersList(TopicQueue topicQueue)
        {
            _topicQueue = topicQueue;
        }

        public QueueSubscriber Subscribe(MyServiceBusSession session)
        {
            lock (_lockObject)
            {
                if (_subscribers.ContainsKey(session.Id))
                    throw new Exception($"Subscriber to topic: {_topicQueue.Topic}  and queue: {_topicQueue.QueueId} is already exists");

                var theSubscriber = new QueueSubscriber(session, _topicQueue);
                
                _subscribers.Add(session.Id, theSubscriber);
                _subscribersByDeliveryId.Add(theSubscriber.ConfirmationId, theSubscriber);
                _subscribersAsList = _subscribers.Values.ToList();

                return theSubscriber;
            }
        }


        private QueueSubscriber Unsubscribe(string sessionId)
        {
            if (_subscribers.Remove(sessionId, out var removedItem))
            {
                _subscribersByDeliveryId.Remove(removedItem.ConfirmationId);
                _subscribersAsList = _subscribers.Values.ToList();
                return removedItem;
            }

            return null;
        }
        
        public QueueSubscriber Unsubscribe(MyServiceBusSession session)
        {
            lock (_lockObject)
            {
                return Unsubscribe(session.Id);
            }
        }

        public QueueSubscriber LeaseSubscriber()
        {
            lock (_lockObject)
            {
                var readyToBeLeased
                    = _subscribers
                        .Values
                        .FirstOrDefault(itm => itm.Status == SubscriberStatus.Ready);

                if (readyToBeLeased == null)
                    return null;
                
                readyToBeLeased.SetToLeased();

                return readyToBeLeased;
            }
        }

        public void UnLease(QueueSubscriber subscriber)
        {
            lock (_lockObject)
            {
                if (subscriber.MessagesSize > 0)
                    subscriber.SetOnDeliveryAndSendMessages();
                else
                    subscriber.Reset();
            }
        }

        public QueueSubscriber TryGetSubscriber(MyServiceBusSession session)
        {
            lock (_lockObject)
            {
                return _subscribers.TryGetValue(session.Id, out var subscriber) 
                    ? subscriber 
                    : null;
            }
        }

        public QueueSubscriber TryGetSubscriber(long confirmationId)
        {
            lock (_lockObject)
            {
                return _subscribersByDeliveryId.TryGetValue(confirmationId, out var subscriber) 
                    ? subscriber 
                    : null;
            }
        }
        
        
        public int GetCount()
        {
            lock (_lockObject)
            {
                return _subscribers.Count;
            }
        }


        public void OneSecondTimer()
        {
            foreach (var subscriber in _subscribersAsList)
                subscriber.OneSecondTimer();
        }


        public IReadOnlyList<QueueSubscriber> GetSubscribers()
        {
            return _subscribersAsList;
        }

        public IReadOnlyList<QueueSubscriber> RemoveAllExceptThisOne(QueueSubscriber subscriber)
        {
            lock (_lockObject)
            {
                var result =  _subscribers.Values.Where(itm => itm.Session.Id != subscriber.Session.Id)
                    .ToList();

                foreach (var queueSubscriber in result)
                    Unsubscribe(queueSubscriber.Session.Id);

                return result;
            }
        }
        
        

    }

}