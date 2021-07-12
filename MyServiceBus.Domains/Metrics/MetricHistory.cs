using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MyServiceBus.Domains.Metrics
{
    public class MetricHistory<T>
    {
        private readonly Queue<T> _items = new Queue<T>();

        private IReadOnlyList<T> _asList;

        public void PutData(T amount)
        {
            _items.Enqueue(amount);

            while (_items.Count > 120)
            {
                _items.Dequeue(); 
            }

            _asList = null;
            SnapshotId++;
        }

        public IReadOnlyList<T> GetItems()
        {
            return _asList ??= _items.ToList();
        }
        
        public long SnapshotId { get; private set; }
    }

    public class MetricsHistoryByTopic<T>
    {
        
        private readonly Dictionary<string, MetricHistory<T>> _messagesPerSeconds = new Dictionary<string, MetricHistory<T>>();

        private readonly ReaderWriterLockSlim _lockSlim = new ReaderWriterLockSlim();

        public void PutData(string topicId, T amount)
        {
            _lockSlim.EnterWriteLock();
            try
            {
                if (_messagesPerSeconds.TryGetValue(topicId, out var list))
                {
                    list.PutData(amount);
                    return;
                }

                list = new MetricHistory<T>();
                _messagesPerSeconds.Add(topicId, list);
                list.PutData(amount);
            }
            finally
            {
                _lockSlim.ExitWriteLock();
            }
        }

        public IReadOnlyList<T> GetRecordsPerSecond(string topicId)
        {
            _lockSlim.EnterReadLock();
            try
            {
                return _messagesPerSeconds.TryGetValue(topicId, out var result)
                    ? result.GetItems()
                    : Array.Empty<T>();
            }
            finally
            {
                _lockSlim.ExitReadLock();
            }
        }
        
    }
    
}