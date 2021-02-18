using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyServiceBus.Domains.Sessions;
using MyServiceBus.Grpc;
using MyServiceBus.Grpc.Contracts;
using MyServiceBus.Grpc.Models;

namespace MyServiceBus.Server.Grpc
{
    public class ManagementGrpcService : ControllerBase, IManagementGrpcService
    {
        public async ValueTask<CreateTopicGrpcResponse> CreateTopicAsync(CreateTopicGrpcRequest request)
        {
            var session = ServiceLocator.SessionsList.GetSession(request.SessionId, DateTime.UtcNow);

            if (session == null)
                return new CreateTopicGrpcResponse
                {
                    SessionId = -1,
                    Status = GrpcResponseStatus.SessionExpired
                };

            
            Console.WriteLine($"Creating topic {request.TopicId} for connection: "+session.Name);
            
            await ServiceLocator.TopicsManagement.AddIfNotExistsAsync(request.TopicId);

            session.PublishToTopic(request.TopicId);
            
            return new CreateTopicGrpcResponse
            {
                SessionId = session.Id,
                Status = GrpcResponseStatus.Ok
            };
        }

        public ValueTask<CreateQueueGrpcResponse> CreateQueueAsync(CreateQueueGrpcRequest request)
        {

            var session = ServiceLocator.SessionsList.GetSession(request.SessionId, DateTime.UtcNow);

            if (session == null)
            {
                var resultNoSession = new CreateQueueGrpcResponse
                {
                    SessionId = -1,
                    Status = GrpcResponseStatus.SessionExpired
                };
                return new ValueTask<CreateQueueGrpcResponse>(resultNoSession);
            }

            var topic = ServiceLocator.TopicsList.TryGet(request.TopicId);

            if (topic == null)
            {
                var topicNotFoundResult = new CreateQueueGrpcResponse
                {
                    SessionId = request.SessionId,
                    Status = GrpcResponseStatus.TopicNotFound
                };
                return new ValueTask<CreateQueueGrpcResponse>(topicNotFoundResult); 
            }

            topic.CreateQueueIfNotExists(request.QueueId, request.DeleteOnNoConnections);

            var okResult = new CreateQueueGrpcResponse
            {
                SessionId = request.SessionId,
                Status = GrpcResponseStatus.Ok

            };
            return new ValueTask<CreateQueueGrpcResponse>(okResult);
            
        }

        public ValueTask<GreetingGrpcResponse> GreetingAsync(GreetingGrpcRequest request)
        {
            var session = ServiceLocator.SessionsList.NewSession(request.Name, "127.0.0.1", DateTime.UtcNow, Startup.SessionTimeout, 0, SessionType.Http);

            var result = new GreetingGrpcResponse
            {
                SessionId = session.Id,
                Status = GrpcResponseStatus.Ok
            };

            return new ValueTask<GreetingGrpcResponse>(result);
        }

        public ValueTask<PingGrpcResponse> PingAsync(PingGrpcRequest request)
        {
            var session = ServiceLocator.SessionsList.GetSession(request.SessionId, DateTime.UtcNow);

            if (session != null)
                return new ValueTask<PingGrpcResponse>(new PingGrpcResponse
                {
                    Status = GrpcResponseStatus.Ok,
                    SessionId = request.SessionId
                });

            var noSession = new PingGrpcResponse
            {
                SessionId = -1,
                Status = GrpcResponseStatus.SessionExpired
            };

            return new ValueTask<PingGrpcResponse>(noSession);
        }

        public ValueTask<LogoutGrpcResponse> LogoutAsync(LogoutGrpcRequest request)
        {
            ServiceLocator.SessionsList.RemoveIfExists(request.SessionId);
            
            return new ValueTask<LogoutGrpcResponse>(new LogoutGrpcResponse
            {
                SessionId = request.SessionId
            });
        }
    }
}