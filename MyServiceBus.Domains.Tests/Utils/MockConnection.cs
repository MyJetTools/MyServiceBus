using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Abstractions;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.Tests.Utils
{
    public class MockConnection : IMyServiceBusSubscriberSession
    {

        private readonly TopicsManagement _topicsManagement;

        private readonly TopicsList _topicsList;

        public readonly MyServiceBusSessionContext MyServiceBusSessionContext = new ();
        
        public MyServiceBusSubscriber Subscriber { get; }
        
        public MyServiceBusPublisher Publisher { get; }


        public MockConnection(IServiceProvider sr, string sessionsName, DateTime dt)
        {
            SubscriberId = sessionsName;
            _topicsList = sr.GetRequiredService<TopicsList>();
            _topicsManagement = sr.GetRequiredService<TopicsManagement>();


            Subscriber = sr.GetRequiredService<MyServiceBusSubscriber>();
            Publisher = sr.GetRequiredService<MyServiceBusPublisher>();
        }
        
        public readonly List<(TopicQueue topicQueue, IReadOnlyList<(MessageContentGrpcModel message, int attemptNo)> messages, long confirmationId)> Messages 
            = new ();

        public (TopicQueue topicQueue, IReadOnlyList<(MessageContentGrpcModel message, int attemptNo)> messages, long confirmationId) GetLastSentMessage()
        {
            return Messages.Last();
        }

        public string Name => "Mock";

        public void SendMessagesAsync(TopicQueue topicQueue, IReadOnlyList<(MessageContentGrpcModel message, int attemptNo)> messages, long confirmationId)
        {
            Messages.Add((topicQueue, messages, confirmationId));
        }

        public string SubscriberId { get; }
        public bool Disconnected { get; private set; }


        public int GetSentPackagesCount()
        {
            return Messages.Count;
        }


        public ExecutionResult PublishMessage( string topicName, byte[] message, DateTime dateTime, bool persistImmediately = false)
        {
            topicName = topicName.ToLower();

            var publishMessage = new PublishMessage
            {
                Data = message,
                MetaData = Array.Empty<MessageContentMetaDataItem>()
            };
            
            return Publisher.PublishAsync(MyServiceBusSessionContext, topicName, new[] {publishMessage}, dateTime, persistImmediately).Result;
        }
        
        public MyTopic CreateTopic(string topicName)
        {
            return _topicsManagement.AddIfNotExistsAsync(topicName).Result;
        }


        public TopicQueue Subscribe(string topicId, string queueId, TopicQueueType topicQueueType = TopicQueueType.DeleteOnDisconnect)
        {
            var topic = _topicsList.Get(topicId);

            var queue = topic.CreateQueueIfNotExists(queueId, topicQueueType, true);
            
            var task = Subscriber.SubscribeToQueueAsync(queue, this);
                
            task.AsTask().Wait();

            return queue;
        }


        public void Disconnect()
        {
            Disconnected = true;
            Subscriber.DisconnectSubscriberAsync(this).AsTask().Wait();
        }

        public void ConfirmDelivery(TopicQueue queue, long confirmationId)
        {
            Subscriber.ConfirmDeliveryAsync(queue.Topic, queue.QueueId, confirmationId, true);
        }
    }
}