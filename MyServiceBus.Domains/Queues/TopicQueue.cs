using System;
using System.Collections.Generic;
using System.Text;
using MyServiceBus.Abstractions;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.Metrics;
using MyServiceBus.Domains.Persistence;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Topics;

namespace MyServiceBus.Domains.Queues
{



    
    
    public class TopicQueue 
    {
        private class ExecutionMonitoring
        {
            public long ExecutedAmount { get; private set; }
            public TimeSpan ExecutionDuration { get; private set; }
            
            public bool HadException { get; private set; }
            
            internal void UpdateLastAmount(int amount, TimeSpan executionDuration, bool exception)
            {
                ExecutedAmount += amount;
                ExecutionDuration += executionDuration;
                if (exception)
                    HadException = true;
            }

            internal void Reset()
            {
                ExecutedAmount = 0;
                ExecutionDuration = TimeSpan.Zero;
                HadException = false;
            }
        }
        public MyTopic Topic { get; }
        public string QueueId { get; }
        public TopicQueueType TopicQueueType { get; private set; }
        
        private readonly QueueWithIntervals _queue;

        private readonly object _queueLock = new();

        private readonly Dictionary<long, int> _attempts = new();

        private readonly MetricHistory<int> _executionDuration = new ();

        private readonly ExecutionMonitoring _executionMonitoring = new ();

        
        public TopicQueueMetrics Metrics { get; } = new ();

        public QueueSubscribersList SubscribersList { get; }

        public TopicQueue(MyTopic topic, string queueId, TopicQueueType topicQueueType, IEnumerable<IQueueIndexRange> ranges)
        {
            Topic = topic;
            QueueId = queueId;
            TopicQueueType = topicQueueType;
            _queue = new QueueWithIntervals(ranges);
            SubscribersList = new QueueSubscribersList(this);
        }

        public TopicQueue(MyTopic topic, string queueId, TopicQueueType topicQueueType, long messageId)
        {
            Topic = topic;
            QueueId = queueId;
            TopicQueueType = topicQueueType;
            _queue = new QueueWithIntervals(messageId);
            SubscribersList = new QueueSubscribersList(this);
        }


        public IReadOnlyList<IQueueIndexRange> GetReadyQueueSnapshot()
        {
            lock (_queueLock)
            {
                return _queue.GetSnapshot();
            }
        }

        public IEnumerable<(long messageId, int attemptNo)> DequeNextMessage()
        {
            lock (_queueLock)
            {
                var result = _queue.Dequeue();

                while (result>-1)
                {
                    var attemptNo = 1;
                    _attempts.TryGetValue(attemptNo, out attemptNo);

                   yield return (result, attemptNo);
                   
                   result = _queue.Dequeue();
                }
            }
   
        }

        public void UpdateTopicQueueType(TopicQueueType topicQueueType)
        {
            TopicQueueType = topicQueueType;
        }


        private void AddAttempt(long messageId)
        {
            if (!_attempts.TryAdd(messageId, 1))
                _attempts[messageId] += 1;
        }

     

        public void EnqueueMessages(IEnumerable<long> messages)
        {
            lock (_queueLock)
            {
                foreach (var messageId in messages)
                    _queue.Enqueue(messageId);       
            }
        }
        
        public void EnqueueMessagesBack(IEnumerable<long> messages)
        {
            lock (_queueLock)
            {
                foreach (var messageId in messages)
                {
                    _queue.Enqueue(messageId);
                    AddAttempt(messageId);
                }
            }
        }
        
        public void ConfirmDelivered(IEnumerable<long> messages)
        {
            lock (_queueLock)
            {
                foreach (var messageId in messages)
                    _attempts.Remove(messageId);
            }
        }



        /*
        public void ConfirmDelivery(TheQueueSubscriber subscriber, TimeSpan executionDuration)
        {
            lock (_topicLock)
            {
                _executionMonitoring.UpdateLastAmount(subscriber.MessagesOnDelivery.Count, executionDuration, false);
                subscriber.SetToUnLeased(); 
            }
        }

        
        public void ConfirmSomeDelivered(TheQueueSubscriber subscriber, TimeSpan executionDuration, QueueWithIntervals okDelivered)
        {
            lock (_queueLock)
            {
                _executionMonitoring.UpdateLastAmount(subscriber.MessagesOnDelivery.Count, executionDuration, true);

                var messagesToGoBack = subscriber.MessagesOnDelivery.ToDictionary(itm => itm.message.MessageId);
                
                foreach (var messageId in okDelivered)
                    messagesToGoBack.Remove(messageId);
                
                
                DisposeNotDeliveredMessages(messagesToGoBack.Values, 1);
                
                
                subscriber.SetToUnLeased(); 
            }
        }
        
        public void ConfirmMessagesByNotDelivery(TheQueueSubscriber subscriber, TimeSpan executionDuration, QueueWithIntervals confirmedMessages)
        {
            lock (_topicLock)
            {
                _executionMonitoring.UpdateLastAmount(confirmedMessages.Count, executionDuration, false);

                subscriber.ConfirmedMessagesAsDelivered(confirmedMessages);
                
            }
        }

        public void ConfirmNotDelivery(TheQueueSubscriber subscriber, TimeSpan executionDuration)
        {
            lock (_topicLock)
            {
                _executionMonitoring.UpdateLastAmount(subscriber.MessagesOnDelivery.Count, executionDuration, true);

                DisposeNotDeliveredMessages(subscriber.MessagesOnDelivery, 1);
                subscriber.SetToUnLeased();
            }
        }
*/

        public void SetMinimalMessageId(long minMessageId, long maxMessageId)
        {
            lock (_queueLock)
            {
                _queue.SetMinMessageId(minMessageId, maxMessageId);
            }
        }
        public long GetMessagesCount()
        {
            lock (_queueLock)
            {
                return _queue.Count;
            }
        }

        public IQueueSnapshot GetSnapshot()
        {
            lock (_queueLock)
            {
                return new QueueSnapshot
                {
                    QueueId = QueueId,
                    RangesData = _queue.GetSnapshot()
                };
                
            }
        }

        public long GetMinMessageId()
        {
            lock (_queueLock)
            {
                return _queue.GetMinId();
            }
        }


        /*
        public async ValueTask<bool> DisconnectedAsync(IMyServiceBusSubscriberSession session)
        {
            
            var theSubscriber = SubscribersList.Unsubscribe(session);
            
            if (theSubscriber == null)
                return false;


            while (theSubscriber.Status == SubscriberStatus.Leased)
                await Task.Delay(100);


            lock (_topicLock)
            {
                if (theSubscriber.MessagesOnDelivery.Count > 0)
                {
                    _executionMonitoring.UpdateLastAmount(theSubscriber.MessagesOnDelivery.Count, DateTime.UtcNow - theSubscriber.OnDeliveryStart, true);
                    DisposeNotDeliveredMessages(theSubscriber.MessagesOnDelivery, 1);
                }

                return true;
            }

        }
        */

        public override string ToString()
        {

            var result = new StringBuilder();


            lock (_queueLock)
            {
                result.Append("Queue:[");
                if (_queue.Count == 0)
                {
                    result.Append("Empty");
                }
                else
                    foreach (var snapshot in _queue.GetSnapshot())
                    {
                        result.Append(snapshot.FromId + " - " + snapshot.ToId + ";");
                    }

                result.Append("]");
            }


            return result.ToString();

        }

        /*
        public void SetInterval(long minId, long maxId)
        {
            lock (_topicLock)
            {
                var subscribersCount = SubscribersList.GetCount();

                if (subscribersCount > 0)
                    throw new Exception(
                        $"Queue has: {subscribersCount}. You can rewind the queue only if it has 0 subscribers");

                _queue.SetMinMessageId(minId, maxId);
            }
        }
*/

        public long GetExecutionDurationSnapshotId()
        {
            lock (_executionDuration)
            {
                return _executionDuration.SnapshotId;
            }
        }

        public IReadOnlyList<int> GetExecutionDuration()
        {
            lock (_executionDuration)
            {
                return _executionDuration.GetItems();
            }
        }

        public void OneSecondTimer()
        {
            
            //SubscribersList.OneSecondTimer();
            
            lock (_executionDuration)
            {
                if (_executionMonitoring.ExecutedAmount == 0)
                    return;

                var amount = _executionMonitoring.ExecutionDuration / _executionMonitoring.ExecutedAmount;
                if (_executionMonitoring.HadException)
                    amount = -amount;
                
                _executionDuration.PutData((int)(amount.TotalMilliseconds * 1000));

                _executionMonitoring.Reset();
            }
        }


    }
}