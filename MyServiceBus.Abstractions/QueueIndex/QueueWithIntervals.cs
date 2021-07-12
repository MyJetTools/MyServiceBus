using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MyServiceBus.Abstractions.QueueIndex
{
    public class QueueWithIntervals : IEnumerable<long>
    {
        public QueueWithIntervals(IEnumerable<IQueueIndexRange> ranges)
        {
            foreach (var queueIndexRange in ranges)
                _ranges.Add(new QueueIndexRange(queueIndexRange));
        }
        
        public QueueWithIntervals(long messageId = 0)
        {
            _ranges.Add(new QueueIndexRange(messageId));
        }

        private readonly List<QueueIndexRange> _ranges = new List<QueueIndexRange>();

        private int GetIndexToInsert(long messageId)
        {
            var i = 0;

            foreach (var range in _ranges)
            {
                if (range.IsBefore(messageId))
                    return i;

                i++;
            }

            return i;
        }

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

        private QueueIndexRange GetInterval(long messageId)
        {


            if (_ranges.Count == 1)
            {
                var firstOne = _ranges[0];
                if (firstOne.IsEmpty() || firstOne.IsMyInterval(messageId))
                    return firstOne;
            }

            foreach (var range in _ranges)
            {
                if (range.IsMyInterval(messageId))
                    return range;
            }

            var index = GetIndexToInsert(messageId);

            var newItem = new QueueIndexRange(0);

            if (index >= _ranges.Count)
                _ranges.Add(newItem);
            else
                _ranges.Insert(index, newItem);

            return newItem;

        }

        public void Enqueue(long messageId)
        {
            var interval = GetInterval(messageId);
            interval.AddNextMessage(messageId);

        }

        public long Dequeue()
        {
            var interval = _ranges[0];
            if (interval.IsEmpty())
                return -1;

            var result = interval.GetNextMessage();

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


        public bool HasMessageId(long messageId)
        {
            foreach (var range in _ranges.Where(itm => !itm.IsEmpty()))
            {
                if (range.FromId <= messageId && messageId <= range.ToId)
                    return true;
            }

            return false;
        }
    }
}