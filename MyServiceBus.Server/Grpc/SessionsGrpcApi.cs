using System;
using System.Threading.Tasks;
using MyServiceBus.GrpcContracts;

namespace MyServiceBus.Server.Grpc
{
    public class SessionsGrpcApi : IMyServiceBusGrpcSessions
    {
        public ValueTask<GreetingGrpcResponse> GreetingAsync(GreetingGrpcRequest request)
        {
            var session = ServiceLocator.GrpcSessionsList.GenerateNewSession(request.Name, request.ClientVersion);

            var response = new GreetingGrpcResponse
            {
                SessionId = session.Id
            };

            return new ValueTask<GreetingGrpcResponse>(response);
        }

        public ValueTask<MyServiceBusGrpcResponse> PingAsync(PingGrpcContract request)
        {
            var session = ServiceLocator.GrpcSessionsList.TryGetSession(request.SessionId, DateTime.UtcNow);
            var result = session == null ? GrpcResponses.InvalidSession : GrpcResponses.OkResponse;
            return new ValueTask<MyServiceBusGrpcResponse>(result);
        }
    }
}