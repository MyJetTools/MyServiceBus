using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    public class MockConnection 
    {
        
        public MyServiceBusSession Session { get; }

        public MyServiceBusPublisherOperations PublisherOperations { get; }
        
        public MyServiceBusSubscriberOperations SubscriberOperations { get; }

        private readonly TopicsList _topicsList;

        public MockConnection(IServiceProvider sr, string sessionsName, DateTime dt)
        {
            SubscriberId = sessionsName;
            var sessionsList = sr.GetRequiredService<SessionsList>();
            Session = sessionsList.IssueSession();

            PublisherOperations = sr.GetRequiredService<MyServiceBusPublisherOperations>();

            SubscriberOperations = sr.GetRequiredService<MyServiceBusSubscriberOperations>();

            _topicsList = sr.GetRequiredService<TopicsList>();
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
            
            return PublisherOperations.PublishAsync(Session, topicName, new[] {publishMessage}, dateTime, persistImmediately).Result;
        }

        public ValueTask<QueueSubscriber> SubscribeAsync(string topicId, string queueId, TopicQueueType topicQueueType = TopicQueueType.DeleteOnDisconnect)
        {
            var topic = _topicsList.Get(topicId);

            var queue = topic.CreateQueueIfNotExists(queueId, topicQueueType, true);
            
             return SubscriberOperations.SubscribeToQueueAsync(queue, Session);

        }
        
        public MyTopic CreateTopic(string topicName)
        {
            return PublisherOperations.CreateTopicIfNotExists(Session, topicName).Result;
        }
        

        public void Disconnect(DateTime now)
        {
            Disconnected = true;
            SubscriberOperations.DisconnectSubscriberAsync(Session, now);
        }

        public ValueTask ConfirmDeliveryAsync(TopicQueue queue, long confirmationId)
        {
            return SubscriberOperations.ConfirmDeliveryAsync(queue, confirmationId);
        }
    }
}