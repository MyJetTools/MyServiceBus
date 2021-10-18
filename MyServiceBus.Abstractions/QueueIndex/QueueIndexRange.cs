using System;
using System.Collections.Generic;

namespace MyServiceBus.Abstractions.QueueIndex
{
    
    public interface IQueueIndexRange 
    {
        long FromId { get; }
        long ToId { get; }
    }
    
    public class QueueIndexRange : IQueueIndexRange
    {

        public QueueIndexRange(long fromId, long toId)
        {
            FromId = fromId;
            ToId = toId;
        }
        public QueueIndexRange(IQueueIndexRange src)
        {
            FromId = src.FromId;
            ToId = src.ToId;
        }

        public static QueueIndexRange CreateEmpty(long messageId = 0)
        {
            return new QueueIndexRange(messageId, messageId-1);
        }

        public bool TryJoin(long idToJoin)
        {
            if (IsEmpty())
            {
                FromId = idToJoin;
                ToId = idToJoin;
            }

            if (idToJoin == FromId - 1)
            {
                FromId = idToJoin;
                return true;
            }

            if (idToJoin == ToId + 1)
            {
                ToId = idToJoin;
                return true;
            }

            return false;
        }

        
        
        public long FromId { get; set; }
        public long ToId { get; set; }

        public long Dequeue()
        {
            if (FromId>ToId)
                return -1;
            
            var result = FromId;
            FromId++;
            return result;
        }

        public void AddNextMessage(long id)
        {

            if (ToId == -1 || ToId < FromId)
            {
                FromId = id;
                ToId = id;
                return;
            }

            if (ToId + 1 == id)
                ToId = id;
            else if (FromId - 1 == id)
                FromId = id;
            else
                throw new Exception("Something went wrong. Invalid interval is choosen");
        }

        public bool IsMyInterval(long id)
        {
            return id >= FromId -1  && id <= ToId + 1;
        }

        public bool HasMessage(long id)
        {
            return id >= FromId  && id <= ToId ;
        }
        
        public bool IsEmpty()
        {
            return ToId < FromId;
        }

  

        public static QueueIndexRange Create(long fromId, long toId)
        {
            return new QueueIndexRange(fromId, toId);
        }
        
        public static QueueIndexRange CreateWithValue(long messageId)
        {
            return new QueueIndexRange(messageId, messageId);
        }

        public bool IsBefore(long messageId)
        {
            return messageId < FromId - 1;
        }

        public override string ToString()
        {
            if (IsEmpty())
                return "EMPTY";

            return FromId + " - " + ToId;
        }

        public int Count => (int)ToId - (int)FromId + 1;

        public IEnumerable<long> GetElements()
        {
            for (var i = FromId; i <= ToId; i++)
                yield return i;
        }

        public bool TryJoinWithTheNextOne(QueueIndexRange nextEl)
        {
            if (ToId + 1 == nextEl.FromId)
            {
                ToId = nextEl.ToId;
                return true;
            }

            return false;

        }
    }
}