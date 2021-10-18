using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace MyServiceBus.Abstractions.QueueIndex
{
    public class QueueWithIntervals : IEnumerable<long>
    {
        public QueueWithIntervals(IEnumerable<IQueueIndexRange> ranges)
        {
            foreach (var queueIndexRange in ranges)
                _ranges.Add(new QueueIndexRange(queueIndexRange));
            
            if (_ranges.Count == 0)
                _ranges.Add(QueueIndexRange.CreateEmpty(0));
        }
        
        public QueueWithIntervals(long messageId = 0)
        {
            _ranges.Add(QueueIndexRange.CreateEmpty(messageId));
        }

        private readonly List<QueueIndexRange> _ranges = new List<QueueIndexRange>();



        private int GetIndexToDelete(long messageId)
        {

            var i = 0;

            foreach (var range in _ranges)
            {
                if (range.FromId <= messageId && messageId <= range.ToId)
                    return i;

                i++;
            }

            return -1;
        }


        public void Enqueue(long messageId)
        {
            const int defaultValue = -1; 

            if (_ranges.Count == 0)
            {
                _ranges.Add(QueueIndexRange.CreateWithValue(messageId));
                return;
            }
            
            if (_ranges.Count == 1)
            {
                var firstItem = _ranges[0];
                if (firstItem.IsEmpty())
                {
                    firstItem.FromId = messageId;
                    firstItem.ToId = messageId;
                        return;
                }
            }

            var foundIndex = defaultValue;

            for (var index = 0; index < _ranges.Count; index++)
            {
                var el = _ranges[index];

                if (el.TryJoin(messageId))
                {
                    foundIndex = index;
                    break;
                }

                if (messageId < el.FromId - 1)
                {
                    var newItem = QueueIndexRange.CreateWithValue(messageId);
                    _ranges.Insert(index, newItem);
                    foundIndex = index;
                    break;
                }
            }

            if (foundIndex == defaultValue)
            {
                var newItem = QueueIndexRange.CreateWithValue(messageId);
                _ranges.Add(newItem);
                return;
            }

            if (foundIndex > 0)
            {
                var currentEl = _ranges[foundIndex];
                var previousEl = _ranges[foundIndex-1];
                if (previousEl.TryJoinWithTheNextOne(currentEl))
                    _ranges.RemoveAt(foundIndex);
            }

            if (foundIndex < _ranges.Count - 1)
            {
                var nextEl = _ranges[foundIndex + 1];
                var currentEl = _ranges[foundIndex];
                
                if (currentEl.TryJoinWithTheNextOne(nextEl))
                    _ranges.RemoveAt(foundIndex+1);
            }

        }

        public long Dequeue()
        {


            if (_ranges.Count == 0)
                return -1;

            var interval = _ranges[0];
            if (interval.IsEmpty())
                return -1;

            var result = interval.Dequeue();

            if (interval.IsEmpty() && _ranges.Count > 1)
                _ranges.RemoveAt(0);

            return result;

        }

        public IEnumerator<long> GetEnumerator()
        {
            return _ranges.SelectMany(range => range.GetElements()).GetEnumerator();
        }

        public override string ToString()
        {
            return $"Intervals: {_ranges.Count}. Count: {Count}";
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IReadOnlyList<QueueIndexRangeReadOnly> GetSnapshot()
        {
            return _ranges.Select(QueueIndexRangeReadOnly.Create).ToList();
        }


        public long GetMinId()
        {
            return _ranges[0].FromId;
        }

        public void Remove(in long messageId)
        {
            var index = GetIndexToDelete(messageId);

            if (index < 0)
            {
                Console.WriteLine("No element " + messageId);
                return;
            }

            var range = _ranges[index];

            if (range.FromId == messageId)
                range.FromId++;
            else if (range.ToId == messageId)
                range.ToId--;
            else
            {
                var newRange = QueueIndexRange.Create(range.FromId, messageId - 1);
                range.FromId = messageId + 1;
                _ranges.Insert(index, newRange);
            }

            if (range.IsEmpty() && _ranges.Count > 1)
                _ranges.RemoveAt(index);
        }

        public int Count => _ranges.Sum(itm => itm.Count);
        
        public void SetMinMessageId(long fromId, long toId)
        {
            while (_ranges.Count>1)
                _ranges.RemoveAt(_ranges.Count - 1);

            _ranges[0].FromId = fromId;
            _ranges[0].ToId = toId;
        }

        public void Clear()
        {
            while (_ranges.Count>1)
                _ranges.RemoveAt(0);

            _ranges[0].FromId = _ranges[0].ToId+1;
        }

        public bool HasMessage(long id)
        {
            return _ranges.Any(range => range.HasMessage(id));
        }
    }
}