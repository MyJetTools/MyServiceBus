using System;
using System.Collections.Generic;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Persistence.Grpc;

namespace MyServiceBus.Domains.QueueSubscribers
{
    public enum SubscriberStatus{
        Ready, Leased, OnDelivery
    }

    public class QueueSubscriber
    {
        private static long _nextConfirmationId;
        public long ConfirmationId { get; }
        
        public TopicQueue TopicQueue { get;  }

        public readonly MetricPerSecond DeliveryEventsPerSecond = new MetricPerSecond();

        public QueueSubscriber(MyServiceBusSession session, TopicQueue topicQueue)
        {
            _nextConfirmationId++;
            ConfirmationId = _nextConfirmationId;
            Session = session;
            TopicQueue = topicQueue;
        }

        public MyServiceBusSession Session { get; }

        public readonly QueueWithIntervals LeasedQueue = new QueueWithIntervals();

        public IReadOnlyList<(MessageContentGrpcModel message, int attemptNo)> MessagesOnDelivery { get; private set; }
        public DateTime OnDeliveryStart { get; private set; }

        internal void SetOnDeliveryAndSendMessages()
        {
            if (Status != SubscriberStatus.Leased)
                throw new Exception($"Only leased status can be switched to - on Deliver. Now status is: {Status}");
            MessagesOnDelivery = MessagesCollector;
            Status = SubscriberStatus.OnDelivery;
            //SendMessages();
        }

        public List<(MessageContentGrpcModel message, int attemptNo)> MessagesCollector { get; private set; }
        
        public void EnqueueMessage(MessageContentGrpcModel messageContent, int attemptNo)
        {
            if (Status != SubscriberStatus.Leased)
                throw new Exception($"Can not add message when Status is: {Status}. Status must Be Leased");
            
            MessagesCollector.Add((messageContent, attemptNo));
            LeasedQueue.Enqueue(messageContent.MessageId);
            MessagesSize += messageContent.Data.Length;
        }
        
        public void SetToLeased()
        {
            if (Status != SubscriberStatus.Ready)
                throw new Exception($"Can not change message to status Leased from Status: {Status}.");

            MessagesCollector = new List<(MessageContentGrpcModel message, int attemptNo)>();

            Status = SubscriberStatus.Leased;
        }
        public void SwitchToUnLeased()
        {
            ClearMessages();
            Status = SubscriberStatus.Ready;
        }
        
        public void Reset()
        {
            ClearMessages();
            Status = SubscriberStatus.Ready;
        }


        public int MessagesSize { get; private set; }
        public SubscriberStatus Status { get; private set; }
        private void ClearMessages()
        {
            MessagesCollector = null;
            MessagesOnDelivery = Array.Empty<(MessageContentGrpcModel message, int attemptNo)>();
            LeasedQueue.Clear();
            if (MessagesSize == 0)
                return;
            MessagesSize = 0;
        }

        public void OneSecondTimer()
        {
            DeliveryEventsPerSecond.OneSecondTimer();
        }


        public QueueWithIntervals GetOnDeliveryIntervals()
        {
            var onDelivery = MessagesOnDelivery;

            var result = new QueueWithIntervals();

            foreach (var (message, attempt) in onDelivery)
            {
                result.Enqueue(message.MessageId);
            }

            return result;
        }

    }

}