using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.TcpContracts;
using MyTcpSockets;

namespace MyServiceBus.TcpClient
{
    public class MyServiceBusTcpContext : ClientTcpContext<IServiceBusTcpContract>
    {

        private readonly Dictionary<string, SubscriberInfo> _subscribers;
        private readonly string _name;
        private readonly Func<IReadOnlyList<(string topicName, int maxCachedSize)>> _checkAndCreateTopicOnConnect;


        private readonly PayLoadCollector _payLoadCollector;

        public MyServiceBusTcpContext(Dictionary<string, SubscriberInfo> subscribers, string name, 
            PayLoadCollector payLoadCollector,
            Func<IReadOnlyList<(string topicName, int maxCachedSize)>> checkAndCreateTopicOnConnect)
        {
            _payLoadCollector = payLoadCollector;
            _subscribers = subscribers;
            _name = name;
            _checkAndCreateTopicOnConnect = checkAndCreateTopicOnConnect;
        }

        protected override ValueTask OnConnectAsync()
        {
            SendGreetings(_name);

            SendPacketVersions();

            foreach (var (topicId, maxCachedSize) in _checkAndCreateTopicOnConnect())
            {
                SetCreateTopicIfNotExistsOnConnect(topicId, maxCachedSize);
            }

            foreach (var subscriber in _subscribers.Values)
            {
                SendSubscribe(subscriber.TopicId, subscriber.QueueId, subscriber.QueueType);
            }
            
            return new ValueTask();
        }


        
        protected override ValueTask OnDisconnectAsync()
        {
            _payLoadCollector.Disconnect(Id);
            return new ValueTask();
        }


        private void HandlePublishResponse(PublishResponseContract pr)
        {
            _payLoadCollector.SetPublished(pr.RequestId);

            var nextPackageToPublish = _payLoadCollector.GetNextPayloadToPublish();

            if (nextPackageToPublish != null)
                Publish(nextPackageToPublish);
        }

        protected override ValueTask HandleIncomingDataAsync(IServiceBusTcpContract data)
        {

            switch (data)
            {
                case NewMessagesContract newMsg:
                    HandleNewMessages(newMsg);
                    break;

                case PublishResponseContract pr:
                    HandlePublishResponse(pr);
                    break;

                case RejectConnectionContract rejectContract:
                    throw new Exception("Reject from server: " + rejectContract.Message);
            }

            return new ValueTask();
        }


        private void SendSubscribe(string topicId, string queueId, TopicQueueType queueType)
        {
            var contract = new SubscribeContract
            {
                TopicId = topicId,
                QueueId = queueId,
                QueueType = queueType
            };
            
            SendDataToSocket(contract);
        }

        
        public static string GetId(string topicId, string queueId)
        {
            return topicId + "|" + queueId;
        }

        private void HandleNewMessages(NewMessagesContract newMsg)
        {
            var id = GetId(newMsg.TopicId, newMsg.QueueId);

            var subscriber = _subscribers[id];

            var ctx = new MessagesConfirmationContext(newMsg.TopicId, newMsg.QueueId, newMsg.ConfirmationId, ConfirmMessages);

            try
            {
                subscriber.InvokeNewMessages(ctx, newMsg.Data,
                    () => SendMessageConfirmation(newMsg),
                 () => SendMessageReject(newMsg), 
                    someMessages => SendSomeMessagesOkSomeRejected(newMsg, someMessages)
                    );
            }
            catch (Exception)
            {
                SendMessageConfirmation(newMsg);
            }
        }
        
        private void SendMessageConfirmation(NewMessagesContract messages)
        {
            var contract = new NewMessageConfirmationContract
            {
                TopicId = messages.TopicId,
                QueueId = messages.QueueId,
                ConfirmationId = messages.ConfirmationId
            };
            
            SendDataToSocket(contract);
        }
        
        private void SendMessageReject(NewMessagesContract messages)
        {
            var contract = new MessagesConfirmationAsFailContract
            {
                TopicId = messages.TopicId,
                QueueId = messages.QueueId,
                ConfirmationId = messages.ConfirmationId
            };
            
            SendDataToSocket(contract);
        }

        private void SendSomeMessagesOkSomeRejected(NewMessagesContract messages, QueueWithIntervals okMessages)
        {
            var contract = new ConfirmSomeMessagesOkSomeFail
            {
                TopicId = messages.TopicId,
                QueueId = messages.QueueId,
                ConfirmationId = messages.ConfirmationId,
                OkMessages = okMessages.GetSnapshot(),
            };
            
            SendDataToSocket(contract);
        }
        
        
        private const int ProtocolVersion = 2; 
        
        
        private static readonly Lazy<string> GetClientVersion = new Lazy<string>(() =>
        {
            try
            {
                return typeof(MyServiceBusTcpContext).Assembly.GetName().Version.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        });
        
        
        
        private void SendGreetings(string name)
        {

            var clientVersion = GetClientVersion.Value;

            clientVersion = clientVersion == null
                ? string.Empty
                : ";ClientVersion:"+clientVersion;

            
            var contract = new GreetingContract
            {
                Name = name +clientVersion,
                ProtocolVersion = ProtocolVersion
            };

            SendDataToSocket(contract);
        }


        private void SendPacketVersions()
        {
            var packetVersions = new PacketVersionsContract();
            packetVersions.SetPacketVersion(CommandType.NewMessage, 1);
            packetVersions.SetPacketVersion(CommandType.Publish, 1);
            SendDataToSocket(packetVersions);
        }

        private void SetCreateTopicIfNotExistsOnConnect(string topicId, int maxCachedSize)
        {
            var contract = new CreateTopicIfNotExistsContract
            {
                TopicId = topicId,
                MaxMessagesInCache = maxCachedSize
            };

            SendDataToSocket(contract);
        }


        protected override IServiceBusTcpContract GetPingPacket()
        {
            return PingContract.Instance;
        }


        public void Publish(PayloadPackage payloadPackage)
        {
           var contract = PublishContract.Create(payloadPackage.TopicId, payloadPackage.RequestId,
                payloadPackage.ImmediatelyPersist, payloadPackage.PayLoads);

            SendDataToSocket(contract);
        }

        public void ConfirmMessages(IConfirmationContext ctx, IEnumerable<long> messagesToConfirm)
        {
            var queueWithIntervals = new QueueWithIntervals();

            foreach (var messageId in messagesToConfirm)
                queueWithIntervals.Enqueue(messageId);

            var contract = ConfirmMessagesByNotDeliveryContract.Create(ctx.TopicId, ctx.QueueId, ctx.ConfirmationId,
                queueWithIntervals.GetSnapshot());
            SendDataToSocket(contract);
        }
    }
    
}