namespace MyServiceBus.Abstractions.QueueIndex
{
    public class QueueIndexRangeReadOnly : IQueueIndexRange
    {
        public long FromId { get; private set; }
        public long ToId { get; private set; }

        public QueueIndexRangeReadOnly(long fromId, long toId)
        {
            FromId = fromId;
            ToId = toId;
        }

        public static QueueIndexRangeReadOnly Create(IQueueIndexRange src)
        {
            return new QueueIndexRangeReadOnly(src.FromId, src.ToId);
        }

        public override string ToString()
        {
            return $"{FromId} - {ToId}";
        }
    }
}