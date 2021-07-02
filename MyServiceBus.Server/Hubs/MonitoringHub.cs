using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using MyServiceBus.Domains.Queues;

namespace MyServiceBus.Server.Hubs
{
    public class MonitoringHub : Hub
    {
        
        private static readonly MonitoringConnectionsList Connections =
            new ();

        public static async Task BroadCasMetricsAsync()
        {
            var connections = Connections.GetAll();


            foreach (var connection in connections)
            {
                try
                {
                    await SyncConnection(connection);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            
        }


        private static async Task SyncTopicsAsync(MonitoringConnection connection)
        {
            var (topics, topicsSnapshotId) = ServiceLocator.TopicsList.GetWithSnapshotId();
            
            if (connection.TopicSnapshotHasChanged(topicsSnapshotId, topics))
                await connection.SendTopicsAsync(topics);
        }

        private static async Task SyncQueuesAsync(MonitoringConnection connection)
        {
            var topicContexts = connection.GetTopicContexts();

            Dictionary<string, IReadOnlyList<TopicQueue>> queuesToSync = null;
            foreach (var topicContext in topicContexts)
            {
                var queues = topicContext.GetQueuesIfNotSynchronized();
                if (queues == null)
                    continue;

                queuesToSync ??= new Dictionary<string, IReadOnlyList<TopicQueue>>();
                
                queuesToSync.Add(topicContext.Topic.TopicId, queues);
            }

            if (queuesToSync != null)
            {
                await connection.SendQueuesAsync(queuesToSync);
            }
        }
        
        
        private static async Task SyncConnection(MonitoringConnection connection)
        {

            await SyncTopicsAsync(connection);

            await SyncQueuesAsync(connection);
            
            await connection.SendTopicMetricsAsync();
            await connection.SendTopicGraphAsync();
            await connection.SendQueueGraphAsync();
            await connection.SendConnectionsAsync();
            await connection.SendPersistentQueueAsync();
        } 
        
        public override async Task OnConnectedAsync()
        {
            var newConnection = new MonitoringConnection(Context.ConnectionId, Clients.Caller);
            Connections.Add(newConnection);
            Console.WriteLine("Monitoring Connection: "+Context.ConnectionId);
            await newConnection.ClientProxy.SendInitAsync();
            await SyncConnection(newConnection);
            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            Console.WriteLine("Monitoring Connection dropped: "+Context.ConnectionId);
            Connections.Remove(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

    }
}