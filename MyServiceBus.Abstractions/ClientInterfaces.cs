using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyServiceBus.Abstractions
{
    public interface IMyServiceBusPublisher
    {
        Task PublishAsync(string topicId, byte[] valueToPublish, bool immediatelyPersist);

        Task PublishAsync(string topicId, IEnumerable<byte[]> valueToPublish, bool immediatelyPersist);
    }


    public interface IMyServiceBusSubscriber
    {
        void Subscribe(string topicId, string queueId, TopicQueueType topicQueueType,
            Func<IMyServiceBusMessage, ValueTask> callback, bool enableDeDublication = false);

        void Subscribe(string topicId, string queueId, TopicQueueType topicQueueType,
            Func<IConfirmationContext, IReadOnlyList<IMyServiceBusMessage>, ValueTask> callback,
            bool enableDeDublication = false);
    }
    
}