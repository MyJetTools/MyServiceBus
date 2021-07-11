using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyServiceBus.Domains.Execution;
using MyServiceBus.GrpcContracts;

namespace MyServiceBus.Server.Grpc
{
    public class PublisherGrpcApi : ControllerBase, IMyServiceBusGrpcPublisher
    {
        public async ValueTask<MyServiceBusGrpcResponse> CreateTopicIfNotExistsAsync(CreateTopicGrpcContract contract)
        {
            GrpcExtensions.GrpcPreExecutionCheck();

            var grpcSession = ServiceLocator.GrpcSessionsList.TryGetSession(contract.SessionId, DateTime.UtcNow);

            if (grpcSession == null)
                return GrpcResponses.InvalidSession;
            
            Console.WriteLine($"Creating topic {contract.TopicId} for connection: "+grpcSession.Name);
            
            await ServiceLocator.TopicsManagement.AddIfNotExistsAsync(contract.TopicId);

            grpcSession.SessionContext.PublisherInfo.AddIfNotExists(contract.TopicId);
            
            return GrpcResponses.OkResponse;
        }

        public async ValueTask<MyServiceBusGrpcResponse> PublishBinaryAsync(IAsyncEnumerable<BinaryDataGrpcWrapper> messages)
        {
            GrpcExtensions.GrpcPreExecutionCheck();

            var request = await messages.ParseFromPayloadAsync<MessagesToPublishGrpcContract>();

            var now = DateTime.UtcNow;

            var session = ServiceLocator.GrpcSessionsList.TryGetSession(request.SessionId, now);

            if (session == null)
                return GrpcResponses.InvalidSession;


            var publishMessages = request.Messages.Select(msg => new PublishMessage
                { Data = msg.Content, MetaData = msg.Headers.ToMessageMetaData() });

            var response = await ServiceLocator
                .MyServiceBusPublisher
                .PublishAsync(session.SessionContext, request.TopicId, publishMessages, now, false);

            if (response == ExecutionResult.TopicNotFound)
            {
                Console.WriteLine($"Attempt to write to Topic {request.TopicId} which does not exist. Disconnecting session for app: "+session.Name);
                return GrpcResponses.TopicNotFoundGrpcResponse;
            }


            return GrpcResponses.OkResponse;
        }
    }
}