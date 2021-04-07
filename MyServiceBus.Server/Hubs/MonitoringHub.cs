using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

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

        private static async Task SyncConnection(MonitoringConnection connection)
        {
            await connection.ClientProxy.SendInitAsync();

            await connection.SendTopicsAsync();
            
            await connection.SendQueuesAsync();
            
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