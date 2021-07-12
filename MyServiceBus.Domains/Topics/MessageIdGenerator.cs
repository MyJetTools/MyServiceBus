using System;

namespace MyServiceBus.Domains.Topics
{

    public interface INextMessageIdGenerator
    {
        long GetNextMessageId();
    }
    
    public class MessageIdGenerator : INextMessageIdGenerator
    {
        public long Value { get; private set; }

        private readonly object _lockObject = new object();


        public void Lock(Action<INextMessageIdGenerator> callback)
        {
            lock (_lockObject)
            {
                callback(this);
            }
        }

        public MessageIdGenerator(long value)
        {
            Value = value;
        }

        long INextMessageIdGenerator.GetNextMessageId()
        {

            try
            {
                return Value;
            }
            finally
            {
                Value++;
            }
        }
    }
}