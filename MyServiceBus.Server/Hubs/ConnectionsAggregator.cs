using System.Collections.Generic;
using System.Linq;
using MyServiceBus.Server.Services.Sessions;
using MyServiceBus.Server.Tcp;

namespace MyServiceBus.Server.Hubs
{
    public static class ConnectionsAggregator
    {

        private static IEnumerable<MyServiceBusTcpContext> GetTcpConnections()
        {
            return ServiceLocator
                .TcpServer
                .GetConnections()
                .Cast<MyServiceBusTcpContext>()
                .OrderBy(itm => itm.ContextName.ToLowerInvariant());   
        }
        

        public static IEnumerable<ConnectionHubModel> GetConnections()
        {
            foreach (var tcpConnection in GetTcpConnections())
                yield return tcpConnection.SessionContext.ToTcpConnectionHubModel();


            foreach (var grpcSession in ServiceLocator.GrpcSessionsList.GetAll())
            {
                yield return grpcSession.Session.ToTcpConnectionHubModel();
            }
        }

    }
}