using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.MessagesContent;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains.Queues
{
    
    public class TopicQueue 
    {
        
        private readonly QueueWithIntervals _queue;
        
        private readonly QueueWithIntervals _leasedQueue;

        private readonly object _lockObject;
        
        private readonly Dictionary<long, int> _attempts = new Dictionary<long, int>();


        private void ResetAttempt(long messageId)
        {
            if (_attempts.ContainsKey(messageId))
                _attempts.Remove(messageId);
        }

        private void IncAttempt(long messageId)
        {
            if (_attempts.ContainsKey(messageId))
                _attempts[messageId]++;
            else
                _attempts.Add(messageId, 2);
        }

        private int GetAttemptNo(long messageId)
        {
            if (_attempts.ContainsKey(messageId))
                return _attempts[messageId];

            return 1;
        }



        public void GetAttempts(Action<Func<long, int>> callback)
        {
            lock (_lockObject)
            {
                callback(GetAttemptNo);
            }
        }
        
        public TopicQueue(MyTopic topic, string queueId, bool deleteOnDisconnect, long messageId, object lockObject)
        {
            _lockObject = lockObject;
            Topic = topic;
            QueueId = queueId;
            DeleteOnDisconnect = deleteOnDisconnect;
            _queue = new QueueWithIntervals(messageId);
            _leasedQueue = new QueueWithIntervals(messageId);
            QueueSubscribersList = new QueueSubscribersList(this, lockObject);
        }
        
        public MyTopic Topic { get; }
        public string QueueId { get;  }
        
        public bool DeleteOnDisconnect { get; }

        public (IReadOnlyList<IQueueIndexRange> queues, IReadOnlyList<IQueueIndexRange> leased) GetQueueIntervals()
        {
            lock (_lockObject)
            {
                return (_queue.GetSnapshot(), _leasedQueue.GetSnapshot());
            }
            
        }
        
        public long DequeAndLease()
        {
            lock (_lockObject)
            {
                var result = _queue.Dequeue();
                if (result>=0)
                    _leasedQueue.Enqueue(result);

                return result;
                
            }
        }

        private void NotDelivered(IMessageContent message)
        {
            _leasedQueue.Remove(message.MessageId); 
            _queue.Enqueue(message.MessageId);
            IncAttempt(message.MessageId);
        }
        

        public void NotDelivered(IReadOnlyList<IMessageContent> messages)
        {
            lock (_lockObject)
            {
                Console.WriteLine("Not delivered for Queue: "+QueueId);
                Console.WriteLine("Not Delivered Before: "+this);
                Console.WriteLine();
                foreach (var message in messages)
                {
                    Console.WriteLine(message.MessageId+";");
                    NotDelivered(message);
                }
                
                Console.WriteLine();
                Console.WriteLine("Not Delivered After: "+this);
            }
        }

        public void NewMessage(long messageId)
        {
            lock (_lockObject)
            {
                _queue.Enqueue(messageId);
            }
        }


        public void ConfirmDelivery(long confirmationId)
        {

            lock (_lockObject)
            {
                var messagesDelivered = QueueSubscribersList.Delivered(confirmationId);

                if (messagesDelivered == null)
                    throw  new Exception($"Can not find collector on delivery with confirmationId {confirmationId} for TopicId: {Topic} and QueueId: {QueueId}");

                foreach (var msgDelivered in messagesDelivered)
                {          
                    _leasedQueue.Remove(msgDelivered.MessageId);
                    ResetAttempt(msgDelivered.MessageId);
                }
            }
        }
        
        public void ConfirmNotDelivery(long confirmationId)
        {

            lock (_lockObject)
            {
                var messagesDelivered = QueueSubscribersList.Delivered(confirmationId);

                if (messagesDelivered == null)
                    throw  new Exception($"Can not find collector on delivery with confirmationId {confirmationId} for TopicId: {Topic} and QueueId: {QueueId}");

                foreach (var message in messagesDelivered)
                {          
                    NotDelivered(message);
                }
                
            }
        }

        
        
        public long GetLeasedMessagesCount()
        {
            lock (_lockObject)
            {
                return _leasedQueue.GetMessagesCount();
            }
        }
        


        public long GetMessagesCount()
        {
            lock (_lockObject)
            {
                return _queue.GetMessagesCount();
            }
        }

        public IQueueSnapshot GetSnapshot()
        {
            return new QueueSnapshot
            {
                QueueId = QueueId,
                RangesData = _queue.GetSnapshot()
            };
        }

        public long GetMinId()
        {

            lock (_lockObject)
            {
                var minFromQueue = _queue.GetMinId();
                var minFromLeasedQueue = _leasedQueue.GetMinId();

                return minFromQueue < minFromLeasedQueue ? minFromQueue : minFromLeasedQueue;
                
            }
        }
        
        public QueueSubscribersList QueueSubscribersList { get; }

        public async ValueTask<bool> DisconnectedAsync(IQueueSubscriber queueSubscriber)
        {

            var theSubscriber = QueueSubscribersList.Unsubscribe(queueSubscriber);

            if (theSubscriber == null)
                return false;

            if (theSubscriber.Status == SubscriberStatus.Leased)
            {
                Console.WriteLine($"Got subscriber {theSubscriber.QueueSubscriber.SubscriberId} in Leased Status");
                
                while (theSubscriber.Status == SubscriberStatus.Leased)
                    await Task.Delay(100);

            }
            
            if (theSubscriber.Status == SubscriberStatus.OnDelivery)
                NotDelivered(theSubscriber.MessagesOnDelivery);

            return true;


        }

        public override string ToString()
        {

            var result = new StringBuilder();


            lock (_lockObject)
            {
                result.Append("Queue:[");
                if (_queue.GetMessagesCount() == 0)
                {
                    result.Append("Empty");
                }
                else
                    foreach (var snapshot in _queue.GetSnapshot())
                    {
                        result.Append(snapshot.FromId + " - " + snapshot.ToId + ";");
                    }

                result.Append("]");

                result.Append("Leased:[");
                if (_leasedQueue.GetMessagesCount() == 0)
                {
                    result.Append("Empty");
                }
                else
                    foreach (var snapshot in _leasedQueue.GetSnapshot())
                    {
                        result.Append(snapshot.FromId + " - " + snapshot.ToId + ";");
                    }

                result.Append("]"); 
            }
           

            return result.ToString();

        }
    }
}