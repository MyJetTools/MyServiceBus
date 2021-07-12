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
    
    public interface ITopicQueueRwAccess
    {
        QueueSubscribersList SubscribersList { get; }
    }
    
    
    public class TopicQueue : ITopicQueueRwAccess
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

        private readonly object _queueLock = new object();

        private readonly Dictionary<long, int> _attempts = new Dictionary<long, int>();

        private readonly MetricHistory<int> _executionDuration = new MetricHistory<int>();

        private readonly ExecutionMonitoring _executionMonitoring = new ExecutionMonitoring();
        
        
        public bool Disposed { get; private set; }

        
        public TopicQueueMetrics Metrics { get; } = new TopicQueueMetrics();

        private readonly QueueSubscribersList _subscribersList;


        public int SubscribersCount => _subscribersList.GetCount();


        public void DisposeIfItDoesNotHaveSubscribers()
        {
            lock (_queueLock)
            {
                if (_subscribersList.GetCount() == 0)
                    Disposed = true;
            }
        }

        public TopicQueue(MyTopic topic, string queueId, TopicQueueType topicQueueType, IEnumerable<IQueueIndexRange> ranges)
        {
            Topic = topic;
            QueueId = queueId;
            TopicQueueType = topicQueueType;
            _queue = new QueueWithIntervals(ranges);
            _subscribersList = new QueueSubscribersList(this);
        }

        public TopicQueue(MyTopic topic, string queueId, TopicQueueType topicQueueType, long messageId)
        {
            Topic = topic;
            QueueId = queueId;
            TopicQueueType = topicQueueType;
            _queue = new QueueWithIntervals(messageId);
            _subscribersList = new QueueSubscribersList(this);
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
            
            if (Disposed)
                return;
            
            _subscribersList.OneSecondTimer();
            
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


        QueueSubscribersList ITopicQueueRwAccess.SubscribersList => _subscribersList;

        public void GetRwAccess(Action<ITopicQueueRwAccess> rwAccess)
        {
            lock (_queueLock)
            {
                if (!Disposed)
                    rwAccess(this);
            }
        }
        
        public T GetRwAccess<T>(Func<ITopicQueueRwAccess, T> rwAccess)
        {
            lock (_queueLock)
            {
                return Disposed ?default : rwAccess(this) ;
            }
        }
    }
}