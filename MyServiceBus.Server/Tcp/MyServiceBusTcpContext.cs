using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyServiceBus.Abstractions;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Persistence.Grpc;
using MyServiceBus.TcpContracts;
using MyTcpSockets;

namespace MyServiceBus.Server.Tcp
{
    public class MyServiceBusTcpContext : TcpContext<IServiceBusTcpContract>, IMyServiceBusSubscriberSession
    {

        public int ProtocolVersion { get; private set; }

        private async ValueTask<string> ExecuteConfirmAsync(string topicId, string queueId, long confirmationId,
            bool ok)
        {
            var topic = ServiceLocator.TopicsList.TryGet(topicId);

            if (topic == null)
                return $"There is a confirmation {confirmationId} for a topic {topicId} which is not found";

            await ServiceLocator.Subscriber.ConfirmDeliveryAsync(topic, queueId, confirmationId, ok);

            return null;
        }



        private async ValueTask<string> ExecuteConfirmDeliveryOfSomeMessagesByNotCommitContract(
            ConfirmMessagesByNotDeliveryContract packet)
        {
            var topic = ServiceLocator.TopicsList.TryGet(packet.TopicId);

            if (topic == null)
                return
                    $"There is a confirmation {packet.ConfirmationId} for a topic {packet.TopicId}/{packet.QueueId} which is not found";

            var confirmedMessages = new QueueWithIntervals(packet.ConfirmedMessages);
            await ServiceLocator.Subscriber.ConfirmMessagesByNotDeliveryAsync(topic, packet.QueueId,
                packet.ConfirmationId, confirmedMessages);
            return null;
        }

        private static async ValueTask<string> ExecuteSomeMessagesAreOkSomeFail(ConfirmSomeMessagesOkSomeFail packet)
        {
            var topic = ServiceLocator.TopicsList.TryGet(packet.TopicId);

            if (topic == null)
                return
                    $"There is a confirmation {packet.ConfirmationId} for a topic {packet.TopicId}/{packet.QueueId} which is not found";

            var okMessages = new QueueWithIntervals(packet.OkMessages);
            await ServiceLocator.Subscriber.ConfirmDeliveryAsync(topic, packet.QueueId, packet.ConfirmationId,
                okMessages);
            return null;
        }

        private async ValueTask<string> PublishAsync(PublishContract contract)
        {
            SessionContext.PublisherInfo.PublishMetricPerSecond.EventHappened();

            var now = DateTime.UtcNow;

            var messages = contract.Data.Select(msg => new PublishMessage
                { Data = msg, MetaData = Array.Empty<MessageContentMetaDataItem>() });

            var response = await ServiceLocator
                .MyServiceBusPublisher
                .PublishAsync(SessionContext, contract.TopicId, messages, now, contract.ImmediatePersist == 1);

            if (response != ExecutionResult.Ok)
                return "Can not publish the message. Reason: " + response;

            var resp = new PublishResponseContract
            {
                RequestId = contract.RequestId
            };

            SendDataToSocket(resp);

            return null;
        }

        public readonly MyServiceBusSessionContext SessionContext =  new ();

        private static readonly Dictionary<int, int> AcceptedProtocolVersions = new ()
        {
            [1] = 1,
            [2] = 2
        };

        private static string GetAcceptedProtocolVersions()
        {
            var result = new StringBuilder();

            foreach (var key in AcceptedProtocolVersions.Keys)
            {
                if (result.Length > 0)
                    result.Append(',');
                result.Append($"{key}");
            }

            return result.ToString();
        }

        private string Greeting(GreetingContract greetingContract)
        {

            if (!AcceptedProtocolVersions.ContainsKey(greetingContract.ProtocolVersion))
                return greetingContract.Name +
                       $" is attempting to connect with invalid protocol version {greetingContract.ProtocolVersion}. Acceptable versions are {GetAcceptedProtocolVersions()}";

            ProtocolVersion = greetingContract.ProtocolVersion;

            SetContextName(greetingContract.Name);
            ServiceLocator.TcpConnectionsSnapshotId++;
            return null;
        }

        private string ExecuteSubscribe(SubscribeContract contract)
        {
            ServiceLocator.ConnectionsLog.AddLog(Id, ContextName, GetIp(), "Subscribed to topic: " + contract.TopicId + " with queue: " + contract.QueueId);

            var topic = ServiceLocator.TopicsList.TryGet(contract.TopicId);

            if (topic == null)
                return
                    $"Client {ContextName} is trying to subscribe to the topic {contract.TopicId} which does not exists";

            var queue = topic.CreateQueueIfNotExists(contract.QueueId, contract.QueueType, true);
            SessionContext.SubscribeToQueue(queue);
            ServiceLocator.Subscriber.SubscribeToQueueAsync(queue, this);

            if (queue.TopicQueueType == TopicQueueType.PermanentWithSingleConnection)
            {
                var subscribersToDisconnect =
                    queue.SubscribersList.GetReadAccess(readAccess => readAccess.GetExceptThisOne(this).ToList());

                foreach (var subscriber in subscribersToDisconnect)
                    subscriber.Session.Disconnect();
            }

            return null;

        }

        protected override ValueTask OnConnectAsync()
        {
            ServiceLocator.ConnectionsLog.AddLog(Id, ContextName, GetIp(), "Connected");

            ServiceLocator.TcpConnectionsSnapshotId++;
            return new ValueTask();
        }


        private string _ip;
        private string GetIp()
        {
            try
            {
                _ip ??= TcpClient.Client.RemoteEndPoint?.ToString(); 
                return _ip ?? "unknown";
            }
            catch (Exception)
            {
                return "exception-unknown";
            }

        }

        protected override ValueTask OnDisconnectAsync()
        {
            ServiceLocator.ConnectionsLog.AddLog(Id, ContextName, GetIp(), "Disconnected");
            ServiceLocator.TcpConnectionsSnapshotId++;
            return ServiceLocator.Subscriber.DisconnectSubscriberAsync(this);

        }


        private async Task HandleGlobalException(string message)
        {
            SendDataToSocket(RejectConnectionContract.Create(message));

            ServiceLocator.ConnectionsLog.AddLog(Id, ContextName, GetIp(), $"Sent reject due to exception {message}, Waiting for 1 sec and disconnect");
            await Task.Delay(1000);
            Disconnect();
        }
        

        protected override async ValueTask HandleIncomingDataAsync(IServiceBusTcpContract data)
        {

            string error = null;
            
            try
            {
                if (ServiceLocator.MyGlobalVariables.ShuttingDown)
                {
                    Disconnect();
                    return;
                }

   
                switch (data)
                {
                    case PingContract _:
                        SendDataToSocket(PongContract.Instance);
                        return;

                    case SubscribeContract subscribeContract:
                        error = ExecuteSubscribe(subscribeContract);
                        return;

                    case PublishContract publishContract:
                        error = await PublishAsync(publishContract);
                        return;

                    case GreetingContract greetingContract:
                        error = Greeting(greetingContract);
                        return;

                    case NewMessageConfirmationContract confirmRequestContract:
                        error = await ExecuteConfirmAsync(confirmRequestContract.TopicId, confirmRequestContract.QueueId,
                            confirmRequestContract.ConfirmationId, true);
                        return;

                    case MessagesConfirmationAsFailContract fail:
                        error = await ExecuteConfirmAsync(fail.TopicId, fail.QueueId, fail.ConfirmationId, false);
                        return;

                    case CreateTopicIfNotExistsContract createTopicIfNotExistsContract:
                        CreateTopicIfNotExists(createTopicIfNotExistsContract);
                        return;

                    case ConfirmSomeMessagesOkSomeFail confirmSomeMessagesOkSomeFail:
                        error = await ExecuteSomeMessagesAreOkSomeFail(confirmSomeMessagesOkSomeFail);
                        return;

                    case ConfirmMessagesByNotDeliveryContract confirmDeliveryOfSomeMessagesByNotCommit:
                        error = await ExecuteConfirmDeliveryOfSomeMessagesByNotCommitContract(
                            confirmDeliveryOfSomeMessagesByNotCommit);
                        return;
                }


            }
            catch (Exception e)
            {
                error = null;
                Console.WriteLine(e);
                await HandleGlobalException(e.Message);
            }
            finally
            {
                if (error != null)
                    await HandleGlobalException(error); 
            }

        }


        private void CreateTopicIfNotExists(CreateTopicIfNotExistsContract createTopicIfNotExistsContract)
        {
            ServiceLocator.ConnectionsLog.AddLog(Id, ContextName, GetIp(), $"Attempt to create topic {createTopicIfNotExistsContract.TopicId}");

            SessionContext.PublisherInfo.AddIfNotExists(createTopicIfNotExistsContract.TopicId);

            ServiceLocator.TopicsManagement.AddIfNotExistsAsync(createTopicIfNotExistsContract.TopicId);
        }

        public void SendMessagesAsync(TopicQueue topicQueue,
            IReadOnlyList<(MessageContentGrpcModel message, int attemptNo)> messages, long confirmationId)
        {
            var messageData = messages.Select(
                msg => new NewMessagesContract.NewMessageData
            {
                Id = msg.message.MessageId,
                Data = msg.message.Data,
                AttemptNo = msg.attemptNo
            }).ToList();

            var contract = new NewMessagesContract
            {
                TopicId = topicQueue.Topic.TopicId,
                QueueId = topicQueue.QueueId,
                ConfirmationId = confirmationId,
                Data = messageData,
            };

            SessionContext.DeliveringToQueue(topicQueue);
            SendDataToSocket(contract);
        }

        private string _subscriberId;
        public string SubscriberId
        {
            get
            {
                if (_subscriberId != null)
                    return _subscriberId;

                _subscriberId = Id.ToString();

                return _subscriberId;
            }
        }

        public bool Disconnected => !Connected;
    }
}