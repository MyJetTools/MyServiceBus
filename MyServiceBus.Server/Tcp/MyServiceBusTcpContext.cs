using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyServiceBus.Abstractions.QueueIndex;
using MyServiceBus.Domains.Execution;
using MyServiceBus.Domains.QueueSubscribers;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Persistence.Grpc;
using MyServiceBus.TcpContracts;
using MyTcpSockets;

namespace MyServiceBus.Server.Tcp
{
    public class MyServiceBusTcpContext : TcpContext<IServiceBusTcpContract>
    {

        public int ProtocolVersion { get; private set; }

        private async ValueTask<string> ExecuteConfirmAsync(string topicId, string queueId, long confirmationId,
            bool ok)
        {
            var topic = ServiceLocator.TopicsList.TryGet(topicId);

            if (topic == null)
                return $"There is a confirmation {confirmationId} for a topic {topicId} which is not found";

            var topicQueue = topic.Queues.TryGetQueue(queueId);
            
            if (topicQueue == null)
                return $"There is a confirmation {confirmationId} for a queue {queueId} with topic {topicId} which is not found";

            if (ok)
                await ServiceLocator.SubscriberOperations.ConfirmDeliveryAsync(topicQueue, confirmationId);
            else
                await ServiceLocator.SubscriberOperations.ConfirmNotDeliveryAsync(topicQueue, confirmationId);
            return null;
        }

        private async ValueTask<string> ExecuteConfirmDeliveryOfSomeMessagesByNotCommitContract(
            ConfirmMessagesByNotDeliveryContract packet)
        {
            var topic = ServiceLocator.TopicsList.TryGet(packet.TopicId);

            if (topic == null)
                return
                    $"There is a confirmation {packet.ConfirmationId} for a topic {packet.TopicId} which is not found";

            var topicQueue = topic.Queues.TryGetQueue(packet.QueueId);

            if (topicQueue == null)
                return
                    $"There is a confirmation {packet.ConfirmationId} for a topic {packet.TopicId}/{packet.QueueId} which is not found";
            
            var notConfirmedMessages = new QueueWithIntervals(packet.ConfirmedMessages);
            
            //ToDo - проверить что не перепутали
            await ServiceLocator.SubscriberOperations.ConfirmMessagesByConfirmedListAsync(topicQueue,
                packet.ConfirmationId, notConfirmedMessages);
            return null;
        }

        private static async ValueTask<string> ExecuteSomeMessagesAreOkSomeFail(ConfirmSomeMessagesOkSomeFail packet)
        {
            var topic = ServiceLocator.TopicsList.TryGet(packet.TopicId);

            if (topic == null)
                return
                    $"There is a confirmation {packet.ConfirmationId} for a topic {packet.TopicId}/{packet.QueueId} which is not found";
            
            var topicQueue = topic.Queues.TryGetQueue(packet.QueueId);

            if (topicQueue == null)
                return
                    $"There is a confirmation {packet.ConfirmationId} for a topic {packet.TopicId}/{packet.QueueId} which is not found";
            
            var okMessages = new QueueWithIntervals(packet.OkMessages);
            
            await ServiceLocator.SubscriberOperations.ConfirmMessagesByNotConfirmedListAsync(topicQueue, packet.ConfirmationId,
                okMessages);
            return null;
        }

        private async ValueTask<string> PublishAsync(PublishContract contract)
        {

            var now = DateTime.UtcNow;

            var messages = contract.Data.Select(msg => new PublishMessage
                { Data = msg, MetaData = Array.Empty<MessageContentMetaDataItem>() });

            var response = await ServiceLocator
                .MyServiceBusPublisherOperations
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

        public readonly MyServiceBusSession SessionContext;

        public MyServiceBusTcpContext()
        {
            SessionContext = ServiceLocator.SessionsList.IssueSession();
            SessionContext.PlugSendMessages(SendMessages);
        }

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
        
            ServiceLocator.SubscriberOperations.SubscribeToQueueAsync(queue, SessionContext);


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
            return ServiceLocator.SubscriberOperations.DisconnectSubscriberAsync(SessionContext, DateTime.UtcNow);

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
            ServiceLocator.MyServiceBusPublisherOperations.CreateTopicIfNotExists(SessionContext, createTopicIfNotExistsContract.TopicId);
        }

        public void SendMessages(QueueSubscriber subscriber)
        {
            var messageData = subscriber.MessagesOnDelivery.Select(
                msg => new NewMessagesContract.NewMessageData
            {
                Id = msg.message.MessageId,
                Data = msg.message.Data,
                AttemptNo = msg.attemptNo
            }).ToList();

            var contract = new NewMessagesContract
            {
                TopicId = subscriber.TopicQueue.Topic.TopicId,
                QueueId = subscriber.TopicQueue.QueueId,
                ConfirmationId = subscriber.ConfirmationId,
                Data = messageData,
            };

            SendDataToSocket(contract);
        }

    }
}