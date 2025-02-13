using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        private bool _handleMessageInProcess = false;


        private object _lockObject;
        private bool _disconnected;
        private readonly Dictionary<long, TaskCompletionSource<int>> _publishTasks = new ();

        public MyServiceBusTcpContext(Dictionary<string, SubscriberInfo> subscribers, string name, 
            Func<IReadOnlyList<(string topicName, int maxCachedSize)>> checkAndCreateTopicOnConnect)
        {
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



        private void HandleDisconnect()
        {
            var list = new List<TaskCompletionSource<int>>();

            lock (_publishTasks)
            {
                while (_publishTasks.Count > 0)
                {
                    var first = _publishTasks.Keys.First();

                    if (_publishTasks.Remove(first, out var result))
                        list.Add(result);
                }
            }

            foreach (var task in list)
            {
                task.SetException(new PublishFailException( PublishFailReason.Disconnected, "Disconnection occured during the publish flow"));
            }
        }
        
        protected override ValueTask OnDisconnectAsync()
        {
            _disconnected = true;

            while (_handleMessageInProcess)
            {
                Thread.Sleep(1000);
            }
            
            var lockObject = _lockObject;
            if (lockObject != null)
            {
                lock (lockObject)
                {
                    HandleDisconnect();    
                }
            }
            else
            {
                HandleDisconnect();
            }
            
            return new ValueTask();
        }


        private void HandlePublishResponse(PublishResponseContract pr)
        {
            TaskCompletionSource<int> task = null;
            lock (_publishTasks)
            {
                if (_publishTasks.Remove(pr.RequestId, out var result))
                    task = result;
            }
            
            task?.SetResult(0);
        }

        protected override ValueTask HandleIncomingDataAsync(IServiceBusTcpContract data)
        {
            _handleMessageInProcess = true;
            try
            {
                if (_disconnected)
                {
                    throw new Exception("tcpSocket is disconnected");
                }

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
            }
            finally
            {
                _handleMessageInProcess = false;
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
        
        
        private static readonly Lazy<string> GetClientVersion = new (() =>
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


        public Task PublishAsync(PublishContract contract, object lockObject)
        {
            _lockObject ??= lockObject;

            if (_disconnected)
                throw new Exception("Disconnected");

            var task = new TaskCompletionSource<int>();

            lock (_publishTasks)
            {
                _publishTasks.Add(contract.RequestId, task);   
            }

            SendDataToSocket(contract);

            return task.Task;
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