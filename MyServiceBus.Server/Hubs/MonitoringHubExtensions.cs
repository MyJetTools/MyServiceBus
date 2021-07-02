using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MyServiceBus.Domains.Queues;
using MyServiceBus.Domains.Topics;
using MyServiceBus.Server.Tcp;

namespace MyServiceBus.Server.Hubs
{
    public static class MonitoringHubExtensions
    {

        public static Task SendInitAsync(this IClientProxy clientProxy)
        {
            var initModel = new InitHubResponseModel
            {
                Version = ServiceLocator.AppVersion
            };
            return clientProxy.SendAsync("init", initModel);
        }

        public static ValueTask SendTopicsAsync(this MonitoringConnection connection, IEnumerable<MyTopic> topics)
        {
            try
            {
                var task = connection.ClientProxy.SendAsync("topics", topics.Select(itm => itm.ToTopicHubModel()).OrderBy(itm => itm.Id));
                return new ValueTask(task);
            }
            catch (Exception e)
            {
                Console.WriteLine("*******SendTopicsAsync Error: "+e.Message);
                Console.WriteLine(e);
                return new ValueTask();
            }
   
        }

        public static ValueTask SendQueuesAsync(this MonitoringConnection connection, IReadOnlyDictionary<string, 
            IReadOnlyList<TopicQueue>> queuesToSend)
        {
            try
            {
                var contract = queuesToSend.ToDictionary(
                    itm => itm.Key,
                    itm => itm.Value.Select(TopicQueueHubModel.Create).ToList());

                return new ValueTask(connection.ClientProxy.SendAsync("queues", contract));
            }
            catch (Exception e)
            {
                Console.WriteLine("*******SendQueuesAsync Error: "+e.Message);
                Console.WriteLine(e);
                return new ValueTask();
            }
 
        }



        private static void CompressGraph(this MonitoringConnection connection,
            Dictionary<string, IReadOnlyList<int>> dataToCompress)
        {
            foreach (var (topicId, value) in dataToCompress.ToList())
            {
                var sentLastTimeAsEmpty = connection.DidWeSendLastTimeAsEmptyTopicGraph(topicId);

                if (value.Count == 0)
                {
                    if (sentLastTimeAsEmpty)
                        dataToCompress.Remove(topicId);
                    else
                        connection.SetSentAsEmptyLastTime(topicId, true);
                }
                else
                    connection.SetSentAsEmptyLastTime(topicId, false);
            }
        }


        public static ValueTask SendTopicGraphAsync(this MonitoringConnection connection)
        {

            try
            {
                var contract = ServiceLocator.TopicsList.Get()
                    .ToDictionary(topic => topic.TopicId, 
                        topic =>
                        {
                            var result = ServiceLocator.MessagesPerSecondByTopic.GetRecordsPerSecond(topic.TopicId);
                            return result.All(itm => itm == 0) ? Array.Empty<int>() : result;
                        });
            
                connection.CompressGraph(contract);

                return contract.Count ==0 
                    ? new ValueTask() 
                    : new ValueTask(connection.ClientProxy.SendAsync("topic-performance-graph", contract));
            }
            catch (Exception e)
            {
                Console.WriteLine("*******SendTopicGraphAsync Error: "+e.Message);
                Console.WriteLine(e);
                return new ValueTask();
            }

        }

        public static ValueTask SendQueueGraphAsync(this MonitoringConnection connection)
        {
            try
            {
                Dictionary<string, IReadOnlyList<int>> contract = null;

                foreach (var topic in ServiceLocator.TopicsList.Get())
                {
                    foreach (var topicQueue in topic.GetQueues())
                    {

                        var lastSentSnapshotId =
                            connection.GetLastQueueDurationGraphSentSnapshot(topicQueue.Topic.TopicId,
                                topicQueue.QueueId);
                        var currentSnapshotId = topicQueue.GetExecutionDurationSnapshotId();

                        if (currentSnapshotId == lastSentSnapshotId)
                            continue;

                        connection.SetLastQueueDurationGraphSentSnapshot(topicQueue.Topic.TopicId, topicQueue.QueueId,
                            currentSnapshotId);

                        contract ??= new Dictionary<string, IReadOnlyList<int>>();
                        contract.Add(topic.TopicId + "-" + topicQueue.QueueId, topicQueue.GetExecutionDuration());
                    }
                }

                return contract == null
                    ? new ValueTask()
                    : new ValueTask(connection.ClientProxy.SendAsync("queue-duration-graph", contract));

            }
            catch (Exception e)
            {
                Console.WriteLine("*******SendQueueGraphAsync Error: "+e.Message);
                Console.WriteLine(e);
                return new ValueTask();
            }
        }

        public static async Task SendConnectionsAsync(this MonitoringConnection connection)
        {

            try
            {
                var connections = ServiceLocator
                    .TcpServer
                    .GetConnections()
                    .Cast<MyServiceBusTcpContext>()
                    .OrderBy(itm => itm.ContextName.ToLowerInvariant());
            
                await connection.ClientProxy.SendAsync("connections", connections.Select(conn => conn.ToTcpConnectionHubModel()));

            }
            catch (Exception e)
            {
                Console.WriteLine("*******SendConnectionsAsync Error: "+e.Message);
                Console.WriteLine(e);
            }
        }


        public static async Task SendTopicMetricsAsync(this MonitoringConnection connection)
        {
            try
            {
                var contexts = connection.GetTopicContexts();
                var contract = contexts.Select(ctx =>TopicMetricsHubModel.Create(ctx.Topic));
                await connection.ClientProxy.SendAsync("topic-metrics", contract);
            }
            catch (Exception e)
            {
                Console.WriteLine("*******SendTopicMetricsAsync Error. "+e.Message);
                Console.WriteLine(e);
            }
        }


        public static async Task SendPersistentQueueAsync(this MonitoringConnection connection)
        {

            try
            {
                var contract
                    = ServiceLocator
                        .MessagesToPersistQueue
                        .GetMessagesToPersistCount()
                        .Select(itm =>
                            new PersistentQueueHubModel
                            {
                                Id = itm.topic,
                                Size = itm.count
                            });

                await connection.ClientProxy.SendAsync("persist-queue", contract);
            }
            catch (Exception e)
            {
                Console.WriteLine("*******SendPersistentQueueAsync Error. "+e.Message);
                Console.WriteLine(e);
            }
        }

    }
}