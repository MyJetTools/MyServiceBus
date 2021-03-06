
using System;
using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Domains.Queues;

namespace MyServiceBus.Domains.QueueSubscribers
{

    public interface IReadSubscribersAccess
    {
        IEnumerable<TheQueueSubscriber> GetSubscribers();
        IEnumerable<TheQueueSubscriber> GetExceptThisOne(IMyServiceBusSubscriberSession subscriberSession);
    }

    public class QueueSubscribersList : IReadSubscribersAccess
    {
        private readonly TopicQueue _topicQueue;
        
        private readonly Dictionary<string, TheQueueSubscriber> _subscribers 
            = new ();
        
        private readonly Dictionary<long, TheQueueSubscriber> _subscribersByDeliveryId 
            = new ();

        private IReadOnlyList<TheQueueSubscriber> _subscribersAsList = Array.Empty<TheQueueSubscriber>();


        private readonly object _lockObject;

        public QueueSubscribersList(TopicQueue topicQueue, object lockObject)
        {
            _topicQueue = topicQueue;
            _lockObject = lockObject;
        }

        public void Subscribe(IMyServiceBusSubscriberSession session)
        {
            lock (_lockObject)
            {
                if (_subscribers.ContainsKey(session.SubscriberId))
                    throw new Exception($"Subscriber to topic: {_topicQueue.Topic}  and queue: {_topicQueue.QueueId} is already exists");

                var theSubscriber = new TheQueueSubscriber(session, _topicQueue);
                
                _subscribers.Add(session.SubscriberId, theSubscriber);
                _subscribersByDeliveryId.Add(theSubscriber.ConfirmationId, theSubscriber);
                _subscribersAsList = _subscribers.Values.ToList();
            }
        }

        public TheQueueSubscriber Unsubscribe(IMyServiceBusSubscriberSession session)
        {
            lock (_lockObject)
            {
                if (_subscribers.Remove(session.SubscriberId, out var removedItem))
                {
                    _subscribersByDeliveryId.Remove(removedItem.ConfirmationId);
                    return removedItem;
                }

                _subscribersAsList = _subscribers.Values.ToList();
                return null;
            }
        }

        public TheQueueSubscriber LeaseSubscriber()
        {
            lock (_lockObject)
            {
                var readyToBeLeased
                    = _subscribers
                        .Values
                        .FirstOrDefault(itm => itm.Status == SubscriberStatus.UnLeased);

                if (readyToBeLeased == null)
                    return null;
                
                readyToBeLeased.SetToLeased();

                return readyToBeLeased;
            }
        }

        public void UnLease(TheQueueSubscriber subscriber)
        {
            lock (_lockObject)
            {
                if (subscriber.MessagesSize > 0)
                    subscriber.SetOnDeliveryAndSendMessages();
                else
                    subscriber.SetToUnLeased();
            }
        }

        public TheQueueSubscriber TryGetSubscriber(IMyServiceBusSubscriberSession session)
        {
            lock (_lockObject)
            {
                return _subscribers.TryGetValue(session.SubscriberId, out var subscriber) 
                    ? subscriber 
                    : null;
            }
        }

        public TheQueueSubscriber TryGetSubscriber(long confirmationId)
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
        
        public void GetReadAccess(Action<IReadSubscribersAccess> callback)
        {
            lock (_lockObject)
            {
                callback(this);
            }   
        }

        public T GetReadAccess<T>(Func<IReadSubscribersAccess, T> callback)
        {
            lock (_lockObject)
            {
                return callback(this);
            }   
        }


        public void OneSecondTimer()
        {
            foreach (var subscriber in _subscribersAsList)
                subscriber.OneSecondTimer();
        }


        IEnumerable<TheQueueSubscriber> IReadSubscribersAccess.GetSubscribers()
        {
            return _subscribers.Values;
        }

        IEnumerable<TheQueueSubscriber> IReadSubscribersAccess.GetExceptThisOne(IMyServiceBusSubscriberSession subscriberSession)
        {
            return _subscribers.Values.Where(itm => itm.Session.SubscriberId != subscriberSession.SubscriberId);
        }

    }

}